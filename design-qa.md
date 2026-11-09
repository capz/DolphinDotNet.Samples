# Native hollow-glass design comparison

Source visual truth: `VideoMode/Design/hollow-glass/approved-target.png`.
Implementation: `VideoMode/Design/hollow-glass/final/01-calendar-front-xfb.png`.
Viewport: native 640×480, 4:3, 480i; no browser, CSS viewport or device-scale factor.
The 1448×1086 reference was downsampled to 640×480. The original native image
was not resized. Full-view evidence: `final/delivered-comparison.jpg` under the
same design directory. Focused glass/menu evidence: `final/delivered-focus.jpg`.
These are explicitly labeled comparisons, not native screenshot composites.

State: Calendar cube-side preview, live system clock, Full motion. The mockup's
08:49 is illustrative; the native clock/date reflect the emulated real clock.
The camera pose varies slightly with idle motion. Existing menu behavior and
footer are retained; this change targets the cube material and shape.

## Findings

- P2 — Reflections remain simpler than the approved optical glass. The normalized
  full and focused comparisons show fewer secondary light returns and a more
  uniform teal top highlight. Curved walls and a dark cavity are present, but
  the photographic optical detail has not been reproduced fully. Further work
  should refine the environment reflection and shared corner-wall surfaces.
- P2 — Fine highlight edges still show native-resolution stair steps and gaps,
  especially around curved junctions and during rotation. Widening the polish
  improves coverage but does not fully match the smooth reference. Further
  native antialiasing/coverage work needs its own performance and scan checks.
- P3 — Fonts remain the existing Nunito atlas, with readable hierarchy, labels
  and right-aligned values. At native resolution they are less sharp than the
  downsampled generated reference. No wrapping or truncation was seen in these
  five frames. Spacing and panel rhythm remain consistent with the existing app.

## Required fidelity surfaces

- Typography: existing licensed Nunito atlas retained; selected Time hierarchy
  and Calendar title remain legible. Native sampling differs from the mockup.
- Layout: larger cube, generous margins, two frosted rows and separate footer;
  slight pose/proportion differences remain visible in the comparison.
- Colors: teal, amber and violet wall lighting with a black cavity is implemented.
  Optical white highlights and secondary colors remain less rich than the target.
- Image/material quality: real procedural geometry, reflection texture and live
  backdrop blur; the approved image is never used as a runtime image overlay.
- Copy: Calendar, Time, Date and platform input glyphs retained. Live time and
  the truthful unavailable-GC-Loader status are expected differences in cloud.

## Comparison history

1. Native 01 established the cavity but the wall was too dark and outline-like.
2. Native 02 broadened colored reflections; logging exposed 68.469 ms/frame.
3. Native 05 cached perimeter lighting and revealed wall depth with a camera-space
   tilt. Mean frame time was 50.048 ms; broad highlights were still weak.
4. Native 06 used GPU-interpolated vertex colors and sharper specular lighting,
   reaching 33.364 ms/frame. Native 07 shared projected boundaries and joined
   face-wall edges; preparation fell to 25.680 ms, but polish appeared too soft.
5. Native 08 separated six body bands from two narrow polish bands. Final native
   captures widened those highlights for native resolution. The new images show
   the intended hollow form, while the two P2 fidelity findings above remain.

Intermediate successful builds without captures are not counted as visual passes.
Native measurements are guest-clock windows with idle motion, not physical
GameCube results or isolated causal benchmarks across identical poses.

## Functional and build checks

All five final frames were opened and inspected. A opens the flat Video menu;
B returns to the same cube side. Menu tests cover existing editing, persistence,
storage and video-preview/revert behavior. Managed/native builds, AOT binary
verification, repository validators and GX index/perspective tests pass locally.
The final 96-frame uninterrupted window averaged 33.364 ms/frame, p95 33.367 ms,
with 26.351 ms preparation and zero managed collections. Hardware testing is
unavailable in this cloud session. This is native-app QA, not browser QA.

## Implementation checklist

- Implemented: hollow transmission, thick rounded walls, broader roundover,
  independent reflection strength, retained row frost and working native input.
- Implemented: shared light/position work and GPU vertex-color interpolation.
- Remaining: detailed optical reflection and fine-edge fidelity findings above.
- Remaining: physical hardware performance and video-signal acceptance.

final result: blocked
