# Native UI-sound integration review

The [action index](action-index.md) records the ten original IPL effects and
their controller actions. UI Sounds adds Off / Low / Medium / Full, defaults to
Medium, previews drafts and saves confirmed values. This review exercises its
four-row Options layout, draft/commit/cancel paths and Storage navigation.

## Native frames

All eight images are completed GameCube DOL output from cloud-local Dolphin
2503, OpenGL/Mesa software rendering and Xvfb. Each 614400-byte GX_CopyDisp XFB
was read after GX_DrawDone at a completed presentation boundary. All frames
are native 640x480 interlaced VI output. The only conversion is BT.601 YUYV422
to RGB: no crop, resize, compositing or generated replacement. Source and PNG
hashes and actual native frame counters are preserved in [events](native/events.json).

- [01-options-cube-medium](native/01-options-cube-medium-xfb.png)
- [02-options-ui-sounds-row](native/02-options-ui-sounds-row-xfb.png)
- [03-ui-sounds-medium-editor](native/03-ui-sounds-medium-editor-xfb.png)
- [04-ui-sounds-off-draft](native/04-ui-sounds-off-draft-xfb.png)
- [05-ui-sounds-off-confirmed](native/05-ui-sounds-off-confirmed-xfb.png)
- [06-ui-sounds-low-draft](native/06-ui-sounds-low-draft-xfb.png)
- [07-ui-sounds-cancel-keeps-off](native/07-ui-sounds-cancel-keeps-off-xfb.png)
- [08-storage-flat-actions](native/08-storage-flat-actions-xfb.png)

Frame 03 visibly reports unavailable menu sounds; no original IPL is configured
in this Cloud emulator. The replay uses NoAudioOutput, and Dolphin reports the
unsupported AESND HLE ucode CRC `8d527c50` and forces AX. These captures verify
graceful unavailability and normal interaction. They do **not** qualify audible
output, the ten original effects, overlapping active voices or physical-console
playback. The missing GC Loader SD also means session values can change while
the app honestly reports that saving failed. Host fixtures cover persistence.

## Quiet-case timing

Both windows use the normal interactive Video cube with High blur, Subtle
motion and VSync: 30 warmup frames followed by 96 uninterrupted samples. The
current UI sound level is Medium, but IPL assets are unavailable and no effects
are mixing. No capture/debugger stop or storage write interrupts sampling.
These are Dolphin guest-clock measurements, not physical GameCube timings.

| Build | Frame ms | Active ms | Prepare ms | Submit ms | Draws / triangles |
| --- | ---: | ---: | ---: | ---: | ---: |
| Previous SDK / High / Subtle | 16.681 | 4.508 | 0.781 | 3.675 | 103 / 45234 |
| SDK 347 / High / Subtle / quiet audio | 16.681 | 4.513 | 0.781 | 3.676 | 103 / 45234 |

The active-work difference is **+0.005 ms**.
SDK and heap differences prevent isolating audio cost. Active time is frame
time minus retrace wait; copy scopes include prior GPU work. Original rows and
summaries are retained in `profile-high-subtle-medium/` and `baseline-high/`.
The current post-window arena is 2254320 of 8388608
bytes, with 0 collections. This is neither peak nor live usage.

## Build and replay

Normal runtime source: `82d6a4f00cbbf34cf394161709ce198a85ab3539`. SDK/runtime/compiler come from public
NuGet `0.1.0-preview.347.1`, source `8f5870f68ec7f5eb1e8103c13af42ce082a3516f`. Optimized cached icons, selectors,
grid, footer and numbers retain their defaults; shared glass, batched blur/text,
retained tint, console and memory-card services are enabled explicitly. The
final backend caps each effect at 2 MiB of native preparation scratch and the
retained clip bank at 6 MiB, independently of the 8 MiB managed heap. The
[budget audit](budget-audit.md) bounds the preparation scratch for every accepted
SDK recipe; the actual sum of all ten supported-ROM clips remains unqualified.
The retained cap is an intentional MEM1 ceiling with graceful cleanup on overflow:

```sh
dotnet build VideoMode/VideoMode.csproj -c Release -p:DndBuildNative=true -p:DndBuildBackend=Docker -p:IplNextSdk=true -p:IplSharedGlass=true -p:IplBatchedBlur=true -p:IplBatchedText=true -p:IplRetainedTint=true -p:IplConsoleServices=true -p:IplMemoryCardServices=true
```

The captured Cloud build ran the same optimized flags with
`DndBuildNative=false`, then the published compiler
`--bundle VideoMode/bin/Release/net8.0/VideoMode.dll VideoMode/obj/Release/net8.0/native --sample-controls`.
Its unchanged generated Makefile was linked with `make -j2` using devkitPPC/libogc2
binaries extracted from the exact pinned image into `/tmp/ui-sounds-toolchain`.
This keeps the toolchain on `/tmp` because Docker's VFS copy exceeded workspace
space during initial setup. The [toolchain record](toolchain-provenance.json)
preserves the image and layers. No SDK, generated C or native runtime was patched.
AOT and DOL/ELF checks passed.

```sh
python3 ci/review_performance_overlay.py --dol /path/to/VideoMode.dol \
  --sdk /path/to/DolphinDotNet --logs /path/to/fresh-review \
  --native-boundaries --ui-sounds-review --ui-sounds-profile \
  --quick-input --profile-seconds 90
```

Use matching DOL/ELF files from the same build. DOL SHA-256:
`8c41e4909effeda13357c74ad79420001113e26a4ff13a62773321d1d5829e3c`. [Provenance](provenance.json) includes runtime, helper, build and
frame hashes. Raw DOL/ELF/YUV/PPM and logs remain in task artifacts. No firmware
or audio asset is included in this evidence.
