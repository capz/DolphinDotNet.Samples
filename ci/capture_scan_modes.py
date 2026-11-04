#!/usr/bin/env python3
"""Run the explicit scan review DOL in cloud-local Dolphin; retain native evidence.
Requires Docker image dnd-cloud-dolphin (Dolphin, Xvfb), Pillow and tesseract.
Build first with IplVideoModeReview=true; add IplVideoModeReviewResolution=576
for the PAL-sized sequence. OCR reads only the diagnostic banner; saved native
PNGs are copied byte-for-byte, never cropped or resized.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import shutil
import subprocess
import time
from PIL import Image, ImageOps


def run(args):
    repo=Path(__file__).resolve().parents[1]
    root=args.logs.resolve()
    if root.exists() and any(root.iterdir()):
        raise ValueError('Use a fresh evidence directory')
    config=root/'user/Config';config.mkdir(parents=True)
    (config/'Dolphin.ini').write_text('[Core]\nFallbackRegion = '+('2' if args.resolution==576 else '1')+'\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\n[DSP]\nBackend = No Audio Output\n[Movie]\nDumpFrames = True\nDumpFramesSilent = True\n')
    (config/'GFX.ini').write_text('[Settings]\nDumpFramesAsImages = True\nInternalResolution = 1\nMSAA = 4\nSSAA = False\nAspectRatio = 2\n[Hacks]\nForceProgressive = '+('False' if args.native_fields else 'True')+'\n')
    dol=repo/'VideoMode/bin/Release/net8.0/VideoMode.dol'
    if not dol.is_file():raise ValueError('Build the scan review DOL first')
    if not shutil.which('tesseract'):raise ValueError('tesseract is required')
    name='ipl-scan-'+str(args.resolution)
    seen=set();records={};started=time.monotonic();last_frame=started;highest=0
    # Mount paths at the same absolute locations: no shell quoting or host GUI.
    command=['docker','run','--rm','--init','--shm-size=1g','--name',name,
             '--volume',f'{repo}:{repo}','--volume',f'{root}:{root}',
             '-e','LP_NUM_THREADS=2',args.image,'xvfb-run','-a',
             '/usr/games/dolphin-emu-nogui','-u',str(root/'user'),'-C','SYSCONF.IPL.PGS=True','-e',str(dol)]
    expected=[1,2,2,1,1,2,1,2,2,1,2,1,2,1]
    with (root/'emulator.log').open('w') as log:
        process=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT)
        try:
            while time.monotonic()-started<args.seconds:
                if process.poll() is not None:raise RuntimeError('Dolphin stopped before acceptance completed')
                files=sorted((root/'user/Dump/Frames').glob('framedump_*.png'),key=lambda p:int(p.stem.split('_')[-1]))
                if files:
                    latest=int(files[-1].stem.split('_')[-1])
                    if latest>highest:highest=latest;last_frame=time.monotonic()
                    elif time.monotonic()-last_frame>30:
                        raise RuntimeError(f'Native frame production stalled at frame {highest}')
                for path in files:
                    number=int(path.stem.split('_')[-1])
                    if number%4 or number in seen:continue
                    raw=path.read_bytes()
                    if not raw.endswith(b'IEND\xaeB\x60\x82'):continue
                    try:
                        with Image.open(io.BytesIO(raw)) as frame:
                            frame.load()
                            # Analysis-only derivative for OCR, never a native artifact.
                            band_height=max(12,round(frame.height*.07))
                            banner=ImageOps.invert(frame.convert('RGB').crop((0,0,frame.width,band_height))).resize((1280,68))
                            banner.save(root/'ocr-analysis.png')
                    except OSError:continue
                    seen.add(number)
                    result=subprocess.run(['tesseract',str(root/'ocr-analysis.png'),'stdout','--psm','7'],check=True,capture_output=True,text=True)
                    text=result.stdout.strip().upper()
                    phase_match=re.search(r'SCAN\s*(\d{1,2})',text)
                    if not phase_match:continue
                    phase=int(phase_match.group(1))
                    if 'FAIL' in text:raise RuntimeError('Native scan assertion failed: '+text)
                    actual=re.search(r'ACTUAL\s*(480|576)\s*([IP1])',text)
                    if phase not in range(1,15) or not actual:continue
                    wanted_resolution=480 if phase==14 else args.resolution
                    wanted_scan='P' if expected[phase-1]==2 else 'I'
                    resolved=actual.group(1)+('I' if actual.group(2)=='1' else actual.group(2))
                    if resolved!=str(wanted_resolution)+wanted_scan:
                        raise RuntimeError(f'Phase {phase}: native readback {resolved}, expected {wanted_resolution}{wanted_scan}')
                    if phase==14 and 'PASS' not in text:continue
                    if phase not in records:
                        dest=root/f'scan-{phase:02}-{resolved.lower()}-native.png';dest.write_bytes(raw)
                        records[phase]={'caption':text,'resolved_mode':resolved,'frame':number,'native_png':dest.name,'sha256':hashlib.sha256(raw).hexdigest()}
                        print(text,flush=True)
                    if len(records)==14:
                        report={'resolution':args.resolution,'native_fields':args.native_fields,'success':True,'dol_sha256':hashlib.sha256(dol.read_bytes()).hexdigest(),
                                'note':'Device display readback asserted by the native driver. Timeout uses a forced logical 12.1-second tick, not a wall-clock measurement.',
                                'phases':[records[p] for p in sorted(records)]}
                        (root/'result.json').write_text(json.dumps(report,indent=2)+'\n')
                        return
                time.sleep(.2)
            raise TimeoutError('Missing native phases: '+str(sorted(set(range(1,15))-records.keys())))
        except Exception as error:
            (root/'result.json').write_text(json.dumps({'resolution':args.resolution,'native_fields':args.native_fields,'success':False,'error':str(error),
                'dol_sha256':hashlib.sha256(dol.read_bytes()).hexdigest(),'phases':[records[p] for p in sorted(records)]},indent=2)+'\n')
            raise
        finally:
            subprocess.run(['docker','stop',name],stdout=subprocess.DEVNULL,check=False)
            process.wait(timeout=20)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--logs',type=Path,required=True)
    parser.add_argument('--resolution',type=int,choices=[480,576],default=480)
    parser.add_argument('--image',default='dnd-cloud-dolphin')
    parser.add_argument('--native-fields',action='store_true',help='Diagnostic: disable Dolphin XFB progressive presentation workaround')
    parser.add_argument('--seconds',type=float,default=240)
    run(parser.parse_args())
