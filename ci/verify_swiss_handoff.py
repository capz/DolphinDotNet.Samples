#!/usr/bin/env python3
"""Verify exact argv in the actual official Swiss ELF after managed chainloading.
Run only in a cloud X11 display with Dolphin NoGUI; uses an original data ISO.
"""
import argparse,hashlib,json,signal,socket,struct,subprocess,time
from pathlib import Path
from benchmark_antialiasing import memory,reply,send
from capture_x11 import capture
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--caller',type=Path,required=True);p.add_argument('--swiss-elf',type=Path,required=True);p.add_argument('--disc',type=Path,required=True);p.add_argument('--logs',type=Path,required=True);a=p.parse_args()
root=a.logs.resolve();config=root/'user/Config';config.mkdir(parents=True)
(config/'Dolphin.ini').write_text('[Core]\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\nDefaultISO = '+str(a.disc.resolve())+'\n[DSP]\nBackend = No Audio Output\n[General]\nGDBPort = 2174\n[Movie]\nDumpFrames = True\nDumpFramesSilent = True\n')
(config/'GFX.ini').write_text('[Settings]\nDumpFramesAsImages = True\nInternalResolution = 1\n')
def symbols(elf):
 result={}
 for line in subprocess.run(['nm','-a',str(elf)],capture_output=True,text=True,check=True).stdout.splitlines():
  fields=line.split()
  if len(fields)==3:result[fields[2].split('.lto_priv.')[0]]=int(fields[0],16)
 return result
swiss=symbols(a.swiss_elf);caller=symbols(a.caller.with_suffix('.elf'));expected=['swiss-gc.dol','AutoBoot=No','Force NTSC Video Mode=480p','Force PAL Video Mode=576p','Prefer Clean Boot=No']
with (root/'emulator.log').open('w') as log:
 proc=subprocess.Popen(['/usr/games/dolphin-emu-nogui','--platform','x11','-u',str(root/'user'),'-C','SYSCONF.IPL.PGS=True','-e',str(a.caller.resolve())],stdout=log,stderr=subprocess.STDOUT)
 try:
  sock=None;deadline=time.monotonic()+30
  while time.monotonic()<deadline:
   if proc.poll() is not None:raise RuntimeError('Emulator exited')
   try:sock=socket.create_connection(('127.0.0.1',2174),timeout=.3);break
   except OSError:time.sleep(.1)
  if sock is None:raise TimeoutError('No debugger')
  with sock:
   sock.settimeout(10);memory(sock,0x80000000,4);send(sock,'c');time.sleep(30);sock.sendall(b'\x03');stop=reply(sock)
   while not stop.startswith(('T','S')):stop=reply(sock)
   pointer=int.from_bytes(memory(sock,swiss['__system_argv'],4),'big');received=[];diagnostic=''
   if 0x80003100<=pointer<=0x817fff00:
    header=struct.unpack('>6I',memory(sock,pointer,24));magic,command,length,count,args,end=header
    if magic==0x5f617267 and count<=64 and 0x80003100<=args<0x817ffe00:
     for i in range(count):
      address=int.from_bytes(memory(sock,args+i*4,4),'big')
      if not 0x80000000<=address<0x817fff00:break
      received.append(memory(sock,address,128).split(b'\0')[0].decode('utf-8',errors='replace'))
   if received!=expected:
    address=int.from_bytes(memory(sock,caller['dnd_static_Program_Status'],4),'big')
    if 0x80003100<=address<0x817fff00:
     size=int.from_bytes(memory(sock,address+8,4),'big')
     if size<=512:diagnostic=memory(sock,address+12,size*2).decode('utf-16-be')
   native_error=''
   if received!=expected and 'exception_text' in caller:
    error=int.from_bytes(memory(sock,caller['exception_text'],4),'big')
    if 0x80003100<=error<0x817fff00:native_error=memory(sock,error,192).split(b'\0')[0].decode('utf-8',errors='replace')
   proc.send_signal(signal.SIGSTOP);capture(root/'native-window.ppm')
   for frame in sorted((root/'user/Dump/Frames').glob('*.png'),key=lambda p:p.stat().st_mtime_ns,reverse=True):
    raw=frame.read_bytes()
    if raw.endswith(b'IEND\xaeB`\x82'):(root/'native-efb.png').write_bytes(raw);break
   report={'environment':'Real official Swiss v0.6r2119 after managed DOL chainload in cloud Dolphin 2503; not physical GC Loader/game autoboot','caller_sha256':hashlib.sha256(a.caller.read_bytes()).hexdigest(),'fixture_iso_sha256':hashlib.sha256(a.disc.read_bytes()).hexdigest(),'expected_argv':expected,'received_argv':received,'caller_diagnostic':diagnostic,'native_error':native_error,'passed':received==expected}
   (root/'result.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report));assert report['passed'],'Official Swiss did not receive exact argv'
 finally:
  if proc.poll() is None:
   proc.send_signal(signal.SIGCONT);proc.terminate()
   try:proc.wait(timeout=5)
   except subprocess.TimeoutExpired:proc.kill();proc.wait()
