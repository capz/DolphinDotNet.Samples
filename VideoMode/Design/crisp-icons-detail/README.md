# Crisp cube icons and Calendar-style details

The block-built icon is composited after the inner Gaussian blur and before
front glass reflections, frosted row panels, tint and text. Its cached 3D
geometry, centered origin and cube projection are retained. The icon stays
sharp in the open interior; the UI's local frosted rows still cover it.

With inner blur enabled, a bounded optical return highlights its existing
cavity boundary. It uses the current inner geometry and environment lookup,
one additional TEV stage, a 48-byte native matrix and twelve managed floats.
The highlight follows the curved normal transition and reflected lighting.
It adds no draw, texture or detached outline mesh.

## Detail controls and style

The cube's Video side remains a read-only preview. A opens one flat
configuration page with three independent green block selectors: Output,
Scan and Aspect. Left/Right selects a field; Up/Down changes its draft. Scan
uses I, P or AUTO blocks with its full name below. A previews the complete
combination; A keeps it and returns to this editor. B cancels or rolls back
to the cube. The twelve-second timeout and guarded rollback remain active.

Video, Calendar and single-value Options editors share Calendar's readable
5/6-pixel block scale, active-field arrows, label baselines and grid. Preview
prompts use green/white typography; actual failures use red. General Options,
game and storage browsing lists keep their navigation semantics.

## Native review

All eleven PNGs are actual normal GameCube DOL output in cloud-local Dolphin
2503 with OpenGL/Mesa and Xvfb. Original GX_CopyDisp YUYV buffers are read after
GX_DrawDone at the completed presentation boundary and decoded with BT.601
color conversion only. Each buffer and PNG hash matches its event metadata;
there is no crop, resize, compositing or generated scene replacement.

- [Options with High blur](native/01-options-high-blur-xfb.png) and the
  [unobstructed centered icon](native/02-crisp-centered-icon-xfb.png) show the
  new layer order. The icon keeps its 3D placement beneath front glass and UI.
- The [turning-corner material probe](native/02b-blur-boundary-corner-xfb.png)
  exposes the cavity transition. Existing turn visibility suppresses the icon
  during this probe; it does not demonstrate icon placement at that angle.
- The [Video cube preview](native/03-video-cube-xfb.png) leads to the
  [grouped editor](native/04-video-grouped-detail-xfb.png),
  [progressive preview](native/05-progressive-preview-xfb.png),
  [kept progressive editor](native/06-progressive-kept-detail-xfb.png),
  [rollback to confirmed progressive](native/07-rollback-to-progressive-cube-xfb.png)
  and [safe interlaced editor](native/08-safe-interlaced-detail-xfb.png).
- [Calendar Date](native/09-calendar-date-detail-xfb.png) and the
  [Options blur value](native/10-options-blur-value-detail-xfb.png) retain the
  shared editor treatment. Native VI bits are checked for every capture:
  progressive for 05/06/07, interlaced for the remaining frames.

For a fresh replay in the configured Linux/Xvfb environment, use the normal
DOL and matching ELF from the same build:

```sh
python3 ci/review_performance_overlay.py --dol /path/to/VideoMode.dol \
  --sdk /path/to/DolphinDotNet --logs /path/to/fresh-review \
  --native-boundaries --detail-review --detail-profile --quick-input \
  --profile-seconds 90
python3 ci/decode_native_xfb.py --captures /path/to/fresh-review \
  --output /path/to/fresh-review
```

Native aliasing, finite blur support and the existing strong grazing glass
reflection remain visible. These captures do not establish an exact match to
the reference mockup. The isolated emulator has no physical GC Loader SD and
shows honest save-failure messages; persistence/recovery uses host fixtures.

## Performance

Both windows use the normal interactive Video cube with High blur, idle motion
and VSync enabled: 30 warmup frames followed by 96 uninterrupted samples.
No screenshots, debugger stops or app storage writes occur during sampling.
These are Dolphin guest-clock measurements, not physical GameCube timings.
Active work is frame time minus retrace wait; copy timings also include waits
on earlier GPU commands and are not isolated GPU costs.

| Build | Frame ms | Active ms | Prepare ms | Submit ms | Draws / triangles |
| --- | ---: | ---: | ---: | ---: | ---: |
| Previous High blur | 16.681 | 4.504 | 0.778 | 3.674 | 103 / 45234 |
| Crisp icons + boundary highlight | 16.681 | 4.508 | 0.781 | 3.675 | 103 / 45234 |

Active-work change: **+0.004 ms**. Original rows and
means are retained under `profile-high/` and `baseline-high/`.
The post-window managed arena reports 220168
of 262144 bytes, with
0 collections. This is neither peak nor
live managed usage.

Host tests cover independent Video fields, combined preview/keep/cancel,
timeout and failed rollback; thirteen expected scan-driver output changes;
shared block scale and animated column bounds; six centered icon layouts,
blur/icon/front/UI ordering and the bounded boundary highlight. The historical
renderer compatibility build retains its supported original flow.

The normal interactive DOL is `VideoMode-crisp-icons-detail.dol`, SHA-256
`2942fffaf07bd70a92a9aa3bc75273150e9ee0c690305f0ba3b6044c2e3bbd51`. Runtime source is `3308c3545e0d7cc64df9b5a69a80a015783d5d8f`. SDK, engine,
toolchain, runtime files, capture-tool snapshot and original hashes are in
[provenance](provenance.json). Raw DOL/ELF/YUV/PPM and logs remain in the task
artifacts outside git.
