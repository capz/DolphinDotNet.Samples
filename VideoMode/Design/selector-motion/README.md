# Active value selector motion

Only the value currently being edited rocks, tilts and floats. The motion
continues without held input until A confirms or B leaves the editor. It follows
the independently selected hour/minute or day/month/year, with a 180 ms ease-in
each time the active part changes. Inactive values, labels, arrows and grid stay
still. Reduced Motion disables the motion; a pending hardware preview is flat.
All existing A/B and preview/revert semantics remain.

The active field uses up to 16 degrees of yaw, 11.5 degrees of pitch and 7.5
degrees of roll, plus about three logical pixels of float. Wide words reduce
roll to avoid the arrows. The pose uses the existing cube clock. Its amplitude
is stronger than the cube's idle movement, without a per-block vibration.

## Implementation and native timing

Field blocks and all three bevel colors are recorded into bounded indexed GX
quad arrays only when text, size or tint changes. The optical rim command-list
path is reused with a smaller native buffer per field. Geometry stays immutable
while the per-field projection/model matrices change between frames. GX performs
the 3D transform and perspective divide. All uploads and resource creation occur
before BeginFrame. `IplCachedSelectors` defaults to true and uses the published
SDK API; the source helper enables it with `--selectors`.

The deterministic scan-editor comparison uses the same source-SDK integration
as submission round 4, with native VSync on, 30 warmup and 96 uninterrupted guest
frames in cloud Dolphin 2503/Mesa. The review clock is fixed at zero, so this is
a **neutral-pose comparison**, not a claim of real-hardware or worst-motion
performance. Matrix preparation and indexed replay still run on every frame.

| Cost | Previous grid-cached editor | Indexed selector editor |
|---|---:|---:|
| Prepare mean | 0.046 ms | 0.072 ms |
| App submission mean | 2.481 ms | 0.985 ms |
| Active work mean, excluding retrace wait | 2.713 ms | 1.243 ms |
| Active work p95 | 2.981 ms | 1.511 ms |
| Actual frame mean | 16.681 ms | 16.681 ms |
| Draws | 37 | 41 |
| Triangles, including transparent padding | 7,094 | 7,176 |

All visible blocks and bevels remain. The 82 extra triangles are degenerate
transparent command-list padding. Native managed arena size after the window is
165,216 / 262,144 bytes, with zero collections at that snapshot. This is not peak
or live memory and does not establish collection behavior over a long session.

[Native neutral editor](native-editor.png) is pixel-identical to its previous
control outside the inclusive overlay mask `(40,12)-(282,453)`; covered pixels
are not compared. [Raw frame CSV](performance/frames.csv),
[overlay CSV](performance/overlay.csv) and [DOL hash/results](performance/result.json)
are retained. No VSync wait was disabled.

## Interactive native evidence

[Native animation clip](native-selectors.mp4) uses 201 consecutive Dolphin EFB
frames at 60 fps (3.35 seconds). It includes idle hour motion, changing the hour,
then switching to and changing the minute. H.264 encoding is the only conversion;
there is no crop, scaling, generated imagery or replacement UI. EFB recordings
precede Dolphin's presentation postprocessing. [Source frame range and DOL hash](native-clip.json)
are retained.

Two native images with unchanged values show [hour pose A](interactive/02-hour-a.png)
and [hour pose B](interactive/03-hour-b.png). Exactly 1,203 pixels differ, all
within `(224,197)-(286,249)` (exclusive maximum), the active hour's bounds.
Minute, arrows, labels, grid and footer are unchanged. Motion follows the
[edited minute](interactive/05-minute-edited.png),
[month](interactive/07-month-edited.png) and [year](interactive/08-year-edited.png).
[A commits 13:01](interactive/06-time-committed.png), while
[B restores the original date](interactive/09-date-cancelled.png).

[Reduced Motion A](interactive/10-reduced-a.png) and
[Reduced Motion B](interactive/11-reduced-b.png), captured 45 frames apart, are
pixel-identical. [Machine-readable comparisons](motion-comparison.json) and
[controller events](interactive/events.json) retain the evidence. All interactive
images are completed, unmodified native 640x528 EFB PNGs. Timing runs use a
separate fixed-clock DOL; the interactive build has the real advancing clock.


## Validation

Behavior tests cover active-part isolation, continuous idle motion, initial/part
change easing, A commit, B discard, Reduced Motion, hardware-preview suppression
and wide-value bounds throughout a minute of poses. Existing time/date, scan
round-trip, rollback and safe-mode regression tests pass. Published-SDK managed
builds and all-feature source review/normal native builds pass.

The normal published-SDK native build also passes. Native controller replay
walks through [the progressive draft](scan/02-progressive-editor.png),
[progressive preview](scan/03-progressive-preview.png),
[A keep](scan/04-progressive-kept.png),
[interlaced preview](scan/05-interlaced-preview.png),
[B rollback to progressive](scan/06-progressive-restored.png), and
[X safe interlaced restore](scan/07-safe-interlaced.png), using the same retained
selector buffers across transitions. Native screenshots, controller events and
[the published DOL hash](scan/build.json) are preserved.

Sample/run/test manifest checks, Python helper syntax and `git diff --check`
pass. This addition uses the existing published raw GX API and makes no SDK or
engine modification. It is based on main after the submission-cache PR merged;
GitHub CI runs separately for the selector addition.
