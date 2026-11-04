#!/usr/bin/env python3
"""Capture native DOL presentation including the selected AA shader in cloud Xvfb.
Run inside dnd-cloud-dolphin via xvfb-run. Saves the full unscaled Dolphin client
window as RGB PPM; convert losslessly to PNG outside timing/measurement runs.
"""
import argparse
from pathlib import Path
import shutil
import signal
import subprocess
import time


def main(args):
    root=args.logs.resolve();config=root/'user/Config'
    if root.exists() and any(root.iterdir()):raise ValueError('Use a fresh capture directory')
    config.mkdir(parents=True)
    (config/'Dolphin.ini').write_text('[Core]\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\n[DSP]\nBackend = No Audio Output\n[Interface]\nOnScreenDisplayMessages = False\n[Movie]\nDumpFrames = True\nDumpFramesSilent = True\n')
    (config/'GFX.ini').write_text('[Settings]\nDumpFramesAsImages = True\nInternalResolution = 1\nMSAA = 1\nSSAA = False\nAspectRatio = 2\n[Enhancements]\nPostProcessingShader = IplEdgeAA\n[Hacks]\nForceProgressive = True\n')
    shaders=root/'user/Shaders';shaders.mkdir();shutil.copyfile(Path(__file__).parents[1]/'VideoMode/Assets/IplEdgeAA.glsl',shaders/'IplEdgeAA.glsl')
    helper=Path(__file__).with_name('capture_x11.py')
    with (root/'emulator.log').open('w') as log:
        process=subprocess.Popen(['/usr/games/dolphin-emu-nogui','--platform','x11','-u',str(root/'user'),'-C','SYSCONF.IPL.PGS=True','-e',str(args.dol.resolve())],stdout=log,stderr=subprocess.STDOUT)
        try:
            began=time.monotonic()
            for _ in range(20):
                time.sleep(1)
                result=subprocess.run(['python3',str(helper),str(root/'unused.ppm'),'--position-only'],capture_output=True,text=True)
                if result.returncode==0:break
                if process.poll() is not None:raise RuntimeError('Dolphin stopped before capture')
            else:raise RuntimeError('Dolphin did not create a client window')
            while time.monotonic()-began<120:
                if process.poll() is not None:raise RuntimeError('Dolphin stopped before capture')
                frames=list((root/'user/Dump/Frames').glob('*.png'))
                if len(frames)>=30 and time.monotonic()-began>=15:break
                time.sleep(.5)
            else:raise RuntimeError('No stable native frame sequence')
            process.send_signal(signal.SIGSTOP);time.sleep(.1)
            subprocess.run(['python3',str(helper),str(root/'native-window.ppm')],check=True)
        finally:
            if process.poll() is None:
                process.send_signal(signal.SIGCONT);process.terminate()
                try:process.wait(timeout=5)
                except subprocess.TimeoutExpired:process.kill();process.wait()


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dol',type=Path,required=True);p.add_argument('--logs',type=Path,required=True);main(p.parse_args())
