#!/usr/bin/env python3
"""Select existing completed native EFB PNGs; never change their pixels.
Dolphin can dump intermediate draws. Require body text and the three footer
legend areas used by this IPL review before copying a frame as evidence.
Run on the capture host with Pillow; the emulator container needs no Pillow.
"""
import argparse,hashlib,json,re,shutil
from pathlib import Path
from PIL import Image

def white(image,box):
    width,height=image.size
    x1,y1,x2,y2=box
    region=image.crop((int(x1*width/640),int(y1*height/480),int(x2*width/640),int(y2*height/480))).convert('RGB')
    pixels=region.get_flattened_data() if hasattr(region,'get_flattened_data') else region.getdata()
    return sum(min(pixel)>180 for pixel in pixels)

def signature(path):
    with Image.open(path) as image:
        return [white(image,box) for box in [(40,20,600,355),(135,412,225,450),(338,412,420,450),(490,412,600,450)]]

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--captures',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
    frames=[]
    for frame in (a.captures/'user/Dump/Frames').glob('*.png'):
        number=re.search(r'_(\d+)\.png$',frame.name)
        if number:frames.append((int(number[1]),frame))
    frames.sort(reverse=True);events=json.loads((a.captures/'events.json').read_text());previous=-1;result=[]
    for event in events:
        limit=event['completed_frames']-1;chosen=None
        for number,frame in frames:
            if not max(previous+1,limit-90)<=number<=limit:continue
            try:counts=signature(frame)
            except (OSError,ValueError):continue
            if counts[0]>100 and min(counts[1:])>20:chosen=frame;break
        if chosen is None:raise ValueError('No complete body/footer frame for '+event['capture'])
        destination=a.output/(event['capture']+'-efb.png');shutil.copyfile(chosen,destination)
        record=dict(event);record.update(efb_source=chosen.name,efb_sha256=hashlib.sha256(destination.read_bytes()).hexdigest(),body_and_footer_white_pixels=counts,pixels_modified=False);result.append(record);previous=limit
    (a.output/'events.json').write_text(json.dumps(result,indent=2)+'\n');print('Selected '+str(len(result))+' native frames with body and complete footer legends; original pixels unchanged.')
if __name__=='__main__':main()
