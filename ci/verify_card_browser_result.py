#!/usr/bin/env python3
"""Read-only acceptance for the original one-block native UI fixture saves.
Checks isolated Dolphin raw card files after shutdown; never opens a device.
Not a general-purpose memory-card editor or an arbitrary save extractor.
"""
import argparse,hashlib,json,struct
from pathlib import Path

def directory(raw):
    versions=[]
    for block_index in (1,2):
        block=raw[block_index*8192:(block_index+1)*8192]
        words=struct.unpack('>4096H',block)
        checksum=sum(words[:-2])&65535;inverse=sum(x^65535 for x in words[:-2])&65535
        if (0 if checksum==65535 else checksum)!=words[-2] or (0 if inverse==65535 else inverse)!=words[-1]:continue
        entries={}
        for i in range(127):
            entry=block[i*64:(i+1)*64]
            if entry[:4]==b'\xff'*4:continue
            name=entry[8:40].split(b'\0')[0].decode('ascii')
            first,blocks=struct.unpack_from('>HH',entry,54)
            entries[name]=(entry,raw[first*8192:(first+blocks)*8192],blocks)
        versions.append((words[-3],entries))
    if not versions:raise ValueError('No checked directory copy')
    current=versions[0]
    for candidate in versions[1:]:
        distance=(candidate[0]-current[0])&65535
        if 0<distance<32768:current=candidate
    return current[1]

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--user',type=Path,required=True);p.add_argument('--dol',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--region',choices=['EUR','USA','JAP'],default='EUR');a=p.parse_args()
    raw_a=(a.user/('GC/MemoryCardA.'+a.region+'.raw')).read_bytes();raw_b=(a.user/('GC/MemoryCardB.'+a.region+'.raw')).read_bytes();source,destination=directory(raw_a),directory(raw_b)
    assert set(source)=={'GlassOrbit','ColorGarden'},source.keys()
    assert set(destination)=={'GlassOrbit','TinyRacer'},destination.keys()
    se,sb,sc=source['GlassOrbit'];de,db,dc=destination['GlassOrbit']
    assert sc==dc==1 and len(sb)==len(db)==8192 and sb==db,'Complete copied save bytes differ'
    for start,end in [(0,40),(44,54),(56,64)]:assert se[start:end]==de[start:end],'Ownership/icon/comment/protection metadata changed'
    report={'environment':'Actual native UI -> SDK card manager -> isolated Dolphin card files; not physical hardware','dol_sha256':hashlib.sha256(a.dol.read_bytes()).hexdigest(),'region':a.region,'source_files':sorted(source),'destination_files':sorted(destination),'copied_bytes':len(sb),'copied_payload_sha256':hashlib.sha256(sb).hexdigest(),'source_retained':True,'original_destination_retained':True,'cancelled_delete_left_both_files':True,'data_and_metadata_match':True,'passed':True}
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
if __name__=='__main__':main()
