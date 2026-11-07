# Historical benchmark worktrees

These patches preserve the remaining local edits from four isolated native
performance comparison worktrees. Each patch applies to its own exact base
revision. They are reproduction inputs for the earlier profiling work; they do
not change the current interactive VideoMode build.

| Patch | Base revision | Preserved comparison setup |
| --- | --- | --- |
| [Initial performance baseline](videomode-perf-baseline.patch) | `9198b85aa8bab53850fead8115440825d862d9e8` | Hardware-runner setup/reporting with the original renderer and baseline timing label. |
| [Follow-up baseline](videomode-followup-baseline.patch) | `9269a00ef0856481f728d7e980274297f840cc14` | Deterministic Options, Cube, Detail and Oblique review scenes. |
| [Indexed-rim comparison](videomode-indexed-baseline.patch) | `30f925616c78e7a18bc3286259ec9a95b8531143` | Historical indexed-rim implementation, submission counters and overlay compatibility. |
| [Overlay control](videomode-overlay-baseline.patch) | `7845b9da12ee59dd45aa9d1246ae167b549223a1` | Separate overlay label/value timings and full/timers/off review controls. |

[manifest.json](manifest.json) records each full base SHA, patch SHA-256 and the
size and SHA-256 of every resulting modified or added source file, including the
previously untracked helper files. Source worktrees remain available locally.

To reconstruct one variant from the repository root:

```sh
archive="$PWD/VideoMode/Design/benchmark-worktrees"
git worktree add --detach ../videomode-baseline-replay 30f925616c78e7a18bc3286259ec9a95b8531143
git -C ../videomode-baseline-replay apply --check "$archive/videomode-indexed-baseline.patch"
git -C ../videomode-baseline-replay apply "$archive/videomode-indexed-baseline.patch"
```

Use the SDK/toolchain and scene documented in the corresponding historical
review: [initial profiling](../performance-review.md),
[follow-up](../performance-followup.md), [indexed rims](../indexed-rims-review.md)
and [overlay cost](../overlay-cost-review.md). Archived variants may use older
SDK interfaces; applying a patch alone does not establish build compatibility
with the latest SDK.

For this archive, all four patches were checked against their exact Git base,
reconstructed in temporary directories and compared byte for byte with their
source worktrees. The manifest hashes also matched every reconstructed file.
No new native or physical-hardware measurements were performed for this archive;
the linked reviews retain their original measurement scope and limitations.
