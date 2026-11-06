#!/usr/bin/env python3
"""Read-only GDB control for stable native screenshots, not timing benchmarks.

VIDEO_WaitVSync is called after GX_CopyDisp/GX_DrawDone. Stop guest execution
there while Dolphin's host renderer presents the complete XFB. Never mutate
application state or render buffers. Count application boundaries rather than
Dolphin movie dumps, which can include intermediate EFB copies.
"""
from pathlib import Path
import socket
import subprocess
import sys
import time


class NativeFrames:
    def __init__(self,sdk,dol,process):
        sys.path.insert(0,str(Path(sdk)/'scripts'))
        from dolphin_controller_test import request,memory
        from dolphin_gdb_probe import parse_stop_pc,_send,_reply
        self.request=request;self.memory=memory;self.parse_stop_pc=parse_stop_pc;self.send=_send;self.reply=_reply;self.count=0
        symbols=[];serials=[]
        for line in subprocess.run(['nm','-a',str(Path(dol).with_suffix('.elf'))],capture_output=True,text=True,check=True).stdout.splitlines():
            parts=line.split()
            if len(parts)==3 and parts[2]=='VIDEO_WaitVSync':symbols.append(int(parts[0],16))
            if len(parts)==3 and parts[2].split('.lto_priv.')[0]=='overlay_serial':serials.append(int(parts[0],16))
        assert len(symbols)==len(serials)==1;self.address=symbols[0];self.serial=serials[0]
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
        assert self.request(self.connection,f'z0,{self.address:x},4')=='OK'
        self.send(self.connection,'c')
        while True:
            time.sleep(.1);self.connection.sendall(b'\x03');reply=self.reply(self.connection,time.monotonic()+10)
            while not reply.startswith(('T','S')):reply=self.reply(self.connection,time.monotonic()+10)
            self.count=int.from_bytes(self.memory(self.connection,self.serial,4),'big')
            if self.count>=target:break
            if time.monotonic()>deadline:raise TimeoutError('Application frame counter stalled')
            self.send(self.connection,'c')
        assert self.request(self.connection,f'Z0,{self.address:x},4')=='OK'
        # Dolphin can send a second stop notification after a raw interrupt.
        # Continue through those notifications and require the actual BP PC.
        for _ in range(8):
            reply=self.request(self.connection,'c',45)
            if self.parse_stop_pc(reply)==self.address:break
        else:raise RuntimeError('Unexpected guest stop instead of native presentation boundary: '+reply)
        self.count=int.from_bytes(self.memory(self.connection,self.serial,4),'big')

    def close(self):
        if self.connection is not None:self.connection.close()
