using System;
using System.Collections.Generic;
using System.IO;
#if IPL_HOST_FILES
using FsFile=System.IO.File;
using FsDirectory=System.IO.Directory;
using FsPath=System.IO.Path;
#else
using FsFile=Dolphin.IO.File;
using FsDirectory=Dolphin.IO.Directory;
using FsPath=Dolphin.IO.Path;
#endif
// Read-only browser. Enumeration retains one six-entry page; opening is handled
// separately. This class has no file mutation operations.
internal sealed class IplSdDirectory : IDisposable
{
    private readonly string root;
    private IEnumerator<string>? entries;
    private bool directories,loadingPage;
    private int seen,wanted,cachedStart;
    private readonly IplBrowserItem?[] page=new IplBrowserItem?[6];
    internal string Path {get;private set;}="";
    internal string Status {get;private set;}="";
    internal int Count {get;private set;}
    internal bool Busy=>entries!=null;
    internal IplSdDirectory(string root){this.root=FsPath.GetFullPath(root);}
    private string Full(string relative)=>relative.Length==0?root:IplPaths.Under(root,FsPath.Combine(root,relative));
    private string Relative(string full){string normalized=IplPaths.Under(root,full);return normalized.Substring(root.TrimEnd('/').Length+1);}
    internal void Begin(string path){Dispose();Full(path);Path=path;Count=0;Start(0,false);}
    private void Start(int first,bool onlyPage){Dispose();wanted=cachedStart=first;seen=0;loadingPage=onlyPage;directories=true;Array.Clear(page,0,page.Length);entries=FsDirectory.EnumerateDirectories(Full(Path)).GetEnumerator();Status="Reading SD storage...";}
    internal void Tick()
    {try{TickEntry();}catch(Exception){Dispose();Status="Could not read this folder. Refresh or go back.";throw;}}
    private void TickEntry()
    {
        if(entries==null)return;
        if(!entries.MoveNext()){
            entries.Dispose();entries=null;
            if(directories){directories=false;entries=FsDirectory.EnumerateFiles(Full(Path)).GetEnumerator();return;}
            Count=seen;Status=Count==0?"This folder is empty.":"Browse folders or open a file.";return;
        }
        string full=IplPaths.Under(root,entries.Current);
        if(seen>=wanted&&seen<wanted+6){string relative=Relative(full),name=IplPaths.Name(full);IplItemKind kind=directories?IplItemKind.Directory:IplPaths.Game(full)?IplItemKind.Game:FsPath.GetExtension(full).ToLowerInvariant()==".dol"?IplItemKind.Dol:IplItemKind.File;
            long length=0;string id="",detail=directories?"Folder":kind==IplItemKind.Dol?"Application":"File";
            if(!directories)try{using(var file=FsFile.OpenRead(full))length=file.Length;}catch(Exception){}
            string title=name;if(kind==IplItemKind.Game){var game=IplPaths.ReadGame(full);if(game==null){kind=IplItemKind.File;detail="Unrecognized image";}else{title=game.Title;id=game.GameId;detail=id;}}
            if(kind==IplItemKind.File)detail=length.ToString()+" bytes";
            page[seen-wanted]=new IplBrowserItem(relative,title,detail,kind,length,id);
        }
        seen++;if(loadingPage&&seen==wanted+6){entries.Dispose();entries=null;Status="Browse folders or open a file.";}
    }
    internal int ReadPage(int start,IplBrowserItem?[] output)
    {if(start<0||output.Length!=6)throw new ArgumentException();Array.Clear(output,0,output.Length);if(Busy)return 0;if(start!=cachedStart){Start(start,true);return 0;}int n=0;for(int i=0;i<6;i++){output[i]=page[i];if(page[i]!=null)n++;}return n;}
    internal string Describe(IplBrowserItem item)
    {
        string full=Full(item.Path);using(var file=FsFile.OpenRead(full)){int n=(int)Math.Min(512,file.Length);var bytes=new byte[n];if(!IplPaths.ReadFully(file,bytes,0,n))throw new EndOfStreamException();bool text=true;for(int i=0;i<n;i++)if(bytes[i]==0){text=false;break;}
            string result=IplPaths.Name(full)+"\n"+file.Length.ToString()+" bytes\n";
            if(text){result+="Text preview (first 512 bytes)\n";var line=new char[52];int used=0,lines=0;for(int i=0;i<n&&lines<5;i++){int b=bytes[i];if(b==13)continue;if(b==10||used==52){result+=new string(line,0,used)+"\n";used=0;lines++;if(b==10)continue;}line[used++]=b>=32&&b<127?(char)b:'?';}if(used>0&&lines<5)result+=new string(line,0,used);}
            else{const string hex="0123456789ABCDEF";result+="Binary preview (first 64 bytes)\n";var line=new char[48];for(int offset=0;offset<Math.Min(n,64);offset+=16){int count=Math.Min(16,Math.Min(n,64)-offset);for(int i=0;i<count;i++){int b=bytes[offset+i];line[i*3]=hex[b>>4];line[i*3+1]=hex[b&15];line[i*3+2]=' ';}result+=new string(line,0,count*3)+"\n";}}
            return result;
        }
    }
    public void Dispose(){entries?.Dispose();entries=null;}
}
