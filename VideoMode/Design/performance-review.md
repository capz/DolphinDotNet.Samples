# Incremental native frame-time review

[Further improvements measured from the Z-overlay build](performance-followup.md)
include native editor and angled-scene comparisons. This page retains the earlier
baseline and checkpoints.

These are **cloud Dolphin 2503 / Mesa llvmpipe guest-clock timings**, not real
GameCube measurements. They identify expensive native application paths and
verify changes incrementally; Dolphin does not model CPU/GPU timing accurately
enough to certify console frame rate. Physical hardware results remain open.

Each run warms up 30 frames, records 96 consecutive native frames in the fixed
640x480i Options scene, then stops at a complete presentation. No debugger
interrupts, frame dumps, screenshots or storage writes occur during sampling.
The 1x Dolphin profile retains IplEdgeAA; hardware framebuffer AA remains off.

## Accepted checkpoints (milliseconds)

| Checkpoint / raw report | Frame median | Frame p95 | Work excluding retrace, median | Prepare median | Submit median |
|---|---:|---:|---:|---:|---:|
| [00-baseline](performance/00-baseline/result.json) | 150.15 | 150.15 | 147.80 | 87.47 | 60.29 |
| [01-visible-faces](performance/01-visible-faces/result.json) | 116.78 | 116.78 | 103.22 | 47.72 | 55.46 |
| [02-shared-blur-mesh](performance/02-shared-blur-mesh/result.json) | 66.73 | 66.73 | 64.93 | 8.85 | 56.03 |
| [03-cached-lighting](performance/03-cached-lighting/result.json) | 66.73 | 66.73 | 60.72 | 9.24 | 51.44 |
| [04-skip-transparent-triangles](performance/04-skip-transparent-triangles/result.json) | 66.73 | 66.73 | 54.01 | 9.24 | 44.73 |
| [05-logger-safety](performance/05-logger-safety/result.json) | 50.05 | 50.05 | 48.93 | 7.53 | 41.36 |

Frame time includes input, geometry, submission, GPU blocking and retrace wait.
`active_us` is the measured total minus `retrace_us`, not a GPU timer. EndFrame
waits for the next retrace: reduced work can increase idle wait without changing
presented frame rate. The default follow-up build takes three approximately 16.68 ms
retrace intervals; this is **not** a 60 fps claim.

1. **Visible faces:** calculate facing once per face, skip faces with identically
   zero contribution, and directly emit visible vertices. This removes two
   managed scratch arrays totaling about 60 KiB. The native capture is identical.
2. **Shared frost mesh:** upload 324 panel vertices once instead of 26,244 per
   frame. All 81 Gaussian samples remain; GX texture matrices supply offsets.
   Precompute weights/offsets and remove 80 native mesh copies. The capture is identical.
3. **Lighting:** precompute invariant wall angles/tints; use a 2052-byte exp(-x)
   lookup with an absolute interpolation bound below 0.000123. Host tests cover
   monotonicity and the bound over 160,001 inputs. Against baseline, 1,120 of
   307,200 pixels change, at most 3/255 in a channel (mean absolute channel error
   below 0.0016/255). Rounded geometry, reflection map and blur density are retained.
4. **Transparent triangles:** skip final alpha-zero triangles before canvas
   staging. Submitted triangles drop from 19,780 to 17,504; capture is identical
   to checkpoint 3.

The four renderer changes reduce total time by 55.6% and work outside retrace
by 63.5%. The logger safety follow-up default build measures 50.05 ms/frame and
48.93 ms outside retrace (66.7% and 66.9% below baseline). Record this build
configuration separately: pruning optional storage code changes generated AOT
code; this additional measured difference is not an isolated renderer change. These percentages apply to this deterministic emulator scene.
Every checkpoint contains the full native PNG, raw per-frame CSV, timing summary
and SHA-256 of its measured DOL. The screenshot files are unscaled 640x480 native
Dolphin client captures, made after sampling. They preserve the existing visual
result; they do not establish a perfect match to the optical-glass mockup.

The final source repeat independently reproduces 50.05 ms/frame and 48.93 ms
outside retrace; [raw repeat](performance/repeats/final/result.json). The baseline
repeat reproduces 150.15 ms/frame. The old baseline's export stalls after sampling,
which is retained explicitly in its diagnostic metadata.

The [oblique stress pose](performance/angle/result.json) is substantially slower:
100.10 ms/frame (p95 100.10), 95.60 ms outside retrace, 20.90 ms preparation and
74.67 ms submission. It exposes three contributing cube faces. It is not directly
comparable to the frontal baseline. Turning still needs work; a frontal-only
benchmark would conceal that limitation. [Full native angle capture](performance/angle/native.png).

## Rejected experiments

- [Opaque primitive batching](performance/rejected-opaque-primitives/result.json):
  66.73 ms/frame, 51.77 ms outside retrace, but the SDK primitive API uses opaque
  blending/depth defaults. The [native capture](performance/rejected-opaque-primitives/native.png)
  exposes broken glass, panel tint and footer. Reverted; no claimed performance win.
- [Alpha rim meshes](performance/rejected-alpha-mesh/result.json): native rendering
  matches checkpoint 3, but per-frame mesh updates raise work outside retrace to
  69.95 ms and total frame time to 83.41 ms. Reverted. Retained timing/capture data
  document why batching was abandoned.

## Logging on a real GameCube

Use the normal interactive build. Press **Y** once, remain in the scene being
measured through 30 warmup plus 96 sampled frames, then read the summary at the
upper left. It shows mean and nearest-rank p95 microseconds. The renderer does
not draw the diagnostic summary while a sample window is active.

By default the summary and bounded raw buffer remain in RAM; no storage is
probed. For physical SD logging, select the adapter that is actually attached:
`-p:IplFrameLogDevice=Sd2Sp2`, `SdSlotB`, `SdSlotA` or `GcLoader`. After sampling,
that build mounts only the selected device and writes a new
`videomode-perf/frame-N.csv`. Existing logs are never overwritten; ordinary
memory-card saves are not used. I/O happens only after the measurement window.
The default is `None`; Y starts another capture in either build.

This explicit selection fixes an observed logging failure: Dolphin 2503 can
wait indefinitely in `EXI_Sync` when probing an absent SD2SP2 adapter. The early
checkpoint DOLs completed their 96 samples before reaching that legacy export
stall, so their timing data remains valid. Final default builds complete without
probing EXI. Select an SD export target only for known attached hardware;
physical SD export has not been exercised in this cloud session.

Export is deferred until any active video preview ends, preserving the keep/back
controls and rollback timer.

CSV columns include unclamped total/prepare/submit/EndFrame microseconds,
blocking GX wait, retrace, capture/blur submission, draw/triangle counts,
BeginFrame copy/gate, actual EFB width/height, scan, face and flat-editor state.
Scan values are 0=Auto, 1=interlaced, 2=progressive; faces are 0=Menu, 1=Video,
2=Calendar, 3=Options, 4=Play, 5=Memory. GPU wait is outstanding GX_DrawDone
blocking time, not total GPU execution time. Capture/blur include their CPU
submission; do not interpret them as isolated hardware GPU timings.

Record console model, loader, SD adapter, cable, scene/motion setting, output
mode and whether navigation happened during capture. Compare both 480i/p and
576i/p, the main cube/turns, a frosted face and a flat editor. Returning baseline
and optimized CSVs from the same console will determine the next optimization;
large GPU waits would motivate a separately measured blur-pass reduction,
whereas large prepare/submit times favor moving more work onto GX.

## Reproducing the cloud measurement

```sh
dotnet build VideoMode/VideoMode.csproj -c Release \
  -p:DndBuildNative=true -p:DndBuildBackend=Docker -p:IplPerformanceReview=true
# Inside dnd-cloud-dolphin, with the repository/binaries mounted and Xvfb:
xvfb-run -a python3 ci/profile_videomode.py \
  --dol VideoMode/bin/Release/net8.0/VideoMode.dol \
  --logs /fresh/log/directory --variant skip-transparent-triangles
```

The performance review build automatically captures the Options pose and holds
the completed buffer for read-only debugger extraction; it is not the normal
interactive DOL. `IplVisualReviewAngle=true` additionally exercises an oblique
pose. The logger itself is available via Y in normal builds. Never mix review
modes or use a review build for normal controller acceptance. For new angled
measurements, use `IplPerformanceReviewScene=Oblique` and pass `--scene Oblique`
to the profiling script so its metadata identifies the pose.

## Validation

All 30 local managed-build/test/manifest commands passed, including the linked
VideoMode interaction and lighting tests. Both native scan reviews passed all
18 stages: [480 sequence](performance/validation/scan480/result.json) and
[576 sequence](performance/validation/scan576/result.json). They cover i/p,
resolution transitions, keep/back, forced logical timeout rollback and safe mode.
The saved native screenshots preserve each stage.

[GitHub run 41930566668](https://github.com/capz/DolphinDotNet.Samples/actions/runs/41930566668)
passed all managed sample builds/tests and the three packaged native AOT groups
for code revision 9198b85. Physical console/cable/SD-device acceptance remains open.
