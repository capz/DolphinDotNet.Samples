# Rider upload and debugging

Every sample includes **<sample> on GameCube (ELF launcher and debug)** alongside
its emulator play and wait-for-debugger configurations. Install DolphinDotNet's
Rider plugin 0.3.2 or later, and boot the launcher DOL bundled with that plugin on
the console before uploading.

Configure the sample's Debug build for the same GDB connection as the saved Rider
target. For example, add the ignored `DolphinDotNet.local.props` beside the sample's
project for a USB Gecko in slot B:

```xml
<Project>
  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <DndGdbTransport>UsbGecko</DndGdbTransport>
    <DndGdbChannel>1</DndGdbChannel>
  </PropertyGroup>
</Project>
```

Channel 0 selects slot A; channel 1 selects slot B. Remove these console-specific
settings before running the emulator profiles. TCP targets instead need the
console's `DndGdbTransport=Tcp`, IP/netmask/gateway and application GDB port.
The receiver upload port defaults to 4299 and must differ from its GDB port.

Build the sample in Debug once so its ELF and matching debug maps exist. In
**Run → Edit Configurations**, select the GameCube configuration and choose a
saved target through **Manage targets**. USB targets require the correct COM /
serial port and expected console slot. The shared configuration intentionally
contains no machine-specific target ID; keep your configured copy local by
turning off **Store as project file** after choosing the target.

Keep **Start on device → DolphinDotNet ELF launcher (BBA / USB Gecko)** selected.
Check receiver readiness, then **Debug** the configuration. Its build runs before
the plugin uploads the ELF, starts the application and attaches the debugger.
Press **Resume** if it stops at entry. Stopping the debugger leaves the application
running; exit the sample back to the resident receiver before another upload.
