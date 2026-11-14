# IPL UI audio preparation budget audit

SDK: published `0.1.0-preview.347.1`, source
`8f5870f68ec7f5eb1e8103c13af42ce082a3516f`.

## Per-effect preparation charge

The SDK's `dnd_audio_begin_menu` rejects a preparation when its running native
charge exceeds the caller's budget. Its accepted bounds give the following
conservative maximum, including the early profile's 96,000-frame feedback tail
within the output-frame bound:

| Component | Upper charge in bytes |
| --- | ---: |
| Stereo PCM16, at most 256,000 frames, aligned to 32 bytes | 1,024,000 |
| Complete recipe packet | 262,144 |
| Two decoded mono sources, each at most 100,000 PCM16 frames | 400,000 |
| Two reverb rings, each at most 100 × 80 PCM16 samples | 32,000 |
| Two flat-source spans, 48 bytes each on the target ABI | 96 |
| `MenuPreparation` on the pinned PowerPC ABI | 1,744 |
| **Total maximum SDK preparation charge** | **1,719,984** |

Therefore **2 MiB (2,097,152 bytes) covers every recipe accepted by these
structural limits**, with a 377,168-byte charge margin. A 1 MiB budget is not
proved by the structural limits or the published modern-profile measurements.
The calculation bounds the SDK's charge, excluding allocator metadata and
unrelated application/AESND state. This is native preparation memory; the
separate managed heap is 8 MiB.

Target sizes were checked without changing application/SDK sources: a temporary
translation unit included the exact bundled `source/audio.c` and emitted
`sizeof` constants with the relocated pinned GCC 16.1.0 and the same PowerPC ABI.
`MenuPreparation=1744`, `DndAudioSpan=48`, `DndAudioSource=196`,
`DndAudioDecoder=344`. The output is `/tmp/ui-sounds-audio-sizes.s`.

Relevant source:

- [Budget checks, output, rings and source allocations](https://github.com/capz/DolphinDotNet/blob/8f5870f68ec7f5eb1e8103c13af42ce082a3516f/source/audio_menu.inc#L74)
- [JAudio flat-source parsing](https://github.com/capz/DolphinDotNet/blob/8f5870f68ec7f5eb1e8103c13af42ce082a3516f/source/audio_formats.inc#L552)
- [Source/span/decoder ABI types](https://github.com/capz/DolphinDotNet/blob/8f5870f68ec7f5eb1e8103c13af42ce082a3516f/include/dnd_audio_codec.h#L8)

## Resident bank

Prepared menu sounds retain only aligned stereo PCM16. `Sound.NativeBytes`
reports that aligned retained length (`audio.c`, `dnd_audio_stat`, kind 8).
The 6 MiB bank budget is an intentional application MEM1 ceiling, distinct from
the transient per-preparation charge.

The generic SDK bound for ten clips is 10 × 1,024,000 = 10,240,000 bytes, so it
does **not** prove that all ten qualified early effects fit the app's 6 MiB cap.
Published research documents do not list all ten exact early output lengths;
the public test fixtures use synthetic recipes. No private qualified early ROM
was available to this audit to independently enumerate those metadata values.
Thus an exact early-bank total is not established by this audit.

Do not raise the resident cap blindly. The catalog exposes each qualified
effect's `SampleFrameCount` and `Channels`; summing
`(SampleFrameCount * Channels * 2 + 31) & ~31` before preparation can report the
actual bank requirement and reject a bank exceeding 6 MiB before allocating it.
The existing backend checks actual `NativeBytes` and cleans up the partial bank
on failure. An eventual real-console or private early-profile run should record
all ten metadata lengths and the final resident total to close this limitation.
