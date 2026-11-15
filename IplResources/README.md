# IPL resource explorer

Build B05 consumes the published SDK/runtime `0.1.0-preview.361.1`. It explores
the console's original font, six textures, all three startup score paths,
twelve menu/error cues, seven card cues, continuous ambience and twelve source
samples. No SDK source checkout or private package feed is required.

- L/R switches categories; Up/down selects an entry in the scrolling list.
- A plays or restarts the selection; B stops all session playback.
- X cycles Logo top/left/right, Environment, Tiles and Wordmark textures.
- Y enables or mutes ambience. Z shows native memory, voices, queue and health
  alongside the shared SDK statistics. Start cancels loading and exits.

Startup contains Normal, One controller Z and Four controller Z. Each plays the
original score and automatically hands over to idle ambience. Y can disable
that handover's ambience without interrupting the startup. Ambience has the
original lead-in of about four seconds; `STARTING AMBIENCE` reports this delay.

Menu contains the ten existing actions plus Press Start and Error. Cards adds
erase/move completion, copy/format completion, repeating transfer from A/B,
copy-transfer completion and move-to-A/B completion. A replaces the previous
audition; the two progress cues repeat until B or the next audition. These are
audio auditions: the demo performs no memory-card operations.

Ambience exposes idle ambience, animated cube/submenu motion and session-local
stereo/mono output. Samples shows each original wave ID, codec/rate and read-only
repeat status. The three looping sources play their intro and then repeat their
authored section automatically. Neither the app nor its caller supplies loop
flags, endpoints or a repeat scheduler.

`SystemFirmware.ReadConsole` reads the built-in ROM through EXI on a GameCube.
No IPL file needs to be installed on a console. In Dolphin, configure an original
IPL in its regional `GC` user directory. The app does not download firmware or
probe storage for `ipl.bin`. The reporting PAL console with GC Loader / CubiBoot
returned CRC-32 `6DAC1F2A`, matching the early reference profile; region alone does
not select the resource profile.

Preparation runs in the background using `IncludeSystemAudio`,
`FirmwareCatalog.SupportsAudioSession` and `SystemResources.CreateAudioSession`.
A prepared session initially remains stopped. The demo uses `PlayStartup`,
`PlayCue`, `PlaySample`, `SetControl`, `OutputMode` and `GetStatistics` directly.
`GetAudioCues` supplies read-only repeat metadata. Sample descriptors come from
`session.GetSample`, so their ownership matches `session.PlaySample`.

The input image is disposed after preparation. Textures are published between
frames on the graphics executor, then the full-session path also disposes the
catalog. Its session owns an independent decoded cache. Stop releases the
current playback handle and clears session voices/tails while keeping the cache
prepared. Shutdown cancels and waits for pending preparation, stops playback,
then disposes the session, textures and font.

Partial profiles preserve the existing qualified startup/menu factories and
lazy `CreateAudioSampleAsync` / parameterless `SystemAudioSample.Play()` path.
The catalog stays alive for that lazy extraction and only one standalone raw
sample is cached. Unsupported resources report their availability independently.
A failed complete-session preparation is reported explicitly. No firmware,
original textures, samples, recordings or substitute tones are bundled.

Complete audio profiles cover early NTSC 1.0, NTSC 1.2 and PAL 1.2. The SDK's
original-score fidelity and sustained-playback qualification is recorded in its
[qualification ledger](https://github.com/capz/DolphinDotNet/blob/main/docs/ipl-complete-audio-progress.md).
New session playback on physical hardware remains to be confirmed.

The `ipl-resources-dol` CI artifact contains `IplResources.dol`. Confirm
`IPL RESOURCES B05` and SDK `0.1.0-preview.361.1` on screen.

```sh
dotnet build IplResources/IplResources.sln -c Release
dotnet run --project IplResources -c Release --no-build
```

The [approved revised mockup](Design/approved-complete-audio.png) is a design
reference, not an emulator screenshot. The SDK [API guide](https://github.com/capz/DolphinDotNet/blob/main/docs/system-firmware-resources.md)
describes extraction, profile qualification, ownership and native budgets.
