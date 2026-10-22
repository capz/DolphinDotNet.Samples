#!/usr/bin/env python3
"""Ensure migrated sample behavior tests are independent of the runtime source checkout."""
import pathlib
import re
import xml.etree.ElementTree as ET

root = pathlib.Path(__file__).resolve().parents[1]
expected = {
    'DiscBrowserNavigationTests', 'FoxRulesSmoke', 'GreenMenRulesSmoke',
    'KartInputSmoke', 'KartItemSmoke', 'KartNetworkSmoke', 'KartRulesSmoke',
    'PongRulesSmoke', 'SampleDownloadTests', 'SampleRecordsTests',
    'SnakeRulesSmoke',
}
projects = list((root / 'tests').glob('*/*.csproj'))
names = {project.parent.name for project in projects}
assert names == expected, (expected - names, names - expected)
root_props = ET.parse(root / 'Directory.Build.props').getroot()
test_props = ET.parse(root / 'tests/Directory.Build.props').getroot()
assert (root_props.find('.//DndPackageVersion').text ==
        test_props.find('.//DndPackageVersion').text)
for project in projects:
    config = ET.parse(project).getroot()
    assert config.attrib.get('Sdk') == 'Microsoft.NET.Sdk', project
    assert not config.findall('.//ProjectReference'), project
    assert (project.parent / 'Program.cs').is_file(), project
    for pkg in config.findall('.//PackageReference'):
        assert pkg.attrib['Include'] in {'DolphinDotNet.Core', 'DolphinDotNet.GameCube'}, project
        assert pkg.attrib['Version'] == '$(DndPackageVersion)', project
    includes = config.findall('.//Compile')
    assert includes, f"No linked sample implementation: {project}"
    for item in includes:
        path = item.attrib.get('Include', '')
        assert path and not re.search(r'(src|samples)/', path), (project, path)
        resolved = (project.parent / path).resolve()
        assert resolved.is_file() and resolved.is_relative_to(root), (project, path)
print(f"Validated {len(projects)} migrated sample test projects and linked sources.")
