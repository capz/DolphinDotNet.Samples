# Rounded-corner native review

The reference remains the [exact mockup](optical-glass-target.png). Its large
outer arcs and layered curved inner walls are visibly round at every corner.
The [previous accepted front](pass-35-native.png) used a 0.075 rounded-solid
radius and 0.075 optical-wall radius, which read almost square at native size.

The first correction used radius 0.12 (flat core 0.88), while optical walls used 0.16 at
the outer boundary, tapering to 0.145 inside. The same 625-vertex face grids and
12 subdivisions per wall quarter-arc are retained. No managed buffers, textures,
or native meshes are added. Localized reflected lights expose the upper-right
and lower corner returns as well as the existing upper-left polish.

## Native comparisons

All implementation images below are unmodified GameCube DOL frame dumps from
cloud-local Dolphin 2503/OpenGL, 4× emulator MSAA. Raw 640×528 frame pixels are
preserved; compare their composition at the console's 4:3 display aspect.

The geometry-only [front trial](pass-42-native.png) and [angle trial](pass-43-native.png)
show broader arcs, but the lower/right corners remain too dark to convey their
curvature as clearly as the mockup. The second iteration adds localized corner
reflections without a continuous outline.

![Final native front](pass-44-native.png)

![Final native angle](pass-45-native.png)

The broader silhouettes and curved wall returns are more visible in both poses.
The mockup still has finer refracted layering and stronger optical detail; this
change does not establish an exact rendering match. The procedural rotating cube,
row frost, controller flow and video preview/rollback are retained.

## Validation

The front and angle review DOLs both build and render natively. The normal
interactive DOL was then rebuilt and [booted in Dolphin](corners-interactive-native.png).
`VideoModeMenuTests`, managed VideoMode build, three repository validators,
native AOT/DOL/PowerPC ELF checks and `git diff --check` pass. The normal native
build has four existing toolchain warnings and no errors. Capture checksums are
recorded in [the native manifest](native-captures.sha256). Hardware frame rate
and physical-display acceptance remain unmeasured.

## Stronger 3D roundover — current

The follow-up doubles the actual rounded-solid radius from 0.12 to **0.24**,
with flat core 0.76. Optical-wall corners increase from 0.16 to **0.28**,
tapering to 0.265 inside. These are geometry changes affecting the silhouette,
surface normals and view-dependent reflections, rather than brighter outlines.
The existing vertex grids, corner subdivisions, textures and native mesh counts
are retained.

![Stronger native front roundover](pass-47-native.png)

![Stronger native angled roundover](pass-48-native.png)

Compared with passes 44/45, the outer silhouette and inner wall returns have
substantially broader arcs. The larger roll also makes the face shoulder more
pronounced. This follows the request for significantly more visible rounding;
it does not claim an exact match to the mockup's finer optical treatment.

[Normal interactive native boot](roundover-interactive-native.png). The updated
front, angle and normal native builds, menu/scan tests, managed build, repository
validators and native AOT/DOL/PowerPC ELF checks pass. Raw captures remain
unmodified, with hashes in the native manifest.
