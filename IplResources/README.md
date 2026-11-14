# IPL resource explorer

Run a single demo of the console's original font, six firmware textures, the
startup sound and ten complete menu effects. It uses the published
`DolphinDotNet.GameCube` NuGet package through `DolphinDotNet.Sdk`; no SDK source
checkout is needed.

- Up/down on the main stick or D-pad selects a sound.
- A plays the selection; B stops the current voice.
- X cycles Logo top/left/right, Environment, Tiles and Wordmark textures.
- Start cancels loading and exits; Z toggles the shared statistics panel.

The list includes the existing Turn side, Turn center and Enter menu sounds,
plus Back, Cancel edit, Confirm edit, Change value, Change field, Change row and
Open editor. Each selection plays the complete prepared effect, including its
authored notes and tails. The three movement identities map to
`OptionMove1`, `OptionMove2` and `OptionMove3` respectively.

`SystemFirmware.ReadConsole` reads the configured console ROM through EXI.
On a real GameCube, that ROM is built in: no IPL file needs to be installed.
Build B02 uses continuous DMA for each 1 KiB ROM page, following libogc's ROM
transfer method. It shows the actual published SDK version below the title,
starts with Startup selected, and displays load errors across two lines.
The [hardware diagnostic](../tools/ipl-diagnostics/README.md) found identical
ROM bytes through all five read methods on the reporting PAL console with
GC Loader / CubiBoot: CRC-32 `6DAC1F2A`, matching the early NTSC 1.0 reference.
The console's region alone therefore does not select its resource profile.
The revised transport still needs confirmation in that console's explorer.
In Dolphin, configure an appropriate original IPL in its regional `GC` user
directory. The app does not download firmware or probe storage for `ipl.bin`.
The menu profile supports NTSC 1.2 and PAL 1.2. The early reference supports
Startup and all six textures, while its different menu DSP setup remains
unqualified; those rows say `Menu profile unsupported`. This is separate from
an `IPL unavailable` acquisition failure. Other firmware can report individual
resources unavailable. Without an IPL the app shows the failure and
retains the sample font. No firmware, original textures, samples or recordings
are bundled, and no generated tone substitutes for an unavailable effect.

Audio preparation runs on `Task.Run` while the screen remains responsive.
`CreateMenuSound` produces ordinary caller-owned `Sound` resources;
`TryGetMenuSoundInfo`, `SourceWaveCount` and `GetSourceWaveId` report the prepared
effect and original instrument IDs, shown in the selected sound's panel and
debug log. These IDs differ from flat WSYS indices.
Textures and the `SystemFont` are published on the graphics executor between
frames. The input image and catalog are disposed before interactive playback;
each texture and sound owns its data. The demo retains eleven clips, below the
sixteen-slot audio limit, and one playback channel at a time.

Start cancels pending preparation. Failure and partial loading dispose prepared
sounds, and shutdown disposes the voice before sounds, textures and fonts.
Continuous IPL ambience and alternate startup variants are not represented by
the finite menu-effect list.

The SDK's [API guide](https://github.com/capz/DolphinDotNet/blob/main/docs/system-firmware-resources.md)
and [audio qualification report](https://github.com/capz/DolphinDotNet/blob/main/docs/system-menu-audio-research.md)
describe the supported profiles, preparation budgets and measured output
differences. Menu rendering is deterministic; bit-identical IPL DSP output and
physical-console playback have not been established.

```sh
dotnet build IplResources/IplResources.sln -c Release
dotnet run --project IplResources -c Release --no-build
```

The [approved mockup](Design/approved-mockup.png) is a design reference, not an
emulator screenshot. The implementation reuses the sample palette and fallback
font, and loads original visual resources locally through the SDK.
