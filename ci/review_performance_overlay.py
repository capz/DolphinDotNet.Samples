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
parser.add_argument('--glass-profile-only',action='store_true',help='Normal-DOL Video profile with only the final native overlay capture')
parser.add_argument('--glass-profile','--rim-profile',dest='rim_profile',action='store_true',help='After glass captures, log an uninterrupted normal-DOL Y profile')
parser.add_argument('--glass-review','--rim-review',dest='rim_review',action='store_true',help='Native Calendar/front, rotating corner and Video/front glass captures')
parser.add_argument('--optics-diagnostic',action='store_true',help='Capture native Fresnel/environment/field diagnostic outputs')
parser.add_argument('--mockup-single',action='store_true',help='Capture only the initial native mockup pose')
parser.add_argument('--mockup-corners',action='store_true',help='Capture the native mockup pose and rotating corner only')
parser.add_argument('--mockup-review',action='store_true',help='Deterministic native Options/flat/return/turn views')
parser.add_argument('--glass-modes',action='store_true',help='Closed-shell rendering after 480p, widescreen, 576i, 240-line, rollback and safe-mode switches')
parser.add_argument('--detail-review',action='store_true',help='High blur/icon layering, grouped Video detail, preview/rollback, Calendar Date and Options value editor')
parser.add_argument('--detail-profile',action='store_true',help='With --detail-review, measure an uninterrupted High-blur Video cube window')
parser.add_argument('--inner-blur-review',action='store_true',help='Use real Options input to capture all inner-glass blur levels and cancellation')
parser.add_argument('--inner-blur-profile',action='store_true',help='With --inner-blur-review, measure each level on the normal interactive Video face')
parser.add_argument('--inner-blur-profile-only',action='store_true',help='Skip repeated editor/list captures during the full blur profile')
parser.add_argument('--profile-seconds',type=int,default=30,help='Uninterrupted host observation window; increase for slow software rendering')
parser.add_argument('--quick-input',action='store_true',help='Use shorter settled editor inputs while retaining full cube-turn settling')
parser.add_argument('--inner-blur-cubes',action='store_true',help='Also capture the unobstructed menu cube at each reviewed blur level')
parser.add_argument('--inner-blur-high',action='store_true',help='Enable High inner-glass blur via Options before --glass-modes')
parser.add_argument('--calendar',action='store_true',help='Review two-row blur and independent time/date editing instead of the overlay sequence')
parser.add_argument('--selector-motion',action='store_true',help='Record active hour/minute/month/year motion, A/B and Reduced Motion')
parser.add_argument('--selector-scan',action='store_true',help='Exercise animated scan values across progressive/interlaced preview, keep, rollback and safe mode')
parser.add_argument('--directional-return',action='store_true',help='Review reverse stick return, hold latching and D-pad Calendar rows')
parser.add_argument('--flat-pages',action='store_true',help='Read-only sides, grouped Video detail, flat option lists and single-B exits')
parser.add_argument('--dense-storage',action='store_true',help='Compact native card/SD grid, list, paging and preserved selection')
parser.add_argument('--storage',action='store_true',help='Actual native card services and original DVD file fixtures')
parser.add_argument('--native-boundaries',action='store_true',help='Pause at actual native presentation boundaries; capture the complete X11 client')
parser.add_argument('--sdk',type=Path)
parser.add_argument('--sd-only',action='store_true',help='Final SD-only smoke on the DVD-backed native review build')
parser.add_argument('--dense-quick',action='store_true',help='Final native fixed-label/cache smoke sequence instead of full paging acceptance')
parser.add_argument('--memory-only',action='store_true',help='Skip SD review when validating a normal DOL without physical GC Loader')
parser.add_argument('--disc',type=Path)
parser.add_argument('--card-user',type=Path)
parser.add_argument('--library',action='store_true',help='Native library layout/navigation using explicitly labeled synthetic UI data')
parser.add_argument('--system-clock',action='store_true',help='Adjust isolated clock, inspect SRAM and Dolphin restart policy, then decrement')
args=parser.parse_args()
if args.optics_diagnostic and not args.mockup_review:parser.error('--optics-diagnostic requires --mockup-review')
if args.mockup_single and not args.mockup_review:parser.error('--mockup-single requires --mockup-review')
if args.mockup_corners and (not args.mockup_review or args.mockup_single or args.optics_diagnostic):parser.error('--mockup-corners requires an independent --mockup-review')
if args.mockup_review and not args.native_boundaries:parser.error('--mockup-review requires --native-boundaries')
if sum([args.mockup_review,args.rim_review,args.glass_modes,args.detail_review,args.calendar,args.widescreen,args.selector_motion,args.selector_scan,args.directional_return,args.system_clock,args.library,args.storage,args.flat_pages,args.dense_storage])>1:parser.error('Choose one review sequence')
if args.detail_profile and not args.detail_review:parser.error('--detail-profile requires --detail-review')
if args.detail_review and not args.native_boundaries:parser.error('--detail-review requires --native-boundaries')
if args.detail_review and (args.inner_blur_review or args.inner_blur_high):parser.error('--detail-review independently enables High blur; disable the other blur review modes')
if args.glass_profile_only and not args.rim_profile:parser.error('--glass-profile-only requires --glass-profile')
if args.inner_blur_profile and (not args.inner_blur_review or args.mockup_review):parser.error('--inner-blur-profile requires --inner-blur-review on a normal DOL')
if args.inner_blur_profile_only and not args.inner_blur_profile:parser.error('--inner-blur-profile-only requires --inner-blur-profile')
if args.profile_seconds<30 or args.profile_seconds>180:parser.error('--profile-seconds must be between 30 and 180')
if args.quick_input and not (args.inner_blur_review or args.glass_modes or args.detail_review or args.selector_scan or args.flat_pages or args.widescreen):parser.error('--quick-input is supported for blur and detail/video reviews')
if args.inner_blur_cubes and not args.inner_blur_review:parser.error('--inner-blur-cubes requires --inner-blur-review')
if (args.inner_blur_review or args.inner_blur_high) and not args.native_boundaries:parser.error('Inner-glass blur review requires --native-boundaries')
if args.inner_blur_high and not args.glass_modes:parser.error('--inner-blur-high requires --glass-modes')
if args.rim_profile and not args.rim_review:parser.error('--rim-profile requires --rim-review')
if args.rim_review and not args.native_boundaries:parser.error('--rim-review requires --native-boundaries')
if args.native_boundaries and args.sdk is None:parser.error('--native-boundaries requires --sdk')
if args.sd_only and (not args.dense_storage or not args.dense_quick or args.memory_only):parser.error('--sd-only requires --dense-storage --dense-quick')
if args.memory_only and not args.dense_storage:parser.error('--memory-only requires --dense-storage')
root=args.logs.resolve()
if root.exists() and any(root.iterdir()):raise ValueError('Use a fresh capture directory')
root.mkdir(parents=True,exist_ok=True)
config=root/'user/Config';config.mkdir(parents=True)
(config/'Dolphin.ini').write_text('[Core]\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\nSIDevice0 = 6\n[DSP]\nBackend = No Audio Output\n[Display]\nRenderWindowXPos = 0\nRenderWindowYPos = 0\nRenderWindowWidth = 640\nRenderWindowHeight = 480\n[Interface]\nOnScreenDisplayMessages = False\n[Movie]\nDumpFrames = True\nDumpFramesSilent = True\n')
if args.storage or args.flat_pages or args.dense_storage:
    with (config/'Dolphin.ini').open('a') as f:f.write('[Core]\nSlotA = 1\nSlotB = 1\n'+('DefaultISO = '+str(args.disc.resolve())+'\n' if args.disc else ''))
    if args.card_user:
        shutil.copytree(args.card_user/'GC',root/'user/GC')
if args.native_boundaries:
    with (config/'Dolphin.ini').open('a') as f:f.write('[General]\nGDBPort = 2174\n[Interface]\nDebugModeEnabled = True\n[Movie]\nDumpFrames = False\n')
(config/'GFX.ini').write_text('[Settings]\nDumpFramesAsImages = True\nInternalResolution = 1\nMSAA = 1\nSSAA = False\nAspectRatio = 2\n[Enhancements]\nPostProcessingShader = IplEdgeAA\n[Hacks]\nForceProgressive = True\n')
if args.native_boundaries:
    with (config/'GFX.ini').open('a') as f:f.write('[Hacks]\nXFBToTextureEnable = False\nDeferEFBCopies = False\n')
(config/'GCPadNew.ini').write_text('[GCPad1]\nDevice = XInput2/0/Virtual core pointer\nButtons/Z = Z\nButtons/A = A\nButtons/B = B\nButtons/X = X\nButtons/Y = Y\nTriggers/L = Q\nTriggers/R = W\nD-Pad/Left = Left\nD-Pad/Right = Right\nD-Pad/Up = Up\nD-Pad/Down = Down\nMain Stick/Up = I\nMain Stick/Down = K\nMain Stick/Left = J\nMain Stick/Right = L\nC-Stick/Up = U\n')
shaders=root/'user/Shaders';shaders.mkdir()
shutil.copyfile(Path(__file__).parents[1]/'VideoMode/Assets/IplEdgeAA.glsl',shaders/'IplEdgeAA.glsl')
x=C.CDLL('libX11.so.6');xt=C.CDLL('libXtst.so.6')
x.XOpenDisplay.argtypes=[C.c_char_p];x.XOpenDisplay.restype=C.c_void_p;d=x.XOpenDisplay(None)
x.XStringToKeysym.argtypes=[C.c_char_p];x.XStringToKeysym.restype=C.c_ulong
x.XKeysymToKeycode.argtypes=[C.c_void_p,C.c_ulong];x.XKeysymToKeycode.restype=C.c_uint
xt.XTestFakeKeyEvent.argtypes=[C.c_void_p,C.c_uint,C.c_int,C.c_ulong]
x.XSync.argtypes=[C.c_void_p,C.c_int]
native_frames=None
def frame_count():
    if native_frames is not None:return native_frames.count
    return len(list((root/'user/Dump/Frames').glob('*.png')))

def wait_frames(start,count):
    if native_frames is not None:
        native_frames.advance(max(0,start+count-native_frames.count));return
    deadline=time.monotonic()+45
    while frame_count()<start+count:
        if p.poll() is not None:raise RuntimeError('Dolphin exited while awaiting input frames')
        if time.monotonic()>deadline:raise TimeoutError('Native frames stalled during input replay')
        time.sleep(.05)

def key(name,held=.15,settle_frames=None):
    code=x.XKeysymToKeycode(d,x.XStringToKeysym(name.encode()))
    before=frame_count()
    assert xt.XTestFakeKeyEvent(d,code,1,0)
    x.XSync(d,0);time.sleep(held);wait_frames(before,6)
    assert xt.XTestFakeKeyEvent(d,code,0,0)
    # A cube turn lasts .64 s (39 frames); row focus needs only .18 s.
    settle=40 if name.lower() in ('b','x','left','right','i','j','k','l','q','w') else 12
    if settle_frames is None:settle_frames=settle if args.quick_input else 45
    x.XSync(d,0);wait_frames(frame_count(),settle_frames)

log=(root/'emulator.log').open('w')
p=subprocess.Popen(['/usr/games/dolphin-emu-nogui','--platform','x11','-u',str(root/'user'),'-C','SYSCONF.IPL.PGS=True','-e',str(args.dol.resolve())],stdout=log,stderr=subprocess.STDOUT)
if args.native_boundaries:
    from native_frame_boundary import NativeFrames
    native_frames=NativeFrames(args.sdk,args.dol,p)
events=[]
def shot(name):
    if args.inner_blur_profile_only and name not in ('01-blur-off','05-blur-low','06-blur-medium','07-blur-high','10-off-restored','cube-off','cube-high'):return
    if p.poll() is not None:raise RuntimeError('Dolphin exited')
    boundary=native_frames.stable_frame() if native_frames is not None else None
    if native_frames is None:p.send_signal(signal.SIGSTOP)
    time.sleep(.3)
    capture(root/(name+'.ppm'))
    if native_frames is not None and native_frames.stable_frame()!=boundary:raise RuntimeError('Native frame advanced during screenshot')
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
    if native_frames is not None:event.update(capture_source='Complete X11 client, guest paused at VIDEO_WaitVSync after GX_CopyDisp/GX_DrawDone',native_boundaries=boundary)
    if native_frames is not None:
        event['xfb']=native_frames.capture_xfb(root/(name+'-xfb.yuv'))
        if args.glass_modes:
            expected_height=576 if name.startswith('05-') else 240 if name.startswith('06-') else 480
            info=event['xfb'];expected_width=320 if expected_height==240 else 640
            if (info['width'],info['height'])!=(expected_width,expected_height):
                raise RuntimeError(f'{name}: native VI dimensions do not match the applied mode: {info}')
            # 240p is VI double-strike (NON_INTERLACE=1), not 480p's PROGRESSIVE=2.
            expected_scan=1 if name.startswith('06-') else 2 if name.startswith(('02-','03-','04-')) else 0
            if info['vi_scan_bits']!=expected_scan:raise RuntimeError(f'{name}: native VI scan mode is wrong: {info}')
        elif args.detail_review:
            info=event['xfb'];expected_scan=2 if name.startswith(('05-','06-','07-')) else 0
            if (info['width'],info['height'],info['vi_scan_bits'])!=(640,480,expected_scan):
                raise RuntimeError(f'{name}: grouped Video preview/keep/rollback has the wrong native VI mode: {info}')
        if native_frames.stable_frame()!=boundary:raise RuntimeError('Native frame advanced during framebuffer read')
    events.append(event)
    if native_frames is None:p.send_signal(signal.SIGCONT)
try:
    if native_frames is None:time.sleep(15)
    else:wait_frames(frame_count(),60)
    capture(root/'unused.ppm',True);time.sleep(.3)
    if args.detail_review:
        # Normal interactive DOL: Options stays a flat list, while Video opens
        # its three Calendar-style parts directly. All changes use controller
        # input, with no native application-state or frame-buffer mutation.
        key('x');key('b') # Establish 480i and a neutral cube using real safe-mode/back input.
        key('Left');key('a');key('Up');key('a');key('Down');key('a');key('b')
        shot('01-options-high-blur')
        key('b');shot('02-crisp-centered-icon')
        key('Right');shot('03-video-cube')
        key('a');shot('04-video-grouped-detail')
        key('Right',settle_frames=12);key('Up');key('a');shot('05-progressive-preview')
        key('a');shot('06-progressive-kept-detail')
        key('Down');key('a');key('b');shot('07-rollback-to-progressive-cube')
        key('x');key('a');shot('08-safe-interlaced-detail')
        key('b');key('b');key('Down',settle_frames=40);key('a');key('Down');key('a');shot('09-calendar-date-detail')
        key('b');key('b');key('Left');key('a');key('Up');key('a');shot('10-options-blur-value-detail')
        if args.detail_profile:
            # Complete the product captures before Y enables diagnostic text.
            # B from the value editor returns instantly to its cube side;
            # B/Right then perform fully settled cube turns to Video.
            key('b',settle_frames=12);key('b');key('Right')
            from profile_interactive_rim import collect
            profile=root/'profile-high';profile.mkdir()
            code=x.XKeysymToKeycode(d,x.XStringToKeysym(b'Y'))
            def profile_key(down):
                assert xt.XTestFakeKeyEvent(d,code,down,0)
                x.XSync(d,0)
            collect(native_frames,args.dol.resolve(),profile,lambda:profile_key(1),lambda:profile_key(0),args.profile_seconds)
    elif args.inner_blur_review:
        if not args.mockup_review:key('Left')
        def cube_blur(level):
            if not args.inner_blur_cubes:return
            if args.inner_blur_profile_only and level not in ('off','high'):return
            key('b');shot('cube-'+level);key('Left')
        def measure_blur(level):
            if not args.inner_blur_profile:return
            from profile_interactive_rim import collect
            key('b');key('Right')
            profile=root/('profile-'+level);profile.mkdir()
            code=x.XKeysymToKeycode(d,x.XStringToKeysym(b'Y'))
            def profile_key(down):
                assert xt.XTestFakeKeyEvent(d,code,down,0)
                x.XSync(d,0)
            collect(native_frames,args.dol.resolve(),profile,lambda:profile_key(1),lambda:profile_key(0),args.profile_seconds)
            key('b');key('Left')
        shot('01-blur-off');cube_blur('off');measure_blur('off')
        key('a')
        for _ in range(2 if args.mockup_review and not args.inner_blur_cubes else 3):key('Down')
        shot('02-options-blur-setting');key('a');shot('03-blur-off-editor')
        for index,level in enumerate(('low','medium','high')):
            if index>0:
                key('a')
                if args.inner_blur_profile or args.inner_blur_cubes:
                    for _ in range(3):key('Down')
                key('a')
            key('Up');key('a')
            if index==0:shot('04-options-low-confirmed')
            key('b');shot(f'{index+5:02d}-blur-{level}');cube_blur(level);measure_blur(level)
        key('a')
        if args.inner_blur_profile or args.inner_blur_cubes:
            for _ in range(3):key('Down')
        key('a');key('Up');shot('08-off-draft');key('b');shot('09-cancel-keeps-high')
        key('a');key('a');key('Up');key('a');key('b');shot('10-off-restored')
    elif args.mockup_review:
        if args.optics_diagnostic:
            shot('01-fresnel');key('z');shot('02-environment-uv');key('z');shot('03-field-uv')
        else:shot('01-options-target')
        if not args.mockup_single and not args.optics_diagnostic:
            if not args.mockup_corners:
                key('a');shot('02-options-flat')
                key('b');shot('03-options-return')
            key('b')
            code=x.XKeysymToKeycode(d,x.XStringToKeysym(b'Right'))
            assert xt.XTestFakeKeyEvent(d,code,1,0)
            x.XSync(d,0);native_frames.advance(4)
            assert xt.XTestFakeKeyEvent(d,code,0,0)
            x.XSync(d,0);shot('04-rotating-corner')
            if not args.mockup_corners:native_frames.advance(60);shot('05-video-target')
    elif args.system_clock:
        key('Down');shot('01-real-clock')
        key('a');key('a');key('Up');key('Right');key('Up');shot('02-clock-draft')
        key('a');shot('03-clock-committed')
        p.terminate();p.wait(timeout=15)
        for saved in (root/'user').rglob('*SRAM*'):
            if saved.is_file():shutil.copyfile(saved,root/('committed-'+saved.name))
        shutil.move(root/'user/Dump/Frames',root/'before-restart-frames')
        p=subprocess.Popen(['/usr/games/dolphin-emu-nogui','--platform','x11','-u',str(root/'user'),'-C','SYSCONF.IPL.PGS=True','-e',str(args.dol.resolve())],stdout=log,stderr=subprocess.STDOUT)
        time.sleep(15);capture(root/'restart-unused.ppm',True);time.sleep(2)
        key('Down');shot('04-clock-after-restart')
        key('a');key('a');key('Down');key('Right');key('Down');key('a');shot('05-clock-decremented-after-emulator-reset')
    elif args.directional_return:
        key('i');shot('01-up-play');key('k',2);shot('02-down-return-held')
        key('k');key('Up');shot('03-calendar-read-only-preview');key('i');shot('04-up-return')
        key('l');shot('05-right-video');key('j');shot('06-left-return')
        key('j');shot('07-left-options');key('l');shot('08-right-return')
    elif args.flat_pages:
        key('Left');key('Down');shot('01-options-side-read-only')
        key('a');shot('02-options-flat-list')
        key('Down');key('a');shot('03-motion-changed-on-flat-page');key('a')
        key('b');shot('04-flat-list-b-to-cube')
        key('b');key('Right');key('a');shot('05-video-grouped-detail')
        key('Right',settle_frames=12);shot('06-scan-field-editor')
        key('Up');key('a');shot('07-progressive-preview')
        key('b');shot('08-b-reverts-before-cube')
        key('b');key('Right');key('a');key('Right',settle_frames=12);key('Up');key('a');key('a');shot('09-kept-progressive-detail')
        key('b');key('b');key('Down',settle_frames=40);key('a');shot('10-calendar-flat-list')
        key('a');key('Up');shot('11-hour-draft')
        key('b');shot('12-editor-b-discards-to-cube')
        key('b');key('w');key('a');shot('13-storage-flat-list')
        key('a');shot('14-native-memory-card-grid')
        key('a');shot('15-native-card-action-dialog')
        key('b');shot('16-dialog-b-closes-to-cube')
        key('b');key('Up',settle_frames=40);key('a');shot('17-game-browser')
        key('b');shot('18-browser-b-to-cube')
        key('x');shot('19-safe-interlaced-preview-side')
    elif args.dense_storage:
        if args.dense_quick:
            if not args.sd_only:
                wait_frames(frame_count(),30);key('w');key('a');key('a');wait_frames(frame_count(),160);shot('01-memory-dense-grid')
                key('y');shot('02-memory-list');key('Down');key('y');shot('03-memory-grid-preserved-selection')
                key('Left');key('Left');shot('04-memory-last-page-grid');key('y');shot('05-memory-last-page-list')
                key('w');shot('05b-memory-slot-b-list');key('q');shot('05c-memory-slot-a-list')
                key('b');shot('06-memory-b-to-cube')
            if not args.memory_only:
                if args.sd_only:wait_frames(frame_count(),30);key('w')
                key('a');key('Down');key('a');wait_frames(frame_count(),160);shot('08-sd-dense-grid')
                key('y');shot('09-sd-list');key('Down');key('y');wait_frames(frame_count(),160);shot('10-sd-grid-file-details')
                key('y');shot('11-sd-list-same-file');key('b');shot('12-sd-b-to-cube')
        else:
            wait_frames(frame_count(),30)
            key('w');key('a');key('a');wait_frames(frame_count(),160);shot('01-memory-dense-grid')
            key('y');shot('02-memory-list')
            for _ in range(6):key('Down')
            key('y');shot('03-memory-grid-preserved-selection')
            for _ in range(6):key('Down')
            for _ in range(9):key('Right')
            shot('04-memory-second-grid-page')
            key('y');shot('05-memory-second-page-list')
            key('w');shot('05b-memory-slot-b-list');key('q');shot('05c-memory-slot-a-list')
            key('a');shot('06-memory-actions-default-cancel')
            key('b');shot('07-memory-dialog-b-to-cube')
            if not args.memory_only:
                key('a');key('Down');key('a');wait_frames(frame_count(),160);shot('08-sd-dense-grid')
                key('y');shot('09-sd-list')
                key('a');shot('10-sd-folder-list')
                key('a');shot('11-sd-bounded-file-preview')
                key('a');key('q');wait_frames(frame_count(),160);shot('12-sd-parent-list')
                key('y');wait_frames(frame_count(),160);shot('13-sd-grid-view-remembered')
                for _ in range(7):key('Down')
                wait_frames(frame_count(),160);shot('14-sd-second-grid-page')
                for _ in range(20):key('Right')
                shot('14b-sd-large-file-size');key('Right');shot('14c-sd-filename-not-header')
                key('y');wait_frames(frame_count(),160);shot('15-sd-list-same-file')
                key('u');wait_frames(frame_count(),160);shot('16-c-stick-refresh')
                key('b');shot('17-sd-b-to-cube')
    elif args.storage:
        key('w');shot('01-storage-cube-face')
        key('a');key('a');shot('02-card-slot-a')
        key('a');shot('03-save-actions')
        key('Right');key('a');shot('04-copy-confirm-cancel')
        key('Right');key('a');shot('05-native-copy-complete')
        key('w');shot('06-card-slot-b')
        key('a');key('Left');key('a');shot('07-delete-confirm-cancel')
        key('b');shot('08-dialog-b-to-cube')
        if args.disc:
            key('a');key('Down');key('a');shot('09-sd-style-file-grid')
            key('a');shot('10-folder-open')
            key('a');shot('11-text-preview')
            key('a');key('q');shot('12-l-parent-folder')
            key('b');shot('13-file-browser-b-to-cube')
    elif args.library:
        key('Up');shot('01-play-library')
        key('a');shot('02-six-glass-game-cards')
        key('Down');key('Down');shot('03-next-library-page')
        key('a');shot('04-swiss-confirm-default-cancel')
        key('Right');key('a');shot('05-fixture-launch-refused')
        key('b');shot('06-browser-b-to-cube')
        key('k');shot('07-reverse-stick-to-cube')
    elif args.glass_modes:
        if args.inner_blur_high:
            key('Left');key('a')
            for _ in range(3):key('Down')
            key('a');key('Down');key('a');key('b')
        key('x');shot('01-safe-480i-cube')
        key('a');key('Right',settle_frames=12);key('Up');key('a');shot('02-480p-preview')
        key('a');key('b');shot('03-480p-cube')
        key('a');key('Right',settle_frames=12);key('Up');key('a');key('a');key('b');shot('04-wide-480p-cube')
        key('x');key('a');key('Up');key('a');key('a');key('b');shot('05-576i-cube')
        # The SDK correctly rejects 240i. Select progressive before applying 240p.
        key('x');key('a');key('Right',settle_frames=12);key('Up');key('Left',settle_frames=12);key('Down');key('a');key('a');key('b');shot('06-240-line-cube')
        key('x');key('a');key('Right',settle_frames=12);key('Up');key('a');key('b');shot('07-b-rolls-back-to-480i-cube')
        key('x');shot('08-safe-mode-cube')
    elif args.selector_scan:
        key('Right');key('a');key('Right',settle_frames=12);shot('01-interlaced-editor')
        key('Up');shot('02-progressive-editor')
        key('a');shot('03-progressive-preview')
        key('a');shot('04-progressive-kept')
        key('Down');key('a');shot('05-interlaced-preview')
        key('b');shot('06-progressive-restored')
        key('x');shot('07-safe-interlaced')
    elif args.selector_motion:
        key('Down');shot('01-calendar')
        key('a');key('a');shot('02-hour-a');wait_frames(frame_count(),45);shot('03-hour-b')
        key('Up');shot('04-hour-edited')
        key('Right');key('Up');shot('05-minute-edited')
        key('a');shot('06-time-committed')
        key('Down');key('a');key('Right');key('Up');shot('07-month-edited')
        key('Right');key('Up');shot('08-year-edited')
        key('b');shot('09-date-cancelled')
        key('b');key('Left');key('a');key('Down');key('a');key('b');key('b');key('Down');key('a');key('a')
        shot('10-reduced-a');wait_frames(frame_count(),45);shot('11-reduced-b')
    elif args.rim_review:
        if args.glass_profile_only:key('Right')
        else:
            key('Down');shot('01-calendar-front')
            key('b')
            code=x.XKeysymToKeycode(d,x.XStringToKeysym(b'Right'))
            assert xt.XTestFakeKeyEvent(d,code,1,0)
            x.XSync(d,0);native_frames.advance(4)
            assert xt.XTestFakeKeyEvent(d,code,0,0)
            x.XSync(d,0);shot('02-rotating-corner')
            native_frames.advance(60);shot('03-video-front')
        if args.rim_profile:
            if not args.glass_profile_only:
                key('a');shot('04-video-grouped-detail')
                key('b');shot('05-detail-b-to-cube')
            from profile_interactive_rim import collect
            code=x.XKeysymToKeycode(d,x.XStringToKeysym(b'Y'))
            def profile_key(down):
                assert xt.XTestFakeKeyEvent(d,code,down,0)
                x.XSync(d,0)
            collect(native_frames,args.dol.resolve(),root,lambda:profile_key(1),lambda:profile_key(0))
            key('z');shot('06-sealed-glass-overlay')
    elif args.calendar:
        key('Down');shot('01-calendar-two-rows')
        key('a');key('a');key('Up');key('Right');key('Up');shot('02-time-parts')
        key('b');key('a');key('Down');key('a');key('Right');key('Up');shot('03-date-parts')
        key('b');shot('04-calendar-cancelled')
    else:
        shot('01-default-hidden')
        key('z',2);shot('02-z-held-visible')
        key('Left');shot('03-options-visible')
        key('a');key('a');shot('04-editor-visible')
        key('z',1);shot('05-z-hidden-editor')
        key('b');key('z');time.sleep(12);shot('06-overlay-visible-cube')
        if args.widescreen:
            key('x');key('a');key('Right',settle_frames=12);key('Right',settle_frames=12)
            key('Up');key('a');key('a');shot('07-widescreen-kept')
            key('z');shot('08-widescreen-hidden')
            key('x');shot('09-safe-mode')
    (root/'events.json').write_text(json.dumps(events,indent=2)+'\n')
finally:
    # Preserve completed capture metadata even if a later profile is incomplete.
    if events:(root/'events.json').write_text(json.dumps(events,indent=2)+'\n')
    if native_frames is not None:native_frames.close()
    if p.poll() is None:
        p.send_signal(signal.SIGCONT);p.terminate()
        try:p.wait(timeout=5)
        except subprocess.TimeoutExpired:p.kill();p.wait()
    log.close()
