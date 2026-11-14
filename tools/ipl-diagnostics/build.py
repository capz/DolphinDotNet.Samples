#!/usr/bin/env python3
"""Build a read-only hardware probe using the published SDK, then add native comparators."""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
IMAGE = "ghcr.io/extremscorner/libogc2@sha256:13bb658d3f18903816617223f5d7c6f776d3db3ea806aae67f8d35a7a69d4c85"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, default=HERE / "bin/diagnostic")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    for name in ("IplDiagnostics-check01.dol", "IplDiagnostics-check01.elf", "build.json"):
        (output / name).unlink(missing_ok=True)
    subprocess.run(["dotnet", "build", str(HERE / "IplDiagnostics.csproj"), "-c", "Release",
                    "-p:DndBuildBackend=Docker"], cwd=ROOT, check=True)
    native = HERE / "obj/Release/net8.0/native"
    version = json.loads((ROOT / "global.json").read_text())["msbuild-sdks"]["DolphinDotNet.Sdk"]
    # This is a diagnostic-only extension to a generated bundle. The published
    # firmware reader and managed APIs are left byte-for-byte intact.
    for name in ("ipl_probe.c", "ipl_analysis.h"):
        shutil.copy2(HERE / name, native / "source" / name)
    probe = native / "source/ipl_probe.c"
    probe.write_text("#define IPL_DIAG_SDK_VERSION " + json.dumps(version) + "\n" + probe.read_text())
    main_c = native / "source/main.c"
    source = main_c.read_text()
    needle = "intptr_t managed_result=dnd_aot_entry(&heap);"
    assert source.count(needle) == 1, "Published SDK main entry changed; inspect before patching."
    main_c.write_text(source.replace(needle, needle + "\n    extern void dnd_ipl_probe_run(intptr_t);\n    dnd_ipl_probe_run(managed_result);"))
    subprocess.run(["docker", "run", "--rm", "-v", f"{native}:/work", "-w", "/work", IMAGE,
                    "make", "-j2"], check=True)
    # The package's Makefile verifies/pads the DOL. Publish only the probe and
    # its reproducibility metadata, never firmware, screenshots or extracted data.
    for suffix in ("dol", "elf"):
        shutil.copy2(native / f"Game.{suffix}", output / f"IplDiagnostics-check01.{suffix}")
    dol = output / "IplDiagnostics-check01.dol"
    metadata = {"build": "IPL CHECK 01", "sdk": version,
                "dol_sha256": hashlib.sha256(dol.read_bytes()).hexdigest(), "dol_bytes": dol.stat().st_size}
    (output / "build.json").write_text(json.dumps(metadata, indent=2) + "\n")
    print(json.dumps(metadata, indent=2), flush=True)


if __name__ == "__main__":
    main()
