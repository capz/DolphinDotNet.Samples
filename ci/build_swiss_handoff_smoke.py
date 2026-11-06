#!/usr/bin/env python3
"""Build a managed -> official Swiss DOL test using an original data-only ISO."""
import argparse,json,struct,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--sdk',type=Path,required=True);p.add_argument('--swiss-dol',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
sdk=a.sdk.resolve();out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
code=a.swiss_dol.read_bytes();fst=0x500;offset=0x8000;data=bytearray(offset+len(code));data[:6]=b'GABE01';data[32:32+len(b'Swiss native handoff fixture')]=b'Swiss native handoff fixture'
for at,value in [(0x1c,0xc2339f3d),(0x424,fst),(0x428,38),(0x42c,38),(fst,0x01000000),(fst+8,2),(fst+12,1),(fst+16,offset),(fst+20,len(code))]:struct.pack_into('>I',data,at,value)
data[fst+24:fst+38]=b'\0swiss-gc.dol\0';data[offset:]=code;(out/'swiss-handoff.iso').write_bytes(data)
(out/'Program.cs').write_text('''using Dolphin.Storage;
using Dolphin.IO;
internal static class Program {
 internal static readonly int[] Result=new int[1];internal static string Status="Not started";
 static int Main(){try{
  if(!Storage.MountDisc())throw new System.IO.IOException("Synthetic fixture disc unavailable.");
  using(var app=DolLauncher.Prepare("dvd:/swiss-gc.dol")){
   app.SetArguments("swiss-gc.dol","AutoBoot=No","Force NTSC Video Mode=480p","Force PAL Video Mode=576p","Prefer Clean Boot=No");
   DolLauncher.RequestLaunch(app);Result[0]=1;Status="Swiss launch queued";
  }return 0;
 }catch(System.Exception e){Result[0]=-1;Status=e.Message??"Native I/O failed.";System.Console.WriteLine(Status);return 1;}}
}''')
root=ET.Element('Project');props=ET.SubElement(root,'PropertyGroup');ET.SubElement(props,'ImportDirectoryBuildProps').text='false';ET.SubElement(root,'Import',Project=str(sdk/'sdk/DolphinDotNet.Sdk/Sdk/Sdk.props'))
props=ET.SubElement(root,'PropertyGroup')
for k,v in {'TargetFramework':'net8.0','OutputType':'Exe','AssemblyName':'SwissHandoffSmoke','DndBuildBackend':'Docker','DndBuildNative':'true','DndSampleControls':'true'}.items():ET.SubElement(props,k).text=v
items=ET.SubElement(root,'ItemGroup');ET.SubElement(items,'ProjectReference',Include=str(sdk/'src/DolphinDotNet.GameCube/DolphinDotNet.GameCube.csproj'));ET.SubElement(root,'Import',Project=str(sdk/'sdk/DolphinDotNet.Sdk/Sdk/Sdk.targets'));ET.indent(root)
project=out/'SwissHandoffSmoke.csproj';ET.ElementTree(root).write(project,encoding='unicode');subprocess.run(['dotnet','build',str(project),'-c','Release'],check=True)
