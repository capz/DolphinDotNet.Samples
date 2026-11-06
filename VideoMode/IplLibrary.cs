using System;
using System.Collections.Generic;
using System.IO;
#if IPL_HOST_FILES
using FsFile=System.IO.File;
using FsDirectory=System.IO.Directory;
using FsPath=System.IO.Path;
using FsStream=System.IO.Stream;
using TextEncoding=System.Text.Encoding;
#else
using FsFile=Dolphin.IO.File;
using FsDirectory=Dolphin.IO.Directory;
using FsPath=Dolphin.IO.Path;
using FsStream=Dolphin.IO.Stream;
using TextEncoding=Dolphin.Text.Encoding;
#endif

internal enum IplItemKind { Directory, Game, Dol, File, Save, CardFormat }
internal sealed class IplBrowserItem
{
    internal readonly string Path,GameId;
    internal string Title,Detail;
    internal object? Handle;
    internal readonly IplItemKind Kind;
    internal readonly long Bytes;
    internal IplBrowserItem(string path,string title,string detail,IplItemKind kind,long bytes=0,string gameId="")
    {Path=path;Title=title;Detail=detail;Kind=kind;Bytes=bytes;GameId=gameId;}
}
internal static class IplPaths
{
    internal static bool Safe(string path)
    {return path.Length>0&&path.Length<=768&&path.IndexOf('\0')<0&&path.IndexOf('\n')<0&&path.IndexOf('\r')<0;}
    internal static string Under(string root,string path)
    {
        string full=FsPath.GetFullPath(path),basePath=FsPath.GetFullPath(root);if(basePath.EndsWith("/"))basePath=basePath.Substring(0,basePath.Length-1);
        if(!Safe(full)||!full.StartsWith(basePath+"/"))throw new InvalidDataException("Path is outside the storage root.");return full;
    }
    internal static bool Game(string path){string ext=FsPath.GetExtension(path).ToLowerInvariant();return ext==".iso"||ext==".gcm";}
    internal static string Name(string path)=>FsPath.GetFileName(path);
    internal static string Parent(string path)=>FsPath.GetDirectoryName(path)??"";
    internal static bool ReadFully(FsStream input,byte[] data,int offset,int length)
    {while(length>0){int n=input.Read(data,offset,length);if(n<=0)return false;offset+=n;length-=n;}return true;}
    internal static uint Crc(byte[] data,int length){uint crc=uint.MaxValue;for(int i=0;i<length;i++){crc^=data[i];for(int b=0;b<8;b++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}return ~crc;}
    internal static uint Get(byte[] data,int at)=>(uint)data[at]|(uint)data[at+1]<<8|(uint)data[at+2]<<16|(uint)data[at+3]<<24;
    internal static void Put(byte[] data,int at,uint value){for(int i=0;i<4;i++)data[at+i]=(byte)(value>>(i*8));}
    internal static string Ascii(byte[] data,int at,int maximum)
    {int n=0;while(n<maximum&&data[at+n]!=0)n++;var chars=new char[n];for(int i=0;i<n;i++)chars[i]=data[at+i]>=32&&data[at+i]<127?(char)data[at+i]:'?';return new string(chars).Trim();}
    internal static IplBrowserItem? ReadGame(string path)
    {
        using(var file=FsFile.OpenRead(path)){var header=new byte[96];if(file.Length<0x440||!ReadFully(file,header,0,header.Length)||header[28]!=0xc2||header[29]!=0x33||header[30]!=0x9f||header[31]!=0x3d)return null;
            string id=Ascii(header,0,6);if(id.Length!=6)return null;for(int i=0;i<id.Length;i++)if(!((id[i]>='0'&&id[i]<='9')||(id[i]>='A'&&id[i]<='Z')))return null;
            string title=Ascii(header,32,64);return new IplBrowserItem(path,title.Length==0?Name(path):title,id,IplItemKind.Game,file.Length,id);}
    }
}

// Fixed-size, individually checksummed records permit O(1) page seeks. Scanning
// advances one file/directory operation per Tick; only six entries are retained.
internal sealed class IplGameIndex : IDisposable
{
    internal const int PageSize=6,HeaderSize=32,RecordSize=1024,MaximumGames=16384;
    private readonly string root,indexPath;
    private readonly IEnumerator<string>?[] files=new IEnumerator<string>?[16],directories=new IEnumerator<string>?[16];
    private readonly IplBrowserItem?[] first=new IplBrowserItem?[PageSize];
    private readonly byte[] record=new byte[RecordSize];
    private FsStream? writer;
    private int depth=-1,seen,firstStart,wantedStart;
    private bool collecting,haveCache;
    internal int Count {get;private set;}
    internal bool Busy=>depth>=0;
    internal string Status {get;private set;}="";
    internal IplGameIndex(string root,string indexPath){this.root=FsPath.GetFullPath(root);this.indexPath=indexPath;}
    private byte[] Header(int count){var h=new byte[HeaderSize];h[0]=73;h[1]=80;h[2]=76;h[3]=71;h[4]=1;IplPaths.Put(h,8,(uint)count);var text=TextEncoding.UTF8.GetBytes(root);IplPaths.Put(h,12,IplPaths.Crc(text,text.Length));IplPaths.Put(h,28,IplPaths.Crc(h,28));return h;}
    internal void Begin(bool refresh=false)
    {
        Dispose();haveCache=false;
        if(!refresh)try{using(var file=FsFile.OpenRead(indexPath)){var h=new byte[HeaderSize];if(IplPaths.ReadFully(file,h,0,h.Length)){int count=(int)IplPaths.Get(h,8);byte[] expected=Header(count);bool valid=count>=0&&count<=MaximumGames&&file.Length==HeaderSize+(long)count*RecordSize;
            for(int i=0;i<h.Length;i++)if(h[i]!=expected[i])valid=false;
            if(valid){Count=count;haveCache=true;Status="Cached library. Y rescans games/.";return;}}}}catch(Exception){}
        Count=0;StartScan(0,false);
    }
    private void Push(string path){var nextFiles=FsDirectory.EnumerateFiles(path).GetEnumerator();IEnumerator<string> nextDirectories;try{nextDirectories=FsDirectory.EnumerateDirectories(path).GetEnumerator();}catch{nextFiles.Dispose();throw;}depth++;files[depth]=nextFiles;directories[depth]=nextDirectories;}
    private void StartScan(int start,bool pageOnly)
    {
        Dispose();seen=0;wantedStart=start;firstStart=start;Array.Clear(first,0,first.Length);collecting=pageOnly;
        if(!pageOnly)try{FsDirectory.CreateDirectory(FsPath.GetDirectoryName(indexPath)!);writer=FsFile.Open(indexPath+".tmp",FileMode.Create,FileAccess.Write,FileShare.None);byte[] h=Header(0);writer.Write(h,0,h.Length);}catch(Exception){writer?.Dispose();writer=null;}
        try{Push(root);Status=pageOnly?"Reading library page...":"Indexing games/...";}catch(Exception){Dispose();Status="The games/ folder is unavailable.";}
    }
    private void Pop(){files[depth]?.Dispose();directories[depth]?.Dispose();files[depth]=directories[depth]=null;depth--;}
    internal void Tick()
    {
        if(!Busy)return;
        try{
            if(files[depth]!.MoveNext()){
                string path=IplPaths.Under(root,files[depth]!.Current);if(!IplPaths.Game(path))return;
                IplBrowserItem? item;try{item=IplPaths.ReadGame(path);}catch(Exception){return;}if(item==null)return;
                if(seen>=wantedStart&&seen<wantedStart+PageSize)first[seen-wantedStart]=item;
                if(writer!=null){Encode(item);try{writer.Write(record,0,record.Length);}catch(Exception){writer.Dispose();writer=null;}}
                seen++;if(seen>=MaximumGames){Status="Library limit reached (16384 games).";Finish();}
                else if(collecting&&seen==wantedStart+PageSize){while(Busy)Pop();Status="Library page loaded (SD cache unavailable).";}
                return;
            }
            if(directories[depth]!.MoveNext()){
                string path=IplPaths.Under(root,directories[depth]!.Current);if(depth<15)try{Push(path);}catch(Exception){}
                return;
            }
            Pop();if(!Busy)Finish();
        }catch(Exception){Dispose();Count=0;Status="Library scan failed. Check the SD card.";}
    }
    private void Finish()
    {
        while(Busy)Pop();if(!collecting)Count=seen;else if(seen<Count)Count=seen;
        if(writer!=null){try{byte[] h=Header(Count);writer.Seek(0,SeekOrigin.Begin);writer.Write(h,0,h.Length);writer.Flush();writer.Dispose();writer=null;if(FsFile.Exists(indexPath))FsFile.Delete(indexPath);FsFile.Move(indexPath+".tmp",indexPath);haveCache=true;}catch(Exception){writer?.Dispose();writer=null;haveCache=false;}}
        if(!Status.StartsWith("Library limit"))Status=haveCache?"Indexed "+Count.ToString()+" games. Y rescans.":"SD cache unavailable; library pages are read incrementally.";
    }
    private void Encode(IplBrowserItem item)
    {
        Array.Clear(record,0,record.Length);byte[] path=TextEncoding.UTF8.GetBytes(item.Path),title=TextEncoding.UTF8.GetBytes(item.Title);
        if(path.Length>768||title.Length>128)throw new InvalidDataException("Library record too large.");
        record[0]=(byte)path.Length;record[1]=(byte)(path.Length>>8);record[2]=(byte)title.Length;record[3]=(byte)(title.Length>>8);for(int i=0;i<6;i++)record[4+i]=(byte)item.GameId[i];
        Array.Copy(path,0,record,16,path.Length);Array.Copy(title,0,record,784,title.Length);IplPaths.Put(record,1020,IplPaths.Crc(record,1020));
    }
    private IplBrowserItem Decode()
    {
        int pn=record[0]|record[1]<<8,tn=record[2]|record[3]<<8;if(pn<1||pn>768||tn>128||IplPaths.Get(record,1020)!=IplPaths.Crc(record,1020))throw new InvalidDataException("Corrupt library record. Rescan with Y.");
        string path=IplPaths.Under(root,TextEncoding.UTF8.GetString(record,16,pn));if(!IplPaths.Game(path))throw new InvalidDataException("Invalid library image path.");string id=IplPaths.Ascii(record,4,6);if(id.Length!=6)throw new InvalidDataException("Invalid library identity.");
        return new IplBrowserItem(path,TextEncoding.UTF8.GetString(record,784,tn),id,IplItemKind.Game,0,id);
    }
    internal int ReadPage(int start,IplBrowserItem?[] output)
    {
        if(start<0||output.Length!=PageSize)throw new ArgumentException("Invalid library page.");Array.Clear(output,0,output.Length);if(Busy)return 0;
        if(haveCache){using(var file=FsFile.OpenRead(indexPath)){file.Seek(HeaderSize+(long)start*RecordSize,SeekOrigin.Begin);int n=Math.Max(0,Math.Min(PageSize,Count-start));for(int i=0;i<n;i++){if(!IplPaths.ReadFully(file,record,0,record.Length))throw new EndOfStreamException();output[i]=Decode();}return n;}}
        if(firstStart!=start){StartScan(start,true);return 0;}
        int count=0;for(int i=0;i<PageSize;i++){output[i]=first[i];if(first[i]!=null)count++;}return count;
    }
    public void Dispose(){while(Busy)Pop();writer?.Dispose();writer=null;}
}
