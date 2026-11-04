#!/usr/bin/env python3
"""Capture the full native Dolphin X11 client window, including postprocessing.
Writes lossless RGB PPM pixels without crop/resize. Intended for cloud Xvfb only.
"""
import ctypes as C
from pathlib import Path
import sys

class Attributes(C.Structure):
    _fields_=[('x',C.c_int),('y',C.c_int),('width',C.c_int),('height',C.c_int),('border',C.c_int),('depth',C.c_int),('visual',C.c_void_p),('root',C.c_ulong),('kind',C.c_int),('bit_gravity',C.c_int),('win_gravity',C.c_int),('backing_store',C.c_int),('backing_planes',C.c_ulong),('backing_pixel',C.c_ulong),('save_under',C.c_int),('colormap',C.c_ulong),('map_installed',C.c_int),('map_state',C.c_int),('all_events',C.c_long),('your_events',C.c_long),('no_events',C.c_long),('override',C.c_int),('screen',C.c_void_p)]
class XImage(C.Structure):
    _fields_=[('width',C.c_int),('height',C.c_int),('xoffset',C.c_int),('format',C.c_int),('data',C.c_void_p),('byte_order',C.c_int),('bitmap_unit',C.c_int),('bitmap_order',C.c_int),('bitmap_pad',C.c_int),('depth',C.c_int),('stride',C.c_int),('bits_per_pixel',C.c_int),('red_mask',C.c_ulong),('green_mask',C.c_ulong),('blue_mask',C.c_ulong)]

def capture(path,position_only=False):
    x=C.CDLL('libX11.so.6');x.XOpenDisplay.argtypes=[C.c_char_p];x.XOpenDisplay.restype=C.c_void_p
    d=x.XOpenDisplay(None)
    if not d:raise RuntimeError('No cloud X11 display')
    x.XDefaultRootWindow.argtypes=[C.c_void_p];x.XDefaultRootWindow.restype=C.c_ulong;root=x.XDefaultRootWindow(d)
    parent=C.c_ulong();root_out=C.c_ulong();children=C.POINTER(C.c_ulong)();count=C.c_uint()
    x.XQueryTree.argtypes=[C.c_void_p,C.c_ulong,C.POINTER(C.c_ulong),C.POINTER(C.c_ulong),C.POINTER(C.POINTER(C.c_ulong)),C.POINTER(C.c_uint)]
    x.XQueryTree(d,root,C.byref(root_out),C.byref(parent),C.byref(children),C.byref(count))
    x.XGetWindowAttributes.argtypes=[C.c_void_p,C.c_ulong,C.POINTER(Attributes)]
    x.XFetchName.argtypes=[C.c_void_p,C.c_ulong,C.POINTER(C.c_char_p)]
    windows=[]
    for i in range(count.value):
        a=Attributes();x.XGetWindowAttributes(d,children[i],C.byref(a));name=C.c_char_p();x.XFetchName(d,children[i],C.byref(name))
        title=name.value.decode(errors='replace') if name.value else ''
        if a.map_state==2 and a.kind==1 and a.depth>=24 and a.width>=320 and a.height>=240:
            print('Mapped client:',title,a.x,a.y,a.width,a.height,a.depth,flush=True)
            windows.append(('Dolphin' in title,a.width*a.height,children[i],a.width,a.height,a.x,a.y))
    if not windows:raise RuntimeError('No mapped Dolphin client window')
    _,_,window,w,h,window_x,window_y=max(windows)
    # NoGUI can create its client partly outside the Xvfb root. Move the whole
    # window onscreen before reading it; retain its native size and all pixels.
    x.XMoveWindow.argtypes=[C.c_void_p,C.c_ulong,C.c_int,C.c_int]
    if window_x!=0 or window_y!=0:x.XMoveWindow(d,window,0,0)
    x.XSync.argtypes=[C.c_void_p,C.c_int];x.XSync(d,0)
    if position_only:return
    x.XGetImage.argtypes=[C.c_void_p,C.c_ulong,C.c_int,C.c_int,C.c_uint,C.c_uint,C.c_ulong,C.c_int];x.XGetImage.restype=C.POINTER(XImage)
    image=x.XGetImage(d,window,0,0,w,h,C.c_ulong(-1).value,2)
    if not image:raise RuntimeError('XGetImage failed')
    info=image.contents
    if info.bits_per_pixel!=32 or (info.red_mask,info.green_mask,info.blue_mask)!=(0xff0000,0xff00,0xff):raise RuntimeError('Unsupported X11 pixel format')
    data=C.string_at(info.data,info.stride*h);rgb=bytearray(w*h*3)
    for y in range(h):
        for xx in range(w):
            at=y*info.stride+xx*4;to=(y*w+xx)*3
            pixel=int.from_bytes(data[at:at+4],'little' if info.byte_order==0 else 'big')
            rgb[to:to+3]=bytes(((pixel>>16)&255,(pixel>>8)&255,pixel&255))
    Path(path).write_bytes(f'P6\n{w} {h}\n255\n'.encode()+rgb)
    x.XDestroyImage.argtypes=[C.POINTER(XImage)];x.XDestroyImage(image)
    x.XCloseDisplay.argtypes=[C.c_void_p];x.XCloseDisplay(d)
    print(f'Captured full Dolphin window {w}x{h}: {path}')

if __name__=='__main__':capture(sys.argv[1],len(sys.argv)>2 and sys.argv[2]=='--position-only')
