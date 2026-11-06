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
    private IplSdPage page;
    internal string Path {get;private set;}="";
    internal string Status {get;private set;}="";
    internal int Count {get;private set;}
    internal bool Busy=>entries!=null;
    internal IplSdDirectory(string root,int pageSize=6){this.root=FsPath.GetFullPath(root);page=new IplSdPage(pageSize);}
    internal void SetPageSize(int count){if(count<1||count>IplStorageLayout.Capacity)throw new ArgumentOutOfRangeException();if(count==page.Capacity)return;Stop();page.Dispose();page=new IplSdPage(count);cachedStart=-1;}
    private string Full(string relative)=>relative.Length==0?root:IplPaths.Under(root,FsPath.Combine(root,relative));
    internal void Begin(string path){Stop();Full(path);Path=path;Count=0;Start(0,false);}
    private void Start(int first,bool onlyPage){Stop();wanted=cachedStart=first;seen=0;loadingPage=onlyPage;directories=true;page.Clear();entries=FsDirectory.EnumerateDirectories(Full(Path)).GetEnumerator();Status="Reading SD storage...";}
    internal void Tick()
    {try{TickEntry();}catch(Exception){Stop();Status="Could not read this folder. Refresh or go back.";throw;}}
    private void TickEntry()
    {
        if(entries==null)return;
        if(!entries.MoveNext()){
            entries.Dispose();entries=null;
            if(directories){directories=false;entries=FsDirectory.EnumerateFiles(Full(Path)).GetEnumerator();return;}
            Count=seen;Status=Count==0?"This folder is empty.":"Browse folders or open a file.";return;
        }
        string full=IplPaths.Under(root,entries.Current);
        if(seen>=wanted&&seen<wanted+page.Capacity){string name=IplPaths.Name(full);IplItemKind kind=directories?IplItemKind.Directory:IplPaths.Game(full)?IplItemKind.Game:FsPath.GetExtension(full).ToLowerInvariant()==".dol"?IplItemKind.Dol:IplItemKind.File;
            long length=directories?0:-1;string id="";
            if(!directories)try{using(var file=FsFile.OpenRead(full))length=file.Length;}catch(Exception){}
            if(kind==IplItemKind.Game){var game=IplPaths.ReadGame(full);if(game==null){kind=IplItemKind.File;}else{id=game.GameId;}}
            page.Set(seen-wanted,name,kind,length,id);
        }
        seen++;if(loadingPage&&seen==wanted+page.Capacity){entries.Dispose();entries=null;Status="Browse folders or open a file.";}
    }
    internal int ReadCatalogPage(int start,int maximum)
    {if(start<0||maximum!=page.Capacity)throw new ArgumentException();if(Busy)return 0;if(start!=cachedStart){Start(start,true);return 0;}return page.Count;}
    internal IplBrowserItem? CatalogItem(int index)=>page.Item(index);
    internal IplItemKind CatalogKind(int index)=>page.Kind(index);
    internal int ReadPage(int start,IplBrowserItem?[] output)
    {Array.Clear(output,0,output.Length);int count=ReadCatalogPage(start,output.Length);for(int i=0;i<count;i++)output[i]=page.Item(i);return count;}
    internal string Describe(IplBrowserItem item)
    {
        string full=Full(Path.Length==0?item.Path:Path+"/"+item.Path);using(var file=FsFile.OpenRead(full)){int n=(int)Math.Min(512,file.Length);var bytes=new byte[n];if(!IplPaths.ReadFully(file,bytes,0,n))throw new EndOfStreamException();bool text=true;for(int i=0;i<n;i++)if(bytes[i]==0){text=false;break;}
            string result=IplPaths.Name(full)+"\n"+file.Length.ToString()+" bytes\n";
            if(text){result+="Text preview (first 512 bytes)\n";var line=new char[52];int used=0,lines=0;for(int i=0;i<n&&lines<5;i++){int b=bytes[i];if(b==13)continue;if(b==10||used==52){result+=new string(line,0,used)+"\n";used=0;lines++;if(b==10)continue;}line[used++]=b>=32&&b<127?(char)b:'?';}if(used>0&&lines<5)result+=new string(line,0,used);}
            else{const string hex="0123456789ABCDEF";result+="Binary preview (first 64 bytes)\n";var line=new char[48];for(int offset=0;offset<Math.Min(n,64);offset+=16){int count=Math.Min(16,Math.Min(n,64)-offset);for(int i=0;i<count;i++){int b=bytes[offset+i];line[i*3]=hex[b>>4];line[i*3+1]=hex[b&15];line[i*3+2]=' ';}result+=new string(line,0,count*3)+"\n";}}
            return result;
        }
    }
    private void Stop(){entries?.Dispose();entries=null;}
    public void Dispose(){Stop();page.Dispose();}
}
