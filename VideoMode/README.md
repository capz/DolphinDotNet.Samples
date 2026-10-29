# VideoMode

A small, asset-free video configuration menu. Changes run between device frames,
using the SDK's `GraphicsDevice.ConfigureDisplay`.

- D-pad up/down: select resolution, scan mode or widescreen.
- D-pad left/right: change the selected value.
- A: apply; press A again within 12 seconds to keep the mode.
- B during preview: restore the previous confirmed mode.
- X at any time: use 640x480 interlaced, 4:3.
- Start: exit (revert an unconfirmed preview first).
- Z: toggle the shared statistics overlay.

Modes are 320x240, 640x480 and 640x576 with Auto, Interlaced or Progressive scan.
Unsupported combinations and insufficient framebuffer memory leave the current
mode active and show an error. 240p double-strike works on analog cables; 240i is
unsupported. 480p/576p require the component/DTV signal and a compatible display.
Auto scan is resolved by the SDK; rollback remembers the resolved mode.

The screen starts in the boot video mode with the SDK's default 4:3 rendering.
Widescreen must be selected explicitly. It changes rendering only; output size
and timing stay the same when only the widescreen row changes. Set the TV to the
matching 4:3 or 16:9 aspect. The square and circle should retain their proportions;
the right edge marker follows the expanded logical canvas automatically.

The menu uses the default display-correct `Draw2D` canvas and scales its layout
for 240-line rendering. The active output and render dimensions are displayed
separately; PAL 576 modes use a 640x480 EFB scaled to a 640x576 output buffer.
On an unusual boot mode outside the three standard presets, revert uses the
corresponding standard mode (or 640x480) rather than preserving custom VI timings.

This sample requires the display APIs introduced by
[SDK PR #134](https://github.com/capz/DolphinDotNet/pull/134). Published preview
`0.1.0-preview.15.1` predates them. Before the normal package-based build, publish
an updated SDK preview and update `global.json` and `DndPackageVersion` in both
root and test `Directory.Build.props`, plus the pinned validator version.

Build and launch from the repository root after that dependency update:

```sh
dotnet build VideoMode/VideoMode.sln -c Release
dotnet run --project VideoMode -c Release --no-build
```

No storage, network services, downloaded fonts or external assets are needed.
The 12-second revert runs even with a disconnected controller. If reverting cannot
allocate replacement buffers, it retries once per second; X can request the safe
mode. The sample does not save settings across launches.
