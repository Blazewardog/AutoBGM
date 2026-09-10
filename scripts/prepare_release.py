"""Validate a tagged build and prepare GitHub release assets."""
import argparse
import json
from pathlib import Path
import re
import shutil
import time
import zipfile


def prepare(build: Path, output: Path, tag: str):
    if not re.fullmatch(r'v\d+\.\d+\.\d+', tag):
        raise ValueError('Release tags must use vMAJOR.MINOR.PATCH')
    manifest = json.loads((build / 'AutoBgm.json').read_text())
    if manifest['AssemblyVersion'] != tag[1:] + '.0':
        raise ValueError('Tag version must match Version in AutoBgm.csproj')
    if manifest['InternalName'] != 'AutoBgm':
        raise ValueError('Unexpected plugin identity')
    archive = build / 'AutoBgm' / 'latest.zip'
    with zipfile.ZipFile(archive) as package:
        required = {'AutoBgm.dll', 'AutoBgm.json', 'Microsoft.Windows.SDK.NET.dll', 'WinRT.Runtime.dll'}
        if not required.issubset(package.namelist()):
            raise ValueError('Plugin archive is missing required files at its root')
        if json.loads(package.read('AutoBgm.json'))['AssemblyVersion'] != manifest['AssemblyVersion']:
            raise ValueError('Archive and repository manifest versions differ')
    output.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(archive, output / 'AutoBgm.zip')
    shutil.copyfile(Path(__file__).resolve().parents[1] / 'helper' / 'autobgm_helper.py', output / 'autobgm_helper.py')
    download = f'https://github.com/Blazewardog/AutoBGM/releases/download/{tag}/AutoBgm.zip'
    manifest.update(RepoUrl='https://github.com/Blazewardog/AutoBGM',
                    DownloadLinkInstall=download, DownloadLinkUpdate=download,
                    IsHide=False, IsTestingExclusive=False, LastUpdate=int(time.time()))
    (output / 'pluginmaster.json').write_text(json.dumps([manifest], indent=2) + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=Path, default=Path('AutoBgm/bin/x64/Release'))
    parser.add_argument('--output', type=Path, default=Path('release-assets'))
    parser.add_argument('--tag', required=True)
    args = parser.parse_args()
    prepare(args.build, args.output, args.tag)
