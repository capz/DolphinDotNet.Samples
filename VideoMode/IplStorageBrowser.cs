using System;
using System.IO;
using Dolphin.Storage;
using Store=Dolphin.Storage.Storage;
using FsFile=Dolphin.IO.File;
internal sealed class IplStorageBrowser : IIplBrowserSource,IIplCardOperations,IIplBrowserNotifications,IIplAnimatedArtwork,IIplBrowserPageSize,IIplBrowserCatalog
{
    private readonly StorageDevice fileDevice;
    private IIplBrowserSource? active;private int pageSize=6;
    public void SetPageSize(int count){pageSize=count;(active as IIplBrowserPageSize)?.SetPageSize(count);}
    internal IplStorageBrowser(StorageDevice fileDevice=StorageDevice.GcLoader){this.fileDevice=fileDevice;}
    public int Total=>active?.Total??0;public bool Busy=>active?.Busy??false;public string Status=>active?.Status??"";public bool LaunchRequested=>active?.LaunchRequested??false;
    public int Version=>(active as IIplBrowserNotifications)?.Version??0;
    public void Begin(IplBrowserKind kind,string path,int slot,bool rescan){active?.Dispose();active=kind==IplBrowserKind.Games?new IplNativeBrowser():kind==IplBrowserKind.Sd?new IplNativeFiles(fileDevice):new IplNativeCards();(active as IIplBrowserPageSize)?.SetPageSize(pageSize);active.Begin(kind,path,slot,rescan);}
    public int ReadCatalogPage(int start,int maximum)=>(active as IIplBrowserCatalog)?.ReadCatalogPage(start,maximum)??0;
    public IplBrowserItem? CatalogItem(int index)=>(active as IIplBrowserCatalog)?.CatalogItem(index);
    public IplItemKind CatalogKind(int index)=>(active as IIplBrowserCatalog)?.CatalogKind(index)??IplItemKind.File;
    public void Tick()=>active?.Tick();
    public int ReadPage(int start,IplBrowserItem?[] output)=>active?.ReadPage(start,output)??0;
    public IplArtwork? Artwork(IplBrowserItem item)=>active?.Artwork(item);
    public int ArtworkFrames(IplBrowserItem item)=>(active as IIplAnimatedArtwork)?.ArtworkFrames(item)??1;
    public int ArtworkDuration(IplBrowserItem item,int frame)=>(active as IIplAnimatedArtwork)?.ArtworkDuration(item,frame)??4;
    public bool ArtworkBounces(IplBrowserItem item)=>(active as IIplAnimatedArtwork)?.ArtworkBounces(item)??false;
    public IplArtwork? Artwork(IplBrowserItem item,int frame)=>active is IIplAnimatedArtwork animated?animated.Artwork(item,frame):frame==0?active?.Artwork(item):null;
    public void Open(IplBrowserItem item,VideoModeSettings video){if(active==null)throw new InvalidOperationException();active.Open(item,video);}
    public string Describe(IplBrowserItem item)=>active?.Describe(item)??"";
    public string SlotCaption(int slot)=>(active as IIplCardOperations)?.SlotCaption(slot)??"Unavailable";
    public bool CanCopy(IplBrowserItem item)=>(active as IIplCardOperations)?.CanCopy(item)??false;
    public bool CanMove(IplBrowserItem item)=>(active as IIplCardOperations)?.CanMove(item)??false;
    public int Progress=>(active as IIplCardOperations)?.Progress??0;
    public void Execute(IplCardAction action,IplBrowserItem? item){if(active is IIplCardOperations cards)cards.Execute(action,item);else throw new InvalidOperationException("Choose a memory card first.");}
    public void Cancel(){if(active is IIplCardOperations cards)cards.Cancel();}
    public void Dispose(){active?.Dispose();active=null;}
}
internal sealed class IplNativeFiles : IIplBrowserSource,IIplBrowserPageSize,IIplBrowserCatalog
{
    private readonly StorageDevice device;private IplSdDirectory? directory;private int pageSize=6;
    public void SetPageSize(int count){pageSize=count;directory?.SetPageSize(count);}
    public int Total=>directory?.Count??0;public bool Busy=>directory?.Busy??false;public string Status=>directory?.Status??"Storage unavailable.";public bool LaunchRequested {get;private set;}
    internal IplNativeFiles(StorageDevice device){this.device=device;}
    public void Begin(IplBrowserKind kind,string path,int slot,bool rescan){Dispose();bool mounted=Store.IsMounted(device)||(device==StorageDevice.DvdDrive?Store.MountDisc():Store.Mount(device));if(!mounted)throw new IOException("SD storage unavailable. Check the card and GC Loader firmware.");directory=new IplSdDirectory(Store.GetPath(device,""),pageSize);directory.Begin(path);}
    public int ReadCatalogPage(int start,int maximum)=>directory?.ReadCatalogPage(start,maximum)??0;
    public IplBrowserItem? CatalogItem(int index)=>directory?.CatalogItem(index);
    public IplItemKind CatalogKind(int index)=>directory?.CatalogKind(index)??IplItemKind.File;
    public void Tick()=>directory?.Tick();
    public int ReadPage(int start,IplBrowserItem?[] output)=>directory?.ReadPage(start,output)??0;
    private string Full(IplBrowserItem item)=>IplPaths.Under(Store.GetPath(device,""),Store.GetPath(device,directory?.Path.Length>0?directory.Path+"/"+item.Path:item.Path));
    public string Describe(IplBrowserItem item)=>directory?.Describe(item)??"";
    public IplArtwork? Artwork(IplBrowserItem item)
    {
        if(item.Kind==IplItemKind.Game){if(device==StorageDevice.DvdDrive)return null;using(var games=new IplNativeBrowser(device))return games.Artwork(new IplBrowserItem(Full(item),item.Title,item.Detail,item.Kind,item.Bytes,item.GameId));}
        if(!item.Path.ToLowerInvariant().EndsWith(".png"))return null;
        using(var file=FsFile.OpenRead(Full(item))){if(file.Length<8||file.Length>32768)return null;var png=new byte[(int)file.Length];if(!IplPaths.ReadFully(file,png,0,png.Length))return null;if(png.Length<24||png[0]!=137||png[1]!=80||png[2]!=78||png[3]!=71||png[16]!=0||png[17]!=0||png[20]!=0||png[21]!=0)return null;int width=png[18]*256+png[19],height=png[22]*256+png[23];if(width<1||height<1||width>256||height>256)return null;return new IplArtwork(0,0,png,IplArtworkFormat.Png);}
    }
    public void Open(IplBrowserItem item,VideoModeSettings video)
    {
        string path=Full(item);
        if(item.Kind==IplItemKind.Dol){using(var executable=Dolphin.IO.DolLauncher.Prepare(path))Dolphin.IO.DolLauncher.RequestLaunch(executable);LaunchRequested=true;return;}
        if(item.Kind!=IplItemKind.Game)throw new InvalidOperationException("Choose a GameCube image or DOL application.");
#if IPL_CONSOLE_SERVICES
        if(device!=StorageDevice.GcLoader)throw new InvalidOperationException("Swiss game launch requires GC Loader storage.");
        var actual=IplPaths.ReadGame(path);if(actual==null||actual.GameId!=item.GameId)throw new InvalidDataException("Game changed. Refresh this folder.");
        using(var executable=Dolphin.IO.DolLauncher.Prepare(Store.GetPath(device,"swiss-gc.dol"))){executable.SetArguments(IplSwiss.Arguments(path,video));IplSwiss.ConfigureGame(Store.GetPath(device,""),item.GameId,video);Dolphin.IO.DolLauncher.RequestLaunch(executable);LaunchRequested=true;}
#else
        throw new InvalidOperationException("Swiss launch needs the console-services SDK source build.");
#endif
    }
    public void Dispose(){directory?.Dispose();directory=null;}
}
