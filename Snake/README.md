# Snake — memory-card save/load sample

A playable C# GameCube Snake demonstrating native `Dolphin.Storage.MemoryCard`
saves, `FrameClock`, `Controller`, `GraphicsDevice` and `Draw2D`. All
graphics, including the seven-row bitmap-like lettering, are created with
rectangles. No textures, SD card or disc assets are required.

## Gameplay

Use **D-pad** or **left analog stick** to steer, eat red fruit, grow and score.
Touching a wall or your body ends the run. Snake accelerates as you eat fruit.

The title screen offers **Continue** (only when a valid save exists) and
**New Game**. Use up/down to select and **A** to confirm; **X** exits from the
title screen. **Start** pauses/resumes during gameplay. **B** saves the current
run and returns to the title screen. On game over, **A** restarts and **B**
returns to the title. The game freezes if the controller disconnects.

## Persistent storage

The game searches for a native GameCube memory card in **slot A**, then
**slot B**. It uses game code `DSNK`, company code `01`, and save name
`SnakeRun`. Dolphin can emulate the GameCube memory card; configure a card
in Dolphin's GameCube slot A or B. For hardware, insert a writable GameCube
memory card in one of the slots.

The sample saves immediately on New Game, every **120 seconds of connected,
active gameplay** (pauses and menus do not count), and when returning to the
title screen with B.
A game over clears its resumable save. Both slots are polled every **two seconds**
with `MemoryCards.Update`/`GetState`. Removing the mounted card releases it and
clears Continue availability; a card in the other slot can take over. Inserting
a card during play or pause saves the current run at the next poll. At the
title screen, an inserted card makes its existing save available for Continue
without automatically loading or overwriting it.

If no card is available, play still works. Requested saves remain queued and
begin once a writable card is present, including after B returns to the title.
Pending writes and failed mounts retry on the two-second poll; game over or
New Game cancels the previous pending run. A busy/unknown probe preserves the
last confirmed connection state. The status line reports failures; physical
presence does not guarantee a formatted, writable card.

`SnakeSaveStore` uses `MemoryCard.Mount`, `GetEntries`, `ReadAllBytes`,
`WriteSave`, `Delete`, and `Dispose`. `WriteSave` provides the card's
transactional replacement mechanism. The game format is compact, versioned,
and checksummed; `SnakeSaveCodec` validates the complete body, direction,
score and food before accepting Continue.

## Build

Open [Snake.sln](Snake.sln) in Visual Studio or Rider and use the Dolphin
launch profile, or build and run from the repository root:

```sh
dotnet run --project Snake -c Release
```

See [IDE setup](https://github.com/capz/DolphinDotNet/blob/main/docs/ide-workflow.md)
for prerequisites and local settings.

The build produces `Snake/bin/Release/net8.0/Snake.dol`, also suitable
for loading on a GameCube, with the project's verified libogc2 EXI override.

Host-side logic and serialization tests:

```sh
dotnet run --project tests/SnakeRulesSmoke/SnakeRulesSmoke.csproj -c Release
```

## Build, upload and debug on GameCube in Rider

The shared **Snake on GameCube (ELF launcher and debug)** configuration builds
the sample's DOL, ELF and debug maps, checks the DolphinDotNet receiver, uploads
`bin/Debug/net8.0/Snake.elf`, starts it, then attaches the GDB debugger.
Boot `DolphinDotNet.Launcher.dol` on the console once and leave it running.
The receiver accepts ELF files; the sample's DOL is also built but is not the
uploaded file in this workflow.

Install a DolphinDotNet Rider plugin that offers **DolphinDotNet ELF launcher
(BBA / USB Gecko)**. Older plugins with only Swiss upload do not support this
configuration. Launcher support is available on the debugger repository's
[feature/dnd-launcher-readiness branch](https://github.com/capz/DolphinDotNet.Debuggers/tree/feature/dnd-launcher-readiness).

One-time setup:

1. Disable **Use ReSharper Build** in Rider so the SDK's native build runs.
   Select the solution's **Debug** configuration.
2. Create `Snake/DolphinDotNet.local.props` (ignored by Git) with the waiting
   GDB stub settings for your target. For USB Gecko:

   ```xml
   <Project>
     <PropertyGroup>
       <DndGdbTransport>UsbGecko</DndGdbTransport>
       <DndGdbChannel>1</DndGdbChannel>
     </PropertyGroup>
   </Project>
   ```

   Channel `1` means memory-card slot B; use `0` for slot A. For BBA / TCP,
   use the following instead, replacing the example console address, netmask
   and gateway with your network settings:

   ```xml
   <Project>
     <PropertyGroup>
       <DndGdbTransport>Tcp</DndGdbTransport>
       <DndGdbIp>192.168.1.80</DndGdbIp>
       <DndGdbNetmask>255.255.255.0</DndGdbNetmask>
       <DndGdbGateway>192.168.1.1</DndGdbGateway>
       <DndGdbPort>2828</DndGdbPort>
     </PropertyGroup>
   </Project>
   ```

   The application owns these GDB settings after handoff; the receiver does
   not configure its stub. Remove the local settings before using the emulator
   launch profiles again.
3. Build once after adding the local settings:
   `dotnet build Snake/Snake.sln -c Debug` from the repository root. The plugin
   validates the ELF and debug maps when checking the configuration, so these
   artifacts must exist even before its first launch.
4. Open **Run → Edit Configurations**, select **Snake on GameCube (ELF launcher
   and debug)**, and choose your saved target using **Manage targets**. For
   USB Gecko, select its serial device and matching slot. For BBA / TCP,
   select the console's address and the application's GDB port (`2828` in the
   example). The receiver upload port is separate (`4299` by default); change
   **Launcher port** if your receiver uses a different port. The shared configuration
   intentionally has no machine-specific target ID. After selecting a target,
   turn off **Store as project file** to keep your configured copy local.
5. With the ELF launcher running on the console, use **Check receiver readiness**
   to verify the connection, then click **Debug** in Rider. The **Build** task
   must succeed before upload. Launch checks receiver readiness again, uploads
   the ELF, starts the application and attaches GDB; use **Resume** to continue.

Stop any other debugger using the connection before starting an upload.

## API updates

Input now uses the shared router, suppressing held actions across screen
transitions. Finishing a run pulses rumble. The HUD uses the common bitmap font;
the existing serialized save format and deterministic RNG are retained.
