# Internal wall refraction

This evidence comes from the normal interactive GameCube DOL in Dolphin 2503
with Mesa/OpenGL, Xvfb and VSync enabled. PNGs are unscaled native XFB output,
decoded from original YUYV buffers using BT.601. `events.json` records buffer
and PNG hashes. The angled capture intentionally retains the existing policy
of hiding the icon during most of a side turn. A opens the flat page and B
returns to the cube. Z shows the performance overlay.

`RESEARCH.md` separates IPL evidence from recreation/inference and explains
the Snell lookup, secondary internal return, centered icon layering and limits.
The approved target is `../sealed-glass/approved-target.png`. Refraction is
implemented, but the sparse broad highlights and dark upper-left corner still
fall short of the target's layered corner reflections. This is not a perfect
match or a reconstruction of the exact Nintendo shader.

Uninterrupted 96-frame guest-clock sample after 30 warmup frames:

| Metric | Centered-icon baseline | Internal refraction |
|---|---:|---:|
| Frame mean | 16.681 ms | 16.681 ms |
| Active mean | 3.428 ms | 3.440 ms |
| Preparation mean | 0.750 ms | 0.753 ms |
| Submission mean | 2.626 ms | 2.635 ms |
| Draws / triangles | 99 / 31,408 | 99 / 31,408 |
| Managed arena after sample | 129,576 bytes | 130,040 bytes |
| Managed collections | 0 | 0 |

The 64 KiB lookup is native texture memory, outside the managed arena.
These are emulator measurements, not a real-hardware GPU cost estimate.
See `profile.json`, `frames.csv` and `build.json`. The separate mode replay
checks actual native VI dimensions and scan bits, including interlaced,
progressive, widescreen, 576i, 240p, rollback and safe mode.
