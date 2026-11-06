using System;
internal enum IplBrowserKind { Games, Sd, MemoryCards }
internal enum IplBrowserPanel { Grid, Launch, Info }
internal enum IplArtworkFormat { Rgba32, Rgb5A3, Png }
internal sealed class IplArtwork
{
    internal readonly int Width,Height;internal readonly byte[] Bytes;internal readonly IplArtworkFormat Format;
    internal IplArtwork(int width,int height,byte[] bytes,IplArtworkFormat format=IplArtworkFormat.Rgba32){Width=width;Height=height;Bytes=bytes;Format=format;}
}
internal interface IIplBrowserSource : IDisposable
{
    int Total {get;} bool Busy {get;} string Status {get;} bool LaunchRequested {get;}
    void Begin(IplBrowserKind kind,string path,int slot,bool rescan);
    void Tick();
    int ReadPage(int start,IplBrowserItem?[] output);
    IplArtwork? Artwork(IplBrowserItem item);
    void Open(IplBrowserItem item,VideoModeSettings video);
    string Describe(IplBrowserItem item);
}
internal interface IIplBrowserCanvas
{
    bool BeginBrowserCache(int style);void EndBrowserCache();bool DrawBrowserCache(int style);
    bool BrowserColorTriangle(IplPoint a,uint ca,IplPoint b,uint cb,IplPoint d,uint cd);
    void DrawArtworks();bool HasArtwork(int slot);
    bool DrawArtwork(int slot,float x,float y,float width,float height);
    void BeginBrowserMask();void BrowserMaskTriangle(IplPoint a,IplPoint b,IplPoint d);void EndBrowserMask();void DrawBrowserFrost();
}
internal sealed class IplBrowser : IDisposable
{
    private readonly IIplBrowserSource source;
    internal readonly IplBrowserItem?[] Items=new IplBrowserItem?[6];
    internal IplBrowserKind Kind {get;private set;}
    internal IplBrowserPanel Panel {get;private set;}
    internal string Path {get;private set;}="";
    internal string Status=>error.Length>0?error:source.Status;
    internal string Information {get;private set;}="";
    internal int Slot {get;private set;}
    internal int PageStart {get;private set;}
    internal int Selection {get;private set;}
    internal int Count {get;private set;}
    internal int Total=>source.Total;
    internal int Revision {get;private set;}
    internal bool Active {get;private set;}
    internal bool Busy=>source.Busy;
    internal bool LaunchRequested=>source.LaunchRequested;
    internal int Choice {get;private set;}
    internal IplBrowserItem? Selected=>Selection<Count?Items[Selection]:null;
    internal string Title=>Kind==IplBrowserKind.Games?"Game Library":Kind==IplBrowserKind.Sd?"SD Storage":"Memory Card";
    private string error="";
    private readonly float[] focus=new float[6];
    internal float Focus(int item)=>focus[item];
    internal IplBrowser(IIplBrowserSource source){this.source=source;}
    internal void Open(IplBrowserKind kind,string path="",int slot=0)
    {Kind=kind;Path=path;Slot=slot;PageStart=Selection=0;Panel=IplBrowserPanel.Grid;Active=true;error="";Array.Clear(Items,0,Items.Length);Count=0;Revision++;try{source.Begin(kind,path,slot,false);if(!source.Busy)Load();}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}}
    private void Load(){error="";if(PageStart>=Total&&Total>0)PageStart=(Total-1)/6*6;Count=0;Array.Clear(Items,0,Items.Length);Revision++;try{Count=source.ReadPage(PageStart,Items);if(Count<0||Count>6)throw new InvalidOperationException("Invalid browser page.");Selection=Math.Min(Selection,Math.Max(0,Count-1));}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";Count=0;Array.Clear(Items,0,Items.Length);}}
    internal void Tick(float dt,bool reducedMotion=false){if(!Active)return;for(int i=0;i<6;i++){float target=i==Selection?1:0;float step=dt/.18f;if(reducedMotion){focus[i]=target;continue;}focus[i]=target>focus[i]?Math.Min(target,focus[i]+step):Math.Max(target,focus[i]-step);}bool busy=source.Busy;try{source.Tick();if(busy&&!source.Busy)Load();}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}}
    internal void Direction(int x,int y)
    {
        if(Busy)return;
        if(Panel==IplBrowserPanel.Launch){if(x!=0||y!=0)Choice=1-Choice;return;}
        if(Panel!=IplBrowserPanel.Grid||Total==0)return;
        int next=PageStart+Selection+(x!=0?x:-y*3);if(next<0)next=Total-1;if(next>=Total)next=0;
        int page=next/6*6;Selection=next%6;if(page!=PageStart){PageStart=page;Load();}
    }
    internal void Confirm(VideoModeSettings video)
    {
        if(Busy)return;
        if(Panel==IplBrowserPanel.Info){Panel=IplBrowserPanel.Grid;return;}
        if(Panel==IplBrowserPanel.Launch){if(Choice==0){Panel=IplBrowserPanel.Grid;return;}if(Selected!=null)try{source.Open(Selected,video);error="";}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}return;}
        var item=Selected;if(item==null)return;
        if(item.Kind==IplItemKind.Directory){Open(Kind,item.Path,Slot);return;}
        if(item.Kind==IplItemKind.Game||item.Kind==IplItemKind.Dol){Panel=IplBrowserPanel.Launch;Choice=0;return;}
        try{Information=source.Describe(item);Panel=IplBrowserPanel.Info;}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}
    }
    // Returns false when B should leave the flat screen for its cube face.
    internal bool Back()
    {
        if(Panel!=IplBrowserPanel.Grid){Panel=IplBrowserPanel.Grid;error="";return true;}
        if(Kind==IplBrowserKind.Sd&&Path.Length>0){Open(Kind,IplPaths.Parent(Path),Slot);return true;}
        source.Dispose();Active=false;return false;
    }
    internal void Refresh(){if(Panel!=IplBrowserPanel.Grid)return;error="";PageStart=Selection=Count=0;Array.Clear(Items,0,Items.Length);Revision++;try{source.Begin(Kind,Path,Slot,true);if(!Busy)Load();}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}}
    internal IplArtwork? Artwork(int index){if(!Active||index<0||index>=Count||Items[index]==null)return null;try{return source.Artwork(Items[index]!);}catch(Exception){return null;}}
    public void Dispose(){source.Dispose();Active=false;}
}
