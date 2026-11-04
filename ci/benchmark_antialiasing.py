#!/usr/bin/env python3
"""Measure native presented-frame throughput inside cloud-local Dolphin/Xvfb.
Run inside dnd-cloud-dolphin with --binaries containing on/off/VideoMode.dol+elf
built using IplVisualReview=true and IplAntialiasingBenchmark=true. No frame dumps
or per-frame breakpoints run during timing. GDB samples the SDK's live frame ring.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import socket
import shutil
import statistics
import struct
import subprocess
import time


def send(conn, text):
    data=text.encode();conn.sendall(b'$'+data+b'#'+f'{sum(data)&255:02x}'.encode())


def reply(conn):
    while True:
        byte=conn.recv(1)
        if not byte:raise ConnectionError('GDB disconnected')
        if byte==b'$':break
        if byte not in (b'+',b'-'):raise ValueError('Unexpected GDB framing')
    data=bytearray()
    while True:
        byte=conn.recv(1)
        if not byte:raise ConnectionError('Truncated GDB reply')
        if byte==b'#':break
        data.extend(byte)
    checksum=conn.recv(2)
    if len(checksum)!=2 or int(checksum,16)!=(sum(data)&255):raise ValueError('GDB checksum mismatch')
    conn.sendall(b'+');return data.decode()


def memory(conn,address,size):
    send(conn,f'm{address:x},{size:x}');data=reply(conn)
    # Dolphin 2503 queues an empty response and may repeat its asynchronous
    # stop notification; neither is a response to the memory-read command.
    while data=='' or data.startswith(('T','S')):data=reply(conn)
    if len(data)!=size*2 or not re.fullmatch('[0-9a-fA-F]+',data):raise ValueError('Invalid GDB memory response: '+data)
    return bytes.fromhex(data)


def symbols(elf):
    text=subprocess.run(['nm','-a',str(elf)],capture_output=True,text=True,check=True).stdout
    result={}
    for line in text.splitlines():
        p=line.split()
        if len(p)==3:
            name=p[2].split('.lto_priv.')[0]
            if name in ('interval_head','mode'):result[name]=int(p[0],16)
    if set(result)!=set(['interval_head','mode']):raise ValueError('Missing native SDK telemetry symbols')
    return result


def trial(args,hardware,samples,method,repeat):
    ssaa=method=='ssaa';postshader='FXAA' if method=='fxaa' else 'IplEdgeAA' if method=='edge' else ''
    name=f'hw-{hardware}-samples-{samples}-{method}-r{repeat}'
    root=args.logs/name;config=root/'user/Config';config.mkdir(parents=True)
    if method=='edge':
        shaders=root/'user/Shaders';shaders.mkdir()
        shutil.copyfile(Path(__file__).parents[1]/'VideoMode/Assets/IplEdgeAA.glsl',shaders/'IplEdgeAA.glsl')
    dol=args.binaries/hardware/'VideoMode.dol';elf=dol.with_suffix('.elf');syms=symbols(elf)
    (config/'Dolphin.ini').write_text('[Core]\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\nEmulationSpeed = 1.0\n[DSP]\nBackend = No Audio Output\n[Display]\nRenderWindowXPos = 0\nRenderWindowYPos = 0\nRenderWindowWidth = 640\nRenderWindowHeight = 480\n[General]\nGDBPort = 2174\n')
    (config/'GFX.ini').write_text(f'[Settings]\nInternalResolution = 1\nMSAA = {samples}\nSSAA = {ssaa}\nAspectRatio = 2\n[Enhancements]\nPostProcessingShader = {postshader}\n[Hacks]\nForceProgressive = True\n')
    with (root/'emulator.log').open('w') as log:
        process=subprocess.Popen(['/usr/games/dolphin-emu-nogui','--platform','x11','-u',str(root/'user'),'-C','SYSCONF.IPL.PGS=True','-e',str(dol)],stdout=log,stderr=subprocess.STDOUT)
        try:
            deadline=time.monotonic()+30;conn=None
            while time.monotonic()<deadline:
                if process.poll() is not None:raise RuntimeError('Dolphin exited before GDB attached')
                try:conn=socket.create_connection(('127.0.0.1',2174),timeout=.25);break
                except OSError:time.sleep(.1)
            if conn is None:raise TimeoutError('Dolphin GDB stub did not open')
            with conn:
                conn.settimeout(10)
                head=int.from_bytes(memory(conn,syms['interval_head'],4),'big');send(conn,'c')
                records=[];began=time.perf_counter();resumed=began;warm_until=began+args.warmup
                frames=0;running=0.;last_progress=began;quality=None
                while time.perf_counter()<warm_until+args.seconds:
                    time.sleep(.25)
                    conn.sendall(b'\x03');stop=reply(conn)
                    while stop=='':stop=reply(conn)
                    stopped=time.perf_counter()
                    if not stop.startswith(('T','S')):raise ValueError('Expected a GDB stop packet, got '+repr(stop))
                    current=int.from_bytes(memory(conn,syms['interval_head'],4),'big')
                    if not 0<=current<32:raise ValueError('Unexpected SDK frame ring')
                    delta=(current-head)%32
                    if delta:last_progress=stopped
                    if stopped-last_progress>20:raise RuntimeError('Native presentation stalled')
                    if stopped>=warm_until:
                        # Polls are <32 frames apart at the 60 Hz speed limit.
                        if resumed>=warm_until:
                            frames+=delta;running+=stopped-resumed
                            records.append({'active_seconds':stopped-resumed,'presented_frames':delta})
                        if quality is None:
                            ptr=int.from_bytes(memory(conn,syms['mode'],4),'big');mode=memory(conn,ptr,56)
                            width,efb,xfb=struct.unpack_from('>HHH',mode,4)
                            quality={'hardware_aa':bool(mode[25]),'efb_width':width,'efb_height':efb,'xfb_height':xfb}
                            if quality['hardware_aa']!=(hardware=='on'):raise RuntimeError('Native AA readback did not match candidate: '+str(quality))
                    head=current;send(conn,'c');resumed=time.perf_counter()
                elapsed=resumed-warm_until
                if frames<5:raise RuntimeError('Too few native frames to measure performance')
                result={'candidate':name,'hardware':hardware,'msaa':samples,'ssaa':ssaa,'postshader':postshader,'repeat':repeat,
                        'frames':frames,'active_seconds':running,'wall_seconds':elapsed,'active_fps':frames/running,
                        'wall_fps':frames/elapsed,'native_quality':quality,'dol_sha256':hashlib.sha256(dol.read_bytes()).hexdigest(),'samples':records}
                (root/'result.json').write_text(json.dumps(result,indent=2)+'\n')
                conn.sendall(b'\x03');stop=reply(conn)
                while stop=='':stop=reply(conn)
                snapshot=subprocess.run(['python3',str(Path(__file__).with_name('capture_x11.py')),str(root/'native-window.ppm')],capture_output=True,text=True)
                (root/'capture.log').write_text(snapshot.stdout+snapshot.stderr)
                send(conn,'c')
                print(json.dumps({k:v for k,v in result.items() if k not in ('samples','dol_sha256')}),flush=True)
                return result
        finally:
            process.terminate()
            try:process.wait(timeout=10)
            except subprocess.TimeoutExpired:process.kill();process.wait()


def main(args):
    args.binaries=args.binaries.resolve();args.logs=args.logs.resolve();args.logs.mkdir(parents=True,exist_ok=True)
    caps=json.loads(subprocess.run(['python3',str(Path(__file__).with_name('query_gl_caps.py'))],capture_output=True,text=True,check=True).stdout)
    candidates=[('off',1,'msaa'),('off',4,'msaa'),('off',4,'ssaa'),('off',1,'fxaa'),('on',1,'msaa'),('on',4,'msaa'),('on',4,'ssaa')]
    if args.candidates:candidates=[tuple((p[0],int(p[1]),p[2])) for p in (v.split(':') for v in args.candidates.split(','))]
    results=[]
    for repeat in range(args.repeats):
        # Reverse alternate sweeps to reduce order/warm-cache bias.
        for hardware,samples,method in candidates if repeat%2==0 else reversed(candidates):
            if samples not in caps['dolphin_aa_modes']:
                print('Unsupported requested sample count; Dolphin would disable AA:',samples,flush=True);continue
            results.append(trial(args,hardware,samples,method,repeat))
            report={'renderer':caps,'warmup_seconds':args.warmup,'measurement_seconds':args.seconds,
                    'method':'SDK native presentation counter sampled through GDB every 250 ms; no dumps/breakpoints; frame ring modulo 32; Dolphin speed limit 1.0; LP_NUM_THREADS=2.',
                    'limitation':'Cloud software-renderer throughput, not physical GameCube timing. The optional performance panel is enabled equally in all benchmark DOLs.',
                    'results':results}
            (args.logs/'results.json').write_text(json.dumps(report,indent=2)+'\n')
    for hardware,samples,method in candidates:
        shader='FXAA' if method=='fxaa' else 'IplEdgeAA' if method=='edge' else ''
        values=[r['active_fps'] for r in results if (r['hardware'],r['msaa'],r['ssaa'],r['postshader'])==(hardware,samples,method=='ssaa',shader)]
        if values:print('MEDIAN',hardware,samples,method,round(statistics.median(values),3),flush=True)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--binaries',type=Path,required=True);p.add_argument('--logs',type=Path,required=True)
    p.add_argument('--warmup',type=float,default=8);p.add_argument('--seconds',type=float,default=18)
    p.add_argument('--repeats',type=int,default=1);p.add_argument('--candidates',default='')
    main(p.parse_args())
