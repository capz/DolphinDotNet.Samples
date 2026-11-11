# Glass wall volume and corner reflections

The exact reference remains [optical-glass-target.png](../optical-glass-target.png).
This follow-up concentrates on the visible glass volume and curved corner
returns. It keeps the frontal framing established in the previous review.

All PNGs here are actual GameCube DOL output captured in cloud-local Dolphin
2503 / OpenGL / Mesa. They are decoded from completed native XFB buffers using
BT.601 YUYV-to-RGB conversion, without resizing or composing scene pixels.
`events.json` files retain the original buffer and PNG hashes. The diagnostic
images are deliberately substituted shader outputs, not beauty renders and
not evidence that a rainbow/gray cube is the intended material.

## Retained native attempts

1. [Pass 01](pass-01/01-options-target-xfb.png) increased normal wall thickness
   to 0.095 and separated the four ordered optical interfaces. Reflection
   normals were made consistent between opposing interfaces. It still read
   incorrectly and did not match the reference. Its DOL hash and capture
   metadata are retained beside the image.
2. [Pass 02](pass-02/01-options-target-xfb.png) added a 256-square reflection
   lookup, localized paired corner arcs and collected light that survives
   grazing-angle Fresnel. This also failed to establish the intended thick
   glass appearance. Both attempts were still affected by the native winding
   error identified below; extra thickness and highlight tuning could not
   compensate for selecting the wrong interface.
3. [Pass 03](pass-03/01-options-target-xfb.png) corrects GX winding, reduces
   thickness to 0.080 and retunes transmitted color against the actual front
   coordinates. It removes the broad false inset border. The real
   [angled capture](pass-03/04-rotating-corner-xfb.png) reveals the hollow
   volume, but a lower-left source spills across a long side wall and the
   bottom opposing return is too bright. This pass is not accepted as the
   final material. Its full native sequence also records A-open and B-return.
4. [Pass 04](pass-04/01-options-target-xfb.png) confines near-corner sources to
   a narrower normal-space window and lowers opposing-interface radiance.
   The large lower-left white flare is substantially reduced, while the
   slimmer cavity spacing is retained. The crown remains too dark: the
   native coordinate probe places its first fully covered pixel near V=0.325,
   whereas the previous source was centered on a mostly subpixel V=0.265.
5. [Pass 05](pass-05/01-options-target-xfb.png) moves that finite crown source
   onto the measured V=0.325 samples and increases outer-front reflection
   exposure. At native (420,63), the crown changes from RGB (39,65,63) to
   (139,162,161); at (320,63), from (20,26,28) to (67,71,74). These are sample
   diagnostics, not an image-wide match score. The reference still has finer
   layered corner returns; the lower-left native return remains stronger.

These failed passes are retained as findings, not presented as successful
matches or silently replaced by the corrected result.

## Native diagnostics found the wrong surface

The diagnostic build preserves the object, camera, generated texture
coordinates, indirect mapping and Fresnel setup. It substitutes the final
color output to show one of three quantities:

- exposed Fresnel alpha as gray;
- environment lookup coordinates as red/green;
- position-light lookup coordinates as red/green.

At the clear gap around native pixel (322,198), the environment coordinate
probe changed from approximately **(109,95)** before the fix to **(144,164)**
afterward. The projected front interface predicts about **(145,164)**; small
differences are consistent with lookup and XFB quantization. At (320,326),
the Fresnel probe changed from **253** to **19**. A nearly opaque grazing
return had been selected where the camera should see the face-on front wall.
These archived probes precede the later front-only reflection exposure
adjustment. A current diagnostic rebuild uses four-times rather than two-times
Fresnel for that artistic front blend, so its face-on gray is about 38 rather
than 19; raw environment/field coordinate probes are unchanged.

| Probe | Before correction | After correction |
|---|---|---|
| Fresnel | [native gray output](diagnostic-before/01-fresnel-xfb.png) | [native gray output](diagnostic-after/01-fresnel-xfb.png) |
| Environment UV | [native coordinate output](diagnostic-before/02-environment-uv-xfb.png) | [native coordinate output](diagnostic-after/02-environment-uv-xfb.png) |
| Field UV | [native coordinate output](diagnostic-before/03-field-uv-xfb.png) | [native coordinate output](diagnostic-after/03-field-uv-xfb.png) |

The mathematical closed manifold had the intended outward/inward winding,
but the native GX front-face convention under this projection was opposite.
`GX_CULL_BACK` was consequently selecting the rear interface. The corrected
`NativeTriangleIndex` adapts triangle order only in the retained native command
stream. The mathematical `TriangleIndex`, welded boundaries, signed volumes
and constant-normal-thickness definition remain intact. Projected near/far
winding checks across all six settled cube faces complement the manifold
checks; a closed mathematical mesh alone would not have caught this bug.

## Corrected material and geometry

The current wall thickness is **0.080**, about **16% lower** than the rejected
0.095 attempt. The outer roundover stays **0.16**, preserving the fitted outer
silhouette; the cavity corner radius is 0.080. The block-built icon remains
at the cube's 3D origin with at least 0.30 units of clearance to the cavity.
There are no detached rim or panel-frame meshes.

The four indexed interface draws are ordered as outer rear, far cavity,
near cavity and outer front. The centered icon draws before the near/front
interfaces, and the one live glass capture includes the transmitted scene.
The near cavity receives lower artistic exposure so it does not dominate as
a separate inset frame. Its opposing return supplies depth rather than a
second equally bright border.

The reflection texture grows from 128 x 128 to **256 x 256 RGBA8**. The extra
**192 KiB is native texture memory**, not a managed array in the 256 KiB managed
arena. Localized paired elliptical returns follow the rounded corner normal
domain. Baseline environment values and transmission are kept within safe
eight-bit TEV inputs; the final mix restores baseline exposure and supplies
two-times corner-highlight exposure without wrapping out-of-range values.
The outer-front blend additionally exposes Fresnel by four rather than two,
with roughly 85% face-on transmission. The physical R-squared echo weight is
retained independently; this gain is an artistic highlight choice. Rear/inner
interfaces retain their original two-times alpha to preserve premultiplied
blend coherence. The crown source is aligned to measured native texels.
The front material still uses six TEV stages and the same texture-fetch
count. Four geometry draws replace three; their cost must be measured rather
than assumed free. Glass vertices and lookups remain retained, with no
per-frame glass vertex rebuild/upload.

The teal, amber, violet and blue field is retuned using the corrected front
coordinate probe. Earlier far-face compensation is no longer appropriate.
Collected light is reduced modestly at grazing angles, using
`(180 - 36 * exposedFresnel) / 255`, rather than disappearing entirely there.
This is authored optical styling and a screen-space transmission/internal
return approximation. It is not a recovered Nintendo IPL shader, a full
curved-volume ray tracer or a claim of an exact reference match.

## Reproduction in the prepared cloud environment

These commands assume the task's existing SDK/Hypercube checkouts and local
`dnd-cloud-dolphin` image. The diagnostic captures used SDK
`4dc80bd10faae69d5b288c7bc227ce6be0e9c750` and Hypercube
`1fa7b6207450ea45732a09d649af8729e7298b5c`. Run from the Samples checkout.
Build/capture containers serially with the VFS daemon, reuse the build output
directory, and choose a fresh `--logs` directory for every capture run.
Preserve each DOL and its sibling ELF before rebuilding when they are needed
for later debugger extraction.
If nested-container GitHub access is unavailable, seed a fresh output's
`obj/Release/net8.0/native/build/exi-upstream` with an independent local clone
of `/workspace/libogc2-reference` (`git clone --no-hardlinks`). The SDK script
still checks out upstream `094b250f1e978d37789f18ffc2d3c9c2325062f8` and verifies
its EXI/VIDEO corrections before compiling; this does not bypass source checks.

```bash
source /workspace/.cloud-setup/activate.sh
export TMPDIR=/tmp

wall_build=(
  --sdk /workspace/DolphinDotNet-card
  --hypercube /workspace/hypercube-passes
  --output /workspace/artifacts/sealed-glass/first
  --icons --glass --blur --glyphs --tints --numbers
  --footer-text --grid --selectors --console-services --memory-cards
)
wall_dol=/workspace/artifacts/sealed-glass/first/bin/Release/net8.0/VideoMode.dol

wall_capture() {
  env -u DOCKER_HOST -u DOCKER_CONTEXT -u DOCKER_TLS \
    -u DOCKER_TLS_VERIFY -u DOCKER_CERT_PATH \
    docker --host=unix:///var/run/docker.sock run --rm --shm-size=1g \
    -e LP_NUM_THREADS=2 -e DISPLAY=:99 \
    -v /workspace:/workspace \
    -v /usr/lib/x86_64-linux-gnu/libXtst.so.6.1.0:/usr/lib/x86_64-linux-gnu/libXtst.so.6:ro \
    dnd-cloud-dolphin sh -c \
    'Xvfb :99 -screen 0 1280x1024x24 -nolisten tcp >/tmp/xvfb.log 2>&1 &
     python3 /workspace/DolphinDotNet.Samples-hollow/ci/review_performance_overlay.py "$@"' \
    wall-capture "$@"
}
```

Build and capture the fixed Options beauty scene:

```bash
python3 ci/build_videomode_source_sdk.py "${wall_build[@]}" --mockup-review
wall_capture --mockup-review --mockup-single --native-boundaries \
  --sdk /workspace/DolphinDotNet-card --dol "$wall_dol" \
  --logs /workspace/artifacts/wall-volume-review-repro
python3 ci/decode_native_xfb.py \
  --captures /workspace/artifacts/wall-volume-review-repro \
  --output /workspace/artifacts/wall-volume-review-repro-png
```

Build and capture the three diagnostic outputs instead:

```bash
python3 ci/build_videomode_source_sdk.py "${wall_build[@]}" \
  --mockup-review --optics-diagnostic
wall_capture --mockup-review --optics-diagnostic --native-boundaries \
  --sdk /workspace/DolphinDotNet-card --dol "$wall_dol" \
  --logs /workspace/artifacts/wall-volume-diagnostic-repro
python3 ci/decode_native_xfb.py \
  --captures /workspace/artifacts/wall-volume-diagnostic-repro \
  --output /workspace/artifacts/wall-volume-diagnostic-repro-png
```

Build the normal interactive DOL and measure it without the fixed scene or
diagnostic substitutions:

```bash
python3 ci/build_videomode_source_sdk.py "${wall_build[@]}" --normal
wall_capture --glass-review --glass-profile --glass-profile-only \
  --native-boundaries --sdk /workspace/DolphinDotNet-card --dol "$wall_dol" \
  --logs /workspace/artifacts/wall-volume-profile-repro
```

That profile uses 30 warmup frames and 96 uninterrupted Video-face samples
with VSync enabled. Native screenshot reads happen outside the sample window.
The resulting timings describe Dolphin guest-clock behavior, not physical
GameCube GPU performance. Normal/review/diagnostic build modes are guarded
against incompatible combinations.

## Native acceptance and remaining differences

Pass 05 is the current accepted iteration for the slimmer-wall request, not
an exact optical match. Its [front view](pass-05/01-options-target-xfb.png),
[A-open flat page](pass-05/02-options-flat-xfb.png),
[B-return](pass-05/03-options-return-xfb.png) and
[rotating view](pass-05/04-rotating-corner-xfb.png) are actual native captures.
The selected 0.080 wall retains a visibly hollow cavity without returning to
the rejected 0.095 spacing. The outer silhouette and centered icon are kept.
Fine corner highlights remain simpler than the reference, and the lower-left
return is stronger. The upper roundover still shows some dotted/stair-step
breakup; the target has a smoother rolled crown with localized harder white
glints. Bottom returns are sharper diagonal/V strokes rather than compact
paired curves with fine chromatic layering. The near-left amber/white return
is also a longer narrow streak than the source's distributed wall light.
These are optical/profile differences, not a reason to thicken the geometry
again. The functional label remains Storage because it opens
both memory-card and SD management; the older mockup says Memory Card.

## Final performance and validation

The final normal interactive DOL is `VideoMode-slim-glass.dol`, SHA-256
`2b49b6c4c5a9539fc988f63869cca8b7ec3c31ff15b409576d9f55614c38706f`.
[Source/dependency hashes](provenance.json), [96 original samples](performance/frames.csv),
[summary](performance/final.json) and the subsequent
[native Z overlay](performance/06-sealed-glass-overlay-xfb.png) are retained.
The overlay was hidden during measurement and enabled afterward.

| Dolphin guest-clock metric | Previous normal build | Final normal build |
|---|---:|---:|
| Mean frame, VSync enabled | 16.681 ms | 16.681 ms |
| Preparation | 0.749 ms | 0.751 ms |
| Submission | 2.639 ms | 3.154 ms |
| Active frame minus retrace | 3.440 ms | 3.957 ms |
| Transmission/backdrop capture gates | 1.526 ms | 2.036 ms |
| Draws / submitted triangles | 99 / 31,408 | 100 / 38,320 |
| Managed collections | 0 | 0 |

This iteration costs **0.517 ms more active work**, about 15%, in this emulator
window. Most of the measured increase appears in the capture gates. This
aggregate includes transmission/backdrop copies, pass transitions and
synchronization with preceding GPU work; it is not an isolated texture-copy
or GPU-time benchmark, nor an isolated CPU draw-setup cost.
The extra near-cavity draw is intentional, and is not claimed to be free.
The post-window managed arena is 114,912 / 262,144 bytes; that is not a peak or
live-memory measurement. Frame p95 remains 16.681 ms. No real-hardware frame
time is inferred from these emulator results.

Local validation passes: linked VideoMode menu/geometry/optics/overlay tests,
managed Release build, source-SDK native review and normal builds, generated
AOT/DOL/PowerPC ELF checks, all three repository configuration validators,
Python syntax checks and incompatible diagnostic-mode rejection checks.
The normal DOL also passes all eight [native mode replay captures](modes/events.json):
480i safe start, 480p preview/keep, 16:9 480p, 576i, 240-line progressive,
B rollback to 480i and X safe mode. The harness asserts actual VI dimensions
and scan bits, rather than accepting menu text alone: 640x480/scan 0 for
480i, 640x480/scan 2 for 480p, 640x576/scan 0 for 576i and 320x240/scan 1
for the 240-line double-strike mode. These are unscaled raw-pixel buffers;
widescreen/PAL display pixel-aspect correction is not baked into their PNGs.
The isolated emulator has no physical GC Loader SD, so this replay does not
revalidate hardware settings persistence or console performance.

Remote CI status is tracked by the pull request's checks, independently of
these local native captures. No pull request is merged by this task.
