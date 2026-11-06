# Native Swiss startup argument acceptance

The caller DOL launched the unmodified official Swiss v0.6r2119 ELF load segments,
losslessly repacked as an uncompressed DOL by `ci/prepare_swiss_gc.py`. GDB read
Swiss's real `__system_argv` after launch and verified all five strings exactly.
The fixture ISO contains only that Swiss executable and an original FST header.
The ISO is sector padded; file contents do not include padding bytes.

This proves native replacement and argument delivery. It does **not** prove a
visible Swiss menu, game autoboot or physical GC Loader behavior: the captured
Dolphin window remained black. No game image was launched and no console is
connected. The standard packed release wrapper separately fails on an expansion
hardware probe unsupported by Dolphin 2503. The prepared SD executable avoids
that wrapper; every loaded code/data byte matches the official ELF. Provenance
records hashes of the archive, ELF, packed release, repacked DOL and segments.

The source SDK now places the active staging region outside executable/BSS
addresses, and copies argv to reserved low memory only after device shutdown.
This also protects arguments from packed loaders overwriting the caller heap.
Host faults/default/O3/sanitizers and this full official-ELF launch cover those
changes. No claims about physical startup or game compatibility are inferred.
