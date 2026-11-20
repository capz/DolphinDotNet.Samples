#!/usr/bin/env python3
"""Verify that every sample can resolve from NuGet, independently of framework source."""
import json
import pathlib
import re
import xml.etree.ElementTree as ET
from collections import Counter

ROOT = pathlib.Path(__file__).resolve().parents[1]
VERSION = "0.1.0-preview.361.1"
sdk = json.loads((ROOT / "global.json").read_text())["msbuild-sdks"]["DolphinDotNet.Sdk"]
props = ET.parse(ROOT / "Directory.Build.props").getroot()
version = props.find(".//DndPackageVersion").text
assert sdk == version == VERSION, (sdk, version)
projects = list(ROOT.glob("*/*.csproj"))
assert len(projects) == 18, f"Expected 18 projects, got {len(projects)}"
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
controller = ROOT / "ControllerInput"
for filename in ("Program.cs", "ControllerSession.cs", "README.md"):
    assert (controller / filename).is_file(), f"Missing controller sample file: {filename}"
controller_project = ET.parse(controller / "ControllerInput.csproj").getroot()
assert controller_project.findtext(".//DndSampleControls") == "false", \
    "ControllerInput must reserve Z and Start for input visualization"
for filename in ("controller_input_capture.py", "controller_input.Dockerfile"):
    assert (ROOT / "ci" / filename).is_file(), f"Missing controller native acceptance tool: {filename}"
# The required native matrix must explicitly cover all migrated applications.
workflow = (ROOT / ".github/workflows/samples.yml").read_text()
groups = dict(re.findall(r'^\s*(\w+)\) samples="([^"]+)" ;;', workflow, re.M))
assert set(groups) == {"basics", "games", "services", "ipl", "controller"}, groups
matrix = re.search(r"group:\s*\[([^\]]+)\]", workflow)
assert matrix and {name.strip() for name in matrix[1].split(',')} == set(groups)
native_samples = Counter(name for samples in groups.values() for name in samples.split())
assert native_samples == Counter(path.stem for path in projects), \
    "Every sample must appear exactly once in the packaged native build groups"
for condition in ("DND_AOT_COMPILER", " --aot ", "DndBuildNative=true",
                  "DndBuildBackend=Docker", "verify_aot.py"):
    assert condition in workflow, condition
print(f"All {len(projects)} sample projects use NuGet preview {VERSION} and require native AOT CI.")
