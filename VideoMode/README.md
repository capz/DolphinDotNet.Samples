# VideoMode: IPL-style native shell

A native, controller-first system-menu demonstration using the published
DolphinDotNet SDK. The visual target is the [committed optical-glass mockup](Design/optical-glass-target.png),
inspired by the Nintendo GameCube IPL: a dark translucent cube floating on black,
layered optical walls, face menus and a bottom controller legend. Flat configuration
screens use green block-built values and a fading grid.
The rounded cube is projected 3D geometry, not a screenshot or rotating image.

Glass uses a rounded-box surface with a visible curved edge profile and
a sampled, view-dependent reflection layer. Front labels sit inside the
rounded rim. Each face has its own subdued beveled-block icon. List rows have
rounded frosted backplates, fixed left/right text anchors and 180 ms focus
scaling (1.00 to 1.30), including shrink-back on the outgoing row.

The native frost is a real backdrop operation: after the cube and icon are
drawn, GX captures the EFB at half resolution. A dense 9×9 Gaussian-weighted kernel samples that
snapshot inside projected rounded row meshes, then tint and crisp
text are drawn on top. Texture allocation and geometry uploads happen between
frames. The host SVG proof approximates blur with an SVG filter and omits native reflection
textures; only a native capture verifies the GX result. The 81 frost draws reuse
one managed vertex buffer and snapshot, with separate native meshes. Hardware
performance has not been measured.

[Six-pass native review and mockup comparisons](Design/six-pass-review.md).
[Rounded-corner correction, front and angle captures](Design/corner-review.md).

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

To switch **480i / 480p**, open Video with right, select **Scan**, press A,
and use up/down to choose **Interlaced** or **Progressive**. A starts the preview;
A again keeps it, or B reverts. Progressive requires a compatible cable/display.
In Dolphin, enable its progressive/component-cable setting (`SYSCONF.IPL.PGS=True`);
without that capability the native API rejects progressive and preserves the
current mode. X always requests 480i, 4:3.

Repeated 576i/576p switching stalled in the pinned native runtime with cloud
Dolphin 2503. Normal builds therefore reject 576p before changing output, and
resolve 576 Auto to interlaced. This is a sample qualification guard, not a claim
that the console cannot produce 576p. [Native scan-mode evidence, commands and
limitations](Design/scan-mode-review.md) document the passing 480 sequence and
the incomplete 576 sequence. Physical console/cable/display testing remains open.

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

## Dolphin antialiasing

Native builds configure this sample's isolated Dolphin play and debug profiles
with **4× MSAA** (`GFX.ini`: `[Settings] MSAA = 4`, `SSAA = False`). Use a Dolphin
hardware backend supporting MSAA, such as OpenGL or Vulkan. Other graphics and
controller preferences are preserved. Build with
`-p:IplEmulatorAntialiasing=false` to stop enforcing these keys and manage AA in
Dolphin yourself; this does not erase settings already written to the profile.

This is emulator antialiasing, not a hardware-AA mode in the DOL. The pinned SDK
has no public hardware-AA display option. Its display, preview/revert and
safe-mode behavior remain unchanged. [Captured comparison](Design/antialiasing-review.md).
