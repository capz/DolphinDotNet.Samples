#!/usr/bin/env python3
"""Produce an unpacked GC Loader SD drop from a pinned official Swiss release.
No files are copied to physical media. No game images or Nintendo firmware.
"""
import argparse,hashlib,json,tarfile,urllib.request
from pathlib import Path
VERSION='v0.6r2119'
URL='https://github.com/emukidid/swiss-gc/releases/download/'+VERSION+'/swiss_r2119.tar.xz'
ARCHIVE_SHA256='97825799dfbe44cca08d1e49f95d15cc996bd15a052f48827b48162005cbe916'
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',type=Path,required=True);p.add_argument('--archive',type=Path,help='Use an already downloaded official archive, still verified');p.add_argument('--app-dol',type=Path);a=p.parse_args()
out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
archive=a.archive or out/'swiss-release.tar.xz'
if not a.archive:
 with urllib.request.urlopen(URL,timeout=60) as source,archive.open('wb') as target:
  while chunk:=source.read(65536):target.write(chunk)
if hashlib.sha256(archive.read_bytes()).hexdigest()!=ARCHIVE_SHA256:raise ValueError('Official Swiss archive hash mismatch')
with tarfile.open(archive) as release:
 executable=release.extractfile('swiss_r2119/DOL/swiss_r2119.dol').read()
 (out/'swiss-gc.dol').write_bytes(executable)
 licenses=out/'swiss-licenses';licenses.mkdir(exist_ok=True)
 for item in release.getmembers():
  if item.isfile() and (item.name=='swiss_r2119/LICENSE.txt' or item.name.startswith('swiss_r2119/Licenses/')):
   (licenses/Path(item.name).name).write_bytes(release.extractfile(item).read())
metadata={'release':VERSION,'release_url':'https://github.com/emukidid/swiss-gc/releases/tag/'+VERSION,'archive_url':URL,'archive_sha256':ARCHIVE_SHA256,'dol_sha256':hashlib.sha256(executable).hexdigest(),'source':'https://github.com/emukidid/swiss-gc/tree/'+VERSION,'source_archive':'https://github.com/emukidid/swiss-gc/archive/refs/tags/'+VERSION+'.tar.gz','license':'See swiss-licenses/LICENSE.txt and bundled third-party notices. Swiss is distributed separately from the sample.'}
(out/'swiss-provenance.json').write_text(json.dumps(metadata,indent=2)+'\n')
(out/'games').mkdir(exist_ok=True)
(out/'README.txt').write_text('Copy this folder’s contents to the root of your GC Loader SD card.\nKeep swiss-gc.dol at the root; put your own ISO/GCM images under games/ (nested folders work).\nVideoMode indexes headers and reads opening.bnr artwork; it includes no games.\nLaunch applies the confirmed video mode to swiss/settings/game/ABCD.ini, preserving other keys\nand an original .videomode.bak backup. A writable card is required when that key must change.\nSwiss release, source URLs, hashes and licenses are included. No physical card was modified here.\n')
if a.app_dol:
 import shutil
 shutil.copyfile(a.app_dol,out/'VideoMode.dol')
print(json.dumps(metadata))
