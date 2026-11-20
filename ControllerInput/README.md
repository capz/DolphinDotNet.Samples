# Controller input and hotplugging

The [approved mockup](Design/approved-mockup.png) supplies the fixed shell,
background and static UI. A shared texture from the original mockup is mapped
onto real 3D controls using the same neutral camera projection. Both sticks pivot, the
D-pad rocks, face buttons and Start depress, the single right-side Z moves, and
L/R follow analog travel with a distinct full-click position. A shell depth mask
hides control bases behind the socket rims while allowing raised caps to protrude.
The camera stays fixed so the moving controls remain registered to the artwork.

The sample reads all four physical ports once per frame. Input activity selects
the controller shown on screen; held buttons or stick drift do not repeatedly
steal selection. The four port indicators and current connection status reflect
SDK hotplug state. Removing the selected controller chooses another connected
port, or returns the controls to neutral when none remain. A held control on
connection establishes a baseline rather than generating a false press.

All twelve digital inputs, both sticks, analog L/R values and separate digital
L/R clicks remain available to test. The displayed analog values include the
SDK's deadzone/calibration policy; they are not raw ADC readings. The sample
opts out of the shared Z statistics shortcut. Release Start and Z, then hold
both for two seconds to exit. Switching or disconnecting a port cancels the
exit gesture.

Geometry and textures are uploaded once. Control animation changes transforms;
changed telemetry updates two bounded meshes. The licensed Nunito atlas is
shared with VideoMode, with its existing SIL OFL notice. The shell depth mask
uses black additive geometry to write depth without changing the plate's color.
No firmware resources, external models or network downloads are needed at runtime.
The project reserves a bounded 4 MiB managed arena through the published SDK's
`DndManagedHeapBytes` setting for texture staging and startup construction. This
arena reservation is separate from native GX geometry and texture bytes.

`Design/clean-plate-source.png` is an inpainted version of the approved artwork
with moving controls and changing UI fields removed. The offline asset cooker
composites its functional cutouts into the original artwork, preserving the
unmasked source pixels. It resamples to 640×480, cooks the two GX textures and
emits the checked-in C# asset factories and provenance hashes. Normal builds use
these checked-in assets and do not run image generation or require an asset tool.
To regenerate after editing the source artwork, install Pillow and the matching
published asset tool, then run from the repository root:

```sh
dotnet tool install DolphinDotNet.Assets --version 0.1.0-preview.361.1 --tool-path .tools/controller-assets
python3 ControllerInput/Tools/cook_assets.py --tool .tools/controller-assets/dnd-assets
```

Build against the repository's pinned published NuGet packages:

```sh
dotnet build ControllerInput/ControllerInput.sln -c Release
dotnet run --project ControllerInput -c Release --no-build
```

The output is `ControllerInput/bin/Release/net8.0/ControllerInput.dol` with its
matching PowerPC ELF. Portable production-linked input tests run with:

```sh
dotnet run --project tests/ControllerInputTests -c Release
```

The controller native CI job also verifies real emulator input, SI-device
hotplugging on all four ports, neutral state after unplugging, trigger detents
and the exit gesture. Its `controller-input-native-review` artifact contains the
DOL/ELF, original native screenshots, a recording and capture/profiling reports.
The design images are source artwork, not application screenshots. Dolphin
measurements do not establish physical-controller timing or rendering performance
on an original console.
