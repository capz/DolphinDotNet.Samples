#!/usr/bin/env python3
"""Keep each sample's portable Rider menu at exactly three automatic actions."""
from pathlib import Path
import xml.etree.ElementTree as ET


def validate(root):
    projects = sorted(root.glob('*/*.csproj'))
    if not projects:
        raise ValueError('No sample projects found')
    expected = dict(play='Run in emulator', emulator='Debug on emulator', target='Debug on target')
    for project in projects:
        configs = list((project.parent / '.run').glob('*.run.xml'))
        if len(configs) != 3:
            raise ValueError(f'{project.stem}: expected exactly three configurations')
        modes = set()
        for path in configs:
            config = ET.parse(path).getroot().find('configuration')
            if config is None:
                raise ValueError(f'{path}: missing configuration')
            mode = config.get('mode')
            if mode not in expected or mode in modes or config.get('name') != expected[mode]:
                raise ValueError(f'{path}: unexpected action or duplicate mode')
            modes.add(mode)
            required = dict(type='DolphinDotNetLaunch', factoryName='DolphinDotNetLaunch',
                            projectFile=f'$PROJECT_DIR$/{project.name}')
            if any(config.get(key) != value for key, value in required.items()):
                raise ValueError(f'{path}: incorrect project or configuration type')
            if set(config.attrib) - {'default', 'name', *required, 'mode'}:
                raise ValueError(f'{path}: unexpected machine-specific configuration settings')
            method = config.find('method')
            if method is None or method.get('v') != '2' or list(method):
                raise ValueError(f'{path}: the plugin owns the build; remove extra before-run tasks')
        if modes != set(expected):
            raise ValueError(f'{project.stem}: missing action')
    return len(projects)


if __name__ == '__main__':
    count = validate(Path(__file__).resolve().parents[1])
    print(f'All {count} samples have exactly the three automatic Rider launch actions.')
