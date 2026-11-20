# Controller input and hotplugging

An original indigo GameCube controller is rendered as a solid 3D model with
separate rigid controls. Both sticks pivot, the D-pad rocks, face buttons and
Start depress, Z moves, and L/R follow analog travel with a distinct full-click
position. The controller proportions and camera follow the supplied hardware
reference and [approved mockup](Design/approved-mockup.png). This PNG is a design
reference, not a screenshot of the application.

The sample reads all four physical ports once per frame. Input activity selects
the controller shown on screen; held buttons or stick drift do not repeatedly
steal selection. Connected port indicators and the latest connection event show
real SDK hotplug state. Removing the selected controller chooses another
connected port, or returns to a neutral model when none remain. A held control
on connection establishes a baseline rather than generating a false press.

All twelve digital inputs, both sticks, analog L/R values and separate digital
L/R clicks remain available to test. The displayed analog values include the
SDK's deadzone/calibration policy; they are not raw ADC readings. The sample
opts out of the shared Z statistics shortcut. Release Start and Z, then hold
both for two seconds to exit. Switching or disconnecting a port cancels the
exit gesture.

Geometry, label meshes and UI chrome are created once. Control animation changes
transforms; changed telemetry updates two bounded meshes. The licensed Nunito
atlas is shared with VideoMode, with its existing SIL OFL notice. No original
Nintendo firmware, game assets or downloaded model is required at runtime.
The project requests a bounded 2 MiB managed arena through the published SDK's
`DndManagedHeapBytes` setting. Startup temporarily owns list storage and upload
arrays together; the default 256 KiB arena cannot hold even the body upload.
This arena reservation is separate from native GX geometry and texture bytes.

The checked-in shell surface shares interior vertices and preserves the exact
silhouette boundary. Its offline generator verifies the boundary, area,
manifold edges and interpolation error before emitting data; it is not part of
normal builds. With NumPy and SciPy 1.17.0 installed, a managed build can be
checked without running any graphics intrinsics:

```sh
python3 ControllerInput/Tools/generate_surface.py --check
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

Actual native screenshots, recording, performance and memory measurements are
recorded separately from the mockup. Dolphin acceptance does not establish
physical-controller timing or original-console rendering performance.
