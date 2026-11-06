# VideoMode: IPL-style native shell

A native, controller-first system-menu demonstration using the published
DolphinDotNet SDK. The visual target is the [committed optical-glass mockup](Design/optical-glass-target.png),
inspired by the Nintendo GameCube IPL: a dark translucent cube floating on black,
layered optical walls, face menus and a bottom controller legend. Flat configuration
screens use green block-built values and a fading grid.

The active value rocks, tilts and floats while its editor is open. Left/right
moves that motion between independent time/date parts; inactive parts and labels
stay still. Motion eases in over 180 ms, stops when confirming or backing out,
and respects Options → Motion → Reduced. Wide values use less roll to keep clear
of the arrows. [Native selector-motion review](Design/selector-motion/README.md).

The rounded cube is projected 3D geometry, not a screenshot or rotating image.

Glass uses a rounded-box surface with a visible curved edge profile and
a sampled, view-dependent reflection layer. Front labels sit inside the
rounded rim. Each face has its own subdued beveled-block icon. List rows have
rounded frosted backplates, fixed left/right text anchors and 180 ms focus
scaling (1.00 to 1.30), including shrink-back on the outgoing row.

The native frost is a real backdrop operation: after the cube and icon are
drawn, GX captures the EFB at half resolution. A dense 9×9 Gaussian-weighted kernel samples that
snapshot inside projected rounded row meshes, then tint and crisp
text are drawn on top. Texture allocation and geometry uploads happen between
frames. The host SVG proof approximates blur with an SVG filter and omits native reflection
textures; only a native capture verifies the GX result. The 81 frost draws reuse
one native rounded mesh and snapshot, with precomputed texture offsets and weights.
Glass skips invisible faces and zero-alpha triangles; invariant wall math is cached.
Physical hardware performance has not been measured in this cloud session.

[Six-pass native review and mockup comparisons](Design/six-pass-review.md).
[Rounded-corner correction, front and angle captures](Design/corner-review.md).

## Controller flow

- At the cube: right opens Video, left Options, up Game Play, down Calendar.
- On a face: pushing the stick opposite the direction used to enter returns to
  the cube. Recenter before entering another face. The D-pad keeps both directions
  available for row selection, including Calendar's Time and Date rows.
- On a face: up/down selects a row; A opens its flat configuration screen.
- In a configuration screen: up/down changes the draft value; A confirms;
  B discards and returns to the same face and row.
- In Time/Date: left/right selects an independent segment; up/down changes
  only that segment. Day counts follow the selected month and leap year.
- B from a face returns to the cube. Memory Card returns to Options first.
- Both the analog stick and D-pad work, with delayed directional repeat.
- X requests 640x480 interlaced, 4:3. Start exits after safely reverting a preview.
- Z toggles a live performance overlay (off at startup). Y still records the
  bounded frame-time sample independently.
- Options > Motion disables idle floating and turning overshoot.

Video settings use real `GraphicsDevice.ConfigureDisplay` calls between frames.
A starts a 12-second hardware preview; A again keeps it, B or the timeout reverts
it. Unsupported modes/cables and allocation failures leave the current mode
active. Rollback allocation failures remain pending and retry once per second.
Widescreen-only changes preserve timing and buffers. The UI uses display-correct
logical coordinates and centers its 4:3 composition inside a widescreen canvas.

To switch **480i / 480p** or **576i / 576p**, open Video with right, choose the
desired **Output** resolution, then select **Scan**, press A,
and use up/down to choose **Interlaced** or **Progressive**. A starts the preview;
A again keeps it, or B reverts. Progressive requires a compatible cable/display.
In Dolphin, enable its progressive/component-cable setting (`SYSCONF.IPL.PGS=True`);
without that capability the native API rejects progressive and preserves the
current mode. X always requests 480i, 4:3.

The SDK's corrected VIDEO library fixes the 576p blanking overflow that previously
froze switching. The app no longer blocks 576p or forces 576 Auto to interlaced;
Auto follows the SDK's normal resolution/cable rules. A native rebuild is required
to include the fix in the DOL. [Native scan-mode evidence, package provenance and
limitations](Design/scan-mode-review.md) cover both resolution sequences and retain
the earlier failure for comparison. Physical console/cable/display testing remains open.

Confirmed video settings, sound and Reduced Motion are stored in alternating
32-byte checksummed records on GC Loader SD (`videomode/settings-a.bin` and
`settings-b.bin`). Startup loads the newest valid record and applies its video
mode, with a boot-output fallback on unsupported modes. Hold X during startup to
bypass saved video. Preview drafts and reverted modes are never saved. A read-only
or missing card is reported without changing another storage device.

Source builds with `IplConsoleServices=true` use the SDK console-services APIs to
read the real local console calendar and write IPL SRAM clock bias and sound.
A confirms Time/Date; B discards. Time edits preserve the current date/seconds;
date edits preserve the current time. The clock refreshes once a second and uses
an independent monotonic animation timer. Builds using the older published SDK
show these services as unavailable. [Native validation and limits](Design/console-settings/README.md).

Game Play now opens a paged GC Loader game library with native banner artwork
and Swiss video-mode launch integration. [Library design, validation and limits](Design/game-library/README.md). Memory Card remains the next storage integration. The sample
does not include Nintendo firmware, fonts, logos or game-save artwork.

## Build and validation

```sh
dotnet build VideoMode/VideoMode.sln -c Release
dotnet run --project tests/VideoModeMenuTests -c Release
```

The repository's native CI also AOT-compiles and links this sample into DOL/ELF
using the compiler and native support in the SDK version pinned in `global.json`.
Hardware/TV acceptance and visual comparison inside Dolphin remain separate
from successful compilation; do not treat host visual proofs as emulator captures.

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

Native builds install the bundled **IplEdgeAA** postprocessing shader in the
isolated Dolphin play/debug profiles. It smooths staircase edges while preserving
bright text/highlight cores, at the full native render size. The selected profile
uses `MSAA = 1`, `SSAA = False`; this is postprocessing AA, not multisampling.
Measured median cloud throughput cost is about 7%, compared with about 30% for 4× MSAA.
[Native comparisons, raw repeated timings and limitations](Design/aa-performance-review.md).

Other graphics and controller preferences are preserved. Build with
`-p:IplEmulatorAntialiasing=false` to stop enforcing these keys and manage AA in
Dolphin yourself; this does not erase settings already written to the profile.
For a stronger GPU, `-p:IplEmulatorSamples=4 -p:IplEmulatorPostProcessing=None`
selects 4× MSAA. `-p:IplEmulatorSupersampling=true` additionally requests SSAA;
only choose sample counts supported by the renderer. Unsupported counts can
silently disable AA in Dolphin. Set postprocessing to `None` to disable the
bundled edge filter, or use another installed shader's name.

The SDK's real GX framebuffer AA is implemented for explicit review/benchmark
builds with `-p:IplHardwareAntialiasing=true`. It applies to 240/480 modes and is automatically
disabled for 576 output, which the SDK does not support with hardware AA. At 480,
it uses a 242-line EFB and RGB565/Z16, so text is softer. The live blur capture
crops two EFB lines to retain GX alignment and compensates its UVs. A boot-time
AA allocation failure keeps the boot mode and disables hardware AA for the session.

Hardware AA is blocked in normal interactive builds: cloud Dolphin 2503 produced
a valid raw EFB dump but a magenta displayed window, and an AA-enabled transition
from 480p to 576p stalled. That candidate was rejected despite its faster frame counter.
Explicit `IplVisualReview` or `IplVideoModeReview` builds retain the diagnostic
for reproduction. Physical console/cable/display
performance remains unmeasured. Display preview/revert and safe mode are retained.
