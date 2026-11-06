# Directional return navigation

On a cube face, move the analog stick opposite its entry direction to return:
Right → Video → Left; Left → Options → Right; Up → Game Play → Down;
Down → Calendar → Up. B still returns. The D-pad retains both directions for row
selection, including Calendar Time/Date. Editors keep up/down value editing and
left/right segmented selection. Video previews still require A keep or B revert.

A fresh return gesture is required. Holding it leaves the cube at home until the
stick is recentered, so a return cannot immediately enter the opposite face.
Changing between D-pad and stick counts as fresh input.

Native evidence uses a normal GameCube DOL in cloud Dolphin with XTest mapped
to real controller stick axes and the D-pad. Eight unmodified native EFB PNGs
cover all four pairs, a two-second held return, and D-pad Date selection. The
normal DOL hash and frame provenance are recorded in build.json/events.json.

Menu/scan/selector regression tests, published-SDK managed build, source-SDK
normal native build, manifest/run/test checks and whitespace validation pass.
Use ci/review_performance_overlay.py --directional-return to reproduce.
