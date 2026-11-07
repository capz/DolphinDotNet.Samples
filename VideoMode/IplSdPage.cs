using System;
#if !IPL_HOST_FILES
using Dolphin.Graphics;
#endif

internal sealed class IplSdPage : IDisposable
{
    private const int RecordBytes=544,NameAt=32;
    internal readonly int Capacity;
    internal int Count {get;private set;}
    private readonly byte[] scratch=new byte[RecordBytes],kinds;
#if IPL_HOST_FILES
    private readonly byte[] data;
#else
    private readonly NativeBuffer data;
#endif
    internal IplSdPage(int capacity){if(capacity<1||capacity>IplStorageLayout.Capacity)throw new ArgumentOutOfRangeException();Capacity=capacity;kinds=new byte[capacity];
#if IPL_HOST_FILES
        data=new byte[capacity*RecordBytes];
#else
        data=NativeBuffer.Allocate(capacity*RecordBytes);
#endif
    }
    internal void Clear(){Count=0;}
    internal IplItemKind Kind(int index){if(index<0||index>=Count)throw new ArgumentOutOfRangeException();return (IplItemKind)kinds[index];}
    internal void Set(int index,string name,IplItemKind kind,long bytes,string id)
    {
        if(index<0||index>=Capacity||name.Length>256||id.Length>6)throw new ArgumentException("Storage entry is too long.");
        Array.Clear(scratch,0,RecordBytes);scratch[0]=(byte)name.Length;scratch[1]=(byte)(name.Length>>8);scratch[2]=(byte)kind;scratch[3]=(byte)id.Length;
        for(int i=0;i<8;i++)scratch[8+i]=(byte)(bytes>>(i*8));
        for(int i=0;i<id.Length;i++)scratch[16+i]=(byte)id[i];
        for(int i=0;i<name.Length;i++){scratch[NameAt+i*2]=(byte)name[i];scratch[NameAt+i*2+1]=(byte)(name[i]>>8);}
#if IPL_HOST_FILES
        Array.Copy(scratch,0,data,index*RecordBytes,RecordBytes);
#else
        data.Write(scratch,0,index*RecordBytes,RecordBytes);
#endif
        kinds[index]=(byte)kind;Count=Math.Max(Count,index+1);
    }
    internal IplBrowserItem? Item(int index)
    {
        if(index<0||index>=Count)return null;
#if IPL_HOST_FILES
        Array.Copy(data,index*RecordBytes,scratch,0,RecordBytes);
#else
        data.Read(scratch,index*RecordBytes,0,RecordBytes);
#endif
        int length=scratch[0]|scratch[1]<<8;var chars=new char[length];for(int i=0;i<length;i++)chars[i]=(char)(scratch[NameAt+i*2]|scratch[NameAt+i*2+1]<<8);string name=new string(chars);
        long bytes=0;for(int i=0;i<8;i++)bytes|=(long)scratch[8+i]<<(i*8);
        var code=new char[scratch[3]];for(int i=0;i<code.Length;i++)code[i]=(char)scratch[16+i];
        var kind=(IplItemKind)scratch[2];return new IplBrowserItem(name,name,kind==IplItemKind.Directory?"Folder":IplStorageLayout.FileSize(bytes),kind,bytes,new string(code));
    }
    public void Dispose(){
#if !IPL_HOST_FILES
        data.Dispose();
#endif
        Count=0;
    }
}
