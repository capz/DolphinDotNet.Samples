#!/usr/bin/env python3
"""Read-only GDB control for stable native screenshots, not timing benchmarks.

VIDEO_WaitVSync is called after GX_CopyDisp/GX_DrawDone. Stop guest execution
there while Dolphin's host renderer presents the complete XFB. Never mutate
application state or render buffers. Count application boundaries rather than
Dolphin movie dumps, which can include intermediate EFB copies.
"""
from pathlib import Path
import socket
import re
import struct
import subprocess
import sys
import time


class NativeFrames:
    def __init__(self,sdk,dol,process):
        sys.path.insert(0,str(Path(sdk)/'scripts'))
        from dolphin_controller_test import request,memory
        from dolphin_gdb_probe import parse_stop_pc,_send,_reply
        self.request=request;self.memory=memory;self.parse_stop_pc=parse_stop_pc;self.send=_send;self.reply=_reply;self.count=0
        symbols=[];serials=[];self.frame_symbols={};functions={}
        for line in subprocess.run(['nm','-a',str(Path(dol).with_suffix('.elf'))],capture_output=True,text=True,check=True).stdout.splitlines():
            parts=line.split()
            if len(parts)==3 and parts[2]=='VIDEO_WaitVSync':symbols.append(int(parts[0],16))
            if len(parts)==3 and parts[2].split('.lto_priv.')[0]=='overlay_serial':serials.append(int(parts[0],16))
            if len(parts)==3 and parts[2].split('.lto_priv.')[0] in ('xfb','fb','mode'):self.frame_symbols[parts[2].split('.lto_priv.')[0]]=int(parts[0],16)
            if len(parts)==3 and parts[1] in ('T','t'):functions[int(parts[0],16)]=parts[2]
        assert len(symbols)==len(serials)==1;self.address=symbols[0];self.serial=serials[0]
        # Match the call *inside* the SDK's completed end-frame function, rather
        # than any use of the library retrace wait (including video setup).
        frame=next(a for a,n in functions.items() if n=='dnd_graphics_end_frame');end=min(a for a in functions if a>frame)
        raw=Path(dol).read_bytes();calls=[]
        for section in range(7):
            offset=struct.unpack_from('>I',raw,section*4)[0];address=struct.unpack_from('>I',raw,0x48+section*4)[0];size=struct.unpack_from('>I',raw,0x90+section*4)[0]
            for at in range(max(0,frame-address),min(size,end-address),4):
                instruction=struct.unpack_from('>I',raw,offset+at)[0]
                if instruction>>26!=18 or not instruction&1:continue
                delta=instruction&0x3fffffc
                if delta&(1<<25):delta-=1<<26
                target=delta if instruction&2 else address+at+delta
                if target==self.address:calls.append(address+at)
        assert len(calls)==1;self.address=calls[0]
        deadline=time.monotonic()+30;self.connection=None
        while time.monotonic()<deadline:
            if process.poll() is not None:raise RuntimeError('Dolphin exited before debugger connection')
            try:self.connection=socket.create_connection(('127.0.0.1',2174),timeout=.3);break
            except OSError:time.sleep(.1)
        if self.connection is None:raise TimeoutError('No native frame-boundary debugger')
        self.connection.settimeout(45)
        self.connection.setsockopt(socket.IPPROTO_TCP,socket.TCP_NODELAY,1)
        assert self.request(self.connection,f'Z0,{self.address:x},4')=='OK'

    def advance(self,count):
        if count<=0:return
        target=self.count+count;deadline=time.monotonic()+90
        while self.count<target:
            # Never interrupt the guest inside a GX FIFO primitive. Run freely,
            # then install a breakpoint at the completed presentation boundary.
            assert self.request(self.connection,f'z0,{self.address:x},4')=='OK'
            self.send(self.connection,'c')
            time.sleep(min(.3,max(.08,(target-self.count)/60)))
            assert self.request(self.connection,f'Z0,{self.address:x},4')=='OK'
            reply=self.request(self.connection,'c',45)
            if self.parse_stop_pc(reply)!=self.address:raise RuntimeError('Unexpected native presentation stop: '+reply)
            self.count=self.read_serial()
            if time.monotonic()>deadline:raise TimeoutError('Application frame counter stalled')

    def read_serial(self):
        for _ in range(4):
            reply=self.request(self.connection,f'm{self.serial:x},4')
            if re.fullmatch('[0-9a-fA-F]{8}',reply):return int(reply,16)
        raise RuntimeError('Could not read paused native presentation counter')

    def close(self):
        if self.connection is not None:self.connection.close()

    def stable_frame(self):
        previous=self.read_serial()
        for _ in range(20):
            time.sleep(.1);current=self.read_serial()
            if current==previous:self.count=current;return current
            previous=current
        raise RuntimeError('Guest did not remain paused for native capture')

    def read_bytes(self,address,length):
        result=bytearray()
        for offset in range(0,length,2048):
            n=min(2048,length-offset);reply=self.request(self.connection,f'm{address+offset:x},{n:x}')
            if not re.fullmatch('[0-9a-fA-F]{'+str(n*2)+'}',reply):raise RuntimeError('Incomplete native framebuffer read: '+reply[:32])
            result.extend(bytes.fromhex(reply))
        return bytes(result)

    def capture_xfb(self,path):
        mode=int.from_bytes(self.read_bytes(self.frame_symbols['mode'],4),'big');dimensions=self.read_bytes(mode,10)
        width=int.from_bytes(dimensions[4:6],'big');height=int.from_bytes(dimensions[8:10],'big')
        index=int.from_bytes(self.read_bytes(self.frame_symbols['fb'],4),'big');assert index in (0,1)
        address=int.from_bytes(self.read_bytes(self.frame_symbols['xfb']+index*4,4),'big')
        assert 0<width<=1024 and 0<height<=1024 and width%16==0
        # The SDK stores XFB pointers in the uncached MEM1 mirror.
        address=(address&0x3fffffff)|0x80000000
        path.write_bytes(self.read_bytes(address,width*height*2))
        tv_mode=int.from_bytes(dimensions[:4],'big')
        return dict(width=width,height=height,efb_height=int.from_bytes(dimensions[6:8],'big'),
                    vi_tv_mode=tv_mode,vi_scan_bits=tv_mode&3,format='YUYV422',
                    source='Native GX_CopyDisp output, read after GX_DrawDone before VIDEO_WaitVSync')
