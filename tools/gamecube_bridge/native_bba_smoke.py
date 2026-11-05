#!/usr/bin/env python3
"""Native DOL -> Dolphin emulated BBA -> Python telemetry codec.

Run inside dnd-cloud-dolphin/Xvfb on an isolated Docker network with container IP
10.0.2.2. Build the public fixture using IplRunnerHost=10.0.2.2 and the token below.
This test never claims physical hardware, Microsoft relay or resident-return proof.
"""
import argparse
import hashlib
import json
from pathlib import Path
import socket
import subprocess

import protocol

FIXTURE_TOKEN = '0123456789abcdef0123456789abcdef'


def main(args):
    root = args.logs.resolve()
    if root.exists() and any(root.iterdir()):
        raise ValueError('Use a fresh smoke-test directory')
    config = root / 'user/Config'
    config.mkdir(parents=True)
    (config / 'Dolphin.ini').write_text('[Core]\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\nSerialPort1 = 12\nBBA_BUILTIN_IP = 10.0.2.15\n[DSP]\nBackend = No Audio Output\n')
    (config / 'GFX.ini').write_text('[Settings]\nInternalResolution = 1\nMSAA = 1\nSSAA = False\nAspectRatio = 2\n[Hacks]\nForceProgressive = True\n')
    with socket.socket() as listener:
        listener.bind(('0.0.0.0', 6281));listener.listen(1);listener.settimeout(150)
        with (root / 'emulator.log').open('w') as log:
            emulator = subprocess.Popen(['/usr/games/dolphin-emu-nogui', '--platform', 'x11', '-u', str(root / 'user'), '-C', 'SYSCONF.IPL.PGS=True', '-e', str(args.dol.resolve())], stdout=log, stderr=subprocess.STDOUT)
            try:
                stream, peer = listener.accept()
                with stream:
                    stream.settimeout(15)
                    token, rows, digest = protocol.telemetry(stream)
                    if token != FIXTURE_TOKEN:
                        raise ValueError('Unexpected smoke-test fixture token')
                    summary, csv = protocol.results(rows)
                    (root / 'frames.csv').write_text(csv)
                    report = {'target': 'Dolphin emulated BBA, NOT physical GameCube', 'frames': len(rows),
                              'peer': peer, 'summary': summary, 'payload_sha256': digest,
                              'dol_sha256': hashlib.sha256(args.dol.read_bytes()).hexdigest(),
                              'note': 'No debugger, frame dumps or host interrupts during sampling. Callback only after the full native window.'}
                    (root / 'result.json').write_text(json.dumps(report, indent=2) + '\n')
                    stream.sendall(b'OKAY')
                    print(json.dumps(report), flush=True)
            finally:
                emulator.terminate()
                try:emulator.wait(timeout=5)
                except subprocess.TimeoutExpired:emulator.kill();emulator.wait()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dol', type=Path, required=True)
    parser.add_argument('--logs', type=Path, required=True)
    main(parser.parse_args())
