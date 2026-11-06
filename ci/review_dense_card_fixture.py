#!/usr/bin/env python3
"""Seed only fresh isolated Dolphin cards with the original dense test DOL."""
import argparse
import hashlib
import json
from pathlib import Path
import socket
import subprocess
import sys
import time

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--sdk',type=Path,required=True);parser.add_argument('--dol',type=Path,required=True);parser.add_argument('--logs',type=Path,required=True)
args=parser.parse_args();sys.path.insert(0,str(args.sdk/'scripts'))
from dolphin_controller_test import memory
from dolphin_gdb_probe import _send,_reply

root=args.logs.resolve();root.mkdir(parents=True,exist_ok=False);config=root/'user/Config';config.mkdir(parents=True)
(config/'Dolphin.ini').write_text('[Core]\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\nEmulationSpeed = 0\nSlotA = 1\nSlotB = 1\n[DSP]\nBackend = No Audio Output\n[General]\nGDBPort = 2174\n')
symbols={}
for line in subprocess.run(['nm','-a',str(args.dol.with_suffix('.elf'))],capture_output=True,text=True,check=True).stdout.splitlines():
    fields=line.split()
    if len(fields)==3:
        for name in ['Result','Steps','Status','Probe']:
            if fields[2].split('.lto_priv.')[0]=='dnd_static_Program_'+name:symbols[name]=int(fields[0],16)
assert set(['Result','Status','Probe'])<=symbols.keys()
with (root/'emulator.log').open('w') as log:
    process=subprocess.Popen(['/usr/games/dolphin-emu-nogui','--platform','x11','-u',str(root/'user'),'-e',str(args.dol.resolve())],stdout=log,stderr=subprocess.STDOUT)
    try:
        deadline=time.monotonic()+30;connection=None
        while time.monotonic()<deadline:
            try:connection=socket.create_connection(('127.0.0.1',2174),timeout=.3);break
            except OSError:time.sleep(.1)
        if connection is None:raise TimeoutError('No native debugger')
        with connection:
            memory(connection,symbols['Result'],4);_send(connection,'c');deadline=time.monotonic()+600
            while True:
                time.sleep(5);connection.sendall(b'\x03');reply=_reply(connection,time.monotonic()+10)
                while not reply.startswith(('T','S')):reply=_reply(connection,time.monotonic()+10)
                array=int.from_bytes(memory(connection,symbols['Result'],4),'big');result=int.from_bytes(memory(connection,array+24,4),'big',signed=True)
                steps=int.from_bytes(memory(connection,symbols['Steps'],4),'big') if 'Steps' in symbols else None;print('Native fixture result:',result,'progress:',steps,flush=True)
                if result!=0:break
                if time.monotonic()>deadline:raise TimeoutError('Dense native fixture did not complete')
                _send(connection,'c')
            address=int.from_bytes(memory(connection,symbols['Status'],4),'big');length=int.from_bytes(memory(connection,address+8,4),'big') if address else 0;assert length<512
            status=memory(connection,address+12,length*2).decode('utf-16-be') if address else '(null managed message)'
            report={'environment':'Native SDK -> fresh isolated Dolphin cards, not physical hardware','dol_sha256':hashlib.sha256(args.dol.read_bytes()).hexdigest(),'result':result,'steps':steps,'status':status,'passed':result==1}
            (root/'result.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report),flush=True);assert report['passed'],status
    finally:
        process.terminate()
        try:process.wait(timeout=5)
        except subprocess.TimeoutExpired:process.kill();process.wait()

# Verify persisted directory checksums and native block sizes after clean shutdown.
from verify_card_browser_result import directory
a=directory((root/'user/GC/MemoryCardA.EUR.raw').read_bytes());b=directory((root/'user/GC/MemoryCardB.EUR.raw').read_bytes())
assert len(a)==117 and len(b)==2,(len(a),len(b))
assert set(v[2] for v in a.values())=={1,2,3}
report.update(persisted_slot_a_saves=len(a),persisted_slot_b_saves=len(b),block_sizes=[1,2,3],directory_checksums_valid=True)
(root/'result.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report),flush=True)
