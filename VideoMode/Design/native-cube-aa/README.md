# Cube antialiasing with full-resolution flat UI

The DOL requests native framebuffer AA while the 3D cube is visible at 240/480
output. Flat menus, editors and storage browsers use the ordinary full-height
EFB. The SDK does not support this AA mode at 576 output, so those modes stay
off. At 480, cube AA uses a 242-line RGB565/Z16 EFB with unchanged 640×480 VI
output; text attached to the cube still shares that reduced resolution. The
recorded source requests AA by default on eligible cube pages, with an explicit
build-property opt-out.

**Dolphin 2503 at MSAA=1 does not emulate GameCube's native three-sample
coverage.** Its [FramebufferManager](https://github.com/dolphin-emu/dolphin/blob/2503/Source/Core/VideoCommon/FramebufferManager.cpp#L117-L129)
uses the user-selected multisample count ([allocation](https://github.com/dolphin-emu/dolphin/blob/2503/Source/Core/VideoCommon/FramebufferManager.cpp#L153-L163));
[BP sample-location writes](https://github.com/dolphin-emu/dolphin/blob/2503/Source/Core/VideoCommon/BPStructs.cpp#L460-L470)
are stored without applying the GC pattern. Its [copy-filter implementation](https://github.com/dolphin-emu/dolphin/blob/2503/Source/Core/VideoCommon/TextureCacheBase.cpp#L2133-L2142)
also collapses seven coefficients into three rows. These screenshots validate
GX configuration, 242-line/copy-filter output, page switches and mipmapped
appearance in this emulator. They do not demonstrate actual GC sample
coverage, its edge-quality benefit, or physical hardware performance.


The three environment-reflection banks retain nine mip levels with trilinear
minification and explicit LOD 0–8. LOD0 remains the authored image. Each chain
occupies 349632 native bytes: 262464 additional bytes across the three banks.
Startup generation uses 256-byte source scratch and the existing 64-byte tile;
it adds no per-frame mip generation, upload or draw. This enables filtered
reflection minification. These combined changes do not isolate its visual or
performance contribution.

## Native verification

All thirteen native XFB PNGs are completed normal GameCube DOL output from
cloud-local Dolphin 2503 / OpenGL / Mesa / Xvfb. Each frame is paused after
GX_CopyDisp/GX_DrawDone before VIDEO_WaitVSync. Mode bytes 24/25 record field
rendering and AA. XFBs use BT.601 YUYV422→RGB conversion only. Matching
`*-window.png` files preserve the complete 640×480 X11 client losslessly.
Neither series is cropped, resized, composed or generated. The presented
window additionally reflects Dolphin's VI aspect and deinterlacing.

Emulator MSAA=1, SSAA=false, no postshader and ForceProgressive=false keep
configuration readback explicit; the coverage-emulation limit above remains. This run uses DSP LLE/JIT with Dolphin's bundled free DSP ROM,
DSPThread=false, NoAudioOutput and requested LP_NUM_THREADS=2. HLE does not
recognize this AESND microcode and forces AX; the earlier incomplete screenshot
attempt is excluded. No original IPL/audio playback is qualified here.

| Capture | XFB pixels | EFB rows | GX AA | VI scan bits |
| --- | ---: | ---: | --- | ---: |
| [01-options-cube-blur-off](native/01-options-cube-blur-off-xfb.png) | 640×480 | 242 | On | 0 |
| [02-options-flat](native/02-options-flat-xfb.png) | 640×480 | 480 | Off | 0 |
| [03-options-cube-blur-high](native/03-options-cube-blur-high-xfb.png) | 640×480 | 242 | On | 0 |
| [04-rotating-corner-high](native/04-rotating-corner-high-xfb.png) | 640×480 | 242 | On | 0 |
| [05-video-480i-high](native/05-video-480i-high-xfb.png) | 640×480 | 242 | On | 0 |
| [06-video-480p-high](native/06-video-480p-high-xfb.png) | 640×480 | 242 | On | 2 |
| [07-576p-preview-high](native/07-576p-preview-high-xfb.png) | 640×576 | 480 | Off | 2 |
| [08-rollback-480p-high](native/08-rollback-480p-high-xfb.png) | 640×480 | 242 | On | 2 |
| [09-video-576p-high](native/09-video-576p-high-xfb.png) | 640×576 | 480 | Off | 2 |
| [10-video-576i-high](native/10-video-576i-high-xfb.png) | 640×576 | 480 | Off | 0 |
| [11-video-240p-high](native/11-video-240p-high-xfb.png) | 320×240 | 240 | On | 1 |
| [12-safe-480i-high](native/12-safe-480i-high-xfb.png) | 640×480 | 242 | On | 0 |
| [13-video-detail-crisp](native/13-video-detail-crisp-xfb.png) | 640×480 | 480 | Off | 0 |

The sequence covers blur Off/High, flat Options, a rotating corner, 480i/p,
576p preview/B rollback, kept 576i/p, 240p, safe 480i and the grouped Video
editor. Static front captures disable idle motion; the corner temporarily
restores Subtle and follows the existing icon-hiding transition rule. The
profile restores Subtle. [Events](native/events.json) retain all source/PNG
hashes and actual native frame counters. All mode assertions passed.

These images establish the native mode and page policy, not native GC sample
coverage, an exact mockup match or physical-console appearance. Cube labels/footer can remain softer;
flat-page readback explicitly shows AA off and a full 480-line EFB. Narrow
corner reflections may still show stair-stepping or shimmer.

## Comparison and timing limits

`baseline-partial/` retains 6 completed AA-off buffers and window captures:
[Off](baseline-partial/01-options-cube-blur-off-xfb.png),
[flat Options](baseline-partial/02-options-flat-xfb.png), and
[High blur](baseline-partial/03-options-cube-blur-high-xfb.png), the turning corner and Video
480i/p. Their real frame counters and AA-off/EFB480 readbacks are retained in
[baseline events](baseline-partial/events.json). The run ended before its later
modes and profile; it does not establish a complete baseline matrix or a paired
timing window. The baseline also predates page-aware AA and reflection mips.

The final normal Video cube uses High blur, Subtle motion and Medium UI volume
with no supplied original IPL. One VSync-enabled window contains 30 warmup
frames and 96 uninterrupted samples without capture/debugger stops or app
storage writes. These are Dolphin guest-clock timings, not physical rendering
cost or active mixer qualification. LLE further limits comparison with HLE.

| Frame ms | Active ms | Prepare ms | Submit ms | Draws / triangles |
| ---: | ---: | ---: | ---: | ---: |
| 16.681 | 4.525 | 0.788 | 3.680 | 103 / 45234 |

Active time excludes retrace wait; copy scopes include earlier GPU work. The
post-window arena is 2289296 of 8388608
bytes with 0 collections, neither peak nor live usage.
Original rows are under `profile-high-subtle-medium/`. The complete earlier
AA-off quiet profile in `prior-context/` uses another SDK/copy setup, HLE,
progressive forcing and IplEdgeAA; it is not a paired AA or mipmap-cost test.

## Source build and dependency

This candidate uses a published SDK/runtime/compiler package. DOL SHA-256:
`87a789a2c90deaa4d84af9d5543cf6649e6f6be68130e7e511671ebe0d84a3e2`. Managed API package: `0.1.0-preview.349.1`; fixed native
SDK/compiler/templates: `09eac4b1040ee8824a37d48c686f7e0f745c6c5f`. The Samples base is
`4e7cfecb66186af61194341832128b7eb9431abe` plus [samples-runtime.patch](samples-runtime.patch), including
any additional runtime-source changes. The page-policy/mipmap code and package
pin are already part of the recorded source commit. This does not establish
later project defaults or package changes.

The [SDK fix](https://github.com/capz/DolphinDotNet/pull/182) sizes XFB storage
for the GX library's quantized copy extent. Pinned 480-AA copies write 483 rows
although VI requests 480. The [GPU guard test](https://github.com/capz/DolphinDotNet/blob/cbf25510df832466f785cf3977b8dbc0078beede/docs/framebuffer-copy-bounds.md)
measured 3840 bytes overwritten with legacy allocation and zero outside
protected buffers. Use source/package content containing this fix for AA.

Apply the runtime patch at the recorded Samples base, then compile:

```sh
dotnet build VideoMode/VideoMode.csproj -c Release -p:DndBuildNative=true -p:DndBuildBackend=Docker -p:IplNextSdk=true -p:IplSharedGlass=true -p:IplBatchedBlur=true -p:IplBatchedText=true -p:IplRetainedTint=true -p:IplConsoleServices=true -p:IplMemoryCardServices=true -p:IplHardwareAntialiasing=true -p:IplAaCopyBoundsValidated=true
```

The Cloud build used managed compilation with `DndBuildNative=false`.
The compiler came from the named published SDK package. The resulting DLL was bundled with `--bundle` and
`--sample-controls`, then the unchanged generated Makefile ran with `make -j2`.
Pinned devkitPPC/libogc2 tools ran locally from /tmp to avoid Docker VFS space
exhaustion. Generated C and app state were not patched.
[Provenance](provenance.json) records source/binary hashes, build logs, policy
and the [capture helper patch](capture-tools.patch), recorded with zero
context; apply it with `git apply --unidiff-zero capture-tools.patch`.
Those helper hashes and
patches are a packaging-time snapshot, not an exact hash of the module loaded
at process start. `NativeFrames` loaded around 22:59:09 UTC; an error-only
`failure_snapshot` counter-read guard was added at 23:01:14 UTC while the run
was active. The successful run did not use that guard, and its normal capture
and profiling paths were unchanged.

With matching DOL/ELF files and a configured Linux/Xvfb environment:

```sh
python3 ci/review_performance_overlay.py --dol /path/VideoMode.dol \
  --sdk /path/to/DolphinDotNet --logs /path/to/fresh-review \
  --native-boundaries --native-aa-review --aa-expected on \
  --native-aa-profile --quick-input --profile-seconds 90
```

Raw DOL/ELF/YUV/PPM and logs remain in task artifacts outside git. No firmware,
original audio or recording is bundled with this evidence. The full-frame-AA
prototype is retained outside git as superseded evidence.
