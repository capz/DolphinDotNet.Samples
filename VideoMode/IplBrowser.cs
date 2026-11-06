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
    void BeginBrowserMask();void BrowserMaskTriangle(IplPoint a,IplPoint b,IplPoint d);void EndBrowserMask(int expectedVertices=576);void DrawBrowserFrost();
}
internal sealed class IplBrowser : IDisposable
{
    private readonly IIplBrowserSource source;
    internal IplBrowserItem?[] Items {get;private set;}=new IplBrowserItem?[6];
    private bool sdList,cardList;
    internal bool Compact=>Kind!=IplBrowserKind.Games;
    internal bool ListView=>Kind==IplBrowserKind.Sd?sdList:Kind==IplBrowserKind.MemoryCards&&cardList;
    internal int PageSize=>Compact?IplStorageLayout.PageSize(ListView):6;
    internal int Columns=>Compact?(ListView?1:IplStorageLayout.Columns):3;
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
    internal int ContentRevision {get;private set;}
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
    private readonly float[] focus=new float[IplStorageLayout.Capacity];
    internal float Focus(int item)=>focus[item];
    internal IplBrowser(IIplBrowserSource source){this.source=source;}
    internal void Open(IplBrowserKind kind,string path="",int slot=0)
    {Kind=kind;ResizePage();Path=path;Slot=slot;PageStart=Selection=0;Panel=IplBrowserPanel.Grid;Active=true;error="";Array.Clear(Items,0,Items.Length);Count=0;Revision++;ContentRevision++;try{source.Begin(kind,path,slot,false);observedVersion=SourceVersion;if(!source.Busy)Load();}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}}
    private void ResizePage(){if(Items.Length!=PageSize)Items=new IplBrowserItem?[PageSize];(source as IIplBrowserPageSize)?.SetPageSize(PageSize);}
    internal void ToggleView(){if(!Compact||Panel!=IplBrowserPanel.Grid||Busy)return;int absolute=PageStart+Selection;if(Kind==IplBrowserKind.Sd)sdList=!sdList;else cardList=!cardList;ResizePage();PageStart=absolute/PageSize*PageSize;Selection=absolute%PageSize;Load();}
    private IIplBrowserCatalog? Catalog=>Compact?source as IIplBrowserCatalog:null;
    internal IplItemKind ItemKind(int index)=>Items[index]?.Kind??Catalog?.CatalogKind(index)??IplItemKind.File;
    private IplBrowserItem? Item(int index)=>Items[index]??Catalog?.CatalogItem(index);
    private void SelectItem(){if(Catalog!=null&&!ListView&&Count>0){Array.Clear(Items,0,Items.Length);Items[Selection]=Catalog.CatalogItem(Selection);}}
    private void Load(){error="";if(PageStart>=Total&&Total>0)PageStart=(Total-1)/PageSize*PageSize;Count=0;Array.Clear(Items,0,Items.Length);Revision++;
        try{var catalog=Catalog;Count=catalog!=null?catalog.ReadCatalogPage(PageStart,PageSize):source.ReadPage(PageStart,Items);if(Count<0||Count>PageSize)throw new InvalidOperationException("Invalid browser page.");
            if(!source.Busy)Selection=Math.Min(Selection,Math.Max(0,Count-1));
            if(catalog!=null){if(ListView){for(int i=0;i<Count;i++)Items[i]=catalog.CatalogItem(i);}else SelectItem();}
            observedVersion=SourceVersion;
        }catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";Count=0;Array.Clear(Items,0,Items.Length);}}
    internal void Tick(float dt,bool reducedMotion=false){if(!Active)return;ReducedMotion=reducedMotion;AnimationTime+=dt;for(int i=0;i<Items.Length;i++){float target=i==Selection?1:0;float step=dt/.18f;if(reducedMotion){focus[i]=target;continue;}focus[i]=target>focus[i]?Math.Min(target,focus[i]+step):Math.Max(target,focus[i]-step);}bool busy=source.Busy;try{source.Tick();if((busy&&!source.Busy)||SourceVersion!=observedVersion){if(SourceVersion!=observedVersion)ContentRevision++;if(Kind==IplBrowserKind.MemoryCards)Panel=IplBrowserPanel.Grid;Load();}}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}}
    internal void Direction(int x,int y)
    {
        if(Busy)return;
        if(Panel==IplBrowserPanel.Launch||Panel==IplBrowserPanel.CardConfirm){if(x!=0||y!=0)Choice=x!=0?(x>0?1:0):(y<0?1:0);return;}
        if(Panel==IplBrowserPanel.CardActions){int choice=Choice+(x!=0?x:-y);Choice=choice<0?3:choice>3?0:choice;return;}
        if(Panel!=IplBrowserPanel.Grid||Total==0)return;
        int next=PageStart+Selection+(x!=0?x:-y*Columns);if(next<0)next=Total-1;if(next>=Total)next=0;
        int page=next/PageSize*PageSize;Selection=next%PageSize;if(page!=PageStart){PageStart=page;Load();}else try{SelectItem();}catch(Exception e){error=e.Message??"Could not read this entry. Refresh storage.";}
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
        if(item.Kind==IplItemKind.Directory){Open(Kind,Path.Length==0?item.Path:Path+"/"+item.Path,Slot);return;}
        if(item.Kind==IplItemKind.Game||item.Kind==IplItemKind.Dol){Panel=IplBrowserPanel.Launch;Choice=0;return;}
        try{Information=source.Describe(item);Panel=IplBrowserPanel.Info;}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}
    }
    // B leaves the entire flat flow. Folder navigation has its own L action.
    internal void ParentDirectory(){if(Kind==IplBrowserKind.Sd&&Panel==IplBrowserPanel.Grid&&!Busy&&Path.Length>0)Open(Kind,IplPaths.Parent(Path),Slot);}
    internal string Exit()
    {
        string status="";
        if(Active&&Kind==IplBrowserKind.MemoryCards&&Busy)try{(source as IIplCardOperations)?.Cancel();}catch(Exception e){status=e.Message??"Transfer stopped. Check the destination card.";}
        try{source.Dispose();}catch(Exception e){status=e.Message??"Storage could not close cleanly. Check the device.";}
        Active=false;Panel=IplBrowserPanel.Grid;Count=0;Array.Clear(Items,0,Items.Length);Revision++;return status;
    }
    internal void Refresh(){if(Panel!=IplBrowserPanel.Grid)return;error="";PageStart=Selection=Count=0;Array.Clear(Items,0,Items.Length);Revision++;ContentRevision++;try{source.Begin(Kind,Path,Slot,true);if(!Busy)Load();}catch(Exception e){error=e.Message??"Storage operation failed. Check the device.";}}
    internal int ArtworkFrames(int index)=>Item(index)!=null?Math.Max(0,Math.Min(8,(source as IIplAnimatedArtwork)?.ArtworkFrames(Item(index)!)??1)):0;
    internal int ArtworkDuration(int index,int frame)=>Math.Max(1,(source as IIplAnimatedArtwork)?.ArtworkDuration(Items[index]!,frame)??4);
    internal bool ArtworkBounces(int index)=>(source as IIplAnimatedArtwork)?.ArtworkBounces(Items[index]!)??false;
    internal IplArtwork? Artwork(int index,int frame=0){if(!Active||index<0||index>=Count)return null;try{var item=Item(index);if(item==null)return null;return source is IIplAnimatedArtwork animated?animated.Artwork(item,frame):frame==0?source.Artwork(item):null;}catch(Exception){return null;}}
    public void Dispose(){source.Dispose();Active=false;}
}
