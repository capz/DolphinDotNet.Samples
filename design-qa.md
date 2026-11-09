# Sealed glass cube — native design QA

Visual truth: [approved continuous-glass mockup](VideoMode/Design/sealed-glass/approved-target.png).
This replaces the rejected six-frame reference in `Design/hollow-glass`.
Native evidence and source/build provenance are in `VideoMode/Design/sealed-glass`.

## Implemented

The cube is one thick-walled rounded solid with two closed, welded boundaries:
exterior and sealed cavity. Outer roundover radius is 0.30; wall thickness is
0.14 along the surface normal, including at the spherical corners. Shared vertex
indices eliminate construction-chart seams. There are no per-face frame bands,
detached outlines, open panel joints or emissive neon edges.

One native indexed object is drawn in three optical-interface passes. GPU normal
and position coordinates drive hard studio reflections, a Fresnel lookup and
live projective refraction of the opposing walls. The old CPU wall-band lighting,
per-frame glass vertex projection/upload and separate reflection meshes are gone.
Only small transformation matrices change per frame. The overlay now reports
`Sealed glass + TEV`; obsolete rim/reflection rows are removed.

The existing menu sides, licensed Nunito atlas, left-label/right-value layout,
rounded live-blur rows, input glyphs, A-open/B-return controls, animated editors,
clock/settings persistence and storage functionality are retained. Time/date in
native captures are the emulator's live clock, not the illustrative mockup time.

## Remaining visual differences

- P2 — The generated reference contains richer secondary reflections, chromatic
  dispersion and optical caustics. The native GX material uses a bounded studio
  environment and two-interface screen refraction; it does not trace rays through
  the glass volume. It should not be described as an exact photographic match.
- P2 — Camera perspective, reflection placement and apparent wall brightness
  still differ from the reference. The rear cavity is visible, but its optical
  returns remain simpler. Inspect both the Calendar view and turning-corner
  capture, rather than judging only a single bright outline.
- P3 — Native-resolution typography and fine highlight coverage are less smooth
  than the downsampled generated image. Screenshots preserve the original XFB
  resolution; the native image is never rescaled to conceal this difference.

`iterations/` records the real material experiments, including failures: broad
white planar reflections, excessive gray clamping and underexposed highlights.
The target was not changed to fit those results. No SVG or generated image is
presented as a native capture.

## Validation scope

Host tests check manifold closure, opposite cavity winding, constant thickness,
pronounced rounded corners and existing menu/preview/revert behavior. The app is
built against the pinned published package for managed validation; the full
interactive native DOL uses the explicit SDK/engine source revisions recorded
with the evidence. AOT validation checks real indexed GX submission and valid
PowerPC ELF/GameCube DOL output. Repository validators and whitespace checks are
also required.

Native screenshots use Dolphin 2503/OpenGL/Mesa in cloud-local Xvfb. Guest execution
is paused at the SDK's completed presentation boundary before reading the real
XFB, followed only by BT.601 YUYV-to-RGB conversion. Timing windows use 30 warmup
frames and 96 uninterrupted samples, with VSync enabled and no dump or debugger
stop during sampling. These are Dolphin guest-clock measurements, not physical
GameCube performance or display-signal acceptance.

Implementation is available for review; exact optical fidelity and physical
hardware acceptance remain open.

Final evidence: [Calendar](VideoMode/Design/sealed-glass/final/01-calendar-front-xfb.png),
[turning corner](VideoMode/Design/sealed-glass/final/02-rotating-corner-xfb.png),
[Video](VideoMode/Design/sealed-glass/final/03-video-front-xfb.png),
[flat page](VideoMode/Design/sealed-glass/final/04-video-flat-xfb.png),
[B returns to cube](VideoMode/Design/sealed-glass/final/05-flat-b-to-cube-xfb.png),
and [labeled reference comparison](VideoMode/Design/sealed-glass/final/comparison.jpg).
Only the reference is downsampled in the comparison; native pixels are unchanged.

The delivered 96-frame window averaged 16.681 ms/frame (p95 16.681), 0.753 ms
preparation, 2.626 ms submission and 3.430 ms active work. VSync remained enabled.
Post-window managed arena use was 130,120/262,144 bytes, with zero collections;
this is not a peak-live-memory measurement. Build and capture provenance include
DOL SHA-256 `2d91b79bd837dc524c722168b86b8ccc960a4eb0c60587dcdc0069f57d8659a2`.

Native mode assertions pass on the same delivered DOL: 480i, 480p preview/keep,
widescreen, 576i, 240p, B rollback and X safe mode. The eight original-resolution
captures and actual VI scan/dimension readings are in
[mode evidence](VideoMode/Design/sealed-glass/modes/events.json).
The 240p capture uses VI double-strike; unsupported 240i remains correctly rejected.
All saved final material/overlay and mode PNGs were opened and inspected.
