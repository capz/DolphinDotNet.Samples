using System;
using System.IO;
internal static class IplLibraryTests
{
    private static void Check(bool b,string text){if(!b)throw new Exception(text);}
    private static void Image(string path,string id,string title){Directory.CreateDirectory(Path.GetDirectoryName(path)!);var bytes=new byte[0x440];for(int i=0;i<6;i++)bytes[i]=(byte)id[i];bytes[28]=0xc2;bytes[29]=0x33;bytes[30]=0x9f;bytes[31]=0x3d;for(int i=0;i<title.Length;i++)bytes[32+i]=(byte)title[i];File.WriteAllBytes(path,bytes);}
    private static void Finish(IplGameIndex index){int steps=0;while(index.Busy){index.Tick();Check(++steps<10000,"Bounded index scan finishes");}}
    internal static void Run()
    {
        string temp=Path.Combine(Path.GetTempPath(),"ipl-library-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try{
            string root=temp+"/games",cache=temp+"/videomode/library.idx";for(int i=0;i<19;i++)Image(root+"/folder "+(i%3)+"/Game "+i+".iso","GABE01","Fixture "+i);File.WriteAllText(root+"/bad.iso","Not a game");
            using(var index=new IplGameIndex(root,cache)){index.Begin();Check(index.Busy,"New library scans incrementally");Finish(index);Check(index.Count==19,"Recursively indexes only valid GameCube headers");var page=new IplBrowserItem?[6];Check(index.ReadPage(0,page)==6&&page[0]!.GameId=="GABE01","Six-entry first page");Check(index.ReadPage(18,page)==1&&page[1]==null,"Bounded last page");}
            using(var index=new IplGameIndex(root,cache)){index.Begin();Check(!index.Busy&&index.Count==19,"Restart reuses fixed-record cache");var page=new IplBrowserItem?[6];index.ReadPage(6,page);Check(page[0]!=null,"Direct middle page seek");}
            byte[] damaged=File.ReadAllBytes(cache);damaged[IplGameIndex.HeaderSize+16]^=1;File.WriteAllBytes(cache,damaged);
            using(var index=new IplGameIndex(root,cache)){index.Begin();bool rejected=false;try{index.ReadPage(0,new IplBrowserItem?[6]);}catch(InvalidDataException){rejected=true;}Check(rejected,"Record CRC rejects corrupted path");index.Begin(true);Finish(index);Check(index.Count==19,"Explicit rescan repairs cache");}
            File.WriteAllText(temp+"/blocked","file prevents cache directory creation");using(var index=new IplGameIndex(root,temp+"/blocked/library.idx")){index.Begin();Finish(index);Check(index.Count==19,"Read-only cache fallback retains total count");var page=new IplBrowserItem?[6];Check(index.ReadPage(0,page)==6,"Read-only first page");index.ReadPage(6,page);Check(index.Busy,"Read-only page loading remains incremental");Finish(index);Check(index.ReadPage(6,page)==6&&index.Count==19,"Read-only middle page without catalog allocation");}
            bool escaped=false;try{IplPaths.Under(root,root+"/../elsewhere.iso");}catch(InvalidDataException){escaped=true;}Check(escaped&&!IplPaths.Safe("bad\npath"),"Root escape and line injection rejected");
            string[] args=IplSwiss.Arguments("gcloader:/games/Folder A/Game.iso",new VideoModeSettings(2,2,false));Check(args[1]=="Autoload=gcldr:/games/Folder A/Game.iso"&&args[5]=="Force NTSC Video Mode=Auto"&&args[6]=="Force PAL Video Mode=576p","Swiss real path/argv protocol and PAL global range");
            string ini="# keep me\r\nUnknown=\u00ff\r\nForce Video Mode=480i\r\nForce Video Mode=Auto\nOther=Yes\n";string changed=IplSwiss.WithVideoMode(ini,"576p");Check(changed.Contains("Unknown=\u00ff\r\n")&&changed.Contains("Other=Yes")&&!changed.Contains("=Auto")&&changed.Contains("Mode=576p"),"Unknown INI bytes/keys retained and duplicate overrides updated");
            string config=temp+"/swiss/settings/game/GABE.ini";Directory.CreateDirectory(Path.GetDirectoryName(config)!);File.WriteAllBytes(config,System.Text.Encoding.Latin1.GetBytes(ini));IplSwiss.ConfigureGame(temp,"GABE01",new VideoModeSettings(2,2,false));Check(File.ReadAllBytes(config+".videomode.bak").AsSpan().SequenceEqual(System.Text.Encoding.Latin1.GetBytes(ini)),"Original game INI backup is byte exact");Check(System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(config))==changed,"Selected game video override is verified");IplSwiss.ConfigureGame(temp,"GABE01",new VideoModeSettings(1,2,false));Check(File.ReadAllBytes(config+".videomode.bak").AsSpan().SequenceEqual(System.Text.Encoding.Latin1.GetBytes(ini)),"Later changes preserve original backup");
            File.Move(config,config+".videomode.old");IplSwiss.ConfigureGame(temp,"GABE01",new VideoModeSettings(1,1,false));Check(File.Exists(config)&&!File.Exists(config+".videomode.old"),"Interrupted rename recovers before writing");
            Console.WriteLine("Game library/Swiss: incremental recursive indexing, cache/CRC/pages, read-only fallback, path bounds, argv, byte-preserving INI update and recovery passed.");
        }finally{Directory.Delete(temp,true);}
    }
}
