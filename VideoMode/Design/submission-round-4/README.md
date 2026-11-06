# Text and flat-editor cache follow-up

The previous submission pass rebuilt every displayed timing value and footer
glyph on each frame, along with the editor's unchanging fading grid. This follow-up retains those buffers using the current
published SDK API. The three caches are enabled by default, independently
controllable with `IplCachedNumbers`, `IplCachedFooterText`, and `IplCachedGrid`.
The package remains `0.1.0-preview.217.1`.

## Measurement

Each result is a real GameCube DOL run in cloud Dolphin 2503 / Mesa llvmpipe,
with native VSync enabled, 30 warmup frames and 96 uninterrupted logged frames.
No screenshot, debugger interruption, frame dumping or file write occurs during
the measurement window. These are comparative guest-clock measurements, **not
physical GameCube timings**. Screenshots are captured after pausing the guest.
`active_us` is logged frame time minus logged retrace wait; subtracting the wait
for analysis does not change pacing. Completion waiting still counts as work.
Every directory includes raw frame/overlay CSVs, result JSON and the DOL hash.

The existing source integration (cached icons, shared glass, ordered 81-pass blur,
packed glyphs and retained row tint) is the control for these increments.

| Native increment | Prepare ms | App submit ms | Overlay draw ms | Active mean ms | Active p95 ms | Frame ms |
|---|---:|---:|---:|---:|---:|---:|
| Previous optimized front | 7.091 | 1.253 | 0.913 | 9.318 | 9.328 | 16.681 |
| Cache overlay values | 7.133 | 1.252 | 0.137 | 8.571 | 9.183 | 16.681 |
| Combine footer text | 7.143 | 1.121 | 0.137 | 8.557 | 9.176 | 16.681 |
| Editor control | 0.053 | 11.016 | 0.137 | 11.255 | 11.873 | 16.681 |
| Cache editor grid | 0.046 | 2.481 | 0.137 | 2.713 | 2.981 | 16.681 |
| All caches, oblique | 16.813 | 1.509 | 0.138 | 18.611 | 19.218 | 33.364 |

Overlay caching cuts draw time from 0.913 to 0.137 ms. Average preparation rises
by 0.042 ms; total active work falls by 0.748 ms. Dirty rebuild preparation can
reach about 0.69 ms. The unchanged-precision comparison prevents unnecessary
rebuilds for sub-digit noise.

Combining footer text saves another 0.132 ms of app submission but raises GPU
completion waiting from 0.009 to 0.115 ms. Its total active saving is only
0.014 ms. This is a small gain, not a claim of a large frame-rate improvement.

The grid saves 8.535 ms of app submission and 8.542 ms of total active work
(about 76%). Its draw count **increases** from 16 to 37; avoiding repeated managed
geometry work matters more here than minimizing the count. Both editor builds
draw 7,094 triangles. Frontal triangle count stays 15,486 and the oblique count
stays 20,614. No blur taps or rendered detail are removed.

The oblique control from the preceding pass used 19.380 ms active work; it now
uses 18.611 ms. Geometry preparation still takes 16.813 ms and actual
presentation remains 33.364 ms. The text changes do not make this a 60 Hz pose.
The flat editor and frontal pose still present at 16.681 ms with VSync enabled.

The accepted frontal, footer, grid and oblique screenshots are pixel-identical
to their controls outside the timing overlay's inclusive rectangle
`(40,12)-(282,453)`. Overlay-covered scene pixels are not covered by that claim.
[Pixel comparison hashes/results](comparisons.json) retain that boundary. These
checks establish visual preservation, not a perfect match to the mockup.

| Native cache snapshot | Arena used / 262,144 bytes | Collections at snapshot |
|---|---:|---:|
| Combine footer text | 147,616 | 0 |
| Editor control | 153,640 | 0 |
| Cache editor grid | 156,456 | 0 |
| All caches, oblique | 157,736 | 0 |

Native screenshots: [control](00-control.png), [number cache](01-numbers.png),
[footer](03-footer.png), [editor control](04-detail-control.png),
[cached grid](05-grid.png), [oblique](06-angle.png).

## Cache behavior and budget

Timing values compare the actual printed hundredths of a millisecond. Only a
changed color group is formatted and uploaded; unchanged frames replay native
indexed meshes. Labels still use the previous immutable cache. `numbers_us` now
includes dirty preparation **and** drawing; dirty preparation is also included
in total frame preparation. `overlay_us` measures drawing alone, so it must not
be presented as the whole saving. Formatting is included in dirty preparation
rather than the old `format_us` counter. Periodic rebuild spikes remain visible
in the raw CSVs and p95 values.

Footer glyphs share a single uniform-color indexed mesh. Text, positions, font
size, stretch, angle and tint invalidate it. Logical output scale changes its
transform. Live menu text stays on the packed-quad path; its motion and smooth
selection growth stay interactive. Editor footer text keeps the live path.

The editor grid is built once, lazily between frames, reusing the same small
staging grid as glass and footer geometry. It keeps every nontransparent cross,
its fade, triangle order and vertex colors. It replays bounded native meshes;
values and arrows still render live. Its initial build cost is outside the
steady-state sampling window. No large managed vertex buffer is added.

The profiler also records an optional native heap snapshot after the measured
window. Arena-used bytes include reusable blocks and are **not** live memory,
peak usage or evidence that a long interactive session never collects garbage.
The managed heap remains 262,144 bytes.

## Rejected experiment

A native mesh/transform for each moving text run lowered app submission to
0.762 ms, but raised completion waiting to 0.490 ms and preparation to 7.216 ms.
Total active work rose from 8.571 to 8.646 ms. This implementation was discarded.
Its [native screenshot](02-rejected-per-run-text.png), CSVs and result are kept
for review. It changed six uncovered text-edge pixels by at most 1/255.
The first attempt also exposed a missing renderer batch-capacity reservation;
that failed run was diagnosed and never counted as a performance result.

## Reproduction

```sh
python ci/build_videomode_source_sdk.py --sdk <sdk-checkout> \
  --hypercube <engine-checkout> --output <out> \
  --icons --glass --blur --glyphs --tints --numbers --footer-text --grid
```

The explicit helper switches override application defaults to allow controlled
comparisons. Omit `--footer-text` for the numbers-only comparison; use
`--scene Detail` with and without `--grid` for the grid increment. Use
`--scene Oblique` for the angled review and `--normal` for the interactive app.
The SDK/engine API-dependent integration paths still need the existing separate
source branches (SDK `56d75f11` / Hypercube `1fa7b620`); this follow-up adds no
SDK or engine API or package dependency.
All new mesh creation/disposal and uploads happen before `BeginFrame`.
The Y/BBA CSV schema and video preview/revert state machine remain unchanged.

## Native controls and validation

The normal all-feature source DOL was replayed through Dolphin's real controller
path, independently from the fixed timing builds. [Z held/toggle](interactive/02-z-held-visible.png),
[A opens the editor](interactive/04-editor-visible.png),
[B returns](interactive/06-reopened-options.png),
[16:9 kept with Z hidden](interactive/08-widescreen-hidden.png), and
[X restores 480 interlaced / 4:3](interactive/09-safe-mode.png) are preserved.
These full 640x480 window captures retain the anamorphic 16:9 output without
resizing it. Cached text survives the output-scale changes and menu transitions.

Calendar captures cover [two rows](calendar/01-calendar-two-rows.png),
[independent hour/minute edits to 13:01](calendar/02-time-parts.png),
[month edited to 02](calendar/03-date-parts.png), and
[B cancels both edits](calendar/04-calendar-cancelled.png).
The Calendar images are completed native 640x528 EFB PNGs copied byte-for-byte;
the source filenames and hashes remain in events.json. Input replay waits for
rendered frames and accepts only complete PNGs. The normal DOL hash is retained.

Local validation: all migrated sample regression programs (the two native-input
smoke projects compile as required by CI), including menu/scan round trips and
cache-precision tests; published-package managed VideoMode build; all-feature
source-SDK review and normal native builds; sample/run/test manifest validators;
Python helper syntax and `git diff --check`.

The normal native build against published SDK `0.1.0-preview.217.1` also passes
and boots in cloud Dolphin with the three cache defaults enabled. Its
[native capture](08-published.png) and [DOL hash/build metadata](08-published.json)
are retained. This boot capture is not a timing comparison with the source-SDK
integration table above.

SDK #155 and Hypercube #9 retain their existing passing CI heads and separate
PRs. No PR is merged. Samples CI runs on the new commit independently.


