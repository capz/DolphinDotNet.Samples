#!/usr/bin/env python3
"""Build a VideoMode integration DOL against an explicit source SDK checkout.

Does not change package pins or installed packages. Run container builds serially
in cloud environments that use Docker's VFS storage driver.
"""
import argparse
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--samples', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--scene', choices=['Options', 'Oblique', 'Detail', 'Cube'], default='Options')
    parser.add_argument('--managed-only',action='store_true',help='Generate/build the source integration project without invoking the native toolchain')
    parser.add_argument('--normal', action='store_true')
    parser.add_argument('--icons', action='store_true')
    parser.add_argument('--glass', action='store_true')
    parser.add_argument('--blur', action='store_true')
    parser.add_argument('--glyphs', action='store_true')
    parser.add_argument('--tints', action='store_true')
    parser.add_argument('--numbers', action='store_true')
    parser.add_argument('--footer-text', action='store_true')
    parser.add_argument('--grid', action='store_true')
    parser.add_argument('--selectors', action='store_true')
    parser.add_argument('--browser-review',action='store_true')
    parser.add_argument('--console-services', action='store_true')
    parser.add_argument('--hypercube', type=Path,
                        help='Link OrderedMeshPasses from this engine checkout with its native backend')
    args = parser.parse_args()
    sdk, samples, out = args.sdk.resolve(), args.samples.resolve(), args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    if out == samples / 'VideoMode':
        parser.error('Use an output directory outside the sample project.')
    original = ET.parse(samples / 'VideoMode/VideoMode.csproj').getroot()
    root = ET.Element('Project')
    imports = ET.SubElement(root, 'PropertyGroup')
    ET.SubElement(imports, 'ImportDirectoryBuildProps').text = 'false'
    ET.SubElement(root, 'Import', Project=str(sdk / 'sdk/DolphinDotNet.Sdk/Sdk/Sdk.props'))
    props = ET.SubElement(root, 'PropertyGroup')
    for child in original.find('PropertyGroup'):
        props.append(ET.fromstring(ET.tostring(child)))
    settings = {
        'DndBuildNative': str(not args.managed_only).lower(),
        'TargetFramework': 'net8.0', 'AssemblyName': 'VideoMode',
        'IplPerformanceReview': str(not args.normal).lower(),
        'IplPerformanceReviewScene': args.scene,
        'IplPerformanceReviewOverlay': 'Off' if args.normal else 'Full',
        'IplCachedIcons': str(args.icons).lower(),
        'IplSharedGlass': str(args.glass).lower(),
        'IplBatchedBlur': str(args.blur).lower(),
        'IplBatchedText': str(args.glyphs).lower(),
        'IplRetainedTint': str(args.tints).lower(),
        'IplCachedNumbers': str(args.numbers).lower(),
        'IplCachedFooterText': str(args.footer_text).lower(),
        'IplCachedGrid': str(args.grid).lower(),
        'IplCachedSelectors': str(args.selectors).lower(),
        'IplBrowserReview': str(args.browser_review).lower(),
        'IplConsoleServices': str(args.console_services).lower(),
        'IplNextSdk': 'true', 'DndSampleControls': 'true', 'DndBuildBackend': 'Docker',
    }
    for key, value in settings.items():
        ET.SubElement(props, key).text = value
    items = ET.SubElement(root, 'ItemGroup')
    for pattern in ['VideoMode/*.cs', 'VideoMode/Assets/*.cs', 'Common/*.cs']:
        for path in sorted(samples.glob(pattern)):
            ET.SubElement(items, 'Compile', Include=str(path))
    ET.SubElement(items, 'ProjectReference', Include=str(
        sdk / 'src/DolphinDotNet.GameCube/DolphinDotNet.GameCube.csproj'))
    if args.hypercube:
        ET.SubElement(items, 'Compile', Include=str(
            args.hypercube.resolve() / 'src/Hypercube.GameCube/OrderedMeshPasses.cs'))
        ET.SubElement(props, 'DefineConstants').text = (
            '$(DefineConstants);HYPERCUBE_NATIVE_PASSES;IPL_HYPERCUBE_PASSES')
    ET.SubElement(root, 'Import', Project=str(sdk / 'sdk/DolphinDotNet.Sdk/Sdk/Sdk.targets'))
    project = out / 'VideoMode.csproj'
    ET.indent(root)
    ET.ElementTree(root).write(project, encoding='unicode')
    # Global properties must be visible before the copied conditional constants
    # are evaluated. AssemblyName must remain local to avoid renaming SDK refs.
    flags = ['-p:' + key + '=' + value for key, value in settings.items()
             if key.startswith('Ipl') or key in ['DndSampleControls', 'DndBuildBackend','DndBuildNative']]
    subprocess.run(['dotnet', 'build', str(project), '-c', 'Release', *flags], check=True)


if __name__ == '__main__':
    main()
