# System menu sound actions

The glass-cube demo uses ten finite IPL effects exposed by published
`DolphinDotNet.GameCube` / SDK `0.1.0-preview.347.1`. The mappings apply to Video,
Calendar, Options and Storage. They describe accepted logical actions, rather
than button presses or rendered frames.

| User action | SDK `SystemMenuSound` |
| --- | --- |
| Turn from the center to a cube side, including R's Storage shortcut | `TurnSide` |
| Return from a side to the center, using B or the opposite stick direction | `TurnCenter` |
| Open a flat menu, storage browser, folder or file-information page | `Select` |
| Leave a flat page/browser, close file information, or use L for a parent folder | `Back` |
| Discard a value, cancel a dialog/transfer, revert a video preview, or report a failed action | `OptionCancel` |
| Commit settings, preview/keep a video mode, confirm a card action, or finish a verified transfer | `OptionConfirm` |
| Change a draft value, including time/date and UI sound volume | `OptionMove1` |
| Select a field/segment, change memory-card slot or grid/list view, or request a refresh | `OptionMove2` |
| Move between menu rows, files, saves or dialog choices | `OptionMove3` |
| Open a value editor, save-actions menu, launch prompt or destructive-action dialog | `OptionSelect` |

One shared cue queue selects at most one effect per input/update cycle. A failed
nested action takes precedence over opening or confirmation sounds. Accepted
copy/move requests and their later verified completion are separate events;
typed operation results distinguish completion from ordinary media probes.
Formatting retains both explicit confirmations. No separate error or
memory-card-specific effect is invented: failures reuse `OptionCancel`.

Blocked/busy input, unchanged selections, redraws, focus animation, clock ticks,
transfer percentages, artwork loads and background storage probes stay silent.
Each accepted held-direction step can sound after the existing 380 ms delay
and 130 ms repeat interval. Navigation/value effects share a 40 ms limiter;
open, confirm and cancel effects bypass it. Six tracked voices allow overlap,
reusing a free slot or stopping the oldest voice when full. New input received
while loading or muted is discarded. There is no startup jingle or ambience.

## Controls and resource lifetime

Options > UI Sounds offers Off / Low / Medium / Full: gains 0 / 0.25 / 0.5 / 1.
Medium is the default. A draft previews immediately, A commits, and B restores
the saved session level. Preferences v4 preserves existing video, Stereo/Mono,
Motion and Glass Blur values; v1–v3 settings adopt Medium. Stereo/Mono remains
the independent console SRAM preference and does not imply AESND downmixing.

Off stops active voices and defers the initial firmware read until sound is
enabled. Otherwise preparation runs once on `Task.Run`: read the console IPL,
select its qualified profile and prepare the ten effects. The firmware image
and catalog are disposed before interactive playback; prepared native clips
remain resident. Shutdown cancels pending preparation, awaits it, and disposes
voices before clips. No ROM, PCM, replacement tone or recording is bundled or
written to storage.

Each effect uses a 1 MiB preparation budget and the retained bank is capped at
6 MiB of native audio memory. These limits are separate from the 2 MiB firmware
image and SDK-managed extraction data. Reaching `FirmwareImage` makes the SDK
select an 8 MiB managed heap, replacing this demo's earlier 256 KiB assumption
in audio-enabled builds. Playback performs no firmware reads or effect
preparation in the frame loop; the SDK creates a small channel wrapper per
played effect. Audio-disabled instrumentation retains its separate baseline.

Profiles are qualified for early NTSC 1.0, NTSC 1.2 and PAL 1.2. A missing IPL,
unsupported profile or failed preparation leaves navigation working and reports
unavailable audio in the UI Sounds editor. The Cloud environment has no original
IPL configured, so native validation there covers graceful unavailability and
normal interaction; it does not establish audible playback on physical hardware.
Host tests verify routing, repeat/no-op behavior, voice policy and settings
migration. Physical listening and supported-ROM playback remain separate checks.

The SDK [firmware resource guide](https://github.com/capz/DolphinDotNet/blob/8f5870f68ec7f5eb1e8103c13af42ce082a3516f/docs/system-firmware-resources.md)
and [menu-audio qualification report](https://github.com/capz/DolphinDotNet/blob/8f5870f68ec7f5eb1e8103c13af42ce082a3516f/docs/system-menu-audio-research.md)
describe the exposed assets and supported profiles. The finite effect list does
not qualify `PressStart`, continuous ambience or bit-identical IPL DSP output.
