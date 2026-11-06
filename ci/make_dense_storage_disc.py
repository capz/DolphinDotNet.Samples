#!/usr/bin/env python3
"""Original read-only DVD fixtures for native SD-layout review, not GC Loader proof."""
import argparse
from pathlib import Path
import struct


def create(path):
    image=bytearray(0x120000)
    def put(at,value):struct.pack_into('>I',image,at,value)
    def text(at,value):
        raw=value.encode('ascii');image[at:at+len(raw)]=raw
    text(0,'TEST01');text(0x20,'Original dense storage UI fixtures');put(0x1c,0xc2339f3d);put(0x424,0x500)
    note=b'Original read-only folder preview. No game assets.\n'
    image[0x8000:0x8000+len(note)]=note
    entries=[('',True,0,0),('Notes',True,0,3),('readme.txt',False,0x8000,len(note))]
    for i in range(125):
        entries.append((f'Archive-{i+1:03}.dat',False,0x9000,(i%4)*512 if i<124 else 1048576))
    # Header title deliberately differs: the SD UI must still show the filename.
    text(0x9000,'GAME01');put(0x901c,0xc2339f3d);text(0x9020,'Original synthetic disc header')
    entries.append(('Original-image.iso',False,0x9000,0x1000));entries[0]=('',True,0,len(entries))
    names=bytearray()
    for i,(name,directory,start,length) in enumerate(entries):
        put(0x500+i*12,(0x01000000 if directory else 0)|len(names));put(0x504+i*12,start);put(0x508+i*12,length)
        names.extend(name.encode('ascii')+b'\0')
    image[0x500+12*len(entries):0x500+12*len(entries)+len(names)]=names;put(0x428,12*len(entries)+len(names))
    path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(image)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('output',type=Path);create(parser.parse_args().output)
