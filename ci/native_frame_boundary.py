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


def _receive(connection,length,deadline,flags=0):
    remaining=deadline-time.monotonic()
    if remaining<=0:raise TimeoutError('Native memory reply deadline exceeded')
    connection.settimeout(remaining)
    data=connection.recv(length,flags)
    if not data:raise ConnectionError('Truncated native memory reply')
    return data


def _exact(connection,length,deadline):
    result=bytearray()
    while len(result)<length:result.extend(_receive(connection,length-len(result),deadline))
    return bytes(result)


def _memory_reply(connection,deadline):
    """Read one checksummed packet without buffering bytes belonging to SDK controls.

    A peek can include the checksum or the next packet. Consume only through
    this packet's delimiter, then exactly its two checksum digits. Guest memory
    is hex-encoded, so its payload cannot contain an escaped '#' delimiter.
    """
    while True:
        header=_exact(connection,1,deadline)
        if header==b'$':break
        if header not in (b'+',b'-'):raise ValueError('Unexpected native GDB framing byte: '+repr(header))
    payload=bytearray()
    while True:
        preview=_receive(connection,min(4096,8193-len(payload)),deadline,socket.MSG_PEEK)
        marker=preview.find(b'#')
        count=marker+1 if marker>=0 else len(preview)
        added=count-1 if marker>=0 else count
        if len(payload)+added>8192:raise ValueError('Oversized native memory reply')
        consumed=_exact(connection,count,deadline)
        payload.extend(consumed[:-1] if marker>=0 else consumed)
        if marker>=0:break
    checksum=_exact(connection,2,deadline)
    if not re.fullmatch(b'[0-9a-fA-F]{2}',checksum) or int(checksum,16)!=(sum(payload)&255):
        raise ValueError('Invalid native memory reply checksum')
    connection.sendall(b'+')
    return bytes(payload)


class NativeFrames:
    def __init__(self,sdk,dol,process):
        sys.path.insert(0,str(Path(sdk)/'scripts'))
        from dolphin_controller_test import request,memory
        from dolphin_gdb_probe import parse_stop_pc,_send,_reply
        self.request=request;self.memory=memory;self.parse_stop_pc=parse_stop_pc;self.send=_send;self.reply=_reply;self.count=0
        symbols=[];serials=[];self.frame_symbols={};functions={};self.diagnostic_symbols={}
        diagnostic_names={'_thr_main','_thr_idle','_thr_executing','_thr_heir',
                          '_thread_dispatch_disable_level','_context_switch_want','_lwp_thr_objects',
                          'video_queue','retraceCount','_gxwaitfinish','_gxfinished','_gxoverflowsuspend',
                          '__aesnddspinit','__aesnddspcomplete','__aesnddspstarttime',
                          '__aesnddspprocesstime','__aesndcurrab','__aesndinit',
                          '__aesndcurrvoice','__aesndvoicesstopped','__aesndglobalpause',
                          '__dsp_currtask','__aesnddsptask'}
        for line in subprocess.run(['nm','-a',str(Path(dol).with_suffix('.elf'))],capture_output=True,text=True,check=True).stdout.splitlines():
            parts=line.split()
            if len(parts)==3 and parts[2]=='VIDEO_WaitVSync':symbols.append(int(parts[0],16))
            if len(parts)==3 and parts[2].split('.lto_priv.')[0]=='overlay_serial':serials.append(int(parts[0],16))
            if len(parts)==3 and parts[2].split('.lto_priv.')[0] in ('xfb','fb','mode'):self.frame_symbols[parts[2].split('.lto_priv.')[0]]=int(parts[0],16)
            if len(parts)==3 and parts[1] in ('T','t'):functions[int(parts[0],16)]=parts[2]
            if len(parts)==3 and parts[2] in diagnostic_names:self.diagnostic_symbols[parts[2]]=int(parts[0],16)
        self.functions=functions
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
            if process.poll() is not None:raise RuntimeError(f'Dolphin exited before debugger connection (exit {process.returncode}); inspect emulator.log')
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
            # Run without a breakpoint, then consume the rearm acceptance and
            # actual stop together. Another Continue after rearming could resume
            # past an already queued stop and leave later reads unsynchronized.
            before=self.count
            assert self.request(self.connection,f'z0,{self.address:x},4')=='OK'
            self.send(self.connection,'c')
            time.sleep(min(.3,max(.08,(target-self.count)/60)))
            self.pause_at_boundary()
            if self.count<=before:raise RuntimeError('Native presentation counter did not advance')
            if time.monotonic()>deadline:raise TimeoutError('Application frame counter stalled')

    def pause_at_boundary(self):
        """Rearm after an uninterrupted run, consuming both OK and its stop.

        The guest is already running. Do not issue another Continue or use the
        generic request helper, which discards unsolicited stop notifications.
        """
        self.send(self.connection,f'Z0,{self.address:x},4')
        accepted=stopped=False;deadline=time.monotonic()+45
        while not(accepted and stopped):
            reply=self.reply(self.connection,deadline)
            if reply=='OK':accepted=True
            elif reply.startswith('T05'):
                if self.parse_stop_pc(reply)!=self.address:raise RuntimeError('Unexpected native presentation stop: '+reply)
                stopped=True
            elif reply!='':raise RuntimeError('Unexpected boundary breakpoint reply: '+reply)
        self.count=self.read_serial()

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
        if not isinstance(address,int) or not isinstance(length,int) or address<0 or length<0 or address>0xffffffff or length>0x100000000-address:
            raise ValueError('Native memory read is outside the 32-bit address space')
        result=bytearray()
        for offset in range(0,length,2048):
            n=min(2048,length-offset);self.send(self.connection,f'm{address+offset:x},{n:x}')
            deadline=time.monotonic()+10;reply=_memory_reply(self.connection,deadline)
            # Retain the SDK request helper's treatment of unsolicited stops and
            # empty packets, while leaving ordinary control requests untouched.
            while reply==b'' or reply.startswith(b'T05'):reply=_memory_reply(self.connection,deadline)
            if len(reply)!=n*2 or not re.fullmatch(b'[0-9a-fA-F]*',reply):
                raise RuntimeError('Incomplete native framebuffer read: '+repr(reply[:32]))
            result.extend(bytes.fromhex(reply.decode('ascii')))
        return bytes(result)

    def describe_xfb(self):
        mode=int.from_bytes(self.read_bytes(self.frame_symbols['mode'],4),'big');dimensions=self.read_bytes(mode,26)
        if dimensions[24] not in (0,1) or dimensions[25] not in (0,1):
            raise ValueError('Invalid native field-rendering/antialiasing flags')
        width=int.from_bytes(dimensions[4:6],'big');height=int.from_bytes(dimensions[8:10],'big')
        assert 0<width<=1024 and 0<height<=1024 and width%16==0
        tv_mode=int.from_bytes(dimensions[:4],'big')
        return dict(width=width,height=height,efb_height=int.from_bytes(dimensions[6:8],'big'),
                    vi_tv_mode=tv_mode,vi_scan_bits=tv_mode&3,field_rendering=bool(dimensions[24]),hardware_aa=bool(dimensions[25]),format='YUYV422',
                    source='Native GX_CopyDisp output, read after GX_DrawDone before VIDEO_WaitVSync')

    def capture_xfb(self,path):
        info=self.describe_xfb();width=info['width'];height=info['height']
        index=int.from_bytes(self.read_bytes(self.frame_symbols['fb'],4),'big');assert index in (0,1)
        address=int.from_bytes(self.read_bytes(self.frame_symbols['xfb']+index*4,4),'big')
        address=(address&0x3fffffff)|0x80000000
        path.write_bytes(self.read_bytes(address,width*height*2))
        return info

    def failure_snapshot(self):
        # Read-only diagnostics after a failed replay; no guest calls or writes.
        self.connection.sendall(b'\x03')
        stop=self.reply(self.connection,time.monotonic()+8)
        def symbol(address):
            candidates=[a for a in self.functions if a<=address]
            base=max(candidates) if candidates else 0
            return dict(address=hex(address),symbol=self.functions.get(base,''),offset=address-base)
        result={'stop':stop,'pc':symbol(self.parse_stop_pc(stop)),'stack':[],
                'last_boundary_serial':self.count}
        try:result['stopped_frame_serial']=self.read_serial()
        except Exception as error:
            result['counter_error']=str(error);return result
        for name,reg in [('lr','43'),('sp','1')]:
            raw=self.request(self.connection,'p'+reg,8)
            if re.fullmatch('[0-9a-fA-F]{8}',raw):result[name]=int(raw,16)
        if 'lr' in result:result['lr_symbol']=symbol(result['lr'])
        def valid(address,length=4):return 0x80000000<=address<=0x81800000-length
        def word(data,offset):return int.from_bytes(data[offset:offset+4],'big')
        def stack(sp):
            frames=[]
            for _ in range(16):
                if not valid(sp,8):break
                previous=word(self.read_bytes(sp,4),0)
                if not sp<previous or not valid(previous,8):break
                saved=word(self.read_bytes(previous+4,4),0)
                frames.append(dict(sp=hex(sp),**symbol(saved)));sp=previous
            return frames
        result['stack']=stack(result.get('sp',0))
        # These offsets come from the pinned libogc2 ELF's DWARF, not a portable
        # LWP ABI. They only explain failed runs and never determine test passes.
        # A normal _cpu_context_switch saves LR/SP, but leaves SRR0 stale.
        lwp={'layout':'Pinned libogc2: lwp_cntrl.context=0xf8, frame_context size=0x2d0; diagnostic only',
             'globals':{}}
        result['lwp']=lwp
        try:
            byte_names={'__aesndvoicesstopped','__aesndglobalpause'}
            wide_names={'__aesnddspstarttime','__aesnddspprocesstime'}
            for name,address in getattr(self,'diagnostic_symbols',{}).items():
                if name in ('_lwp_thr_objects','__aesnddsptask'):continue
                length=1 if name in byte_names else 8 if name in wide_names else 4
                value=int.from_bytes(self.read_bytes(address,length),'big')
                lwp['globals'][name]=dict(address=hex(address),value=value)
            globals_=lwp['globals']
            states={1:'dormant',2:'suspended',4:'transient',8:'delaying',0x10:'waiting_for_time',
                    0x20:'waiting_for_buffer',0x40:'waiting_for_segment',0x80:'waiting_for_message',
                    0x100:'waiting_for_event',0x200:'waiting_for_mutex',0x400:'waiting_for_semaphore',
                    0x800:'waiting_for_condvar',0x1000:'waiting_for_join',0x2000:'waiting_for_rpc_reply',
                    0x4000:'waiting_for_period',0x8000:'waiting_for_signal',0x10000:'waiting_for_join_at_exit',
                    0x10000000:'interruptible_by_signal'}
            def thread(address,include_stack=False):
                data=self.read_bytes(address,0x1b8);state=word(data,0x20)
                waiting=dict(id=word(data,0x30),count=word(data,0x34),return_code=word(data,0x44),
                             queue=hex(word(data,0x54)))
                waiting['matching_handles']=[name for name in ('video_queue','_gxwaitfinish')
                                             if name in globals_ and globals_[name]['value']==waiting['id']]
                context=dict(sp=hex(word(data,0x108)),lr=symbol(word(data,0x1a8)),
                             srr0_stale=symbol(word(data,0xfc)),msr=hex(word(data,0x1b4)))
                info=dict(address=hex(address),id=word(data,8),priority=data[0x10],state=state,
                          state_names=[name for bit,name in states.items() if state&bit] or ['ready'],
                          wait=waiting,entry=symbol(word(data,0x98)),context=context,
                          stack_base=hex(word(data,0xa0)),stack_size=word(data,0xa4))
                if include_stack:
                    info['stack']=stack(word(data,0x108))
                    queue=word(data,0x54)
                    if state&0xbee0 and valid(queue,0x40):
                        raw=self.read_bytes(queue,0x40)
                        waiting.update(sync_state=word(raw,0x30),mode=word(raw,0x34),
                                       state=word(raw,0x38),timeout_status=word(raw,0x3c))
                        if state&0x200 and valid(queue,0x5c):
                            mutex=self.read_bytes(queue,0x5c)
                            waiting['mutex']=dict(lock=word(mutex,0x4c),nest_count=word(mutex,0x50),
                                                  blocked_count=word(mutex,0x54),holder=hex(word(mutex,0x58)))
                        elif state&0x400 and valid(queue,0x4c):
                            waiting['semaphore_count']=word(self.read_bytes(queue+0x48,4),0)
                return info
            main=globals_.get('_thr_main',{}).get('value',0)
            if valid(main,0x1b8):lwp['main']=thread(main,True)
            objects=getattr(self,'diagnostic_symbols',{}).get('_lwp_thr_objects',0)
            if valid(objects,0x28):
                raw=self.read_bytes(objects,0x28);count=word(raw,8);table=word(raw,0x10)
                lwp['thread_objects']=dict(address=hex(objects),max_nodes=count,local_table=hex(table))
                if 0<count<=256 and valid(table,min(count,32)*4):
                    entries=self.read_bytes(table,min(count,32)*4);lwp['threads']=[]
                    for offset in range(0,len(entries),4):
                        address=word(entries,offset)
                        if valid(address,0x1b8) and address!=main:
                            lwp['threads'].append(thread(address))
            task=getattr(self,'diagnostic_symbols',{}).get('__aesnddsptask',0)
            if valid(task,0x40):
                raw=self.read_bytes(task,0x40)
                lwp['aesnd_task']=dict(address=hex(task),state=word(raw,0),flags=word(raw,8),
                                       iram_length=word(raw,0x10),vectors=raw[0x24:0x28].hex())
        except Exception as error:
            lwp['error']=str(error)
        return result
