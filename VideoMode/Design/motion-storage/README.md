# Motion levels, dark inner boundary and top Storage side

Motion now offers **Disabled**, **Subtle** (the default), and **Pronounced**.
The Calendar-style editor uses Up/Down, A to save and B to cancel. Pronounced
adds bounded yaw, pitch and bob to Subtle's original motion. Glass, centered
icon and UI share one pose. Preferences v3 preserves existing v1/v2 values.

With inner blur enabled, the cavity transition has a narrow dark optical band
beside its existing reflected highlight. Both follow the current curved shell
geometry and environment lighting. The combined light/dark boundary uses one
existing conditional TEV stage; adding the dark band to the previous highlight
adds no stage or draw, mesh, texture or managed array.

The top side is now **Storage**. A opens Memory Card / SD Storage; B returns
to its cube side. Reverse stick Down returns to the neutral cube; R remains
a shortcut. Options now has Sound, Motion and Glass Blur.

## Native review

All eleven PNGs are original normal interactive GameCube DOL output from
cloud-local Dolphin 2503, OpenGL/Mesa software rendering and Xvfb. Each source
GX_CopyDisp XFB was read after GX_DrawDone at a completed presentation boundary.
All eleven are native 640x480 interlaced VI output. Conversion is BT.601 YUYV422
to RGB only, with no crop, resize, compositing or generated scene replacement.
All 614400-byte source buffers and decoded PNG hashes were verified.

- Motion pairs: [Disabled A](native/01-disabled-a-xfb.png) /
  [B](native/02-disabled-b-xfb.png), [Subtle A](native/03-subtle-a-xfb.png) /
  [B](native/04-subtle-b-xfb.png), and
  [Pronounced A](native/06-pronounced-a-xfb.png) /
  [B](native/07-pronounced-b-xfb.png).
- [Pronounced value editor](native/05-pronounced-value-detail-xfb.png) shows
  the shared block selectors.
- [Storage cube side](native/08-storage-top-side-xfb.png),
  [flat storage actions](native/09-storage-flat-actions-xfb.png), and
  [reverse-stick return](native/10-storage-down-return-xfb.png) exercise the
  ordinary controller path.
- [Turning corner](native/11-dark-border-corner-xfb.png) is a material probe;
  the existing turn rule hides the icon during this frame.

The replay requests 45 frames between each pair. The table records the actual
completed native frame counters, since the capture helper can overshoot.
Whole-frame RGB pixel changes also include lighting/UI effects and do not
measure maximum motion amplitude. Full-cycle motion bounds are covered by
host tests.

| Motion | First frame | Second frame | Actual delta | Changed pixels / 307200 |
| --- | ---: | ---: | ---: | ---: |
| Disabled | 427 | 472 | 45 | 0 |
| Subtle | 568 | 613 | 45 | 66097 |
| Pronounced | 707 | 752 | 45 | 75594 |

Compared with the [reference mockup](../optical-glass-target.png), the native
cube retains visible 480-line aliasing and stronger grazing reflections; these
images do not establish an exact visual match. The corner image exposes the
light/dark cavity boundary, while the three motion pairs document distinct
idle settings at their sampled phases. The isolated emulator has no physical
GC Loader SD, so its save-failure status is honest; persistence and recovery
are validated with host storage fixtures.

## Performance

The previous High-blur Video cube used the prior idle motion (now Subtle).
The current window uses High blur and Pronounced motion. Both retain VSync,
30 warmup frames and one uninterrupted 96-sample window, with no captures,
debugger stops or app storage writes during sampling. Timings use Dolphin's
guest clock; they are not physical GameCube measurements. Active time is
frame time minus retrace wait. Copy timings include earlier GPU command waits.

| Build | Frame ms | Active ms | Prepare ms | Submit ms | Draws / triangles |
| --- | ---: | ---: | ---: | ---: | ---: |
| Previous High / Subtle | 16.681 | 4.508 | 0.781 | 3.675 | 103 / 45234 |
| High / Pronounced + dark boundary | 16.681 | 4.518 | 0.791 | 3.675 | 103 / 45234 |

The active-work difference is **+0.010 ms**.
Motion levels differ, so this does not isolate the optical band's cost. Original
rows/summaries are retained. The post-window managed arena reports 152080 of
262144 bytes and zero collections. This is neither peak nor live managed
usage; navigation history differs, so it does not establish a live-memory
improvement.

Replay in the configured Linux/Xvfb environment:

```sh
python3 ci/review_performance_overlay.py --dol /path/to/VideoMode.dol \
  --sdk /path/to/DolphinDotNet --logs /path/to/fresh-review \
  --native-boundaries --motion-storage-review --motion-storage-profile \
  --quick-input --profile-seconds 90
```

DOL SHA-256: `1ce34b7417f2bd00d9effca4f341597c92d355a1611bf16f383d3277c3538e7e`. Runtime source: `f83464b90127cd1ec6bf6da4681f396df8f67e81`.
Capture harness, test and documentation followup: `c9db7245a544de7ddeb36f80dfab4597b1cbbcf3`.
[Provenance](provenance.json) records SDK, engine, toolchain, hashes and actual
frame counters. Raw DOL/ELF/YUV/PPM and logs remain in task artifacts outside git.
