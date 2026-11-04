# Six-pass mockup comparison, native passes 30–35

Continues commit `68cb0548d07785152a7cf1b18ec4cdcaca5ecf91` against the
[exact mockup](optical-glass-target.png). All implementation images are native
GameCube DOL frame dumps from cloud-local Dolphin/OpenGL with 4× emulator MSAA.
The 640×528 raw video pixels must be viewed at the console's 4:3 display aspect
when comparing positions with the 1448×1086 mockup. No screenshot is edited.
Each refinement is committed with its native capture before the next change.

## Pass 1/6 — native pass 30

![Pass 30](pass-30-native.png)

Adjusted projected width/center, panel width, label/value anchors and selected
text growth. These bring layout and focus hierarchy closer to the mockup. The
material still reads as a thin frame, with insufficient optical wall depth.

## Pass 2/6 — native pass 31

![Pass 31](pass-31-native.png)

Deepened the optical cross-section and offset its transmitted inner boundary.
The wall reads thicker and less symmetric. The inner boundary is still too
uniformly blue, and corner highlights remain weaker than the mockup.

## Pass 3/6 — native pass 32

![Pass 32](pass-32-native.png)

Added localized layered polish, a third optical cross-section lobe and restrained
chromatic separation. Increased reflection exposure and enlarged a studio light.
The uniform blue frame is subdued and corners are brighter. Left polish still
outweighs the right, and the face lighting is darker/browner than the target.

## Pass 4/6 — native pass 33

![Pass 33](pass-33-native.png)

Rebalanced and localized transmitted teal/amber/violet light, and repositioned
the upper-right reflected studio light. At fixed corresponding unblurred patches,
upper-right native RGB is `(60,118,110)` versus target `(63,119,111)`; left amber
is `(95,52,17)` versus `(95,55,21)`. These local samples do not imply a global
image match. Row frost is still too pale/grid-like and the gear is too low.

## Pass 5/6 — native pass 34

![Pass 34](pass-34-native.png)

Raised/reshaped the gear, warmed its purple tone, moved the title down and
introduced a wider weighted blur with less opaque row tint. Title/gear placement
is closer, but the sparse 25-tap wide kernel exposes a repeated sampling grid.
The final pass must address this regression rather than treating it as accepted
frost quality.
