# Cube icon centering

The block-built icons now occupy planes through the cube's 3D origin. Their
menu side controls orientation, without the former offset to the outside face.
The visible block bounds are centered on both axes, including unused bitmap
columns/rows, and normalized to a maximum span of 1.10 cube units. This leaves
at least 0.30 units to the closed cavity walls, including rounded corners.
The icon and shell share the same rotation, so that clearance is invariant.
Block bevels and highlight offsets scale with the icon. Native indexed caches
and the uncached/proof path use the same center-plane mapping.

Tests cover every block envelope of all six icons against the actual inner
rounded-box distance, centered visible bounds and orthonormal orientation.
Native Calendar/Video captures, A-open/B-return and the Z overlay are saved here.
The turning-corner capture retains the existing policy of hiding icons during
most of a menu-side turn; the mathematical clearance check covers all poses.
These are unscaled native XFB captures from Dolphin 2503, not generated images.

The normal interactive DOL's uninterrupted 96-frame sample (30 warmup frames,
VSync enabled) averaged 16.681 ms/frame, 0.750 ms preparation, 2.626 ms submission
and 3.428 ms active work. Draw/triangle counts remain 99/31,408. There were zero
managed collections. These are Dolphin guest-clock results, not hardware data.
See `profile.json`, `frames.csv`, `events.json` and `build.json` for provenance.
