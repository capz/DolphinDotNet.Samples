#!/usr/bin/env python3
"""Ensure the NuGet SDK's AOT compiler generated C and a real GameCube DOL/ELF."""
import argparse
from pathlib import Path
import struct
import re

def check(name: str, configuration: str, generated_c: Path) -> None:
    root = Path(__file__).resolve().parents[1]
    output = root / name / "bin" / configuration / "net8.0"
    c = generated_c.read_text()
    if "dnd_aot_entry" not in c:
        raise RuntimeError(f"{name}: AOT C is missing the managed entry point")
    if name == "IplResources":
        for api in ("SystemFirmware_ReadConsole", "SystemResources_CreateMenuSound",
                    "SystemResources_CreateStartupSound", "SystemResources_CreateStartupVariantSound", "FirmwareCatalog_TryGetStartupSoundInfo", "SystemResources_CreateTexture",
                    "SystemFont_Load", "SystemMenuSoundInfo_get_SourceWaveCount",
                    "SystemMenuSoundInfo_GetSourceWaveId", "SystemResources_CreateAudioSampleAsync",
                    "SystemAudioSample_Play", "FirmwareCatalog_GetAudioSamples",
                    "FirmwareAudioSampleInfo_get_HasLoop", "SystemResources_CreateAudioSession",
                    "FirmwareCatalog_get_SupportsAudioSession", "FirmwareCatalog_GetAudioCues",
                    "SystemAudioSession_GetSample", "SystemAudioSession_PlaySample",
                    "SystemAudioSession_PlayCue", "SystemAudioSession_PlayStartup",
                    "SystemAudioSession_SetControl", "SystemAudioSession_GetStatistics",
                    "SystemAudioSession_get_IsStartupPlaying", "SystemAudioSession_set_OutputMode",
                    "SystemAudioCueInfo_get_RepeatsUntilStopped", "SystemAudioPlayback_Dispose"):
            if api not in c:
                raise RuntimeError(f"IplResources: published firmware API is not reachable: {api}")
    if name == "VideoMode":
        # Audio-disabled measurement builds deliberately omit this backend.
        if "IplUiAudio" in c:
            for api in ("FirmwareCatalog_get_SupportsAudioSession", "SystemResources_CreateAudioSession",
                        "SystemAudioSession_PlayCue", "SystemAudioSession_SetControl",
                        "SystemAudioSession_set_Volume", "SystemAudioPlayback_Dispose"):
                if api not in c:
                    raise RuntimeError(f"VideoMode: IPL session API is not reachable: {api}")
            frames = re.search(r"intptr_t dnd_value_VideoMode_Program__RunFrames[^(]*MoveNext[^(]*\([^;]+?\) \{(.*?)\n\}", c, re.S)
            if frames is None or "Time_DelayAsync" not in frames.group(1):
                raise RuntimeError("VideoMode: audio-enabled frame loop must yield to the SDK scheduler")
        def glass_body(method):
            match=re.search(r"intptr_t dnd_value_VideoMode_SealedGlassShell_"+method+r"_[^(]+\([^;]+?\) \{(.*?)\n\}",c,re.S)
            return match.group(1) if match else None
        rear,front,setup=glass_body("DrawRear"),glass_body("DrawFront"),glass_body("BeginInterfacePass")
        sealed = re.search(r"intptr_t dnd_value_VideoMode_SealedGlassShell_Draw_[^(]+\([^;]+?\) \{(.*?)\n\}", c, re.S)
        if rear is not None or front is not None:
            if rear is None or front is None or setup is None or rear.count("GX_CallDispList")!=2 or front.count("GX_CallDispList")!=2 or "GX_CopyTex" not in front or "GX_SetTevIndirect" not in front or "GX_SetTexCoordGen" not in setup or "BufferWrite" in rear+front+setup:
                raise RuntimeError("VideoMode: refraction requires four retained optical interfaces, live capture and normal-driven TEV without vertex uploads")
            if not front.index("GX_CallDispList")<front.index("GX_CopyTex")<front.rindex("GX_CallDispList"):
                raise RuntimeError("VideoMode: inner-front reflection must cover the centered icon before the live front-wall capture")
            commands=glass_body("BuildCommands")
            if commands is None or "IplSealedGlassGeometry_NativeTriangleIndex" not in commands:
                raise RuntimeError("VideoMode: native display lists must adapt mathematical winding to GX facing")
        elif sealed is not None:
            body=sealed.group(1)
            if body.count("GX_CallDispList")!=3 or "GX_SetTexCoordGen" not in body or "GX_SetTevAlphaIn" not in body or "BufferWrite" in body:
                raise RuntimeError("VideoMode: sealed glass requires static indexed interfaces and normal-driven TEV without in-pass uploads")
        else:
            # Compatibility for historical renderer CI, before the sealed shell.
            uploader = re.search(r"intptr_t dnd_value_VideoMode_GameCubeCanvas_GlassVertex_[^(]+\([^;]+?\) \{(.*?)\n\}", c, re.S)
            if uploader is None or "dnd_array_store_r8_fast" in uploader.group(1):
                raise RuntimeError("VideoMode: missing reflection uploader or unsafe 8-byte strength store")
    dol = output / (name + ".dol")
    elf = output / (name + ".elf")
    program = dol.read_bytes()
    if len(program) < 0x100:
        raise RuntimeError(f"{name}: missing or invalid GameCube DOL")
    offsets = struct.unpack_from(">18I", program, 0)
    sizes = struct.unpack_from(">18I", program, 0x90)
    valid = [(offset, size) for offset, size in zip(offsets, sizes)
             if offset and size and offset >= 0x100 and offset + size <= len(program)]
    if not valid or not any(sizes[:7]):
        raise RuntimeError(f"{name}: DOL lacks valid executable sections")
    header = elf.read_bytes()[:20]
    if len(header) < 20 or header[:6] != b"\x7fELF\x01\x02" or header[18:20] != b"\x00\x14":
        raise RuntimeError(f"{name}: expected a big-endian 32-bit PowerPC ELF")
    print(f"{name}: NuGet SDK AOT C, valid GameCube DOL and PowerPC ELF")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("name", help="Sample project/directory name")
    parser.add_argument("--configuration", default="Release")
    parser.add_argument("--generated-c", type=Path, required=True)
    args = parser.parse_args()
    check(args.name, args.configuration, args.generated_c)
