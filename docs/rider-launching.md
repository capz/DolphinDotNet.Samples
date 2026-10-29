# Run and debug in Rider

Install **DolphinDotNet Rider plugin 0.4.0 or later**, then open a sample's solution.
Every sample includes exactly these three shared configurations:

| Configuration | Action |
| --- | --- |
| **Run in emulator** | Build the sample and launch Dolphin for normal play. Use **Run**. |
| **Debug on emulator** | Build, launch a waiting local Dolphin instance, attach automatically, install breakpoints and continue. Use **Debug**. |
| **Debug on target** | Build for the first saved target, upload the ELF through the DolphinDotNet launcher, attach, install breakpoints and continue. Use **Debug**. |

The plugin runs the native build itself; do not add a separate Build before-run
task. It discovers the project's output folder after building, so no initial
build or machine-specific output path is required. Build failures stop the launch.
Docker or devkitPPC must be set up as described in the repository README.

For emulator actions, set `DOLPHIN_PATH` or `DolphinPath` in the ignored
`DolphinDotNet.local.props`, or set the optional Dolphin executable field in a
local copy of the configuration. Emulator debugging always uses local Dolphin;
it does not use the saved hardware target. It disables the console GDB stub even
if local properties enable USB Gecko. Stopping emulator debugging closes only
the Dolphin instance started by that launch.

For hardware, open **Run → Attach to Nintendo GameCube… → Manage targets** and
configure the target you want to use first. USB Gecko needs its COM / serial port
and console slot. The plugin builds channel 0 for slot A or channel 1 for slot B.
For BBA / TCP, set the console's IPv4 address and application GDB port; configure
`DndGdbNetmask` and `DndGdbGateway` in local properties if your network needs them.
The ELF receiver uses upload port 4299, separate from the application GDB port.

Boot the `DolphinDotNet.Launcher.dol` bundled with the plugin on the console before
using **Debug on target**. Stopping the hardware debugger leaves the game running;
exit the game back to the resident receiver before another upload.

The status bar shows **Target GameCube available** when the first saved target's
ELF launcher answers a readiness check, or **Target GameCube not available** when
it does not. Polling pauses during connections and debug sessions. The indicator
appears only in projects referencing DolphinDotNet.

The SDK's `launchSettings.json` profiles remain available for Visual Studio and
command-line workflows. The three `.run` files are the Rider launch menu.
