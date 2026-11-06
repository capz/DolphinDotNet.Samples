#!/usr/bin/env python3
"""Drive a normal DOL through native overlay, selector and scan reviews in Xvfb.

Requires Dolphin NoGUI, XInput2 and libXtst. Saves full unscaled X11 PPMs and
the last completed native EFB PNG at each step. Inspect the saved images;
this is an input/capture aid, not a physical-hardware performance benchmark.
"""
import argparse
import ctypes as C
import json
import hashlib
from pathlib import Path
import shutil
import signal
import subprocess
import time

from capture_x11 import capture

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--logs',type=Path,required=True)
parser.add_argument('--dol',type=Path,required=True)
parser.add_argument('--widescreen',action='store_true',help='Also preview/keep 16:9, hide Z, and return through X safe mode')
parser.add_argument('--calendar',action='store_true',help='Review two-row blur and independent time/date editing instead of the overlay sequence')
parser.add_argument('--selector-motion',action='store_true',help='Record active hour/minute/month/year motion, A/B and Reduced Motion')
parser.add_argument('--selector-scan',action='store_true',help='Exercise animated scan values across progressive/interlaced preview, keep, rollback and safe mode')
parser.add_argument('--directional-return',action='store_true',help='Review reverse stick return, hold latching and D-pad Calendar rows')
parser.add_argument('--storage',action='store_true',help='Actual native card services and original DVD file fixtures')
parser.add_argument('--disc',type=Path)
parser.add_argument('--card-user',type=Path)
parser.add_argument('--library',action='store_true',help='Native library layout/navigation using explicitly labeled synthetic UI data')
parser.add_argument('--system-clock',action='store_true',help='Adjust isolated clock, inspect SRAM and Dolphin restart policy, then decrement')
args=parser.parse_args()
if sum([args.calendar,args.widescreen,args.selector_motion,args.selector_scan,args.directional_return,args.system_clock,args.library,args.storage])>1:parser.error('Choose one review sequence')
root=args.logs.resolve()
if root.exists() and any(root.iterdir()):raise ValueError('Use a fresh capture directory')
root.mkdir(parents=True,exist_ok=True)
config=root/'user/Config';config.mkdir(parents=True)
(config/'Dolphin.ini').write_text('[Core]\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\nSIDevice0 = 6\n[DSP]\nBackend = No Audio Output\n[Display]\nRenderWindowXPos = 0\nRenderWindowYPos = 0\nRenderWindowWidth = 640\nRenderWindowHeight = 480\n[Interface]\nOnScreenDisplayMessages = False\n[Movie]\nDumpFrames = True\nDumpFramesSilent = True\n')
if args.storage:
    with (config/'Dolphin.ini').open('a') as f:f.write('[Core]\nSlotA = 1\nSlotB = 1\n'+('DefaultISO = '+str(args.disc.resolve())+'\n' if args.disc else ''))
    if args.card_user:
        shutil.copytree(args.card_user/'GC',root/'user/GC')
        # Original fixture DOL boots as EUR; the original data-only DVD fixture
        # has a USA header. Both use western card encoding. Keep the seeded
        # files under the region-specific names Dolphin actually opens.
        for slot in ('A','B'):
            seed=root/('user/GC/MemoryCard'+slot+'.EUR.raw')
            if seed.exists():shutil.copyfile(seed,root/('user/GC/MemoryCard'+slot+'.USA.raw'))
(config/'GFX.ini').write_text('[Settings]\nDumpFramesAsImages = True\nInternalResolution = 1\nMSAA = 1\nSSAA = False\nAspectRatio = 2\n[Enhancements]\nPostProcessingShader = IplEdgeAA\n[Hacks]\nForceProgressive = True\n')
(config/'GCPadNew.ini').write_text('[GCPad1]\nDevice = XInput2/0/Virtual core pointer\nButtons/Z = Z\nButtons/A = A\nButtons/B = B\nButtons/X = X\nButtons/Y = Y\nTriggers/L = Q\nTriggers/R = W\nD-Pad/Left = Left\nD-Pad/Right = Right\nD-Pad/Up = Up\nD-Pad/Down = Down\nMain Stick/Up = I\nMain Stick/Down = K\nMain Stick/Left = J\nMain Stick/Right = L\n')
shaders=root/'user/Shaders';shaders.mkdir()
shutil.copyfile(Path(__file__).parents[1]/'VideoMode/Assets/IplEdgeAA.glsl',shaders/'IplEdgeAA.glsl')
x=C.CDLL('libX11.so.6');xt=C.CDLL('libXtst.so.6')
x.XOpenDisplay.argtypes=[C.c_char_p];x.XOpenDisplay.restype=C.c_void_p;d=x.XOpenDisplay(None)
x.XStringToKeysym.argtypes=[C.c_char_p];x.XStringToKeysym.restype=C.c_ulong
x.XKeysymToKeycode.argtypes=[C.c_void_p,C.c_ulong];x.XKeysymToKeycode.restype=C.c_uint
xt.XTestFakeKeyEvent.argtypes=[C.c_void_p,C.c_uint,C.c_int,C.c_ulong]
x.XSync.argtypes=[C.c_void_p,C.c_int]
def frame_count():
    return len(list((root/'user/Dump/Frames').glob('*.png')))

def wait_frames(start,count):
    deadline=time.monotonic()+45
    while frame_count()<start+count:
        if p.poll() is not None:raise RuntimeError('Dolphin exited while awaiting input frames')
        if time.monotonic()>deadline:raise TimeoutError('Native frames stalled during input replay')
        time.sleep(.05)

def key(name,held=.15):
    code=x.XKeysymToKeycode(d,x.XStringToKeysym(name.encode()))
    before=frame_count()
    assert xt.XTestFakeKeyEvent(d,code,1,0)
    x.XSync(d,0);time.sleep(held);wait_frames(before,6)
    assert xt.XTestFakeKeyEvent(d,code,0,0)
    x.XSync(d,0);wait_frames(frame_count(),45)

log=(root/'emulator.log').open('w')
p=subprocess.Popen(['/usr/games/dolphin-emu-nogui','--platform','x11','-u',str(root/'user'),'-C','SYSCONF.IPL.PGS=True','-e',str(args.dol.resolve())],stdout=log,stderr=subprocess.STDOUT)
events=[]
def shot(name):
    if p.poll() is not None:raise RuntimeError('Dolphin exited')
    p.send_signal(signal.SIGSTOP);time.sleep(.2)
    capture(root/(name+'.ppm'))
    frames=sorted((root/'user/Dump/Frames').glob('*.png'),key=lambda a:a.stat().st_mtime_ns)
    event={'capture':name,'completed_frames':len(frames),'input':'XTest -> Dolphin GameCube controller -> SDK Controller.GetState(0)'}
    # SIGSTOP may interrupt the movie writer. Keep the newest complete PNG,
    # rather than a zero-byte or truncated file from the in-flight frame.
    for frame in reversed(frames):
        raw=frame.read_bytes()
        if raw.startswith(b'\x89PNG\r\n\x1a\n') and raw.endswith(b'IEND\xaeB\x60\x82'):
            (root/(name+'-efb.png')).write_bytes(raw)
            event['efb_source']=frame.name;event['efb_sha256']=hashlib.sha256(raw).hexdigest()
            break
    events.append(event)
    p.send_signal(signal.SIGCONT)
try:
    time.sleep(15);capture(root/'unused.ppm',True);time.sleep(2)
    if args.system_clock:
        key('Down');shot('01-real-clock')
        key('a');key('Up');key('Right');key('Up');shot('02-clock-draft')
        key('a');shot('03-clock-committed')
        p.terminate();p.wait(timeout=15)
        for saved in (root/'user').rglob('*SRAM*'):
            if saved.is_file():shutil.copyfile(saved,root/('committed-'+saved.name))
        shutil.move(root/'user/Dump/Frames',root/'before-restart-frames')
        p=subprocess.Popen(['/usr/games/dolphin-emu-nogui','--platform','x11','-u',str(root/'user'),'-C','SYSCONF.IPL.PGS=True','-e',str(args.dol.resolve())],stdout=log,stderr=subprocess.STDOUT)
        time.sleep(15);capture(root/'restart-unused.ppm',True);time.sleep(2)
        key('Down');shot('04-clock-after-restart')
        key('a');key('Down');key('Right');key('Down');key('a');shot('05-clock-decremented-after-emulator-reset')
    elif args.directional_return:
        key('i');shot('01-up-play');key('k',2);shot('02-down-return-held')
        key('k');key('Up');shot('03-calendar-dpad-date');key('i');shot('04-up-return')
        key('l');shot('05-right-video');key('j');shot('06-left-return')
        key('j');shot('07-left-options');key('l');shot('08-right-return')
    elif args.storage:
        key('w');shot('01-storage-cube-face')
        key('a');shot('02-card-slot-a')
        key('a');shot('03-save-actions')
        key('Right');key('a');shot('04-copy-confirm-cancel')
        key('Right');key('a');shot('05-native-copy-complete')
        key('w');shot('06-card-slot-b')
        key('a');key('Left');key('a');shot('07-delete-confirm-cancel')
        key('b');key('b');shot('08-storage-face-return')
        if args.disc:
            key('Down');key('a');shot('09-sd-style-file-grid')
            key('a');shot('10-folder-open')
            key('a');shot('11-text-preview')
            key('b');key('b');key('b');shot('12-back-to-storage')
        else:
            key('b');shot('09-storage-to-cube')
    elif args.library:
        key('Up');shot('01-play-library')
        key('a');shot('02-six-glass-game-cards')
        key('Down');key('Down');shot('03-next-library-page')
        key('a');shot('04-swiss-confirm-default-cancel')
        key('Right');key('a');shot('05-fixture-launch-refused')
        key('b');key('b');shot('06-back-to-play')
        key('k');shot('07-reverse-stick-to-cube')
    elif args.selector_scan:
        key('Right');key('Down');key('a');shot('01-interlaced-editor')
        key('Up');shot('02-progressive-editor')
        key('a');shot('03-progressive-preview')
        key('a');shot('04-progressive-kept')
        key('a');key('Down');key('a');shot('05-interlaced-preview')
        key('b');shot('06-progressive-restored')
        key('x');shot('07-safe-interlaced')
    elif args.selector_motion:
        key('Down');shot('01-calendar')
        key('a');shot('02-hour-a');wait_frames(frame_count(),45);shot('03-hour-b')
        key('Up');shot('04-hour-edited')
        key('Right');key('Up');shot('05-minute-edited')
        key('a');shot('06-time-committed')
        key('Down');key('a');key('Right');key('Up');shot('07-month-edited')
        key('Right');key('Up');shot('08-year-edited')
        key('b');shot('09-date-cancelled')
        key('b');key('Left');key('Down');key('a');key('b');key('Down');key('a')
        shot('10-reduced-a');wait_frames(frame_count(),45);shot('11-reduced-b')
    elif args.calendar:
        key('Down');shot('01-calendar-two-rows')
        key('a');key('Up');key('Right');key('Up');shot('02-time-parts')
        key('b');key('Down');key('a');key('Right');key('Up');shot('03-date-parts')
        key('b');shot('04-calendar-cancelled')
    else:
        shot('01-default-hidden')
        key('z',2);shot('02-z-held-visible')
        key('Left');shot('03-options-visible')
        key('a');shot('04-editor-visible')
        key('z',1);shot('05-z-hidden-editor')
        key('b');key('z');time.sleep(12);shot('06-reopened-options')
        if args.widescreen:
            key('x');key('Down');key('Down')
            key('a');key('Up');key('a');key('a');shot('07-widescreen-kept')
            key('z');shot('08-widescreen-hidden')
            key('x');shot('09-safe-mode')
    (root/'events.json').write_text(json.dumps(events,indent=2)+'\n')
finally:
    if p.poll() is None:
        p.send_signal(signal.SIGCONT);p.terminate()
        try:p.wait(timeout=5)
        except subprocess.TimeoutExpired:p.kill();p.wait()
    log.close()
