# IPL resource explorer

Build B04 uses public SDK/runtime `0.1.0-preview.353.1` to explore the console's
original font, six textures, three stereo startup variants, ten complete menu
effects, and twelve source samples. No SDK source checkout is required.

- L/R switches categories; Up/down selects a sound in the scrolling list.
- A plays; B stops. Embedded sample loops repeat automatically.
- X cycles Logo top/left/right, Environment, Tiles and Wordmark textures.
- Z toggles shared statistics. Start cancels loading and exits.

Startup includes Normal, One controller Z and Four controller Z. Menu includes
Turn side, Turn center, Enter menu, Back, Cancel edit, Confirm edit, Change value,
Change field, Change row and Open editor. Samples displays original wave IDs,
format/rate and read-only automatic-loop status. Cards and Ambience are reserved
for the next SDK session milestone; their controls are not enabled in this
checkpoint. Startup clips contain the original paired samples; the persistent
score's envelopes and automatic ambience handover belong to that later API.

`SystemFirmware.ReadConsole` reads the built-in ROM through EXI on a GameCube.
No IPL file needs to be installed on a console. In Dolphin, configure an
appropriate original IPL in its regional `GC` user directory. The app does not
download firmware or probe storage for `ipl.bin`. The reporting PAL console
with GC Loader / CubiBoot returned CRC-32 `6DAC1F2A`, matching the early reference
profile; region alone therefore does not select the resource profile.

`IncludeSystemAudio` retains the complete compressed bank. Startup uses
`TryGetStartupSoundInfo` and `CreateStartupVariantSound`; menu uses the existing
`CreateMenuSound`. All thirteen clips are prepared in the background. Source
samples are created lazily with `CreateAudioSampleAsync`, then played with
parameterless `SystemAudioSample.Play()`. The API handles the intro and authored
repeat section. No loop flag, endpoints or repeat scheduler is needed.

The input image is disposed after preparation. The owned compressed catalog
remains alive for lazy source extraction. Each prepared resource is independent
of it, and only one raw sample is cached: at most fourteen clips and one playing
channel. Stop cancels pending autoplay, and shutdown cancels/waits for work,
stops playback, then disposes all owned resources. Textures and the system font
are published on the graphics executor between frames.

Unknown resources fail explicitly and retain the fallback font. No firmware,
original textures, samples, recordings or substitute tones are bundled. Menu
profiles include early NTSC 1.0, NTSC 1.2 and PAL 1.2. Physical-console playback
of the newly added effects and startup variants remains to be confirmed.

Native acceptance used the installed Dolphin emulator with DSP LLE and public
NuGet packages: all three startups, all ten menu cues, six textures, a source
loop beyond its original duration, Stop and exit cleanup passed. Exit left zero
native clips and voices. This is emulator evidence, separate from hardware
confirmation and original-score fidelity qualification.

The `ipl-resources-dol` CI artifact contains `IplResources-early-ipl.dol`.
Confirm `IPL RESOURCES B04` and the pinned SDK version on screen.

```sh
dotnet build IplResources/IplResources.sln -c Release
dotnet run --project IplResources -c Release --no-build
```

The [approved revised mockup](Design/approved-complete-audio.png) is a design
reference, not an emulator screenshot. It extends the existing navy palette
and compact typography. The SDK [API guide](https://github.com/capz/DolphinDotNet/blob/main/docs/system-firmware-resources.md)
describes extraction, profile qualification and native budgets.
