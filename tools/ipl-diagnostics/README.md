# IPL hardware diagnostic

`IplDiagnostics-check01.dol` investigates an `IPL unavailable` / encoding error
on a real GameCube. It uses the published SDK selected by the repository's
`global.json`, including its firmware reader without modification. It is a
diagnostic program; the resource explorer remains in `IplResources`.

Run it through the same launcher as the failing demo and wait for the result
screen. The title must say **IPL CHECK 01**. The program performs these checks:

- The published managed cipher processes a generated 2 MiB image, checks its
  complete keystream fingerprint, and restores it to zero on a second pass.
  It also auto-detects a scrambled, generated image and extracts two Yay0
  records through the public `FirmwareImage` and catalog APIs.
- `SDK32` reads the ROM using the package's production reader at 32 MHz, with
  the same 4 KiB calls used by `SystemFirmware.ReadConsole`. In SDK 335 this
  used immediate transfers; the updated SDK uses continuous page DMA.
- `DMA32` uses bounded 1 KiB DMA reads, following libogc's ROM access method.
- `IMM16` and `DMA16` compare immediate and DMA transfers at 16 MHz.
- `AGAIN` repeats the production read to reveal unstable results.

Each row displays an ordinary CRC-32 of the complete ROM as read, a CRC-32 of
`[0x100, 0x200000)` after the independent native cipher, counts of `Yay0`
signatures before/after that transform, and a known revision fingerprint.
`P` means a fingerprint matched before transforming, `S` means it matched
afterward. `UNKNOWN/?` can mean another legitimate revision; this small table
only includes the three references already used to qualify the SDK. Signature
counts are clues, not validation. The console's printable revision suffix is
also displayed. The firmware header and fonts are outside the cipher region.

Matching rows verify that the methods returned the same bytes. A successful DMA
row with different production-reader results isolates the transport path. A
failed cipher check isolates the managed cipher/catalog path independently of
the ROM. These results do not establish successful resource playback.

Only checksums, counts and revision text are shown. There are no firmware files,
original textures or samples in the DOL, and no storage or network writes.
All bus waits are bounded; failures display the code and ROM offset. Start exits
from the final screen. This tool reuses the SDK's existing diagnostic overlay.

```sh
python3 tools/ipl-diagnostics/build.py
```

Requires .NET 8, Docker and Python 3. The script first builds normally with the
published NuGet SDK, adds the independent native comparators to the generated
bundle, and links it again using the same pinned devkitPPC/libogc2 image. It
does not replace the package's reader or add an SDK API. Outputs, including a
SHA-256 manifest and ELF symbols, are under `bin/diagnostic/`. A failed build
removes the previous deliverable before starting.

The stream-cipher reference is the same MIT-licensed reference used by the SDK;
see [LICENSE.cipher.txt](LICENSE.cipher.txt). Dolphin supplies already-decoded
IPL data, so an emulator run cannot establish physical ROM transport correctness.
