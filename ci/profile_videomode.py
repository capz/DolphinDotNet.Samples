#!/usr/bin/env python3
"""Extract the bounded native frame log after an uninterrupted profiling run.

Run inside dnd-cloud-dolphin/Xvfb. Build with IplPerformanceReview=true and
IplPerformanceReviewScene matching --scene (Options by default).
No debugger interrupts, screenshots, frame dumps or file writes during sampling.
The guest-clock timings are Dolphin measurements, NOT physical GameCube results.
"""
import argparse
import csv
import hashlib
import io
import json
import math
from pathlib import Path
import shutil
import socket
import statistics
import struct
import subprocess
import time
from benchmark_antialiasing import memory, reply, send

FIELDS = 'frame_us prepare_us submit_us end_us gpu_wait_us retrace_us capture_us blur_us draws triangles begin_copy_us gate_us width height scan face editing'.split()


def summarize(rows):
    result = {}
    for field in FIELDS[:12] + ['active_us']:
        values = sorted(row[field] if field != 'active_us' else row['frame_us'] - row['retrace_us'] for row in rows)
        result[field] = {'mean': statistics.mean(values), 'median': statistics.median(values),
                         'p95': values[math.ceil(len(values) * .95) - 1], 'max': max(values)}
    return result


def run(args):
    root = args.logs.resolve()
    if root.exists() and any(root.iterdir()):
        raise ValueError('Use a fresh log directory')
    config = root / 'user/Config'
    config.mkdir(parents=True)
    shaders = root / 'user/Shaders'
    shaders.mkdir()
    shutil.copyfile(Path(__file__).parents[1] / 'VideoMode/Assets/IplEdgeAA.glsl', shaders / 'IplEdgeAA.glsl')
    (config / 'Dolphin.ini').write_text('[Core]\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\nEmulationSpeed = 1.0\n[DSP]\nBackend = No Audio Output\n[Display]\nRenderWindowXPos = 0\nRenderWindowYPos = 0\nRenderWindowWidth = 640\nRenderWindowHeight = 480\n[Interface]\nOnScreenDisplayMessages = False\n[General]\nGDBPort = 2174\n')
    (config / 'GFX.ini').write_text('[Settings]\nInternalResolution = 1\nMSAA = 1\nSSAA = False\nAspectRatio = 2\n[Enhancements]\nPostProcessingShader = IplEdgeAA\n[Hacks]\nForceProgressive = True\n')
    dol = args.dol.resolve()
    table = subprocess.run(['nm', '-a', str(dol.with_suffix('.elf'))], check=True, text=True, capture_output=True).stdout
    symbols = {}
    for line in table.splitlines():
        parts = line.split()
        if len(parts) == 3:
            for name in ('Count', 'Ready', 'Samples', 'Status', 'Summary'):
                if parts[2].split('.lto_priv.')[0] == 'dnd_static_IplFrameProfiler_' + name:
                    symbols[name] = int(parts[0], 16)
    if not {'Count', 'Ready', 'Samples'} <= symbols.keys():
        raise ValueError('Missing profiling symbols: ' + repr(symbols))
    with (root / 'emulator.log').open('w') as log:
        process = subprocess.Popen(['/usr/games/dolphin-emu-nogui', '--platform', 'x11', '-u', str(root / 'user'), '-C', 'SYSCONF.IPL.PGS=True', '-e', str(dol)], stdout=log, stderr=subprocess.STDOUT)
        try:
            deadline = time.monotonic() + 30
            conn = None
            while time.monotonic() < deadline:
                if process.poll() is not None:
                    raise RuntimeError('Dolphin exited before debugger attached')
                try:
                    conn = socket.create_connection(('127.0.0.1', 2174), timeout=.25)
                    break
                except OSError:
                    time.sleep(.1)
            if conn is None:
                raise TimeoutError('No Dolphin debugger')
            with conn:
                conn.settimeout(15)
                memory(conn, symbols['Count'], 4)  # Complete initial GDB handshake.
                send(conn, 'c')
                # Deliberately no interrupts until the entire window has completed.
                time.sleep(args.wait)
                conn.sendall(b'\x03')
                stop = reply(conn)
                while not stop.startswith(('T', 'S')):
                    stop = reply(conn)
                count = int.from_bytes(memory(conn, symbols['Count'], 4), 'big')
                ready = int.from_bytes(memory(conn, symbols['Ready'], 4), 'big')
                if count != 96 or not ready:
                    raise RuntimeError(f'Incomplete uninterrupted run: {count}/96, ready={ready}; rerun with a longer --wait')
                pointer = int.from_bytes(memory(conn, symbols['Samples'], 4), 'big')
                header = struct.unpack('>6I', memory(conn, pointer, 24))
                if header[2:4] != (96 * len(FIELDS), 4):
                    raise ValueError('Unexpected managed int[] layout: ' + repr(header))
                raw = b''.join(memory(conn, pointer + 24 + at, min(512, 96 * len(FIELDS) * 4 - at)) for at in range(0, 96 * len(FIELDS) * 4, 512))
                values = struct.unpack('>' + 'i' * (96 * len(FIELDS)), raw)
                rows = [dict(zip(FIELDS, values[i:i + len(FIELDS)])) for i in range(0, len(values), len(FIELDS))]
                if any(r['frame_us'] <= 0 or r['prepare_us'] <= 0 or r['width'] != 640 or r['height'] != 480 for r in rows):
                    raise ValueError('Invalid frame log')
                expected_face,expected_editing={'Options':(3,0),'Oblique':(3,0),'Cube':(0,0),'Detail':(1,1)}[args.scene]
                if any(r['face']!=expected_face or r['editing']!=expected_editing for r in rows):
                    raise ValueError('Native scene does not match --scene '+args.scene)
                output = io.StringIO()
                writer = csv.DictWriter(output, FIELDS, lineterminator='\n')
                writer.writeheader()
                writer.writerows(rows)
                (root / 'frames.csv').write_text(output.getvalue())
                report = {'variant': args.variant, 'environment': 'cloud Dolphin 2503 / Mesa llvmpipe, NOT physical hardware',
                          'clock': 'native guest monotonic microseconds; emulated CPU/GPU timing does not establish hardware performance',
                          'method': '30 warmup + 96 recorded frames, fixed '+args.scene+' pose, no interrupts or dumps during measurement; Y logger uses the same buffer',
                          'scene': args.scene,
                          'dol_sha256': hashlib.sha256(dol.read_bytes()).hexdigest(), 'frames': len(rows), 'summary': summarize(rows)}
                for name in ('Status', 'Summary'):
                    if name not in symbols:
                        continue
                    pointer = int.from_bytes(memory(conn, symbols[name], 4), 'big')
                    length = int.from_bytes(memory(conn, pointer + 8, 4), 'big')
                    if length > 256:
                        raise ValueError('Unexpected profiler status length')
                    report['native_' + name.lower()] = memory(conn, pointer + 12, length * 2).decode('utf-16-be')
                send(conn, 'p40')
                pc = reply(conn)
                while pc == '' or pc.startswith(('T', 'S')):
                    pc = reply(conn)
                report['completion_pc'] = pc
                (root / 'result.json').write_text(json.dumps(report, indent=2) + '\n')
                if 'native_status' in report and not report['native_status'].startswith(('Captured in RAM', 'No writable SD', 'Saved ')) and not args.allow_legacy_export_stall:
                    raise RuntimeError('Native post-capture export did not finish: ' + report['native_status'])
                (root / 'result.json').write_text(json.dumps(report, indent=2) + '\n')
                subprocess.run(['python3', str(Path(__file__).with_name('capture_x11.py')), str(root / 'native-window.ppm')], check=True)
                print(json.dumps(report), flush=True)
        finally:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dol', type=Path, required=True)
    parser.add_argument('--logs', type=Path, required=True)
    parser.add_argument('--variant', required=True)
    parser.add_argument('--scene',choices=('Options','Oblique','Cube','Detail'),default='Options')
    parser.add_argument('--wait', type=float, default=45)
    parser.add_argument('--allow-legacy-export-stall', action='store_true', help='Only for archived baseline DOLs: samples finish, but legacy absent-SD probing then stalls; record status and PC without accepting this behavior in current builds')
    run(parser.parse_args())
