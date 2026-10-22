#!/usr/bin/env python3
"""Ensure the NuGet SDK's AOT compiler generated C and a real GameCube DOL/ELF."""
import argparse
from pathlib import Path
import struct

def check(name: str, configuration: str, generated_c: Path) -> None:
    root = Path(__file__).resolve().parents[1]
    output = root / name / "bin" / configuration / "net8.0"
    c = generated_c.read_text()
    if "dnd_aot_entry" not in c:
        raise RuntimeError(f"{name}: AOT C is missing the managed entry point")
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
    if len(header) < 20 or header[:6] != b"\\x7fELF\\x01\\x02" or header[18:20] != b"\\x00\\x14":
        raise RuntimeError(f"{name}: expected a big-endian 32-bit PowerPC ELF")
    print(f"{name}: NuGet SDK AOT C, valid GameCube DOL and PowerPC ELF")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("name", help="Sample project/directory name")
    parser.add_argument("--configuration", default="Release")
    parser.add_argument("--generated-c", type=Path, required=True)
    args = parser.parse_args()
    check(args.name, args.configuration, args.generated_c)
