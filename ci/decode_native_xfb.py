#!/usr/bin/env python3
"""Decode original GameCube GX_CopyDisp YUYV output as unscaled RGB PNGs.

Standard BT.601 limited-range color conversion only. Raw native buffers and
their hashes remain the capture source; no scene pixels are composed or edited.
Run on the capture host with Pillow.
"""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image


def decode(raw,width,height):
    if width%2 or len(raw)!=width*height*2:raise ValueError('Incomplete native XFB')
    rgb=bytearray(width*height*3)
    def clamp(v):return max(0,min(255,v))
    for i in range(0,len(raw),4):
        y0,u,y1,v=raw[i:i+4];d=u-128;e=v-128
        for j,y in enumerate((y0,y1)):
            c=y-16;at=(i//2+j)*3
            rgb[at]=clamp((298*c+409*e+128)>>8);rgb[at+1]=clamp((298*c-100*d-208*e+128)>>8);rgb[at+2]=clamp((298*c+516*d+128)>>8)
    return Image.frombytes('RGB',(width,height),bytes(rgb))


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--captures',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True);events=json.loads((args.captures/'events.json').read_text())
    for event in events:
        source=args.captures/(event['capture']+'-xfb.yuv');raw=source.read_bytes();info=event['xfb'];target=args.output/(event['capture']+'-xfb.png')
        with decode(raw,info['width'],info['height']) as image:image.save(target)
        event.update(xfb_source=str(source),xfb_sha256=hashlib.sha256(raw).hexdigest(),png_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),conversion='BT.601 limited-range YUYV422 to RGB8; no crop/resize/composition')
    (args.output/'events.json').write_text(json.dumps(events,indent=2)+'\n');print('Decoded',len(events),'original native framebuffers without resizing.')


if __name__=='__main__':main()
