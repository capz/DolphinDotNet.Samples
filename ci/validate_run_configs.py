#!/usr/bin/env python3
"""Check that every independently opened sample includes its Rider launch paths."""
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def validate(root):
    projects = sorted(root.glob('*/*.csproj'))
    if not projects:
        raise ValueError('No sample projects found')
    for project in projects:
        name = project.stem
        profiles = json.loads((project.parent / 'Properties/launchSettings.json').read_text())['profiles']
        configs = [ET.parse(path).getroot().find('configuration')
                   for path in (project.parent / '.run').glob('*.run.xml')]
        launch_profiles = set()
        names = set()
        hardware = []
        for config in configs:
            if config is None:
                raise ValueError(f'{name}: missing configuration element')
            if not config.get('name') or config.get('name') in names:
                raise ValueError(f'{name}: missing or duplicate run configuration name')
            names.add(config.get('name'))
            if config.find("method/option[@name='Build']") is None:
                raise ValueError(f'{name}: launch must build before running')
            if config.get('type') == 'LaunchSettings':
                options = {o.get('name'): o.get('value') for o in config.findall('option')}
                if options.get('LAUNCH_PROFILE_PROJECT_FILE_PATH') != f'$PROJECT_DIR$/{name}.csproj':
                    raise ValueError(f'{name}: configuration references the wrong sample')
                if options.get('LAUNCH_PROFILE_TFM') != 'net8.0':
                    raise ValueError(f'{name}: incorrect launch framework')
                profile = options.get('LAUNCH_PROFILE_NAME')
                if profile not in profiles:
                    raise ValueError(f'{name}: unknown launch profile {profile}')
                launch_profiles.add(profile)
            if config.get('type') == 'DolphinDotNetAttach':
                hardware.append(config)
        if not {'Dolphin Play', 'Dolphin Debug'} <= launch_profiles:
            raise ValueError(f'{name}: missing emulator play/debug configurations')
        if len(hardware) != 1:
            raise ValueError(f'{name}: expected exactly one hardware launcher configuration')
        config = hardware[0]
        expected = dict(buildFolder='$PROJECT_DIR$/bin/Debug/net8.0', targetId='',
                        factoryName='DolphinDotNetAttach',
                        useDolphinDotNetLauncher='true', launcherPort='4299')
        if any(config.get(key) != value for key, value in expected.items()):
            raise ValueError(f'{name}: hardware upload settings are incomplete or machine-specific')
    return len(projects)


if __name__ == '__main__':
    count = validate(Path(__file__).resolve().parents[1])
    print(f'All {count} samples include emulator play/debug and GameCube ELF upload configurations.')
