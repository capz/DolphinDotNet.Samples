#!/usr/bin/env python3
"""Drive a normal interactive DOL through a native Z-overlay review in Xvfb.

Requires Dolphin NoGUI, XInput2 and libXtst. Saves full unscaled X11 PPMs and
the last completed native EFB PNG at each step. Inspect the saved images;
this is an input/capture aid, not a physical-hardware performance benchmark.
"""
import argparse
import ctypes as C
import json
import hashlib
from pathlib import Path
import shutil
import signal
import subprocess
import time

from capture_x11 import capture

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--logs',type=Path,required=True)
parser.add_argument('--dol',type=Path,required=True)
parser.add_argument('--widescreen',action='store_true',help='Also preview/keep 16:9, hide Z, and return through X safe mode')
parser.add_argument('--calendar',action='store_true',help='Review two-row blur and independent time/date editing instead of the overlay sequence')
args=parser.parse_args()
if args.calendar and args.widescreen:parser.error('Choose calendar or widescreen review')
root=args.logs.resolve()
if root.exists() and any(root.iterdir()):raise ValueError('Use a fresh capture directory')
root.mkdir(parents=True,exist_ok=True)
config=root/'user/Config';config.mkdir(parents=True)
(config/'Dolphin.ini').write_text('[Core]\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\nSIDevice0 = 6\n[DSP]\nBackend = No Audio Output\n[Display]\nRenderWindowXPos = 0\nRenderWindowYPos = 0\nRenderWindowWidth = 640\nRenderWindowHeight = 480\n[Interface]\nOnScreenDisplayMessages = False\n[Movie]\nDumpFrames = True\nDumpFramesSilent = True\n')
(config/'GFX.ini').write_text('[Settings]\nDumpFramesAsImages = True\nInternalResolution = 1\nMSAA = 1\nSSAA = False\nAspectRatio = 2\n[Enhancements]\nPostProcessingShader = IplEdgeAA\n[Hacks]\nForceProgressive = True\n')
(config/'GCPadNew.ini').write_text('[GCPad1]\nDevice = XInput2/0/Virtual core pointer\nButtons/Z = Z\nButtons/A = A\nButtons/B = B\nButtons/X = X\nD-Pad/Left = Left\nD-Pad/Right = Right\nD-Pad/Up = Up\nD-Pad/Down = Down\n')
shaders=root/'user/Shaders';shaders.mkdir()
shutil.copyfile(Path(__file__).parents[1]/'VideoMode/Assets/IplEdgeAA.glsl',shaders/'IplEdgeAA.glsl')
x=C.CDLL('libX11.so.6');xt=C.CDLL('libXtst.so.6')
x.XOpenDisplay.argtypes=[C.c_char_p];x.XOpenDisplay.restype=C.c_void_p;d=x.XOpenDisplay(None)
x.XStringToKeysym.argtypes=[C.c_char_p];x.XStringToKeysym.restype=C.c_ulong
x.XKeysymToKeycode.argtypes=[C.c_void_p,C.c_ulong];x.XKeysymToKeycode.restype=C.c_uint
xt.XTestFakeKeyEvent.argtypes=[C.c_void_p,C.c_uint,C.c_int,C.c_ulong]
x.XSync.argtypes=[C.c_void_p,C.c_int]
def frame_count():
    return len(list((root/'user/Dump/Frames').glob('*.png')))

def wait_frames(start,count):
    deadline=time.monotonic()+45
    while frame_count()<start+count:
        if p.poll() is not None:raise RuntimeError('Dolphin exited while awaiting input frames')
        if time.monotonic()>deadline:raise TimeoutError('Native frames stalled during input replay')
        time.sleep(.05)

def key(name,held=.15):
    code=x.XKeysymToKeycode(d,x.XStringToKeysym(name.encode()))
    before=frame_count()
    assert xt.XTestFakeKeyEvent(d,code,1,0)
    x.XSync(d,0);time.sleep(held);wait_frames(before,6)
    assert xt.XTestFakeKeyEvent(d,code,0,0)
    x.XSync(d,0);wait_frames(frame_count(),45)

log=(root/'emulator.log').open('w')
p=subprocess.Popen(['/usr/games/dolphin-emu-nogui','--platform','x11','-u',str(root/'user'),'-C','SYSCONF.IPL.PGS=True','-e',str(args.dol.resolve())],stdout=log,stderr=subprocess.STDOUT)
events=[]
def shot(name):
    if p.poll() is not None:raise RuntimeError('Dolphin exited')
    p.send_signal(signal.SIGSTOP);time.sleep(.2)
    capture(root/(name+'.ppm'))
    frames=sorted((root/'user/Dump/Frames').glob('*.png'),key=lambda a:a.stat().st_mtime_ns)
    event={'capture':name,'completed_frames':len(frames),'input':'XTest -> Dolphin GameCube controller -> SDK Controller.GetState(0)'}
    # SIGSTOP may interrupt the movie writer. Keep the newest complete PNG,
    # rather than a zero-byte or truncated file from the in-flight frame.
    for frame in reversed(frames):
        raw=frame.read_bytes()
        if raw.startswith(b'\x89PNG\r\n\x1a\n') and raw.endswith(b'IEND\xaeB\x60\x82'):
            (root/(name+'-efb.png')).write_bytes(raw)
            event['efb_source']=frame.name;event['efb_sha256']=hashlib.sha256(raw).hexdigest()
            break
    events.append(event)
    p.send_signal(signal.SIGCONT)
try:
    time.sleep(15);capture(root/'unused.ppm',True);time.sleep(2)
    if args.calendar:
        key('Down');shot('01-calendar-two-rows')
        key('a');key('Up');key('Right');key('Up');shot('02-time-parts')
        key('b');key('Down');key('a');key('Right');key('Up');shot('03-date-parts')
        key('b');shot('04-calendar-cancelled')
    else:
        shot('01-default-hidden')
        key('z',2);shot('02-z-held-visible')
        key('Left');shot('03-options-visible')
        key('a');shot('04-editor-visible')
        key('z',1);shot('05-z-hidden-editor')
        key('b');key('z');time.sleep(12);shot('06-reopened-options')
        if args.widescreen:
            key('x');key('Down');key('Down')
            key('a');key('Up');key('a');key('a');shot('07-widescreen-kept')
            key('z');shot('08-widescreen-hidden')
            key('x');shot('09-safe-mode')
    (root/'events.json').write_text(json.dumps(events,indent=2)+'\n')
finally:
    if p.poll() is None:
        p.send_signal(signal.SIGCONT);p.terminate()
        try:p.wait(timeout=5)
        except subprocess.TimeoutExpired:p.kill();p.wait()
    log.close()
