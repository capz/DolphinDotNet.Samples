# Reflected environment and inner-front glass

Reference: [the exact mockup](../optical-glass-target.png). Baseline
[native output](../wall-volume/pass-05/01-options-target-xfb.png) and
[timing](../wall-volume/performance/final.json) are from PR #27.

This change keeps the fitted camera, 0.080 wall thickness, 0.16 outer roundover
and icon origin. It changes the reflected lighting and cavity-surface response.

- A finite ceiling/softbox source and a darker divided reflection are sampled
  through the existing normal/depth environment coordinates. Their footprints
  bend over the upper-right roundover as the object rotates.
- The Fresnel texture's unused rows now store a two-angle bounded internal
  return. View-normal cosine still supplies opacity. Refracted incident cosine
  supplies glass-to-air reflection and its critical-angle behavior. The near
  cavity receives `R_air + 0.20 * max(0, R_glass_to_air - R_air)` as radiance;
  its alpha retains `R_air`. This strengthens light caught by existing geometry.
- One central ray intersects the actual rounded-box boundary before Snell
  refraction. Its normal remains continuous across a 45-degree turn. That ray
  approximates the incident path for the whole object; this is not a per-pixel
  curved-wall path tracer.
- One additional arithmetic TEV stage quarters transmitted scene color before
  the four-times reflection mix. It restores scene exposure while allowing
  bright white reflections without increasing opacity or passing HDR values
  through TEV's eight-bit color inputs. Transmission can lose up to three
  channel levels from this intermediate quantization.

The outer-front material uses seven TEV stages. Draw count, geometry and texture
fetch count remain the same. Near and opposing interfaces retain separate
illumination exposure. Separate retained rear/outer-front/inner-front lighting
lookups add 512 KiB native memory, plus one 48-byte matrix and twelve managed
floats. Lookup and
vertex uploads happen outside frames; no texture-sized managed array is added.

## Native review

Every PNG here is real GameCube DOL output in cloud-local Dolphin 2503,
OpenGL/Mesa. The completed native GX_CopyDisp XFB is read after GX_DrawDone and
before VIDEO_WaitVSync, then decoded with BT.601 YUYV-to-RGB conversion. No crop,
resize, compositing or image-generated replacement is applied to native PNGs.
Capture metadata retains original buffer and PNG hashes.

[Pass 01](pass-01/01-options-target-xfb.png): the upper-right white environment reflection is visible. At native
(420,64), RGB changes from baseline (115,142,140) to (252,255,254). The same
source also catches an opposing wall, creating an excessive long white streak
at the left. The A-open flat page and B-return sequence remain intact. This
pass is retained as a failed material iteration.

[Pass 02](pass-02/01-options-target-xfb.png): the near interfaces use the new studio accents; the opposing interfaces
retain the quieter baseline environment. This targets the unwanted opposite-wall
streak without hiding the hollow volume. The long opposing-wall flare is
reduced in front and angled captures.

[Pass 03](pass-03/01-options-target-xfb.png) adds soft side sources, improving the inner-right sample at (451,220)
from RGB (18,27,30) to (32,40,48), versus reference (50,47,73).

[Pass 04](pass-04/01-options-target-xfb.png) adds a dark environment return that masks collected colored light
through the already-sampled environment alpha channel. Dielectric opacity
remains independent. At (452,74), native RGB changes from (79,130,126) to
(27,45,46). The angled view reveals an overbright reflection band; this is retained as
a failed review case.

[Pass 05](pass-05/01-options-target-xfb.png) confines those side sources to the inner-front lighting lookup. Its
outer-front lookup retains the ceiling/softbox and shadow. This reduces the
broader band, but its angled capture still shows a saturated specular strip.

The two diagnostic builds preserve the original DOL and replace exactly one
PPC display-list call with a NOP: either the near cavity or outer-front draw.
They are native layer-isolation diagnostics, not final material captures. The
patch address, instruction and original/diagnostic hashes are retained. The
layer tests motivated a lower cavity exposure, although they do not isolate
its pixel cost or establish identical pose between the original captures.
Older angle captures used elapsed timesteps, so their exact pose can differ
with submission cost.

[Pass 06](pass-06/01-options-target-xfb.png) lowers near-cavity radiance and opacity exposure together, retaining
the bounded Fresnel response. The review build now advances the shell at a
fixed 1/60 second per frame for reproducible scripted turns; normal builds
keep their ordinary clock. The front retains its white crown, dark return and
subtler inner lip. The [angled view](pass-06/04-rotating-corner-xfb.png) still contains a strong grazing specular streak; this is a remaining
visual difference, not an exact reference match. Final inner-right RGB at
(451,220) is (23,34,39), versus the original (16,24,28) and reference
(50,47,73): a modest improvement, less bright than pass 03 after reducing
cavity exposure. The dark-return sample remains (27,45,46).

## Validation and performance

Linked math tests cover reciprocal interfaces, critical-angle reflection,
bounded radiance gain, Snell direction and unit length, flat parallel-wall
symmetry, rounded-boundary entry normals and continuity through 45 degrees.
Native menu A/open and B/return sequences were inspected in passes 01 and 04.
Front/angle captures are retained for all six passes. The final turn remains
more streak-like than the compact curved reflection structure in the supplied
front-on mockup. The native resolution also exposes some stair-step breakup.
No exact-match or real-hardware performance claim is made.

The normal DOL is `VideoMode-reflected-glass.dol`, SHA-256
`911d200725d43660eb23d9d239dcd4091030156c26dc8aaa490f42d9dcd18733`.
Its uninterrupted 96-frame Video-face window after 30 warmup frames, VSync on,
measures 16.681 ms/frame and 3.965 ms active work. Baseline active work was
3.957 ms: the difference is +0.008 ms (~0.2%) in this Dolphin guest-clock
sample, not a hardware measurement. Preparation is 0.759 ms and submission
3.155 ms; draws/triangles remain 100 / 38,320, with zero managed collections.
The post-window managed arena is 115,152 / 262,144 bytes, not peak/live usage.
Original samples and metadata are in `performance/`.

The normal DOL passes all eight native VI checks in `modes/events.json`: safe
480i, 480p preview/keep, widescreen 480p, 576i, 240-line progressive, B rollback
and X safe mode. The harness asserts dimensions and VI scan bits: 640x480/0,
640x480/2, 640x576/0 and 320x240/1 respectively. PNGs retain raw pixel aspect;
they do not bake in PAL/widescreen display correction. The emulator has no
physical GC Loader SD, so these captures do not revalidate real storage
persistence or console performance.

Local linked tests, managed Release, native review/normal compilation, AOT/DOL/
PowerPC ELF checks, repository configuration validators and Python syntax checks
pass. Remote CI is reported in the pull request checks.

Cloud build uses the same SDK/compiler, pinned libogc2 toolchain, stock generated
Makefile and optimized features as the preceding source-SDK builds. To avoid
nested network fetches and preserve enough VFS disk space, the managed review
assembly is built with `ci/build_videomode_source_sdk.py --managed-only`, then the
SDK compiler runs `--bundle <assembly> <native-directory> --sample-controls` and
the stock `make -j2` runs in the pinned toolchain. The EXI/VIDEO source verifier
still checks its exact upstream revision before compilation. Final native
DOL/ELF section/AOT validation is unchanged.
