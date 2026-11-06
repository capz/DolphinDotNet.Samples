# Glass Storage screens

Press R at the front menu to preview the rear Storage side. Cube sides show
read-only option previews. A opens the complete flat menu; select a row there
with the D-pad or stick, then A opens its editor or storage browser. B closes
any flat menu, field editor, browser or dialog directly to its cube side.
Unconfirmed drafts are discarded, active card transfers are cancelled safely,
and video rollback must succeed before returning. A keeps a value and returns
to the flat list. B or the reverse stick direction on a cube side returns to
the front menu. Memory-card L/R selects slots A/B. In SD folders, L opens the
parent folder; B always closes the whole flat flow. Y refreshes storage.

The captures below document the original Storage feature acceptance. Current
navigation captures and details are in `../flat-navigation/`.

The memory-card grid reads every game's directory, blocks, title/comment,
permissions and native RGB5A3/CI8 icons. Up to eight icon frames are cached as
native textures and honor loop/bounce timing; Reduced Motion freezes them.
One image read/upload happens between frames. Copy/move use one native 8 KiB
step per update, destination CRC readback and metadata verification. A move only
removes its source after verification. Copy/move permissions are independent.
Existing destination names are never overwritten. Cancel removes only the
verified partial destination belonging to this operation. Hotplug/revisions
invalidate stale selections. Actions and confirmations default to Cancel.

Formatting is offered only when the SDK reports a card needing formatting or
with incompatible encoding. It requires two separate confirmations and erases
all saves on that selected card. Healthy cards are never formatted automatically.
Raw Japanese filenames/ownership are preserved by native operations; this small
font displays ASCII titles with a game-code fallback, not a Japanese translation.
The SDK reports a committed operation with failed directory refresh separately,
so the UI does not invite repeating a destructive action.

SD Storage is read-only browsing/opening: six-entry incremental pages, folders
first, root-bounded parent navigation, text/hex previews, bounded PNG thumbnails,
confirmed DOL applications and valid GameCube images through Swiss. It has no
copy, move, rename or delete controls. The normal storage device is GC Loader.

These screens require the source SDK card/console services from SDK PRs #156
and #157. The ordinary pinned NuGet build remains buildable and reports missing
services honestly. Build the complete native application with:

```
python ci/build_videomode_source_sdk.py --sdk /path/to/DolphinDotNet \
  --hypercube /path/to/hypercube-engine --output /path/to/output --normal \
  --icons --glass --blur --glyphs --tints --numbers --footer-text --grid \
  --selectors --console-services --memory-cards
```

Host tests cover read-only real-filesystem browsing, previews, root bounds,
card protection, default Cancel, transfer cancellation, two-stage formatting,
slot routing and media revisions. SDK PR #157 records actual native transfer,
CRC, move/delete and stale-snapshot acceptance on isolated Dolphin cards, plus
host I/O/removal/cancellation faults. `native-review/` records twelve completed native screen captures with original
fixture saves and an original data-only DVD filesystem standing in for SD files.
It exercises both slots, a verified copy, cancelled deletion, directory navigation,
text opening and B return. `normal-cards/` repeats card acceptance with the final
normal interactive DOL, real clock/preferences services enabled and no review
fixtures. Independent post-shutdown raw-card readback matches the full copied
8192-byte payload plus ownership/icon/comment/protection metadata, keeps the
source and old destination, and confirms cancelled deletion retained both files.
`normal-scan/` records progressive/interlaced previews, keep, B rollback and X
safe mode on that same normal DOL. All named native windows and completed EFB
frames are retained with DOL hashes and input events. The raw EFB may be taller
than the final 640x480 presentation; compare the unscaled `-window.png` files.

The native review caught and fixed a cached-footer tint mismatch. A subsequent
replay had an extra B press; only the corrected twelve-phase replay is acceptance
evidence. Those earlier diagnostics remain in the cloud artifact directories.
The final face layout gives long labels space before their right-hand values.
The final DOL is Release/O3/LTO, keeps VSync and the existing rendering caches,
and contains no synthetic browser/review source. Its exact source SDK/engine
revisions, size and hash are in `normal-build-manifest.json`.

Physical GC Loader reads/writes, hardware clock retention and Swiss game autoboot
remain unverified. Swiss native startup argument delivery is recorded separately
under `../game-library/swiss-native`; the emulator window stayed black. UI file
fixtures use Dolphin's DVD filesystem, not an emulated GC Loader SD driver. No physical console is connected; no physical card was modified.
`OriginalCardFixture.cs.txt` is test-only source for three original UI saves. It
must only run with fresh isolated emulator cards; it is never the delivered app.
