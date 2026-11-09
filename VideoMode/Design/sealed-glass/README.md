# Sealed thick-walled glass cube

[Approved reference](approved-target.png) is the user's newer selected mockup.
It supersedes the rejected per-face bands in `../hollow-glass`. It is never
loaded into the game as a background, overlay or reflection texture.

## Continuous object

`IplSealedGlassGeometry` constructs one rounded glass solid with a sealed hollow
cavity. Its outer and inner boundaries each have 3,458 welded vertices. Every
triangle edge has two incident triangles, including edges between construction
charts. There are no panel seams, free edges, detached rims or neon frame bands.
The outer roundover radius is 0.30; normal wall thickness is 0.14, leaving a
rounded cavity radius of 0.16. Analytic normals remain continuous at chart joins.
The six logical menu sides still rotate with the same object.

`SealedGlassShell` stores positions/normals and indexed GX display lists once in
native memory. Three interface passes draw the same object: rear exterior,
cavity, then front exterior. These are optical interfaces, not six panel draws.
Only small GPU transformation matrices change each frame; the native path no
longer computes, projects or uploads thousands of glass vertices on the CPU.
The remaining `IndexedRims` class serves animated value selectors only.

## Native TEV material

- Normal-driven studio reflections, varied by view position using GX indirect
  texture coordinates, produce hard highlights on smooth rounded surfaces.
- A lookup provides an exposure-adjusted Fresnel response: face-on surfaces
  transmit; grazing curved surfaces reflect. This is an artistic fixed-function
  approximation, not physically accurate ray tracing.
- Rear interfaces are copied from the live EFB. The front interface uses
  projective screen coordinates and normal-driven indirect displacement to
  refract that live image before combining it with reflections.
- Texture uploads and allocations happen before rendering. The rear image uses
  a bounded native allocation for the demo's maximum 640×576 mode. Capture size
  and projected coordinates follow actual physical/logical dimensions, including
  the EFB height used with hardware AA. GX copy ordering uses FIFO synchronization.
- Existing rounded row panels still blur the live backdrop. UI geometry, input,
  persistence, system clock, storage and guarded video switching remain intact.

The performance overlay reports this as **Sealed glass + TEV**. The obsolete rim
and separate reflection rows are removed; reflection work is part of this draw.

## Evidence and limitations

`iterations/` preserves original native XFB-to-RGB screenshots and timing
provenance. These are actual normal interactive GameCube DOLs in Dolphin 2503,
OpenGL/Mesa, native resolution, with idle motion and VSync enabled. Raw framebuffers
were read only after native presentation completed; BT.601 conversion is the only
image conversion. These images are not SVG proofs or generated mockups.

The 96-frame timing windows follow 30 warmup frames, with no capture/debugger stop
or logging I/O during the window. Values are Dolphin guest-clock observations,
not physical GameCube results or controlled fixed-pose causal benchmarks.

| Iteration | Native finding | Frame mean | Active mean |
| --- | --- | ---: | ---: |
| 01 | Welded closed geometry; normal-only reflections fill flat faces | 16.681 ms | 3.407 ms |
| 02 | Position mask reduces coverage but leaves weak optical definition | 16.681 ms | 3.415 ms |
| 03 | Indirect reflections are localized but overly opaque | 16.681 ms | 3.415 ms |
| 04 | Fresnel and live rear refraction; cavity too dim, underside too visible | 16.681 ms | 3.429 ms |
| 05 | Correct top/right view, brighter cavity; top reflection too broad | 16.681 ms | 3.429 ms |
| 06 | Larger body, narrower light source; planar reflection still too broad | 16.681 ms | 3.429 ms |
| 07 | Strong indirect displacement eliminates white flood but clamps into gray border | 16.681 ms | 3.429 ms |
| 08 | View-normal Z Fresnel, explicit coordinate scales/swaps; clear front, gray top | 16.681 ms | 3.430 ms |
| 09 | Corrected vertical reflection parallax; localized but underexposed light returns | 16.681 ms | 3.430 ms |
| 10 | Brighter white/colored returns; top reflection overlaps the title | 16.681 ms | 3.430 ms |
| 11 | Narrow top/right and bottom returns retain readable title; ambient roof still gray | 16.681 ms | 3.430 ms |
| 12 | Dark ambient clears roof but hides too much of the left roundover | 16.681 ms | 3.430 ms |
| 13 | Balanced dark ambient and corner light; continuous shell delivered for review | 16.681 ms | 3.430 ms |

[Final Calendar](final/01-calendar-front-xfb.png),
[turning corner](final/02-rotating-corner-xfb.png),
[Video side](final/03-video-front-xfb.png) and
[labeled target comparison](final/comparison.jpg) show the delivered material.
The comparison scales only the reference; the native 640×480 image is unchanged.
The final sample averaged 0.753 ms preparation, 2.626 ms submission and 3.430 ms
active work, with 16.681 ms frame mean/p95. Post-window managed arena use was
130,120 of 262,144 bytes, with zero collections. See the original 96 rows in
`final/frames.csv` and the exact DOL/source hashes in `build.json`.
The [native Z overlay](final/06-sealed-glass-overlay-xfb.png) is captured after
the uninterrupted timing window and reports the combined glass submission.

Reference fidelity must be judged from the final native comparison. The approved
image contains complex photographic optical returns that this GX approximation
cannot reproduce exactly. Do not describe these captures as pixel-perfect.

Native replay commands (normal interactive DOL, Linux/Xvfb):

```text
python3 ci/review_performance_overlay.py --glass-review --glass-profile --native-boundaries --sdk <sdk> --dol <dol> --logs <output>
python3 ci/review_performance_overlay.py --glass-modes --native-boundaries --sdk <sdk> --dol <dol> --logs <output>
python3 ci/decode_native_xfb.py --captures <output> --output <decoded>
```

## Native video-switch checks

The delivered DOL completed all eight captures in `modes/`, with assertions against
actual native VI state: 640×480 interlaced, 640×480 progressive preview/keep,
16:9 progressive, 640×576 interlaced, 320×240p double-strike, B rollback to 480i,
and X safe mode. The 16:9 screenshot is the unscaled anamorphic XFB. The mode
captures retain their original dimensions, including the 320×240 image.
`modes/provenance.json` records the same DOL hash as the final material capture.

The first replay attempted unsupported 240i and correctly retained 480i. The
replay now selects progressive before requesting 240p and asserts dimensions/
VI scan bits so a rejected change cannot be mislabeled as a successful switch.
These are emulator checks; physical display/cable acceptance remains untested.
