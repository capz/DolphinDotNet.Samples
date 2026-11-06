# Glass Storage screens

Press R at the cube, or open Options → Storage, to reach the rear Storage face.
Its two rounded rows open Memory Card and SD Storage. The stick reverses back
from the four directionally entered faces; the D-pad selects their rows. In flat
screens both select items. A opens; B cancels/returns. Memory-card L/R selects
slots A/B. Y refreshes storage. Original B navigation remains available.

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
host I/O/removal/cancellation faults. Native screen acceptance is being completed
in this draft. No physical console is connected; no physical card was modified.
`OriginalCardFixture.cs.txt` is test-only source for three original UI saves. It
must only run with fresh isolated emulator cards; it is never the delivered app.
