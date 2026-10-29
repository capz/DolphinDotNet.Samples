# VideoMode: IPL-style native shell

A native, controller-first system-menu demonstration using the published
DolphinDotNet SDK. The visual target is the Nintendo GameCube IPL: a dark,
translucent rounded cube floating on black, thin glossy edges, edge-mounted
labels, green block-built values, a fading grid and a bottom controller legend.
The rounded cube is projected 3D geometry, not a screenshot or rotating image.

Glass uses a rounded-box surface with a visible curved edge profile and
per-vertex interpolated reflection highlights. Front labels sit inside the
rounded rim. Each face has its own subdued beveled-block icon. List rows have
rounded frosted backplates, fixed left/right text anchors and 180 ms focus
scaling (1.00 to 1.14), including shrink-back on the outgoing row.

The native frost is a real backdrop operation: after the cube and icon are
drawn, GX captures the EFB at half resolution. Nine offset samples of that
snapshot are averaged inside projected rounded row meshes, then tint and crisp
text are drawn on top. Texture allocation and geometry uploads happen between
frames. The host SVG proof approximates the same blur with an SVG filter and
subdivides the native interpolated glass shading; only a native capture verifies
the GX result.

## Controller flow

- At the cube: right opens Video, left Options, up Game Play, down Calendar.
- On a face: up/down selects a row; A opens its flat configuration screen.
- In a configuration screen: up/down changes the draft value; A confirms;
  B discards and returns to the same face and row.
- In Time/Date: left/right selects an independent segment; up/down changes
  only that segment. Day counts follow the selected month and leap year.
- B from a face returns to the cube. Memory Card returns to Options first.
- Both the analog stick and D-pad work, with delayed directional repeat.
- X requests 640x480 interlaced, 4:3. Start exits after safely reverting a preview.
- Options > Motion disables idle floating and turning overshoot.

Video settings use real `GraphicsDevice.ConfigureDisplay` calls between frames.
A starts a 12-second hardware preview; A again keeps it, B or the timeout reverts
it. Unsupported modes/cables and allocation failures leave the current mode
active. Rollback allocation failures remain pending and retry once per second.
Widescreen-only changes preserve timing and buffers. The UI uses display-correct
logical coordinates and centers its 4:3 composition inside a widescreen canvas.

Time, date and sound are explicitly session-only interaction demonstrations.
They do not write the console RTC or system SRAM. Game Play and Memory Card are
honest empty demonstration faces, not a disc launcher or card manager. The sample
does not include Nintendo firmware, fonts, logos or game-save artwork.

## Build and validation

```sh
dotnet build VideoMode/VideoMode.sln -c Release
dotnet run --project tests/VideoModeMenuTests -c Release
```

The repository's native CI also AOT-compiles and links this sample into DOL/ELF
using the compiler and native support in NuGet preview `0.1.0-preview.103.1`.
Hardware/TV acceptance and visual comparison inside Dolphin remain separate
from successful compilation; do not treat host visual proofs as emulator captures.

To inspect deterministic visual proofs from the exact shared C# scene renderer:

```sh
dotnet run --project tests/VideoModeMenuTests -c Release -- --proof /path/to/proofs
```

Open the generated `index.html`. It exports the cube, Video face, flat aspect
selector and independent date segments. SVG is only the host inspection adapter;
the application uses native Draw2D triangles and a bundled glyph atlas.

Typography is an original atlas generated from Nunito weight 800, licensed under
SIL OFL in `Assets/OFL-Nunito.txt`. It is a close substitute, not the IPL font.
A pixel-identical reproduction and complete platform input/hardware acceptance
must not be claimed without a captured native run against the reference images.
