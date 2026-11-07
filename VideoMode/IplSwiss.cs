using System;
using System.IO;
#if IPL_HOST_FILES
using FsFile=System.IO.File;
using FsDirectory=System.IO.Directory;
using FsStream=System.IO.Stream;
using TextEncoding=System.Text.Encoding;
#else
using FsFile=Dolphin.IO.File;
using FsDirectory=Dolphin.IO.Directory;
using FsStream=Dolphin.IO.Stream;
using TextEncoding=Dolphin.Text.Encoding;
#endif

internal static class IplSwiss
{
    internal static string VideoMode(VideoModeSettings video)=>video.Resolution==0?"240p":video.Resolution==2?(video.Scan==2?"576p":"576i"):(video.Scan==2?"480p":"480i");
    internal static string SwissPath(string full)
    {if(!IplPaths.Safe(full)||!full.StartsWith("gcloader:/"))throw new InvalidDataException("Swiss launch requires a GC Loader path without line breaks.");return "gcldr:/"+full.Substring(10);}
    internal static string[] Arguments(string full,VideoModeSettings video)
    {string mode=VideoMode(video);return new[]{"swiss-gc.dol","Autoload="+SwissPath(full),"AutoBoot=Yes","BS2Boot=No","Prefer Clean Boot=No","Force NTSC Video Mode="+(video.Resolution==2?"Auto":mode),"Force PAL Video Mode="+mode};}
    // Swiss loads per-game settings after global argv. Update just this key so
    // an existing per-game INI cannot silently override the selected video mode.
    internal static string WithVideoMode(string text,string mode)
    {
        if(text.Length>8192)throw new InvalidDataException("Swiss game configuration is too large to edit safely.");
        string[] lines=text.Split('\n');bool found=false;
        for(int i=0;i<lines.Length;i++){int equals=lines[i].IndexOf('=');if(equals>=0&&lines[i].Substring(0,equals).Trim()=="Force Video Mode"){lines[i]="Force Video Mode="+mode+(lines[i].EndsWith("\r")?"\r":"");found=true;}}
        string result=string.Join("\n",lines);return found?result:result+(result.Length>0&&!result.EndsWith("\n")?"\n":"")+"Force Video Mode="+mode+"\n";
    }
    private static string Read(string path)
    {using(var file=FsFile.OpenRead(path)){if(file.Length>8192)throw new InvalidDataException("Swiss game configuration is too large.");var bytes=new byte[(int)file.Length];if(!IplPaths.ReadFully(file,bytes,0,bytes.Length))throw new EndOfStreamException();return TextEncoding.GetEncoding(28591).GetString(bytes,0,bytes.Length);}}
    private static void WriteVerified(string path,string text)
    {byte[] bytes=TextEncoding.GetEncoding(28591).GetBytes(text);using(var file=FsFile.Open(path,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush();}if(Read(path)!=text)throw new IOException("Swiss configuration write verification failed.");}
    internal static void ConfigureGame(string storageRoot,string gameId,VideoModeSettings video)
    {
        if(gameId.Length!=6)throw new ArgumentException("Invalid game identity.");for(int i=0;i<6;i++)if(!((gameId[i]>='A'&&gameId[i]<='Z')||(gameId[i]>='0'&&gameId[i]<='9')))throw new ArgumentException("Invalid game identity.");
        string directory=storageRoot.TrimEnd('/')+"/swiss/settings/game";FsDirectory.CreateDirectory(directory);
        string path=directory+"/"+gameId.Substring(0,4)+".ini",old=path+".videomode.old",backup=path+".videomode.bak",temporary=path+".videomode.tmp";
        if(!FsFile.Exists(path)&&FsFile.Exists(old))FsFile.Move(old,path);
        string before=FsFile.Exists(path)?Read(path):"",after=WithVideoMode(before,VideoMode(video));if(after==before)return;
        if(FsFile.Exists(path)&&!FsFile.Exists(backup))WriteVerified(backup,before);
        WriteVerified(temporary,after);
        if(FsFile.Exists(old))FsFile.Delete(old);
        bool moved=false;
        try{if(FsFile.Exists(path)){FsFile.Move(path,old);moved=true;}FsFile.Move(temporary,path);if(Read(path)!=after)throw new IOException("Swiss video configuration could not be verified.");}
        catch{if(moved&&FsFile.Exists(old)){if(FsFile.Exists(path))FsFile.Delete(path);FsFile.Move(old,path);}throw;}
        if(FsFile.Exists(old))FsFile.Delete(old);
    }
}
