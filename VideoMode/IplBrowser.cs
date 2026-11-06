using System;
internal enum IplBrowserKind { Games, Sd, MemoryCards }
internal enum IplBrowserPanel { Grid, Launch, Info, CardActions, CardConfirm, Progress }
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
    internal string Status=>error.Length>0?error:source.Status??"";
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
    internal float AnimationTime {get;private set;}
    internal bool ReducedMotion {get;private set;}
    private string error="";
    private int observedVersion;
    internal IplCardAction CardAction {get;private set;}
    internal int FormatStage {get;private set;}
    internal int TransferProgress=>(source as IIplCardOperations)?.Progress??0;
    internal string SlotCaption(int slot)=>(source as IIplCardOperations)?.SlotCaption(slot)??"Unavailable";
    internal bool CanCopy=>Selected!=null&&(source as IIplCardOperations)?.CanCopy(Selected)==true;
    internal bool CanMove=>Selected!=null&&(source as IIplCardOperations)?.CanMove(Selected)==true;
    internal void SwitchSlot(int slot){if(Kind==IplBrowserKind.MemoryCards&&Panel==IplBrowserPanel.Grid&&!Busy)Open(Kind,"",slot);}
    private int SourceVersion=>(source as IIplBrowserNotifications)?.Version??0;
    private readonly float[] focus=new float[6];
    internal float Focus(int item)=>focus[item];
    internal IplBrowser(IIplBrowserSource source){this.source=source;}
    internal void Open(IplBrowserKind kind,string path="",int slot=0)
    {Kind=kind;Path=path;Slot=slot;PageStart=Selection=0;Panel=IplBrowserPanel.Grid;Active=true;error="";Array.Clear(Items,0,Items.Length);Count=0;Revision++;try{source.Begin(kind,path,slot,false);observedVersion=SourceVersion;if(!source.Busy)Load();}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}}
    private void Load(){error="";if(PageStart>=Total&&Total>0)PageStart=(Total-1)/6*6;Count=0;Array.Clear(Items,0,Items.Length);Revision++;try{Count=source.ReadPage(PageStart,Items);if(Count<0||Count>6)throw new InvalidOperationException("Invalid browser page.");Selection=Math.Min(Selection,Math.Max(0,Count-1));observedVersion=SourceVersion;}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";Count=0;Array.Clear(Items,0,Items.Length);}}
    internal void Tick(float dt,bool reducedMotion=false){if(!Active)return;ReducedMotion=reducedMotion;AnimationTime+=dt;for(int i=0;i<6;i++){float target=i==Selection?1:0;float step=dt/.18f;if(reducedMotion){focus[i]=target;continue;}focus[i]=target>focus[i]?Math.Min(target,focus[i]+step):Math.Max(target,focus[i]-step);}bool busy=source.Busy;try{source.Tick();if((busy&&!source.Busy)||SourceVersion!=observedVersion){if(Kind==IplBrowserKind.MemoryCards)Panel=IplBrowserPanel.Grid;Load();}}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}}
    internal void Direction(int x,int y)
    {
        if(Busy)return;
        if(Panel==IplBrowserPanel.Launch||Panel==IplBrowserPanel.CardConfirm){if(x!=0||y!=0)Choice=x!=0?(x>0?1:0):(y<0?1:0);return;}
        if(Panel==IplBrowserPanel.CardActions){int choice=Choice+(x!=0?x:-y);Choice=choice<0?3:choice>3?0:choice;return;}
        if(Panel!=IplBrowserPanel.Grid||Total==0)return;
        int next=PageStart+Selection+(x!=0?x:-y*3);if(next<0)next=Total-1;if(next>=Total)next=0;
        int page=next/6*6;Selection=next%6;if(page!=PageStart){PageStart=page;Load();}
    }
    internal void Confirm(VideoModeSettings video)
    {
        if(Busy)return;
        if(Panel==IplBrowserPanel.CardActions){
            if(Choice==0){Panel=IplBrowserPanel.Grid;return;}
            if(Choice==1&&!CanCopy){error="This saved file is copy protected.";return;}
            if(Choice==2&&!CanMove){error="This saved file is move protected.";return;}
            CardAction=Choice==1?IplCardAction.Copy:Choice==2?IplCardAction.Move:IplCardAction.Delete;Choice=0;Panel=IplBrowserPanel.CardConfirm;return;
        }
        if(Panel==IplBrowserPanel.CardConfirm){
            if(Choice==0){Panel=IplBrowserPanel.Grid;return;}
            if(CardAction==IplCardAction.Format&&FormatStage==1){FormatStage=2;Choice=0;return;}
            try{if(source is IIplCardOperations cards)cards.Execute(CardAction,Selected);else throw new InvalidOperationException("Card service unavailable.");error="";Panel=source.Busy?IplBrowserPanel.Progress:IplBrowserPanel.Grid;if(!source.Busy)Load();}
            catch(Exception e){error=e.Message??"Memory-card operation failed. Refresh the slot.";Panel=IplBrowserPanel.Grid;}
            return;
        }
        if(Panel==IplBrowserPanel.Info){Panel=IplBrowserPanel.Grid;return;}
        if(Panel==IplBrowserPanel.Launch){if(Choice==0){Panel=IplBrowserPanel.Grid;return;}if(Selected!=null)try{source.Open(Selected,video);error="";}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}return;}
        var item=Selected;if(item==null)return;
        if(item.Kind==IplItemKind.Save){try{Information=source.Describe(item);Choice=0;Panel=IplBrowserPanel.CardActions;}catch(Exception e){error=e.Message??"Could not read this save.";}return;}
        if(item.Kind==IplItemKind.CardFormat){CardAction=IplCardAction.Format;FormatStage=1;Choice=0;Panel=IplBrowserPanel.CardConfirm;return;}
        if(item.Kind==IplItemKind.Directory){Open(Kind,item.Path,Slot);return;}
        if(item.Kind==IplItemKind.Game||item.Kind==IplItemKind.Dol){Panel=IplBrowserPanel.Launch;Choice=0;return;}
        try{Information=source.Describe(item);Panel=IplBrowserPanel.Info;}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}
    }
    // Returns false when B should leave the flat screen for its cube face.
    internal bool Back()
    {
        if(Panel==IplBrowserPanel.Progress){try{(source as IIplCardOperations)?.Cancel();error="";}catch(Exception e){error=e.Message??"Transfer stopped; check the destination card.";}Panel=IplBrowserPanel.Grid;Load();return true;}
        if(Panel!=IplBrowserPanel.Grid){Panel=IplBrowserPanel.Grid;error="";return true;}
        if(Kind==IplBrowserKind.Sd&&Path.Length>0){Open(Kind,IplPaths.Parent(Path),Slot);return true;}
        source.Dispose();Active=false;return false;
    }
    internal void Refresh(){if(Panel!=IplBrowserPanel.Grid)return;error="";PageStart=Selection=Count=0;Array.Clear(Items,0,Items.Length);Revision++;try{source.Begin(Kind,Path,Slot,true);if(!Busy)Load();}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}}
    internal int ArtworkFrames(int index)=>Items[index]!=null?Math.Max(0,Math.Min(8,(source as IIplAnimatedArtwork)?.ArtworkFrames(Items[index]!)??1)):0;
    internal int ArtworkDuration(int index,int frame)=>Math.Max(1,(source as IIplAnimatedArtwork)?.ArtworkDuration(Items[index]!,frame)??4);
    internal bool ArtworkBounces(int index)=>(source as IIplAnimatedArtwork)?.ArtworkBounces(Items[index]!)??false;
    internal IplArtwork? Artwork(int index,int frame=0){if(!Active||index<0||index>=Count||Items[index]==null)return null;try{return source is IIplAnimatedArtwork animated?animated.Artwork(Items[index]!,frame):frame==0?source.Artwork(Items[index]!):null;}catch(Exception){return null;}}
    public void Dispose(){source.Dispose();Active=false;}
}
