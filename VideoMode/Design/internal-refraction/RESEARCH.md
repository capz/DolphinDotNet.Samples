# IPL glass research and implemented approximation

## What the public sources establish

[GC-anim-Wii](https://github.com/supertazon/GC-anim-Wii) is a first-hand
recreation based on IPL asset extraction. Its author identifies an environment
texture used by several intro models and describes tinting a grayscale image
through TEV. The [recreation's cube code](https://github.com/supertazon/GC-anim-Wii/blob/main/source/cubes.c)
uses normal-generated texture coordinates. The author explicitly reports
differences from the original projection and color. This supports environment
mapping as an IPL technique; it does not establish the exact interactive-menu
glass material or prove that its lights move.

[Factor 5's GameCube shader article](https://www.gamedeveloper.com/programming/shader-integration-merging-shading-technologies-on-the-nintendo-gamecube)
describes environment/specular maps, generated coordinates and TEV lighting
composition in shipped GameCube work. It establishes hardware capability, not
the IPL's implementation. A stationary environment can produce moving
reflections when object normals or the texture matrix rotate.

[gc-ipl](https://github.com/ogamespec/gc-ipl) is an independent IPL reverse
engineering project, but does not provide a complete named glass-material
reconstruction. The [gcnlle FIFO inventory](https://github.com/mstan/gcnlle/blob/master/docs/GX_INVENTORY.md)
records real GX state; its observed output is the large glossy purple G logo,
not a verified capture of the interactive glass-menu scene. Its observations
must not be generalized to that scene. An exact answer would require a FIFO
capture at the menu and inspection of its texture, texgen, TEV, indirect and
lighting state. No original IPL assets or ROM are included in this change.

## What this application now does

The closed outer shell and reversed cavity remain static indexed geometry.
Rear interfaces draw first, then the centered block icon, then the front wall.
One live EFB copy between the icon and front wall makes the icon part of the
transmitted image instead of painting it over the glass afterward.

A native lookup evaluates Snell displacement through a locally parallel wall
with index 1.50. Its scale follows the actual 0.14-unit wall thickness and
current projection/output size. Normal incidence has zero displacement;
oblique views shift the sampled rear scene. A second sample at three wall
traversals adds one internal round-trip return, weighted by single-interface
Fresnel reflectance squared. The final blend combines transmission with the
existing artist-authored procedural environment and exposed Fresnel gloss.
The two-path blend is an approximation, not an exact infinite optical series.

[PBRT's dielectric model](https://pbr-book.org/4ed/Reflection_Models/Dielectric_BSDF)
provides the Fresnel/Snell basis and explains repeated internal interactions
between parallel interfaces. This renderer does not trace rays through the
whole volume, solve curved-corner intersections, simulate caustics, or model
total internal reflection at every interface. It cannot reproduce all the
mockup's optical detail with these two screen-space transmission samples.

The added lookup occupies 64 KiB of native texture memory, not the 256 KiB
managed arena. It is built once. Geometry still takes three indexed draws
and glass still takes one EFB capture; the front material grows from three to
five TEV stages. Vertex data is not rebuilt or uploaded per frame. The extra
sample's cost must be judged on real hardware; Dolphin timings are evidence
only for the tested emulator configuration.

## Visual limits

Native evidence shows displaced rear-wall highlights and the centered icon
behind the front material. The approved mockup still has substantially richer
corner returns, a brighter upper-left corner and more varied hard gloss.
Our broad sparse highlights and different perspective remain visible gaps.
An authored, better matched environment and a more complete corner optical
model are separate follow-up work. This change is not a pixel-perfect match
or a claim to have recovered Nintendo's shader.
