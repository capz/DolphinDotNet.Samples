# Cube previews and flat interaction

Cube sides preview their options without row editing or toggles. A opens a full
flat list for Video, Calendar, Options and Storage. Game Play opens the flat game
library directly. Select rows on the flat page, then A opens the existing value
editor; Motion changes on the flat page. Confirmed edits return to that list.

B closes any flat list, field, file preview, game/card dialog or browser directly
to the same cube side. Pending drafts are discarded. An unconfirmed video mode
is restored first; failed rollback stays protected until its retry succeeds.
Leaving an active memory-card transfer cancels and cleans up its partial copy;
cleanup errors remain visible on the cube. Destructive confirmations retain a
default Cancel action, which A can choose without leaving the browser. L selects
the SD parent directory so B has one consistent destination.

The flat menus reuse the live nine-tap backdrop and native rounded-mask buffer.
One retained colored mesh holds all row surfaces for each row-count layout; focus
text eases by 14%. No large managed buffers or new SDK/engine APIs are needed.
The existing value-selector motion and rendering caches remain enabled.

Host tests cover preview/list/editor routing, single-B exits from nested file and
card dialogs, transfer cancellation, read-only folder navigation, independent
calendar parts, persistence, video preview/keep/revert and deferred rollback.
Native input acceptance uses `ci/review_performance_overlay.py --flat-pages`.
The native build is the normal interactive application with real console/card
services, without synthetic browser or deterministic scene switches.

`native/` records nineteen controller-driven native captures from Dolphin 2503.
They show read-only previews, all four flat lists, Motion changing only on the
flat page, video preview rollback and confirmed progressive output, a cancelled
hour draft, memory-card dialog exit, game-browser exit and safe interlaced output.
B returns to the same cube side in each applicable phase. The isolated card files
are byte-for-byte unchanged after browsing and cancelling the card dialog.

Dolphin can dump intermediate draws, including frames missing the footer. The
selection helper chooses existing PNGs with visible body text and all three footer
legends, without altering pixels; selected frames were also visually inspected.
Events record each original frame and SHA256. Raw EFBs are 640x528 here and are
kept unscaled. Original X11 captures and intermediate dumps remain in the cloud
artifacts. These are real native renders, not host SVG proofs or generated images.

The exact DOL hash, source revisions and build profile are in the native manifest.
Physical GC Loader persistence and Swiss game boot remain unverified, as before.
