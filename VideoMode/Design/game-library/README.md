# GC Loader library and Swiss integration

The Play face opens a flat six-card library. `games/` is scanned recursively one
file/directory step at a time, with a 16-level depth bound and 16384-game cap.
Only valid plain ISO/GCM GameCube headers are indexed. The cache has a checked
32-byte header and individually checksummed 1024-byte records, allowing bounded
six-entry page seeks. Missing/read-only cache storage uses incremental rescans
and retains one page, not an unbounded catalog. Y requests a rescan.

Artwork streams the 6144-byte tiled RGB5A3 portion of `opening.bnr` directly into
a native texture. It avoids the larger managed allocations of a full decoded
banner object. Titles and native textures are retained per page. Glass-card and
rounded artwork geometry are cached; six 32-segment masks reuse the existing
625-vertex staging buffer. A nine-tap live backdrop capture softens the flat grid
inside cards. It uses a smaller kernel than the cube rows because six large
panels cover substantially more pixels. This is not a physical-hardware timing
claim. VSync stays enabled.

`native-01/` contains actual native DOL captures driven through Dolphin input,
with SHA256 and events. The titles/artwork are clearly labeled original UI
fixtures. The first surfaces looked too solid against the optical-glass target;
the next refinement adds live frost, lighter transmission, specular sheen and
rounded artwork. Further native acceptance is in progress in this draft.

Swiss receives real configuration-string argv: `Autoload=gcldr:/...`, `AutoBoot`,
and the supported NTSC/PAL video keys. Because Swiss loads per-game INI after its
global defaults, the app changes only `Force Video Mode` in
`swiss/settings/game/ABCD.ini`. It preserves other bytes/keys, an original backup,
and recovery from an interrupted rename. It verifies the new file before
requesting launch. Existing per-game video cannot silently override the selected
mode. A required write failing stops launch; nothing is overwritten blindly.

`ci/prepare_swiss_gc.py` prepares unpacked SD contents from official Swiss
v0.6r2119, pinned by archive hash, with its source URLs, licenses and provenance.
`swiss-gc.dol` belongs at the GC Loader SD root, alongside VideoMode.dol. No
physical card is modified by that script. No game images or Nintendo firmware
are distributed. Source integration uses the console-services SDK; the older
published package reports launch unavailable.

Host tests cover actual filesystem index/cache behavior, torn/corrupt records,
read-only fallback, root bounds, argv and byte-preserving per-game configuration.
Native official-Swiss handoff and physical GC Loader/game autoboot acceptance
are separate from the labeled visual fixture. The former is still being tested
in this draft; no physical console is connected to this cloud session.
