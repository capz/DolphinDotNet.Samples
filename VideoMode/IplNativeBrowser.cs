using System;
using System.IO;
using Dolphin.Storage;
using FsFile=Dolphin.IO.File;
using Store=Dolphin.Storage.Storage;

internal sealed partial class IplNativeBrowser : IIplBrowserSource
{
    private readonly StorageDevice device;
    private IplGameIndex? index;
    public int Total=>index?.Count??0;
    public bool Busy=>index?.Busy??false;
    public string Status {get;private set;}="";
    public bool LaunchRequested {get;private set;}
    internal IplNativeBrowser(StorageDevice device=StorageDevice.GcLoader){this.device=device;}
    public void Begin(IplBrowserKind kind,string path,int slot,bool rescan)
    {
        Dispose();
        if(kind!=IplBrowserKind.Games)throw new InvalidOperationException("This storage screen is not enabled in this build.");
        if(!Store.IsMounted(device)&&!Store.Mount(device))throw new IOException("GC Loader SD unavailable. Check the card and firmware.");
        index=new IplGameIndex(Store.GetPath(device,"games"),Store.GetPath(device,"videomode/library.idx"));index.Begin(rescan);Status=index.Status;
    }
    public void Tick(){if(index!=null){index.Tick();Status=index.Status;}}
    public int ReadPage(int start,IplBrowserItem?[] output){if(index==null)return 0;int count=index.ReadPage(start,output);Status=index.Status;return count;}
    public IplArtwork? Artwork(IplBrowserItem item)
    {
        if(item.Kind!=IplItemKind.Game)return null;
        try{
            if(!Store.MountDisc(item.Path))return null;
            using(var file=Store.Open(StorageDevice.DvdDrive,"opening.bnr",FileMode.Open,FileAccess.Read,FileShare.Read)){
                var header=new byte[32];if(file.Length<0x1960||!IplPaths.ReadFully(file,header,0,header.Length)||header[0]!=66||header[1]!=78||header[2]!=82||(header[3]!=49&&header[3]!=50))return null;
                var pixels=new byte[6144];if(!IplPaths.ReadFully(file,pixels,0,pixels.Length))return null;
                file.Seek(0x1860,SeekOrigin.Begin);var title=new byte[64];if(IplPaths.ReadFully(file,title,0,title.Length)){string text=IplPaths.Ascii(title,0,title.Length);if(text.Length>0)item.Title=text;}
                return new IplArtwork(96,32,pixels,IplArtworkFormat.Rgb5A3);
            }
        }finally{if(Store.IsMounted(StorageDevice.DvdDrive))Store.Unmount(StorageDevice.DvdDrive);}
    }
    public void Open(IplBrowserItem item,VideoModeSettings video)
    {
#if IPL_CONSOLE_SERVICES
        if(item.Kind!=IplItemKind.Game)throw new InvalidOperationException("Unsupported launch type.");
        string path=IplPaths.Under(Store.GetPath(device,"games"),item.Path);var actual=IplPaths.ReadGame(path);
        if(actual==null||actual.GameId!=item.GameId)throw new InvalidDataException("Game image changed. Rescan and select it again.");
        string swiss=Store.GetPath(device,"swiss-gc.dol");if(!FsFile.Exists(swiss))throw new FileNotFoundException("Place official swiss-gc.dol at the GC Loader SD root.");
        using(var executable=Dolphin.IO.DolLauncher.Prepare(swiss)){
            executable.SetArguments(IplSwiss.Arguments(path,video));
            IplSwiss.ConfigureGame(Store.GetPath(device,""),item.GameId,video);
            Dolphin.IO.DolLauncher.RequestLaunch(executable);LaunchRequested=true;
        }
#else
        throw new InvalidOperationException("Swiss launch needs the console-services SDK source build.");
#endif
    }
    public string Describe(IplBrowserItem item)=>item.Path;
    public void Dispose(){index?.Dispose();index=null;}
}
