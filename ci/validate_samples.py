#!/usr/bin/env python3
"""Verify that every sample can resolve from NuGet, independently of framework source."""
import json
import pathlib
import re
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
VERSION = "0.1.0-preview.15.1"
sdk = json.loads((ROOT / "global.json").read_text())["msbuild-sdks"]["DolphinDotNet.Sdk"]
props = ET.parse(ROOT / "Directory.Build.props").getroot()
version = props.find(".//DndPackageVersion").text
assert sdk == version == VERSION, (sdk, version)
projects = list(ROOT.glob("*/*.csproj"))
assert len(projects) == 15, f"Expected 15 migrated projects, got {len(projects)}"
for path in projects:
    xml = ET.parse(path).getroot()
    assert xml.attrib.get("Sdk") == "DolphinDotNet.Sdk", str(path)
    refs = xml.findall(".//PackageReference")
    assert len(refs) == 1 and refs[0].attrib == {
        "Include": "DolphinDotNet.GameCube", "Version": "$(DndPackageVersion)"
    }, str(path)
    assert not xml.findall(".//ProjectReference"), str(path)
    sln = path.with_suffix(".sln")
    assert sln.is_file(), str(path)
    source = sln.read_text()
    assert "DolphinDotNet.GameCube.csproj" not in source
    assert "DolphinDotNet.Core.csproj" not in source
    assert not re.search(r"\.\.\\\.\.\\src\\", source), str(sln)
for name in ("HelloGameCube", "ManagedGameCube", "ShaderDemo", "Pong", "GreenMen",
             "Fox", "Kart", "Snake", "DiscBrowser", "StorageIntegrity",
             "AsyncNetworking", "AudioDemo", "GoogleLogoAram", "HttpTexture",
             "HardwareDiagnostics"):
    assert (ROOT / name / (name + ".csproj")).is_file(), name
assert "DndBuildNative>false" not in (ROOT / "Directory.Build.props").read_text()
workflow = (ROOT / ".github/workflows/aot-build.yml").read_text()
for word in ("DND_AOT_COMPILER", "--aot", "DndBuildNative=true", "DndBuildBackend=Docker", "verify_aot.py"):
    assert word in workflow, word
print(f"All {len(projects)} sample projects use NuGet preview {VERSION} and require native AOT CI.")
