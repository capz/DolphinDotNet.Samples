#!/usr/bin/env python3
"""Capture and exercise the packaged ControllerInput DOL in real Dolphin.

Run under Xvfb. Native screenshots come from the completed, original GameCube
XFB, never a mockup or composited scene. Paused captures are not timing
measurements. The default acceptance path additionally exercises real
controller inputs and emulator SI hotplug; --initial-only is a visual review.
"""
import argparse
from contextlib import contextmanager
from fractions import Fraction
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import struct
import subprocess
import sys
import time
import urllib.request
import xml.etree.ElementTree as ET

from PIL import Image, ImageChops, ImageDraw, ImageStat

from decode_native_xfb import decode
from controller_hotplug import DolphinHotplug


SDK_TOOLS_COMMIT = '0d378dba1cbe88392ab74d4031ac179ec05c6022'
SDK_TOOL_FILES = ('dolphin_gdb_probe.py', 'dolphin_smoke.py', 'dolphin_controller_test.py')
SDK_TOOL_HASHES = {
    'dolphin_gdb_probe.py': '3c0cec51fe61b68a0d4224c10f25d4c09ae2df79614ec30e4f966c645308ac52',
    'dolphin_smoke.py': '94277377b4cac7356f0878afb635060b362c39ce0d827098ddb4f9d9708d18e7',
    'dolphin_controller_test.py': 'd682093bea5c84c66b0a86bdf98dd7a04d7366248ff4bc0aa561a6884c457779',
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_provenance(source):
    def git(*args):
        return subprocess.check_output(['git','-c','safe.directory='+str(source),'-C',str(source),*args],text=True).strip()
    project=source/'ControllerInput/ControllerInput.csproj'
    files=set((source/'ControllerInput').glob('*.cs'))
    files.update(p for p in (source/'ControllerInput/Assets').rglob('*') if p.is_file())
    files.update(p for p in (source/'ControllerInput/Tools').rglob('*') if p.is_file() and '__pycache__' not in p.parts)
    files.update((source/'ControllerInput/Design').glob('*.png'))
    files.update((source/'Common').glob('*.cs'))
    files.update((project,source/'global.json',source/'Directory.Build.props'))
    for optional in ('Directory.Build.targets','NuGet.Config','nuget.config'):
        if (source/optional).is_file():files.add(source/optional)
    for element in ET.parse(project).getroot().iter():
        include=element.get('Include','')
        if element.tag not in ('Compile','None','EmbeddedResource','Content') or '$(' in include or '*' in include:continue
        path=(project.parent/include).resolve()
        if path.is_file():
            path.relative_to(source);files.add(path)
    approved=source/'ControllerInput/Design/approved-mockup.png'
    if approved.is_file():files.add(approved)
    status=git('status','--porcelain','--untracked-files=no')
    untracked=git('ls-files','--others','--exclude-standard','ControllerInput','Common').splitlines()
    return dict(git_head=git('rev-parse','HEAD'),git_tree=git('rev-parse','HEAD^{tree}'),
                tracked_tree_dirty=bool(status),tracked_status=status.splitlines(),
                untracked_production_files=untracked,
                source_file_sha256={str(p.relative_to(source)):digest(p) for p in sorted(files)})


def sdk_tools(args, output):
    if args.sdk:
        root = args.sdk.resolve()
        if not all((root/'scripts'/name).is_file() for name in SDK_TOOL_FILES):
            raise ValueError('--sdk must contain the three SDK capture test helpers')
    else:
        # Test tooling only: the DOL remains built from the published NuGet SDK.
        # Pin every helper URL to the published 361.1 repository revision.
        root = output/'sdk-test-tools'
        (root/'scripts').mkdir(parents=True)
        for name in SDK_TOOL_FILES:
            url = f'https://raw.githubusercontent.com/capz/DolphinDotNet/{SDK_TOOLS_COMMIT}/scripts/{name}'
            request = urllib.request.Request(url, headers={'User-Agent': 'ControllerInput-capture'})
            with urllib.request.urlopen(request, timeout=30) as response:
                data = response.read(1024*1024+1)
            if len(data)>1024*1024:raise ValueError('Oversized SDK test helper')
            (root/'scripts'/name).write_bytes(data)
    hashes={name: digest(root/'scripts'/name) for name in SDK_TOOL_FILES}
    if hashes!=SDK_TOOL_HASHES:raise ValueError('SDK capture helpers differ from the pinned public package revision')
    return root,hashes


def symbols(elf):
    found = {};probe=None
    for line in subprocess.check_output(['nm', '-S', '-a', str(elf)], text=True).splitlines():
        fields = line.split()
        if len(fields)!=4:continue
        address, size, _, name = fields
        if name=='dnd_graphics_end_frame' and int(size,16)>=32:probe=int(address,16)
        key = name.split('.lto_priv.')[0]
        wanted={'pads':144,'lines':790,'dnd_value_heap':4,'total_bytes':4,
                'overlay_serial':4,'currentFb':4,'mode':4,'dnd_type_ControllerSession':44}
        if key in wanted:
            expected = wanted[key]
            if int(size,16)!=expected:continue
            if key in found:raise ValueError('Ambiguous native diagnostic symbol: '+key)
            found[key] = int(address,16)
    if set(found)!=set(wanted):
        raise ValueError('Missing native read-only diagnostics: '+str(found))
    # Prove the shared mapping is MEM1 at offset 0 instead of trusting its name
    # or size. This custom SDK function is not a Dolphin HLE patch target.
    if probe is None:raise ValueError('Missing DOL text mapping probe')
    dol=elf.with_suffix('.dol').read_bytes()
    for section in range(7):
        offset=struct.unpack_from('>I',dol,section*4)[0]
        address=struct.unpack_from('>I',dol,0x48+section*4)[0]
        size=struct.unpack_from('>I',dol,0x90+section*4)[0]
        if address<=probe<=address+size-32:
            found['mem1_probe_address']=probe
            found['mem1_probe_bytes']=dol[offset+probe-address:offset+probe-address+32]
            break
    else:raise ValueError('Native mapping probe is outside the matching DOL text')
    return found


class LiveMemory:
    """Read Dolphin's Linux MEM1 mapping; never call or write the guest.

    A stopped host process makes PAD, heap and currently scanned framebuffer
    reads coherent. Steady measurements deliberately keep the process running.
    """
    def __init__(self,process,points):
        self.process=process;self.points=points;self.fd=None
        deadline=time.monotonic()+20
        while time.monotonic()<deadline:
            if process.poll() is not None:raise RuntimeError('Dolphin exited before MEM1 was available')
            for path in Path(f'/proc/{process.pid}/fd').iterdir():
                try:
                    target=os.readlink(path)
                    if 'dolphin-emu' not in target or '/dev/shm/' not in target:continue
                    fd=os.open(path,os.O_RDONLY)
                    if os.fstat(fd).st_size>=24*1024*1024:
                        actual=os.pread(fd,32,points['mem1_probe_address']-0x80000000)
                        if actual==points['mem1_probe_bytes']:
                            self.fd=fd;self.mapping_probe=dict(address=hex(points['mem1_probe_address']),
                                matching_dol_text_sha256=hashlib.sha256(actual).hexdigest(),bytes=32)
                            return
                    os.close(fd)
                except (FileNotFoundError,PermissionError):pass
            time.sleep(.05)
        raise TimeoutError('Dolphin MEM1 shared mapping was not found')

    def read_bytes(self,address,length):
        address=(address&0x3fffffff)|0x80000000
        if not 0x80000000<=address<=0x81800000-length:raise ValueError('MEM1 read outside GameCube RAM')
        raw=os.pread(self.fd,length,address-0x80000000)
        if len(raw)!=length:raise ValueError('Truncated read-only MEM1 snapshot')
        return raw

    def word(self,address):return int.from_bytes(self.read_bytes(address,4),'big')
    def read_serial(self):return self.word(self.points['overlay_serial'])

    def wait_frames(self,count,timeout=120):
        start=self.read_serial();deadline=time.monotonic()+timeout
        while time.monotonic()<deadline:
            if self.process.poll() is not None:raise RuntimeError('Dolphin exited during runtime exercise')
            now=self.read_serial()
            if now-start>=count:return now
            time.sleep(.02)
        raise TimeoutError(f'Native presentation stalled: {start} -> {self.read_serial()}, expected +{count}')

    @contextmanager
    def paused(self):
        self.process.send_signal(signal.SIGSTOP)
        deadline=time.monotonic()+3
        while '\nState:\tT' not in Path(f'/proc/{self.process.pid}/status').read_text():
            if time.monotonic()>deadline:raise TimeoutError('Cannot pause native presentation for coherent read')
            time.sleep(.005)
        try:yield
        finally:self.process.send_signal(signal.SIGCONT)

    def session(self):
        heap=self.word(self.points['dnd_value_heap'])
        start,capacity,used,block,_,_=struct.unpack('>6I',self.read_bytes(heap,24))
        seen=set();matches=[]
        while block:
            if block in seen or not start<=block<start+used:raise ValueError('Invalid managed block chain')
            seen.add(block);raw=self.read_bytes(block,16)
            size=int.from_bytes(raw[:4],'big');next_block=int.from_bytes(raw[8:12],'big')
            if not raw[5] and size>=76 and self.word(block+16)==self.points['dnd_type_ControllerSession']:
                matches.append(block+16)
            block=next_block
        if len(matches)!=1:raise ValueError(f'Expected one retained ControllerSession, found {len(matches)}')
        obj=matches[0]
        def text(offset):
            pointer=self.word(obj+offset);length=self.word(pointer+8)
            if length>1024:raise ValueError('Invalid retained session string')
            return self.read_bytes(pointer+12,length*2).decode('utf-16-be')
        # Verified against this sample's generated public-361.1 getters. These
        # are diagnostics, not SDK ABI or writes to the retained session.
        return dict(selected_port=struct.unpack('>i',self.read_bytes(obj+44,4))[0],
                    status=text(48),buttons=text(56),last_event=text(64),
                    exit_requested=bool(self.read_bytes(obj+72,1)[0]))

    def capture_xfb(self,path):
        mode=self.word(self.points['mode']);raw=self.read_bytes(mode,26)
        width=int.from_bytes(raw[4:6],'big');height=int.from_bytes(raw[8:10],'big')
        if not 0<width<=1024 or not 0<height<=1024 or width%16:raise ValueError('Invalid native XFB dimensions')
        address=self.word(self.points['currentFb'])
        if not address:raise ValueError('No presented libogc framebuffer')
        path.write_bytes(self.read_bytes(address,width*height*2))
        return dict(width=width,height=height,format='YUYV422',
                    source='libogc VIDEO_GetCurrentFramebuffer currentFb, coherent host-paused read after native scanout',
                    vi_tv_mode=int.from_bytes(raw[:4],'big'))

    def close(self):
        if self.fd is not None:os.close(self.fd);self.fd=None


def native_snapshot(frames, points):
    raw = frames.read_bytes(points['pads'],144)
    pads=[]
    for port in range(4):
        held,pressed,released,flags,*axes = struct.unpack_from('>HHH2xi6f',raw,port*36)
        pads.append(dict(port=port+1,connected=bool(flags&1),flags=flags,held=held,
                         pressed=pressed,released=released,axes=axes))
    lines=frames.read_bytes(points['lines'],790)
    console=[lines[at:at+79].split(b'\0',1)[0].decode(errors='replace') for at in range(0,790,79)]
    heap=int.from_bytes(frames.read_bytes(points['dnd_value_heap'],4),'big')
    _,capacity,used,_,_,collections=struct.unpack('>6I',frames.read_bytes(heap,24))
    if not 0<used<=capacity<=16*1024*1024:raise ValueError('Invalid managed heap readback')
    return dict(pads=pads,console=console,
                native_draw_bytes=int.from_bytes(frames.read_bytes(points['total_bytes'],4),'big'),
                managed_heap=dict(capacity_bytes=capacity,arena_used_bytes=used,collections=collections,
                                  scope='Paused post-frame arena, not peak/live memory'))


def configure(output, sdk, *, debugger=True, dump=False):
    sys.path.insert(0,str(sdk/'scripts'))
    from dolphin_controller_test import pad_config, commands
    user=output/'user';config=user/'Config';config.mkdir(parents=True)
    (user/'Logs').mkdir();pipes=user/'Pipes';pipes.mkdir()
    (config/'GCPadNew.ini').write_text(pad_config())
    (config/'Dolphin.ini').write_text(
        '[Core]\nGFXBackend = OGL\nCPUCore = 1\nCPUThread = False\n'
        'SIDevice0 = 6\nSIDevice1 = 0\nSIDevice2 = 0\nSIDevice3 = 0\n'
        '[DSP]\nBackend = No Audio Output\n[General]\nGDBPort = 2174\n'
        '[Display]\nRenderToMain = False\nRenderWindowXPos = 0\nRenderWindowYPos = 0\nRenderWindowWidth = 640\nRenderWindowHeight = 480\n'
        '[Interface]\nOnScreenDisplayMessages = False\nConfirmStop = False\nPauseOnFocusLost = False\nHideMainWindowWhenRunning = False\n'
        '[Input]\nBackgroundInput = True\n'
        '[Analytics]\nEnabled = False\nPermissionAsked = True\n')
    (config/'GFX.ini').write_text('[Settings]\nInternalResolution = 1\nMSAA = 1\nSSAA = False\nAspectRatio = 2\n'
                                '[Hacks]\nForceProgressive = True\nXFBToTextureEnable = False\nDeferEFBCopies = False\n')
    if not debugger:
        ini=config/'Dolphin.ini';ini.write_text(ini.read_text().replace('[General]\nGDBPort = 2174\n',''))
    if dump:
        ini=config/'Dolphin.ini';ini.write_text(ini.read_text()+'\n[Movie]\nDumpFrames = True\nDumpFramesSilent = True\n')
        ini=config/'GFX.ini';ini.write_text(ini.read_text().replace('[Settings]\n','[Settings]\nDumpFramesAsImages = True\n'))
    (config/'Logger.ini').write_text('[Options]\nWriteToFile = True\nWriteToConsole = True\nVerbosity = 3\n[Logs]\nBOOT = True\nCORE = True\nPOWERPC = True\nOSREPORT = True\n')
    fds=[]
    for port in range(4):
        path=pipes/f'dnd{port}';os.mkfifo(path)
        fd=os.open(path,os.O_RDWR|os.O_NONBLOCK);fds.append(fd)
        os.write(fd,commands(dict(port=0),port).encode())
    return user,fds


def environment(output):
    env=dict(os.environ,LIBGL_ALWAYS_SOFTWARE='1',SDL_AUDIODRIVER='dummy',QT_SCALE_FACTOR='1',QT_FONT_DPI='96',LANG='C.UTF-8')
    for key,folder in (('XDG_CONFIG_HOME','home/config'),('XDG_CACHE_HOME','home/cache'),('XDG_DATA_HOME','home/data')):
        path=output/folder;path.mkdir(parents=True,exist_ok=True);env[key]=str(path)
    return env


def runtime_capture(memory,points,output,name):
    with memory.paused():
        serial=memory.read_serial();snapshot=native_snapshot(memory,points);snapshot['session']=memory.session()
        raw=output/(name+'-xfb.yuv');info=memory.capture_xfb(raw)
        if len(set(raw.read_bytes()))<8:raise AssertionError('Native XFB remains clear: Dolphin RAM copies are required')
        png=output/(name+'-xfb.png');decode(raw.read_bytes(),info['width'],info['height']).save(png)
    return dict(capture=name,frame_serial=serial,mem1_mapping_probe=memory.mapping_probe,
                xfb=info,xfb_file=raw.name,xfb_sha256=digest(raw),
                native_png=png.name,png_sha256=digest(png),snapshot=snapshot)


def capture_client(process,env,user,output):
    """Keep the entire 4:3 Dolphin client, with no crop/resize/compositing."""
    windows=DolphinHotplug(process,env,user)._windows()
    render=next((w['id'] for w in windows if 'JIT' in w['title'] or 'ControllerInput' in w['title']),None)
    if render is None:raise AssertionError('The real Dolphin render client was not found')
    hidden=[w['id'] for w in windows if w['id']!=render and w['title'].startswith('Dolphin')]
    try:
        for window in hidden:subprocess.run(['xdotool','windowunmap',str(window)],env=env,check=True)
        ppm=output/'neutral-native-window.ppm'
        subprocess.run(['python3',str(Path(__file__).with_name('capture_x11.py')),str(ppm)],env=env,check=True)
        png=output/'neutral-native-window.png'
        with Image.open(ppm) as image:
            size=image.size;image.save(png)
        return dict(file=png.name,sha256=digest(png),width=size[0],height=size[1],
                    source='Complete native 4:3 X11 render client; no crop/resize/compositing')
    finally:
        for window in hidden:subprocess.run(['xdotool','windowmap',str(window)],env=env,check=True)
        subprocess.run(['xdotool','windowraise',str(render),'windowfocus','--sync',str(render)],env=env,check=True)


def stop(process):
    if process and process.poll() is None:
        process.send_signal(signal.SIGCONT);process.terminate()
        try:process.wait(timeout=8)
        except subprocess.TimeoutExpired:process.kill();process.wait()


def benchmark(dol,points,sdk,output):
    """No GDB, frame dumps, screenshots, pauses or injected inputs in this run."""
    output.mkdir();user,fds=configure(output,sdk,debugger=False);env=environment(output)
    process=None;memory=None
    try:
        with (output/'emulator.log').open('w') as log:
            process=subprocess.Popen(['/usr/games/dolphin-emu-nogui','--platform','x11','-u',str(user),
                                      '-C','SYSCONF.IPL.PGS=True','-e',str(dol)],env=env,stdout=log,stderr=subprocess.STDOUT)
            memory=LiveMemory(process,points)
            memory.wait_frames(165,timeout=180)
            before=native_snapshot(memory,points);start=memory.read_serial();started=time.monotonic()
            memory.wait_frames(180,timeout=180)
            elapsed=time.monotonic()-started;end=memory.read_serial();after=native_snapshot(memory,points)
            for snapshot in (before,after):snapshot['managed_heap']['scope']='Uninterrupted sparse native read; arena extent, not peak/live memory'
            console=after['console']
            profile=next((re.search(r'CONTROLLER PROFILE samples (\d+) active_us (\d+) max_us (\d+)',line) for line in console if 'CONTROLLER PROFILE' in line),None)
            frame=next((re.search(r'CONTROLLER FRAME mean_us (\d+) native (\d+)',line) for line in console if 'CONTROLLER FRAME' in line),None)
            if not profile or not frame:raise AssertionError('Missing uninterrupted native 120-frame profile')
            project=Path(__file__).resolve().parents[1]/'ControllerInput/ControllerInput.csproj'
            configured=ET.parse(project).find('.//DndManagedHeapBytes')
            if configured is None or configured.text is None:raise AssertionError('Sample must declare its native managed heap budget')
            expected_capacity=int(configured.text.strip(),0)
            if after['managed_heap']['capacity_bytes']!=expected_capacity:
                raise AssertionError('Packaged managed heap property did not reach native runtime')
            mode=memory.word(points['mode']);tv_mode=memory.word(mode);standard=tv_mode>>2
            hz=50 if standard in (1,4) else 60
            result=dict(method='Uninterrupted real Dolphin, no debugger/dumps/screenshots/input, software OpenGL; original hardware unmeasured',
                        clock_scope='Application measurements use emulated GameCube Clock; wall interval is separately measured on the host',
                        configured_managed_heap_bytes=expected_capacity,
                        vi_tv_mode=tv_mode,vi_standard=standard,native_refresh_hz=hz,native_frame_target_us=1000000/hz,
                        samples=int(profile[1]),active_mean_us=int(profile[2]),active_max_us=int(profile[3]),
                        frame_mean_us=int(frame[1]),profile_native_bytes=int(frame[2]),
                        steady_presentations=end-start,steady_wall_seconds=elapsed,
                        before=before,after=after,
                        arena_used_delta_bytes=after['managed_heap']['arena_used_bytes']-before['managed_heap']['arena_used_bytes'],
                        collections_delta=after['managed_heap']['collections']-before['managed_heap']['collections'],
                        native_draw_delta_bytes=after['native_draw_bytes']-before['native_draw_bytes'])
            (output/'profile.json').write_text(json.dumps(result,indent=2)+'\n')
            return result
    finally:
        if memory:memory.close()
        stop(process)
        for fd in fds:os.close(fd)


def make_video(user,output,hz):
    source=user/'Dump/Frames';frames=[]
    for path in sorted(source.glob('framedump_*.png'),key=lambda p:int(p.stem.rsplit('_',1)[1])):
        if path.read_bytes().endswith(b'IEND\xaeB`\x82'):frames.append(path)
    if len(frames)<60:raise AssertionError('No sufficient completed real Dolphin frames for video')
    listing=output/'video-frames.txt'
    listing.write_text(''.join("file '"+str(p).replace("'","'\\''")+f"'\nduration {1/hz:.9f}\n" for p in frames))
    video=output/'controller-input.mp4'
    subprocess.run(['ffmpeg','-v','error','-y','-f','concat','-safe','0','-i',str(listing),
                    '-an','-r',str(hz),'-c:v','libx264','-preset','fast','-crf','18','-pix_fmt','yuv420p',
                    '-aspect','4:3',str(video)],check=True)
    return dict(file=video.name,sha256=digest(video),frames=len(frames),
                source='Chronological real Dolphin frame dumps; no resized pixels, replacement frames, or post-capture visual overlays',
                display_aspect='4:3; sample-aspect metadata preserves the original native frame dimensions',
                encoded_video=probe_video(video),
                presentation_rate_hz=hz,emulator_wall_speed_is_playback_speed=False)


def probe_video(video):
    probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-select_streams','v:0',
        '-show_entries','stream=codec_name,width,height,sample_aspect_ratio,display_aspect_ratio,r_frame_rate,nb_frames',
        '-show_entries','format=duration,size','-of','json',str(video)],text=True))
    if len(probe['streams'])!=1:raise AssertionError('Expected one native capture video stream')
    stream=probe['streams'][0]
    sar=Fraction(stream['sample_aspect_ratio'].replace(':','/'))
    dar=Fraction(stream['display_aspect_ratio'].replace(':','/'))
    if dar!=Fraction(4,3) or Fraction(stream['width'],stream['height'])*sar!=Fraction(4,3):
        raise AssertionError('Native capture video must display at the approved 4:3 aspect ratio')
    return dict(coded_width=stream['width'],coded_height=stream['height'],
                sample_aspect_ratio=stream['sample_aspect_ratio'],display_aspect_ratio=stream['display_aspect_ratio'],
                codec=stream['codec_name'],frame_rate=stream['r_frame_rate'],frames=int(stream['nb_frames']),
                duration_seconds=float(probe['format']['duration']),bytes=int(probe['format']['size']))


FACE_ROIS={'A':(435.8,170.4,27,27),'B':(391.6,190.3,17,17),'X':(477.6,159.6,16,24),
           'Y':(423.9,128.8,24,17),'START':(319.1,166.6,12,12),'Z':(448.4,95.3,35,17),
           'L':(190.9,92.4,37,24),'R':(447.5,86.2,32,16)}


def face_difference(neutral,current,button):
    # Approved plate/control registration is defined in a 640x480 viewport;
    # normalized coordinates also cover the native PAL viewport, without
    # resizing either capture or comparing HUD text instead of the real cap.
    with Image.open(neutral) as base,Image.open(current) as image:
        if base.size!=image.size:raise AssertionError('Native capture dimensions changed')
        width,height=base.size;x,y,rx,ry=FACE_ROIS[button]
        box=(round((x-rx)*width/640),round((y-ry)*height/480),
             round((x+rx)*width/640),round((y+ry)*height/480))
        a=list(base.convert('RGB').crop(box).getdata());b=list(image.convert('RGB').crop(box).getdata())
        changed=sum(max(abs(c-d) for c,d in zip(p,q))>=12 for p,q in zip(a,b))
        mean=sum(abs(c-d) for p,q in zip(a,b) for c,d in zip(p,q))/(len(a)*3)
        return dict(roi=list(box),pixels=len(a),changed_pixels=changed,mean_absolute_channel_difference=mean)


def plate_registration(native,approved):
    """Check fixed artwork, allowing hardware texture/color quantization.

    All changing controls and live UI are excluded. This checks the real XFB
    against the approved source, not against another generated screenshot.
    """
    with Image.open(native) as image,Image.open(approved) as source:
        actual=image.convert('RGB');width,height=actual.size
        reference=source.convert('RGB').resize(actual.size,Image.Resampling.LANCZOS)
        mask=Image.new('L',actual.size,255);draw=ImageDraw.Draw(mask)
        # Include a generous border around moving controls and their clean-plate
        # cavities, so input geometry or foreground masks cannot skew this check.
        excluded=[(150,65,232,120),(410,66,487,116),
                  (169,136,238,204),(225,221,289,285),(347,220,415,288),
                  (405,139,467,202),(371,169,412,211),(458,131,497,188),
                  (396,108,452,150),(304,151,334,182),
                  (440,20,622,68),(30,366,610,430),(30,431,616,458)]
        for x0,y0,x1,y1 in excluded:
            draw.rectangle((round(x0*width/640),round(y0*height/480),
                            round(x1*width/640),round(y1*height/480)),fill=0)
        difference=ImageChops.difference(actual,reference)
        statistics=ImageStat.Stat(difference,mask)
        return dict(reference='ControllerInput/Design/approved-mockup.png',
                    reference_sha256=digest(approved),native_size=list(actual.size),
                    compared_pixels=statistics.count[0],
                    mean_absolute_channel_difference=sum(statistics.mean)/3,
                    maximum_allowed_mean_difference=12,
                    exclusions='Moving controls/cavities, live UI, and texture-filter borders')


def main(args):
    output=args.output.resolve()
    if output.exists() and any(output.iterdir()):raise ValueError('Use a fresh capture output directory')
    output.mkdir(parents=True,exist_ok=True)
    dol=args.dol.resolve();elf=dol.with_suffix('.elf')
    if not dol.is_file() or not elf.is_file():raise ValueError('Build the matching packaged DOL and ELF first')
    sdk,helper_hashes=sdk_tools(args,output)
    points=symbols(elf)
    version=subprocess.check_output(['/usr/games/dolphin-emu-nogui','--version'],text=True).strip()
    source=Path(__file__).resolve().parents[1]
    report=dict(scope='initial neutral visual review' if args.initial_only else 'controller runtime acceptance',
                success=False,dol_sha256=digest(dol),elf_sha256=digest(elf),
                sdk_version='0.1.0-preview.361.1',sdk_tools_commit=SDK_TOOLS_COMMIT,sdk_tool_hashes=helper_hashes,
                source_override=False,emulator=version,original_hardware_measured=False,
                capture_script_sha256=digest(Path(__file__)),hotplug_script_sha256=digest(Path(__file__).with_name('controller_hotplug.py')),
                source_provenance=source_provenance(source),events=[])
    process=None;memory=None;fds=[]
    try:
        if not args.initial_only:
            print('Measuring uninterrupted native frames and steady memory',flush=True)
            report['profile']=benchmark(dol,points,sdk,output/'steady-profile')
        user,fds=configure(output,sdk,debugger=False,dump=True);env=environment(output)
        with (output/'emulator.log').open('w') as log:
            process=subprocess.Popen(['/usr/games/dolphin-emu','-u',str(user),'-C','SYSCONF.IPL.PGS=True','-e',str(dol)],env=env,stdout=log,stderr=subprocess.STDOUT)
            memory=LiveMemory(process,points);memory.wait_frames(10)
            event=runtime_capture(memory,points,output,'neutral');report['events'].append(event)
            registration=plate_registration(output/event['native_png'],source/'ControllerInput/Design/approved-mockup.png')
            report['approved_art_registration']=registration
            if registration['mean_absolute_channel_difference']>registration['maximum_allowed_mean_difference']:
                raise AssertionError('Native fixed shell/background do not match the approved mockup')
            report['native_presentation']=capture_client(process,env,user,output)
            report['capture_vi_tv_mode']=event['xfb']['vi_tv_mode']
            if not event['snapshot']['pads'][0]['connected'] or any(p['connected'] for p in event['snapshot']['pads'][1:]):
                raise AssertionError('Initial real SI port configuration did not reach PAD polling')
            if not any('CONTROLLER LAB READY' in line for line in event['snapshot']['console']):
                raise AssertionError('The controller application did not reach its ready marker')
            if not args.initial_only:
                from dolphin_controller_test import commands,BUTTONS
                hotplug=DolphinHotplug(process,env,user)
                masks={'A':0x100,'B':0x200,'X':0x400,'Y':0x800,'Z':0x10,'START':0x1000,
                       'L':0x40,'R':0x20,'D_UP':8,'D_DOWN':4,'D_LEFT':1,'D_RIGHT':2}
                def inject(case,name,*,image=False):
                    for port,fd in enumerate(fds):
                        data=commands(case,port).encode()
                        if os.write(fd,data)!=len(data):raise RuntimeError('Partial real controller pipe write')
                    # Keep each visual action on screen for 0.3 native seconds
                    # in the chronological video, and let its spring settle.
                    hold=18 if image else 4
                    memory.wait_frames(hold)
                    if image:result=runtime_capture(memory,points,output,name)
                    else:
                        with memory.paused():
                            snapshot=native_snapshot(memory,points);snapshot['session']=memory.session()
                            result=dict(capture=name,frame_serial=memory.read_serial(),snapshot=snapshot)
                    result['input']=case;result['input_hold_native_frames']=hold;report['events'].append(result)
                    port=case['port'];pad=result['snapshot']['pads'][port]
                    if not pad['connected']:raise AssertionError('Input delivered to a disconnected real SI controller')
                    held=sum(masks[b] for b in case.get('buttons',()))
                    if pad['held']!=held:raise AssertionError(f'{name}: actual PAD held {pad["held"]:x}, expected {held:x}')
                    names=[b.removeprefix('D_') for b in masks if b in case.get('buttons',())]
                    expected_text=' + '.join(names) if names else 'NONE'
                    if result['snapshot']['session']['buttons']!=expected_text:
                        raise AssertionError('Retained button label differs from actual PAD input')
                    if held and result['snapshot']['session']['selected_port']!=port:
                        raise AssertionError('Retained sample did not select the real active controller')
                    return result
                # Every button is observed held and released through libogc.
                for button in BUTTONS:
                    print('Real input:',button,flush=True)
                    result=inject(dict(port=0,buttons=[button]),'button-'+button.lower(),image=True)
                    if button in FACE_ROIS:
                        difference=face_difference(output/'neutral-xfb.png',output/result['native_png'],button)
                        result['cap_visual_difference']=difference
                        if difference['changed_pixels']<(8 if button=='START' else 25):
                            raise AssertionError(f'{button} actual cap pixels did not visibly respond')
                    inject(dict(port=0),'release-'+button.lower())
                for axis in range(4):
                    for value in (-1,1):
                        result=inject(dict(port=0,axis=axis,value=value),f'axis-{axis}-{value}',image=True)
                        actual=result['snapshot']['pads'][0]['axes'][axis]
                        if actual*value<.5:raise AssertionError('Actual analog stick did not reach requested direction')
                        inject(dict(port=0),f'axis-{axis}-neutral')
                for axis in (4,5):
                    for value in (.25,.5,1):
                        result=inject(dict(port=0,axis=axis,value=value),f'trigger-{axis-4}-{value}',image=True)
                        actual=result['snapshot']['pads'][0]['axes'][axis]
                        # A Dolphin pipe is raw 0..255. The public SDK's default
                        # InputOptions maps the raw range 30..180 to 0..1.
                        expected=max(0,min(1,(value*255-30)/150))
                        result['expected_sdk_trigger']=expected
                        if abs(actual-expected)>.02:raise AssertionError('Actual analog trigger differs from SDK normalization')
                    button='L' if axis==4 else 'R'
                    clicked=inject(dict(port=0,axis=axis,value=1,buttons=[button]),f'trigger-{button.lower()}-digital-detent',image=True)
                    difference=face_difference(output/result['native_png'],output/clicked['native_png'],button)
                    clicked['digital_detent_visual_difference']=difference
                    if difference['changed_pixels']<8:raise AssertionError('Full analog and digital trigger detent look identical: '+button)
                    inject(dict(port=0),f'trigger-{axis-4}-neutral')
                expected=[True,False,False,False]
                def plug(port,connected):
                    print(f'Real SI hotplug: port{port+1} -> {connected}',flush=True)
                    gui=hotplug.set_port(port,connected);expected[port]=connected
                    deadline=time.monotonic()+45
                    while time.monotonic()<deadline:
                        memory.wait_frames(3,timeout=45)
                        with memory.paused():snapshot=native_snapshot(memory,points)
                        if [p['connected'] for p in snapshot['pads']]==expected:break
                    else:raise AssertionError('Dolphin SI hotplug did not reach actual libogc PAD snapshots')
                    # Let retained spring/shading animations settle after the
                    # real PAD topology changed, then inspect neutral pixels.
                    memory.wait_frames(12)
                    event=runtime_capture(memory,points,output,f'port-{port+1}-'+('connect' if connected else 'disconnect'))
                    gui['guest_state_verified']=True;event['hotplug']=gui;report['events'].append(event)
                    if not any(expected) and event['snapshot']['session']['selected_port']!=-1:
                        raise AssertionError('Disconnected scene retained a selected controller')
                    return event
                for port in range(1,4):plug(port,True)
                for port in range(4):
                    inject(dict(port=port,buttons=['A']),f'port-{port+1}-focus',image=True)
                    inject(dict(port=port),f'port-{port+1}-release')
                    plug(port,False);plug(port,True)
                    inject(dict(port=port,buttons=['B']),f'port-{port+1}-reconnect-input',image=True)
                    inject(dict(port=port),f'port-{port+1}-reconnect-release')
                for port in range(4):plug(port,False)
                plug(3,True)
                inject(dict(port=3,buttons=['A']), 'port-4-only-input',image=True)
                unplugged=plug(3,False)
                if unplugged['snapshot']['session']['buttons']!='NONE':raise AssertionError('Unplug retained stale button labels')
                for button in FACE_ROIS:
                    difference=face_difference(output/'neutral-xfb.png',output/unplugged['native_png'],button)
                    unplugged.setdefault('neutral_cap_differences',{})[button]=difference
                    if difference['mean_absolute_channel_difference']>6:
                        raise AssertionError('Unplug left stale active controller pixels: '+button)
                # Release through the real pipe before reconnecting: held-on-
                # connect input must not invent a new action or arm an exit.
                for port,fd in enumerate(fds):os.write(fd,commands(dict(port=3),port).encode())
                plug(3,True)
                inject(dict(port=3,buttons=['START','Z']), 'real-exit-chord',image=True)
                deadline=time.monotonic()+60
                while time.monotonic()<deadline:
                    lines=memory.read_bytes(points['lines'],790)
                    console=[lines[at:at+79].split(b'\0',1)[0].decode(errors='replace') for at in range(0,790,79)]
                    if any('MANAGED MAIN: FAILED' in line for line in console):raise AssertionError('Real exit chord failed during managed teardown')
                    if any('MANAGED MAIN: OK' in line for line in console):
                        report['real_exit_chord']=dict(success=True,console=console,
                            proof='Captured real port4 START+Z, then SDK managed-main success after normal disposal');break
                    time.sleep(.08)
                else:raise AssertionError('Real START + Z hold did not request sample exit')
                report['hotplug_history']=hotplug.history
            report['success']=True
    except BaseException as error:
        report['error']=str(error)
        raise
    finally:
        if memory:memory.close()
        stop(process)
        for fd in fds:os.close(fd)
        if report['success'] and not args.initial_only:
            try:report['video']=make_video(user,output,50 if report['capture_vi_tv_mode']>>2 in (1,4) else 60)
            except BaseException as error:
                report['success']=False;report['error']=str(error)
                (output/'capture.json').write_text(json.dumps(report,indent=2)+'\n');raise
        (output/'capture.json').write_text(json.dumps(report,indent=2)+'\n')
    print(output/'neutral-xfb.png',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dol',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--sdk',type=Path,help='Optional checkout containing capture-only SDK Python helpers')
    parser.add_argument('--initial-only',action='store_true',help='Capture visual review only; not full input acceptance')
    main(parser.parse_args())
