# Configurable inner-glass Gaussian blur

Options adds **Glass Blur: Off / Low / Medium / High**. Off is the default.
Open Options with A, select Glass Blur, open its editor with A, change strength
with Up/Down, then confirm with A. B discards the draft and returns to the cube.
Confirmed settings use the existing verified two-slot GC Loader SD store.
Version-two settings include blur; version-one slots still load with blur Off.
An unavailable or write-protected SD reports that the setting was not saved.

The filter softens the scene transmitted through the existing inner boundary.
Outer glass highlights and UI are drawn afterwards, keeping them sharp. Each
axis uses a dense nine-sample Gaussian expressed as five bilinear texture
fetches. Symmetric byte weights sum to 255. The vertical kernel matches the
horizontal kernel's actual variance, including finite support, quantization and
EFB pixel aspect. A projective offset applies `T + dy*Q` before division, so the
radius stays constant with depth.

When enabled, the filter adds two texture copies and three indexed draws: a
half-size horizontal filter, an exact source restore, and the vertical filter
clipped by the existing inner-shell geometry. Restoring the source prevents a
blurred halo outside the cube. Off skips these copies and draws. The roughly
361 KiB native scratch allocation is created on first use and retained; no
texture-sized managed array is added.

## Native review

Every PNG is an actual GameCube DOL frame from cloud-local Dolphin 2503,
OpenGL/Mesa. Guest execution is stopped at the completed presentation boundary.
Original GX_CopyDisp YUYV buffers are decoded with BT.601 color conversion only.
The packager verified each original YUV and PNG hash against capture metadata.
No resize, composition or generated replacement pixels are used. Raw buffers,
emulator logs and binaries remain outside git; provenance retains their hashes.

The [initial sparse kernel](initial-sparse/07-blur-high-xfb.png) produced repeated
and striped detail at High. Its ten captures are retained as a rejected visual
iteration, including the [new Options row](initial-sparse/02-options-blur-setting-xfb.png)
and A-confirm/B-cancel sequence. The final dense kernel uses adjacent weighted
texel pairs to avoid those widely separated repeated samples.

Final normal-DOL captures show [Off](final/01-blur-off-xfb.png),
[Low](final/05-blur-low-xfb.png), [Medium](final/06-blur-medium-xfb.png),
[High](final/07-blur-high-xfb.png), and [Off restored](final/10-off-restored-xfb.png).
The unobstructed [cube Off](final/cube-off-xfb.png) and
[cube High](final/cube-high-xfb.png) views expose the transmitted icon without
frosted menu rows. Native aliasing and finite filter support remain visible;
this optional filter does not establish an exact match to the reference glass.

## Performance

These are Dolphin guest-clock samples from the normal interactive DOL, on the
Video face with idle motion and VSync enabled. Each level has 30 warmup frames
and 96 uninterrupted samples. No debugger stops, screenshots, frame dumps or
application storage writes occur during those windows. They do not establish
physical GameCube performance. Active time is frame time minus retrace wait;
copy timing includes synchronization with preceding GPU work, so it is not an
isolated CPU or GPU cost.

| Blur | Frame ms | Active ms | Delta from Off ms | Prepare ms | Submit ms | Draws / triangles |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Off | 16.681 | 3.964 | +0.000 | 0.757 | 3.155 | 100 / 38320 |
| Low | 16.681 | 4.504 | +0.539 | 0.778 | 3.674 | 103 / 45234 |
| Medium | 16.681 | 4.504 | +0.539 | 0.778 | 3.674 | 103 / 45234 |
| High | 16.681 | 4.504 | +0.539 | 0.778 | 3.674 | 103 / 45234 |

Original profiles and all samples are in `final/profile-*/`. Post-window managed
arena values in each JSON are neither peak nor live allocation measurements.
The filter strengths share the same draw/fetch count; their weights and offsets
change when strength or display dimensions change.

The earlier dense-kernel run completed Off and Low before stopping during
Medium at 24/96 samples. Only its completed profiles are retained in
`intermediate-dense/`; the incomplete Medium window is excluded from all
averages. The original aborted run did not retain capture events or an
incomplete-profile marker. Missing metadata has not been reconstructed. Those
earlier measurements also include additional retained panel geometry that the
final build no longer submits for the three-row Video face.

## Video modes and persistence

The eight [native mode captures](modes/events.json) replay safe 480i, 480p
preview/keep, widescreen 480p, 576i, 240-line output, B rollback and X safe mode.
Native XFB dimensions and VI scan bits are checked for every capture. PNGs keep
their original pixel aspect; PAL/widescreen display correction is not baked in.
The replay enables High through Options before switching modes.

Host tests cover dense Gaussian impulse response, symmetric normalized bilinear
kernels, effective variance and projected radius; four-row cube/flat layout;
draft/commit/cancel/reload; strict settings validation, legacy version-one
migration and torn-slot recovery. The isolated emulator has no physical GC
Loader SD. Native settings display honest save failures there; host storage
fixtures exercise persistence and recovery.

The delivered normal DOL is `VideoMode-inner-glass-blur.dol`, SHA-256
`466d9483f2b462ad42e92951067e0f7ad2d30ddb8896d1837cce24f4e956829f`. Its runtime source is
`5c9832d38b4befb0a2dd9b5c487604f01da827f2`; later capture-helper changes do
not change application rendering. SDK, engine, toolchain and individual source
hashes are retained in [provenance](provenance.json). Native builds use the same
pinned source SDK/compiler, libogc2 image and stock generated Makefile as the
preceding glass builds. The normal build retains the ordinary application clock.
