# VideoMode: IPL-style native shell

A native, controller-first system-menu demonstration using the published
DolphinDotNet SDK. The visual target is the [committed optical-glass mockup](Design/optical-glass-target.png),
inspired by the Nintendo GameCube IPL: a dark translucent cube floating on black,
layered optical walls, face menus and a bottom controller legend. Flat configuration
screens use green block-built values and a fading grid.

The active value rocks, tilts and floats while its editor is open. Left/right
moves that motion between independent Video or time/date parts; inactive parts
and labels stay still. Motion eases in over 180 ms and pauses during a video
preview. It stops when leaving the value editor and respects Options → Motion →
Disabled. Wide values use less roll to keep clear
of the arrows. [Native selector-motion review](Design/selector-motion/README.md).

The rounded cube is projected 3D geometry, not a screenshot or rotating image.

Glass uses a rounded-box surface with a visible curved edge profile and
a sampled, view-dependent reflection layer. Front labels sit inside the
rounded rim. Each face has its own subdued beveled-block icon. List rows have
rounded frosted backplates, fixed left/right text anchors and 180 ms focus
scaling (1.00 to 1.30), including shrink-back on the outgoing row.

Options > Motion offers Disabled, Subtle and Pronounced. Subtle is the default
and preserves the original cube float. Pronounced adds roughly three times the
angular sway and vertical float with a smooth secondary wiggle. Disabled keeps
the cube stationary and retains the reduced-motion behavior for UI animation.
Open the setting with A, change its draft with Up/Down, then A saves or B discards.

Options > UI Sounds offers Off, Low, Medium (default) and Full, independently
of the existing Sound > Stereo/Mono console preference. Volume changes preview
while editing; A commits them and B restores the previous level. Navigation,
menus, value changes, confirmations and cancellations use the console's original
IPL effects through published SDK `0.1.0-preview.349.1`. Up to six effects can
overlap; there is no startup jingle. See the [action index and audio limits](Design/ui-sounds/action-index.md).

The native frost is a real backdrop operation: after the cube and icon are
drawn, GX captures the EFB at half resolution. A dense 9×9 Gaussian-weighted kernel samples that
snapshot inside projected rounded row meshes, then tint and crisp
text are drawn on top. Texture allocation and geometry uploads happen between
frames. The host SVG proof approximates blur with an SVG filter and omits native reflection
textures; only a native capture verifies the GX result. The 81 frost draws reuse
one native rounded mesh and snapshot, with precomputed texture offsets and weights.
Glass skips invisible faces and zero-alpha triangles; invariant wall math is cached.
Physical hardware performance has not been measured in this cloud session.

Options > Glass Blur controls the cube's inner glass: Off (default), Low,
Medium or High. Open Options with A, select Glass Blur, then use Up/Down and
A to confirm; B discards the draft and returns to the cube. The effect filters
the scene through the user-facing inner boundary before the centered 3D icon,
glass reflections and UI are composited. Icons keep their original projected
3D position and stay crisp between the frosted UI rows. Exterior highlights and
labels stay sharp. A narrow environment-lit highlight follows the existing
rounded cavity at the blur boundary, accompanied by a narrow dark absorption
band just inside the bright return. Both follow the existing glass geometry.
It uses a separable nine-tap Gaussian kernel fused into five bilinear samples
in each axis, a retained
half-resolution intermediate and the existing indexed cavity mesh. Off skips
all extra blur draws and copies. The intermediate is allocated between frames
on first use and retained until shutdown (about 361 KiB native memory).

[Six-pass native review and mockup comparisons](Design/six-pass-review.md).
[Rounded-corner correction, front and angle captures](Design/corner-review.md).

## Controller flow

- At the cube: right opens Video, left Options, up Storage, down Calendar.
- On a face: pushing the stick opposite the direction used to enter returns to
  the cube. Recenter before entering another face.
- On a face: A opens its flat configuration screen. Video opens three block-style
  fields together; Options and Calendar open a list with up/down row selection.
- In a value editor: up/down changes the draft value; A confirms or previews;
  B discards and returns directly to the same cube side.
- In Video or Time/Date: left/right selects an independent segment; up/down changes
  only that segment. Day counts follow the selected month and leap year.
- B from a cube side returns to the front cube menu.
- Both the analog stick and D-pad work, with delayed directional repeat.
- X requests 640x480 interlaced, 4:3. Start exits after safely reverting a preview.
- In memory-card/SD browsers, Z (or Y) switches between a 105-icon grid and a nine-row
  list; C-stick Up refreshes. Memory cards show game, blocks and KB. SD shows
  filenames and file sizes. See [compact storage views](Design/dense-storage/README.md).
- Outside memory-card/SD browsers, Z toggles a live performance overlay (off at startup). Outside browsers, Y records the
  bounded frame-time sample independently.
- Storage opens Memory Card and SD Storage; R is a shortcut to the same top side.
- Options contains Sound, Motion, Glass Blur and UI Sounds.

Video settings use real `GraphicsDevice.ConfigureDisplay` calls between frames.
A starts a 12-second hardware preview; A again keeps it, B or the timeout reverts
it. Unsupported modes/cables and allocation failures leave the current mode
active. Rollback allocation failures remain pending and retry once per second.
Widescreen-only changes preserve timing and buffers. The UI uses display-correct
logical coordinates and centers its 4:3 composition inside a widescreen canvas.

To switch **480i / 480p** or **576i / 576p**, turn right to Video and press A.
Left/right selects **Output**, **Scan** or **Aspect**; up/down changes only that
field. Scan shows **I**, **P** or **AUTO**, with its full name below. A previews
all drafted fields together; A again keeps them and returns to the same editor,
or B reverts and returns to the cube. Video, Calendar and Options value editors
share the same green block values, arrows, labels and fading grid.
Progressive requires a compatible cable/display.
In Dolphin, enable its progressive/component-cable setting (`SYSCONF.IPL.PGS=True`);
without that capability the native API rejects progressive and preserves the
current mode. X always requests 480i, 4:3.

The SDK's corrected VIDEO library fixes the 576p blanking overflow that previously
froze switching. The app no longer blocks 576p or forces 576 Auto to interlaced;
Auto follows the SDK's normal resolution/cable rules. A native rebuild is required
to include the fix in the DOL. [Native scan-mode evidence, package provenance and
limitations](Design/scan-mode-review.md) cover both resolution sequences and retain
the earlier failure for comparison. Physical console/cable/display testing remains open.

Confirmed video settings, sound, Motion, Glass Blur and UI Sounds are stored in alternating
32-byte checksummed records on GC Loader SD (`videomode/settings-a.bin` and
`settings-b.bin`). Startup loads the newest valid record and applies its video
mode, with a boot-output fallback on unsupported modes. Hold X during startup to
bypass saved video. Preview drafts and reverted modes are never saved. A read-only
or missing card is reported without changing another storage device. Version-one
through version-three settings remain readable: Full maps to Subtle and Reduced
maps to Disabled; earlier settings adopt Medium UI sound volume. Version one
also loads Glass Blur as Off. Confirmed writes use version four. Motion, blur
and UI sound volume drafts are not saved.

Source builds with `IplConsoleServices=true` use the SDK console-services APIs to
read the real local console calendar and write IPL SRAM clock bias and sound.
A confirms Time/Date; B discards. Time edits preserve the current date/seconds;
date edits preserve the current time. The clock refreshes once a second and uses
an independent monotonic animation timer. Builds without console services show
them as unavailable. [Native validation and limits](Design/console-settings/README.md).

Storage replaces the former top Game Play entry. The earlier GC Loader library
and Swiss launch integration remains in the source as an internal path;
[its design and evidence](Design/game-library/README.md) describe the earlier UI.
The sample does not include Nintendo firmware, fonts, logos or game-save artwork.

## Build and validation

```sh
dotnet build VideoMode/VideoMode.sln -c Release
dotnet run --project tests/VideoModeMenuTests -c Release
```

The repository's native CI also AOT-compiles and links this sample into DOL/ELF
using the compiler and native support in the SDK version pinned in `global.json`.
Hardware/TV acceptance and visual comparison inside Dolphin remain separate
from successful compilation; do not treat host visual proofs as emulator captures.

Normal builds enable system menu audio. Preparation reads the console IPL once
in the background and retains the resulting native clips; it does not read
firmware or synthesize effects on each input. Without a supported IPL the demo
remains usable and the UI Sounds editor reports audio unavailable. In Dolphin,
an original IPL must already be configured for successful playback. No ROM or
prepared PCM is bundled. `-p:IplSystemAudio=false` excludes this backend;
deterministic visual/performance and hardware-runner builds default to that
setting to keep background preparation outside their measurement windows.

To inspect deterministic visual proofs from the exact shared C# scene renderer:

```sh
dotnet run --project tests/VideoModeMenuTests -c Release -- --proof /path/to/proofs
```

Open the generated `index.html`. It exports the cube, Video face, flat aspect
selector and independent date segments. SVG is only the host inspection adapter;
the application uses native Draw2D triangles and a bundled glyph atlas.

Typography is an original atlas generated from Nunito weight 800, licensed under
SIL OFL in `Assets/OFL-Nunito.txt`. It is a close substitute, not the IPL font.
A pixel-identical reproduction and complete platform input/hardware acceptance
must not be claimed without a captured native run against the reference images.

## Antialiasing and performance

[Indexed optical-rim submission, timings and native checks](Design/indexed-rims-review.md)
include the current frontal/oblique results and 36 scan-mode acceptance phases.

[Latest incremental timings and native comparisons](Design/performance-followup.md)
cover the frontal menu, flat editor and oblique stress pose.

The Z overlay shows mean milliseconds over roughly 250 ms for glass/row geometry,
transmission, optical rims, gloss, icons, backdrop capture, frosted rows, row tint,
text, footer and the flat editor. These are exclusive CPU submission scopes;
buffered GX work can finish later. Preparation and submission totals include
their listed scopes and other frame setup. Finish/present includes the SDK's GX
completion and VSync waits; GX wait alone is not total GPU execution time.
Overlay CPU is measured separately from app submission. Frame and presentation
waits include the overlay's GPU work. Hide it for baseline Y/bridge measurements.
Values use reusable buffers and timing scopes stop collecting while hidden.
[Native Z-button review and captures](Design/performance-overlay.md).
[Overlay cost investigation and cached-label native comparisons](Design/overlay-cost-review.md)
explain the original 50.05 ms VSync step and the restored 33.36 ms visible-overlay frame.
[Optical-wall preparation follow-up](Design/rim-generation-review.md) records three
further increments saving 2.00 ms frontally and 5.72 ms obliquely, with native
images unchanged outside timing values; these gains add headroom within the same
presentation intervals.
[The current outer-rim-only rendering](Design/outer-rim-only-review.md) removes
the middle and inset inner returns entirely while retaining the rounded outer
polish. Its native frontal/angled screenshots and updated timings supersede the
earlier full-wall appearance.
[Cross-layer submission investigation](Design/submission-followup-review.md)
records the cached footer, separate SDK/Hypercube PRs, controlled indexed-color
prototype timings and the remaining CPU hotspots. The stock package pin remains
unchanged; the combined prototype is an explicit integration control.

[Further submission savings](Design/submission-round-3/README.md) cover ordered glyph
batches and reusing the blur mesh for row tint. The source-SDK build helper accepts
`--glyphs --tints` alongside `--icons --glass --blur`; these API-dependent paths
stay opt-in with the older published package pin. VSync remains enabled.

[Text and editor caching](Design/submission-round-4/README.md) retains unchanged
overlay values, combined footer glyphs and the static editor grid. These caches
use the published SDK and are enabled by default; set `IplCachedNumbers=false`,
`IplCachedFooterText=false` or `IplCachedGrid=false` to disable each for comparison.
The source build helper enables them explicitly with
`--numbers --footer-text --grid`. Moving menu text remains live.

For an uninterrupted native benchmark, build with `IplPerformanceReview=true`
and `IplPerformanceReviewScene=Options`, `Oblique`, `Cube` or `Detail`, then run
`ci/profile_videomode.py --scene` with the same scene. The default is Options.
The extractor defaults to native AA: cube scenes use a 242-line EFB with
640×480 VI output; the flat Detail scene keeps 480 EFB lines. It verifies the
actual GX mode and disables Dolphin postprocessing/forced progressive output.
For archived or explicit `IplHardwareAntialiasing=false` builds, also pass
`--hardware-aa off` to retain their historical IplEdgeAA presentation profile.
Each run retains 30 warmup and 96 measured frames with the live overlay hidden.
For overlay controls, set `IplPerformanceReviewOverlay=Off`, `Timers` (collect without
drawing), or `Full`, and pass the matching `--overlay` option to the extractor.
Review builds also emit a separate overlay breakdown CSV; the Y/BBA format is unchanged.

For a BBA + GC Loader console, the [Windows hardware bridge](../tools/gamecube_bridge/README.md)
uses a private Microsoft dev tunnel, resident ELF uploads and post-window native
timing callbacks. It keeps a debugger out of the optimized Release build. Its
Windows setup scripts, client and wire-protocol tests are included; physical rig
activation is separate from the validated Dolphin BBA transport.

Press **Y** in the normal app to capture frame times (30 warmup + 96 samples).
Mean and p95 appear on screen; raw samples remain in RAM. For physical SD export,
build with `-p:IplFrameLogDevice=Sd2Sp2` (or `SdSlotA`, `SdSlotB`, `GcLoader`)
for the adapter actually attached. After sampling, that build writes
`videomode-perf/frame-N.csv`. Default `None` avoids absent-device probes. Logging includes geometry, submission, GX
blocking, retrace, capture/blur and actual mode/scene information. It performs no
storage I/O during measurement. [Incremental optimizations, accepted/rejected
experiments, raw native logs and hardware instructions](Design/performance-review.md)
separate Dolphin guest-clock results from the pending physical-console measurements.

Native builds enable GX three-sample antialiasing on the visible cube by
default. Dolphin play/debug profiles use `MSAA = 1`, `SSAA = False` and no
postprocessing filter, so the DOL provides the same AA on console. Build with
`-p:IplHardwareAntialiasing=false` to disable native AA; that fallback installs
the bundled **IplEdgeAA** Dolphin postprocessing shader. The optional shader's
historical emulator-only cost and comparisons are documented separately:
[raw repeated timings and limitations](Design/aa-performance-review.md).

Other graphics and controller preferences are preserved. Build with
`-p:IplEmulatorAntialiasing=false` to stop enforcing these keys and manage AA in
Dolphin yourself; this does not erase settings already written to the profile.
For a stronger GPU, `-p:IplEmulatorSamples=4 -p:IplEmulatorPostProcessing=None`
selects 4× MSAA. `-p:IplEmulatorSupersampling=true` additionally requests SSAA;
only choose sample counts supported by the renderer. Unsupported counts can
silently disable AA in Dolphin. Set postprocessing to `None` to disable the
bundled edge filter, or use another installed shader's name.

Native GX three-sample AA is available for the visible cube at 240/480 output.
Flat settings and storage pages switch back to the full EFB height so their
text stays sharp. At 480, AA uses a 242-line EFB and RGB565/Z16; attached cube
labels and the cube footer consequently remain softer. AA is disabled for
576 output, which the SDK does not support with hardware AA. A failed quality
allocation preserves the current output and disables further automatic quality
requests for the session. Preview, rollback and safe mode retain their guards.

The three glass environment maps now have box-filtered 256-to-1 mip chains and
trilinear minification. Resolved highlights keep the original base image;
subpixel reflections use filtered levels. This adds 262,464 native bytes and
320 bytes of startup scratch without adding draws or per-frame mip generation.
Fresnel, cavity-boundary and refraction lookup textures retain their filters.

The pinned published SDK `0.1.0-preview.349.1` includes
[SDK PR #182](https://github.com/capz/DolphinDotNet/pull/182), which sizes both
initial and replacement framebuffers for the quantized GX display-copy extent.
Its GPU canary verifies the actual copy bounds; SDK 347.1 underallocated
480-line AA copies by 3,840 bytes with the pinned toolchain. Source SDK builds
can opt in with `ci/build_videomode_source_sdk.py --hardware-aa`; the helper
checks that this copy-bounds fix is present.

Native-AA Dolphin profiles disable the additional presentation filter by
default. [Native captures, mode checks and timing limits](Design/native-cube-aa/README.md)
include both original XFB output and complete emulator windows, with the
conditions and incomplete attempts recorded alongside them. Physical-console
visual quality and timing still require hardware qualification.
