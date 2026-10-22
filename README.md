# DolphinDotNet Samples

Run commands from the repository root. These are independent NuGet consumers of [`DolphinDotNet.Sdk`](https://www.nuget.org/packages/DolphinDotNet.Sdk/) and [`DolphinDotNet.GameCube`](https://www.nuget.org/packages/DolphinDotNet.GameCube/). Both are pinned to preview `0.1.0-preview.15.1` in `global.json` and `Directory.Build.props`; the `DolphinDotNet.Core` dependency is resolved transitively. No checkout of the compiler/runtime repository is required. All examples target .NET 8 for compilation;
the GameCube runs generated native code, using only the supported AOT API subset.

| Sample | Demonstrates | Setup / result |
| --- | --- | --- |
| [HelloGameCube](HelloGameCube/Program.cs) | `int Main`, `DebugOverlay.WriteLine`, integer arithmetic and standard `Int32.ToString` | Prints a greeting and 42, then shows the result screen |
| [ManagedGameCube](ManagedGameCube/Program.cs) | Cached controller snapshots, debug text and `ImmediateRenderer`; the scene belongs to `DemoScene`, not the framework | Rotating triangle; press Start on controller port 0 to exit |
| [ShaderDemo](ShaderDemo/README.md) | GPU TEV color ramp and two-stage toon shading, with a smooth reference | No assets or network; Start pauses, B exits, Z statistics |
| [HttpTexture](HttpTexture/README.md) | Download a PNG with `HttpClient`, decode in memory into tiled RGBA8 and display it | Internet-capable BBA; DHCP/DNS defaults, B exits |
| [AsyncNetworking](AsyncNetworking/Program.cs) | Standard `async Task<int> Main`, `HttpClient`, `Task.Run` and `ConfigureAwait(false)`; bounded Dolphin JSON parsing | Local DNS/HTTP fixture defaults; returns 0 when JSON `value` is 42, otherwise 1 |
| [HardwareDiagnostics](HardwareDiagnostics/Program.cs) | Safe/extended GameCube device scanning, presence status, physical reset button, controlled reboot | Controller 1 A scans, Start+X restarts, Start+Y requests menu |
| [DiscBrowser](DiscBrowser/README.md) | GC Loader SD mount, read-only GameCube ISO FST, original-game FST on DVD interface | GC Loader-compatible firmware or original DVD/ODE optional; skips unavailable devices |
| [Kart](Kart/README.md) | Original 3D island kart racer: drift boosts, gliding jump, items, three laps and two-player UDP peer discovery | Immediate offline play; optional LAN BBA with DHCP, UDP 28777 |
| [Fox](Fox/README.md) | Minimal polygon rail shooter: 3D flight, twin lasers, boost/brake, barrel rolls, shield rings and a boss | One controller; generated geometry/audio, no external assets |
| [GreenMen](GreenMen/README.md) | Space Invaders-style arcade game: marching pixel aliens, destructible bunkers, waves, three lives and synthesized sound | One controller; no external assets or storage required |
| [Snake](Snake/README.md) | Classic Snake with Continue/New Game title menu, checked native memory-card saves and two-minute autosave | GameCube memory card A or B recommended; works without card but cannot persist |
| [Pong](Pong/README.md) | Complete two-player Pong: controller input, delta-time physics, 2D rectangles, generated PCM8 sounds | Self-contained GameCube DOL; two controllers, no storage/asset files |
| [AudioDemo](AudioDemo/Program.cs) | AESND WAV effects and looping PCM music with async loading | Mount an SD device containing `audio/laser.wav` and `audio/theme.wav` (PCM WAV) |
| [GoogleLogoAram](GoogleLogoAram/README.md) | Streamed HTTP to `AramStream` with standard `Stream.CopyToAsync`; partial-read-safe PNG signature verification | Run the PC relay and configure its LAN address; returns 0 on success, otherwise 1 |

## Shared controls

In every GameCube sample, press **Z on controller 1 (port 0)** to toggle the
statistics panel, including its network and debugger status. Holding Z toggles
only once; release and press again to toggle back. In Dolphin, use the keyboard
or gamepad input mapped to the GameCube **Z button** in Controller Settings.
ManagedGameCube starts with statistics visible; the other samples start hidden.

When a sample's managed Main finishes, the native launcher keeps its result
screen open: Z still toggles statistics and **Start** exits. Async demos reach
this screen after their operation completes or fails. Games retain their existing
controls during play. The shared `Directory.Build.props` enables this
behavior with `DndSampleControls`; set it to `false` to restore immediate exit.
Applications outside `samples` keep their existing input and exit behavior.

## Build a GameCube DOL

Every sample has a solution named after its directory, such as
[Snake/Snake.sln](Snake/Snake.sln) or [Pong/Pong.sln](Pong/Pong.sln).
Install .NET 8, Dolphin, and Docker with Linux containers, or a native
Linux/macOS devkitPPC/libogc2 toolchain; see [IDE setup](https://github.com/capz/DolphinDotNet/blob/main/docs/ide-workflow.md).

- **Visual Studio:** open the sample's `.sln`, select its executable as the
  startup project, choose **Dolphin Play**, and press **Ctrl+F5**.
- **Rider:** open the sample's `.sln`, disable **Use ReSharper Build**, select
  **<sample> in Dolphin (play without debugger)**, and click **Run**.

Choose any sample directory name from the table for the CLI:

```sh
sample=ManagedGameCube
dotnet build "$sample/$sample.sln" -c Release
dotnet run --project "$sample" -c Release --no-build
```

Build produces `<sample>/bin/Release/net8.0/<sample>.dol` and the
matching `.elf`. Every real build regenerates and fully rebuilds native code.
Omit `--no-build` to build and launch in one command. Each sample preserves its
own Dolphin settings in `obj/dolphin-user/`. Configure controllers, storage and
networking as required by the table above; building does not provision external
assets or network services.

## Debug builds and Rider attach

All samples use the shared DolphinDotNet SDK and compiler. Debug builds preserve
Portable PDB locals and publish the source/address maps Rider needs by default;
Release builds retain their normal optimization policy. Sample projects follow
those shared defaults instead of forcing optimization in Debug.

From the repository root, substitute any sample name from the table:

```powershell
dotnet build Snake/Snake.sln -c Debug
python scripts/validate_sample_build.py samples/Snake
```

The validator checks the DOL, PowerPC ELF, launcher, LF-only bundled shell script,
matching map hashes and verified breakpoint addresses in the sample's own
`Program.cs`. A successful build must produce these files together:

```text
<sample>/bin/Debug/net8.0/<sample>.dol
<sample>/bin/Debug/net8.0/<sample>.elf
<sample>/bin/Debug/net8.0/<sample>.dnd-debug.json
<sample>/bin/Debug/net8.0/<sample>.dnd-addresses.json
```

In Rider, open that sample's solution. Its existing **<sample> in Dolphin**
configuration now launches with GDB enabled and waits for Rider; **<sample> in
Dolphin (wait for Rider)** is an equivalent alias. To play normally, select
**<sample> in Dolphin (play without debugger)**. Debug **CLI** / project launches
also wait on port 2166; Release CLI launches run normally. For debugging, install
the [Rider attach plugin](https://github.com/capz/DolphinDotNet/blob/main/docs/rider-debugging.md),
run **<sample> in Dolphin**, then choose **Run → Attach to DolphinDotNet…**. That launch creates
the emulator's GDB settings automatically in a separate profile, prints their
location, and waits for Rider at **127.0.0.1:2166**. No manual `Dolphin.ini` edit
is needed. If the configuration is not imported, create a **.NET Launch Settings
Profile** configuration for that sample's **Dolphin Debug** profile with a Build task.
Choose `<sample>/bin/Debug/net8.0` as its debug build folder, then save
and select a TCP target matching Dolphin's host and GDB port. When opening the
sample's solution directly, the folder is `$PROJECT_DIR$/bin/Debug/net8.0`.
Click **Attach** in the dialog. C# gutter breakpoints recognize DolphinDotNet SDK
projects automatically; selecting an attach run configuration is unnecessary.
Existing attach configurations remain supported for multilaunch setups.

To build and validate every sample in PowerShell:

```powershell
Get-ChildItem */*.sln | ForEach-Object {
    dotnet build $_.FullName -c Debug
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $($_.Name)" }
}
python scripts/validate_sample_build.py
```

Run this validation locally after native sample builds. Windows/macOS CI builds
the managed sample solutions and verifies their SDK imports, launch targets and
evaluated Debug/Release settings; it does not build native DOLs or boot Dolphin.

The shared build fixes apply to every sample: Windows selects `docker.exe`, native
cleanup handles read-only Git metadata and retains ownership on failure, emitted
Bash scripts use LF, and full Windows PDB filenames survive native address mapping.
See [build troubleshooting](https://github.com/capz/DolphinDotNet/blob/main/docs/ide-workflow.md) for restore errors and recovery
from partial directories left by older drivers. Keep `obj/project.assets.json`
when removing a partial `obj/Debug/net8.0/native` directory. `dotnet build` restores
packages automatically. `DndDebugSymbols=false` is an explicit opt-out from maps.

This verifies compilation and debugger artifacts. Each sample still needs the
controllers, storage, assets or services listed above; interactive Rider and
physical console acceptance remain separate checks.

## Package-based builds

The repository uses a versioned MSBuild SDK from NuGet and has **no** local source-project references. From its root:

```sh
dotnet restore Pong/Pong.sln
dotnet build Pong/Pong.sln -c Release
dotnet build Fox/Fox.sln -c Debug
```

The first build restores the SDK/runtime packages, runs the managed C# compiler and builds the GameCube `.dol` with Docker or devkitPPC. The CI workflow builds the managed assemblies only (`-p:DndBuildNative=false`) to avoid requiring an emulator/toolchain for every PR.

To inspect the generated application without building a DOL, use `dotnet build Pong/Pong.sln -p:DndBuildNative=false`. To use a newer preview, update **both** `global.json` and the `DndPackageVersion` in `Directory.Build.props`.

For SDK and native toolchain documentation, see [NuGet SDK](https://github.com/capz/DolphinDotNet/blob/main/docs/nuget-sdk.md) and [standalone builds](https://github.com/capz/DolphinDotNet/blob/main/docs/standalone-native-builds.md).

## Async and platform APIs

The native bootstrap starts the service thread and joins it on exit. Applications
use ordinary awaits; no `Scheduler.Pump` or `NetworkRuntime.Pump` calls are needed.
Default pending awaits capture the current executor. `Task.Run` uses the background
executor and `ConfigureAwait(false)` opts out of capturing the application executor.
Managed execution remains serialized. Keep graphics/input work on the application
executor; see the [async runtime contract](https://github.com/capz/DolphinDotNet/blob/main/docs/async-runtime.md).

Platform operations still need explicit initialization: networking samples await
`NetworkRuntime.StartAsync`, and the ARAM sample initializes `Aram`. Resource owners
use `using` and networking stops in `finally`.

These focused examples do not cover every feature. Models, animation, scene
transforms, cameras and ARAM residency use the separate
[model API](https://github.com/capz/DolphinDotNet/blob/main/docs/model-assets-runtime.md); the triangle intentionally uses
immediate rendering without an external model asset. ManagedGameCube enables the
[performance overlay](https://github.com/capz/DolphinDotNet/blob/main/docs/debug-overlay.md) and displays
`csharp with dolphin dotnet` above the scene.
Consult the [API inventory](https://github.com/capz/DolphinDotNet/blob/main/docs/dotnet-standard-status.md) before adding desktop
.NET APIs: TLS, `ValueTask` and `System.Text.Json` are not supported.

## Networking setup and checks

AsyncNetworking defaults match `tests/network_fixtures.py`: DNS on loopback port
15353 and `http://config.test:18081/json`. For hardware, change `ConfigUrl`,
`DnsPort` (usually 53) and `DnsServers` to reachable LAN services. Loopback on a
console does not refer to the development PC. HTTP currently has no TLS support.

CI compiles all samples. It checks HelloGameCube's AOT output,
records ManagedGameCube's complete frame and Start-button exit through a host
backend, and executes AsyncNetworking against the DNS/HTTP fixture with normal
and stressed GC. AudioDemo is compiled through AOT; GoogleLogoAram is compiled through AOT; its live relay and console
network/ARAM operation need hardware testing.

## Sample regression tests

The sample-specific host tests from the original [DolphinDotNet repository](https://github.com/capz/DolphinDotNet) now live under [tests](tests).
They cover gameplay and generated audio for Pong, Snake (including saves and card hotplug), Fox,
GreenMen and Kart; Kart input/items/networking; DiscBrowser navigation; and shared
network-download and memory-card-record helpers. The tests compile the corresponding
sample source files directly and, where necessary, resolve `DolphinDotNet.Core`
or `DolphinDotNet.GameCube` from the same pinned preview NuGet release.

Run all sample behavior tests without the GameCube toolchain:

```sh
python3 ci/validate_sample_tests.py
for project in tests/*/*.csproj; do dotnet run --project "$project" -c Release || exit 1; done
```

CI executes nine CLR-compatible host suites and verifies that the two AOT-native
Kart input/networking harnesses compile against the preview package. The full Kart
native-input and UDP fixtures must be run through the DolphinDotNet toolchain's
emulator/host harness, since the published GameCube package rejects native
intrinsics when executed under the desktop CLR.

CI runs these tests separately from the fifteen managed sample builds.
Compiler runtime, native GX, PowerPC, and emulator integration tests remain
in the [DolphinDotNet runtime repository](https://github.com/capz/DolphinDotNet),
where their internal native sources and fixtures are maintained.
