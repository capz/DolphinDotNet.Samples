#!/usr/bin/env python3
"""Produce an unpacked GC Loader SD drop from a pinned official Swiss release.
No files are copied to physical media. No game images or Nintendo firmware.
"""
import argparse,hashlib,json,struct,tarfile,urllib.request
from pathlib import Path
VERSION='v0.6r2119'
URL='https://github.com/emukidid/swiss-gc/releases/download/'+VERSION+'/swiss_r2119.tar.xz'
ARCHIVE_SHA256='97825799dfbe44cca08d1e49f95d15cc996bd15a052f48827b48162005cbe916'
def release_elf_to_dol(elf):
 # Preserve every loadable byte from the official, unmodified ELF. Its XZ
 # wrapper probes expansion hardware unsupported by Dolphin 2503; the full
 # Swiss executable works without that wrapper on both the console and Dolphin.
 if elf[:6]!=b'\x7fELF\x01\x02':raise ValueError('Expected ELF32 big-endian')
 machine=struct.unpack_from('>H',elf,18)[0]
 if machine!=20:raise ValueError('Expected PowerPC ELF')
 entry,phoff=struct.unpack_from('>II',elf,24);phsize,count=struct.unpack_from('>HH',elf,42)
 dol=bytearray(256);text=data=0;bss=[];segments=[]
 for i in range(count):
  kind,offset,address,physical,size,memory,flags,alignment=struct.unpack_from('>8I',elf,phoff+i*phsize)
  if kind!=1:continue
  if memory<size or address<0x80003100 or memory>0x81800000-address or offset>len(elf) or size>len(elf)-offset:raise ValueError('Invalid official ELF load segment')
  if memory>size:bss.append((address+size,address+memory))
  if not size:continue
  if flags&1:index=text;text+=1
  else:index=7+data;data+=1
  if text>7 or data>11:raise ValueError('Too many official ELF load segments')
  dol.extend(b'\0'*((-len(dol))%32));target=len(dol);payload=elf[offset:offset+size]
  for base,value in [(0,target),(0x48,address),(0x90,size)]:struct.pack_into('>I',dol,base+index*4,value)
  dol.extend(payload);segments.append({'address':hex(address),'size':size,'sha256':hashlib.sha256(payload).hexdigest()})
 if bss:struct.pack_into('>II',dol,0xd8,min(x for x,y in bss),max(y for x,y in bss)-min(x for x,y in bss))
 struct.pack_into('>I',dol,0xe0,entry)
 return bytes(dol),segments
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',type=Path,required=True);p.add_argument('--archive',type=Path,help='Use an already downloaded official archive, still verified');p.add_argument('--app-dol',type=Path);a=p.parse_args()
out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
archive=a.archive or out/'swiss-release.tar.xz'
if not a.archive:
 with urllib.request.urlopen(URL,timeout=60) as source,archive.open('wb') as target:
  while chunk:=source.read(65536):target.write(chunk)
if hashlib.sha256(archive.read_bytes()).hexdigest()!=ARCHIVE_SHA256:raise ValueError('Official Swiss archive hash mismatch')
with tarfile.open(archive) as release:
 packed=release.extractfile('swiss_r2119/DOL/swiss_r2119.dol').read()
 elf=release.extractfile('swiss_r2119/DOL/swiss_r2119.elf').read()
 executable,segments=release_elf_to_dol(elf)
 (out/'swiss-gc.dol').write_bytes(executable)
 licenses=out/'swiss-licenses';licenses.mkdir(exist_ok=True)
 for item in release.getmembers():
  if item.isfile() and (item.name=='swiss_r2119/LICENSE.txt' or item.name.startswith('swiss_r2119/Licenses/')):
   (licenses/Path(item.name).name).write_bytes(release.extractfile(item).read())
metadata={'release':VERSION,'release_url':'https://github.com/emukidid/swiss-gc/releases/tag/'+VERSION,'archive_url':URL,'archive_sha256':ARCHIVE_SHA256,'dol_sha256':hashlib.sha256(executable).hexdigest(),'format':'Uncompressed DOL converted losslessly from official ELF load segments; no code changes','official_packed_dol_sha256':hashlib.sha256(packed).hexdigest(),'official_elf_sha256':hashlib.sha256(elf).hexdigest(),'load_segments':segments,'source':'https://github.com/emukidid/swiss-gc/tree/'+VERSION,'source_archive':'https://github.com/emukidid/swiss-gc/archive/refs/tags/'+VERSION+'.tar.gz','license':'See swiss-licenses/LICENSE.txt and bundled third-party notices. Swiss is distributed separately from the sample.'}
(out/'swiss-provenance.json').write_text(json.dumps(metadata,indent=2)+'\n')
(out/'games').mkdir(exist_ok=True)
(out/'README.txt').write_text('Copy this folder’s contents to the root of your GC Loader SD card.\nKeep swiss-gc.dol at the root; put your own ISO/GCM images under games/ (nested folders work).\nVideoMode indexes headers and reads opening.bnr artwork; it includes no games.\nLaunch applies the confirmed video mode to swiss/settings/game/ABCD.ini, preserving other keys\nand an original .videomode.bak backup. A writable card is required when that key must change.\nSwiss is a lossless DOL repack of the official unmodified ELF (see provenance).\nThe packed release wrapper probes expansion hardware unavailable in Dolphin 2503.\nSwiss release, source URLs, hashes and licenses are included. No physical card was modified here.\n')
if a.app_dol:
 import shutil
 shutil.copyfile(a.app_dol,out/'VideoMode.dol')
print(json.dumps(metadata))
