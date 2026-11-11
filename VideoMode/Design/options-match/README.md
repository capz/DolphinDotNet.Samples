# Options reference matching

The requested image is exactly `../optical-glass-target.png`, 1448 x 1086.
It supersedes the later, much more oblique Calendar reference for this review.
Native captures are real GameCube DOL output in Dolphin 2503 / OpenGL / Mesa.
PNG files are decoded from completed native XFB buffers without resizing;
`events.json` records raw buffer and PNG hashes. They are not SVG or generated
image proofs. Timing data, when reported, is emulator guest-clock data.

## Measured design changes

At 640 x 480 the reference body spans approximately x181.7..461.4 and
y61.9..348.3, centered at (321.7,205.1). Its nearly frontal camera has weak
perspective, horizontal rows and shallow opposing walls. The old forced
25-degree yaw and 12-degree pitch exposed far more top/side wall area and
rolled the text. A shared longer-distance camera replaces the inconsistent
CPU/GPU/icon projection constants; a small idle wiggle remains interactive.

The reference's outer corner radius is about 20 native pixels. This is tighter
than the later Calendar reference. Outer/cavity surfaces remain closed welded
manifolds, with a 0.16-unit roundover and 0.07-unit normal wall thickness.
The opposing edges are parts of this solid, not detached rim geometry.

The panel measurements imply 0.33 height, 0.434 row spacing, 0.087 corner
radius and bounds -0.79..0.81 on the front face. Typography is normalized by
the shared front-plane magnification. Centered icon bounds grow to 1.20 units
while retaining at least 0.30 units of cavity clearance.

The old reflection map contained almost-black ambient and binary white boxes.
It produced broad triangular/slab reflections and lacked the target's colored
face illumination. The replacement separates narrow layered environment
returns from a smooth position-dependent teal/amber/violet light field. The
field is authored illumination, not a recovered original IPL shader or a
physically exact caustics simulation. Snell transmission/internal return and
the centered icon's rear/front layering remain.

## Native iterations

1. **Camera, profile and material separation:** frontal view, shared camera,
   tighter roundover/walls, matched row bounds and new environment/field.
   Capture exposed vertically reversed light colors and broad grazing-wall
   reflection slabs. Both were real failures, not accepted as target matches.
2. **Wall-depth reflection mapping:** correct field orientation, compensate
   scale using actual rounded silhouette, and include wall depth in reflection
   coordinates so a grazing flat wall crosses narrow light bands instead of
   reflecting the same white source across its whole surface.
3. **Outer-contour returns:** move profiled light bands onto the curved contour,
   increase edge sampling from eight to ten segments without adding vertices,
   and lower the heading by the measured three pixels. Native evidence still
   showed black outer corners and highlights dropping out at native resolution.
4. **Continuous corner polish and color:** broaden the dim shoulder around
   bright returns, add normal-mapped corner glints, narrow the panels and
   reduce accumulated row-spacing error. Move/strengthen the teal and amber
   lobes using measured native/reference samples. Add the light field after
   the reflected/transmitted blend so grazing Fresnel cannot erase bottom
   violet light. This remains six TEV stages with the same texture fetch count.
   Native capture passed Options/A-open/B-return and the rotating corner view.
   The capture also exposed excess cyan illumination on the outer right wall.
5. **Transparent outer contour:** taper collected color toward the silhouette
   and concentrate the crown highlight toward the upper-right. The native
   Options output is closer in framing, color and gloss, but not an exact
   optical match. Fine secondary corner returns remain simpler than the
   target. The centered icon also appears about 9–10 pixels lower than the
   image; it remains at the 3D origin as explicitly requested, rather than
   being moved upward merely to alter one projection.
6. **Measured color falloff:** narrow the teal lobe, broaden amber falloff and
   extend collected light farther through the interior before the contour
   taper. Strengthen the violet return. At matched 7x7 median sample patches,
   teal at (390,126) improves to RGB (36,85,82), versus reference (41,85,83);
   upper-left at (215,126) is (39,30,21), versus (35,30,23). Upper-right and
   bottom glints remain brighter than the image. These few patch comparisons
   are diagnostics, not an image-wide fidelity score or exact-match claim.

All six review DOLs compiled natively, with recorded hashes. The pass-04
navigation sequence uses the same source as pass 05 except its final optical
lookup coefficients. The normal interactive build is validated separately.

The pass-05 normal interactive DOL's uninterrupted 96-frame Video sample
(30 warmup, VSync enabled) averages 16.681 ms/frame and 3.441 ms active work,
with 99 draws / 31,408 triangles and zero managed collections. See
`performance/pass-05.json` and its original samples/overlay. These are Dolphin
guest-clock timings, not real hardware results. Pass 06 only changes lookup
generation constants. Its final interactive DOL was also profiled: 16.681 ms
per frame, 0.749 ms preparation, 2.639 ms submission and 3.440 ms active work,
again 99 draws / 31,408 triangles and zero managed collections. See
`performance/pass-06.json`, its 96 original samples and native Z overlay.
This shorter profile-only navigation visits fewer screens before measuring;
its smaller post-window arena is not a measured reduction in peak/live memory.

The app retains the functional label **Storage**, where the older image says
**Memory Card**: that row opens both memory-card and SD management. It is not
renamed to an inaccurate label merely to alter screenshot similarity.

Geometric framing/manifold/clearance tests do not establish a pixel-perfect
material match. Each native pass must be inspected against the image. The
single-view camera fit is not a unique recovery of the mockup's 3D camera.
