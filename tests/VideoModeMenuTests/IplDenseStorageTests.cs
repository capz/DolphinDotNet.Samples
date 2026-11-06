using System;
using System.IO;

internal static class IplDenseStorageTests
{
    private static void Check(bool value,string text){if(!value)throw new Exception(text);}
    private sealed class Source : IIplBrowserSource,IIplBrowserPageSize
    {
        private int cachedStart=-1,size,wait;internal bool Async;
        public int Total=>127;public bool Busy=>wait>0;public string Status=>"Original dense fixture";public bool LaunchRequested=>false;
        public void Begin(IplBrowserKind kind,string path,int slot,bool refresh){cachedStart=-1;}
        public void SetPageSize(int count){if(size!=count){size=count;cachedStart=-1;}}
        public void Tick(){if(wait>0)wait--;}
        public int ReadPage(int start,IplBrowserItem?[] output){if(Async&&start!=cachedStart){cachedStart=start;wait=2;return 0;}cachedStart=start;int n=Math.Min(Total-start,output.Length);for(int i=0;i<n;i++)output[i]=new IplBrowserItem("save"+(start+i),"Save "+(start+i),IplStorageLayout.SaveSize(8192*(1+i%3)),IplItemKind.Save,8192*(1+i%3)){GameTitle="Original fixture game"};return n;}
        public IplArtwork? Artwork(IplBrowserItem item)=>null;
        public void Open(IplBrowserItem item,VideoModeSettings video){}
        public string Describe(IplBrowserItem item)=>item.Path;
        public void Dispose(){wait=0;}
    }
    private sealed class CatalogSource : IIplBrowserSource,IIplBrowserCatalog
    {
        private int start;internal int Materialized;
        public int Total=>117;public bool Busy=>false;public string Status=>"Native catalog contract";public bool LaunchRequested=>false;
        public void Begin(IplBrowserKind kind,string path,int slot,bool refresh){}
        public void Tick(){}
        public int ReadPage(int start,IplBrowserItem?[] output)=>throw new Exception("A dense native source must use its bounded catalog");
        public int ReadCatalogPage(int first,int maximum){start=first;return Math.Min(maximum,Total-first);}
        public IplItemKind CatalogKind(int index)=>IplItemKind.Save;
        public IplBrowserItem? CatalogItem(int index){Materialized++;return new IplBrowserItem("save"+(start+index),"Save "+(start+index),"1 block / 8 KB",IplItemKind.Save,8192){GameTitle="Original game"};}
        public IplArtwork? Artwork(IplBrowserItem item)=>null;
        public void Open(IplBrowserItem item,VideoModeSettings video){}
        public string Describe(IplBrowserItem item)=>item.Title;
        public void Dispose(){}
    }
    internal static void Run()
    {
        var source=new Source();using var browser=new IplBrowser(source);browser.Open(IplBrowserKind.MemoryCards);
        Check(browser.Count==105&&browser.Columns==15,"Dense card grid fits 105 saves in fifteen columns");
        for(int i=0;i<6;i++)browser.Direction(0,-1);
        for(int i=0;i<14;i++)browser.Direction(1,0);
        Check(browser.Selection==104&&browser.PageStart==0,"Last full-grid cell is reachable");browser.Direction(1,0);
        Check(browser.PageStart==105&&browser.Selection==0&&browser.Count==22,"Grid crosses onto the final page");
        browser.ToggleView();Check(browser.ListView&&browser.PageStart==99&&browser.Selection==6&&browser.Count==9&&browser.Columns==1,"List preserves the selected absolute save");
        browser.Direction(0,-1);Check(browser.Selected!.Path=="save106","List vertical navigation moves one entry");
        browser.ToggleView();Check(!browser.ListView&&browser.PageStart==105&&browser.Selection==1,"Returning to grid preserves selection");
        browser.Direction(-1,0);browser.Direction(-1,0);Check(browser.PageStart==0&&browser.Selection==104,"Reverse page boundary preserves the last cell");
        browser.Confirm(default);browser.ToggleView();Check(!browser.ListView&&browser.Panel==IplBrowserPanel.CardActions,"View changes cannot bypass a card dialog");browser.Exit();
        browser.Open(IplBrowserKind.Sd);browser.ToggleView();Check(browser.ListView,"SD can select list view independently");browser.Open(IplBrowserKind.MemoryCards);Check(!browser.ListView,"Each storage type remembers its own view");browser.ToggleView();browser.SwitchSlot(1);Check(browser.ListView&&browser.Slot==1,"Slot switching retains the selected view");
        source.Async=true;browser.Open(IplBrowserKind.Sd);for(int i=0;i<3;i++)browser.Tick(.016f);Check(browser.Count==9,"Async SD publishes its list page");
        for(int i=0;i<8;i++)browser.Direction(0,-1);browser.ToggleView();Check(browser.Busy&&browser.Selection==8&&browser.Count==0,"Async layout change clears old rows without discarding desired selection");
        browser.ToggleView();for(int i=0;i<3;i++)browser.Tick(.016f);Check(!browser.ListView&&browser.Selected!.Path=="save8","Pending selection survives async page completion");
        Check(IplStorageLayout.SaveSize(8192)=="1 block / 8 KB"&&IplStorageLayout.SaveSize(24576)=="3 blocks / 24 KB","Card size follows native 8192-byte blocks");
        Check(IplStorageLayout.FileSize(512)=="512 B"&&IplStorageLayout.FileSize(1536)=="1.5 KB"&&IplStorageLayout.FileSize(1048576)=="1 MB"&&IplStorageLayout.FileSize(-1)=="Size unavailable","SD sizes use file units and do not invent unavailable lengths");
        string root=Path.Combine(Path.GetTempPath(),"ipl-dense-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{
            Directory.CreateDirectory(Path.Combine(root,"nested"));for(int i=0;i<126;i++)File.WriteAllText(Path.Combine(root,"nested","file-"+i.ToString("D3")+".txt"),"Original fixture");
            using var sd=new IplSdDirectory(root,105);sd.Begin("nested");Finish(sd);var page=new IplBrowserItem?[105];Check(sd.ReadPage(0,page)==105&&sd.Count==126,"SD retains one dense page while counting all entries");
            var first=page[0]!;Check(!first.Path.Contains('/')&&first.Title==first.Path&&first.Detail=="16 B"&&first.GameTitle=="","SD entries retain filename and size without game or block metadata");
            Check(sd.Describe(first).Contains("Original fixture"),"A compact child filename still opens under its actual parent");
            Check(sd.ReadPage(105,page)==0&&sd.Busy,"Dense SD next page is incremental");Finish(sd);Check(sd.ReadPage(105,page)==21,"Final dense SD page is bounded");
            sd.SetPageSize(9);var list=new IplBrowserItem?[9];sd.ReadPage(117,list);Finish(sd);Check(sd.ReadPage(117,list)==9&&sd.Count==126,"SD switches to bounded list pages without losing total");
            Check(Directory.GetFiles(Path.Combine(root,"nested")).Length==126,"Both views preserve SD contents");
        }finally{Directory.Delete(root,true);}
        var cell=new byte[IplStorageIcons.CellBytes];IplStorageIcons.Raster(cell,IplItemKind.File);Check(cell[0]==0,"Icon roundover has transparent corners");
        Check(IplStorageIcons.TileRowOffset(14,0)==9*128*64&&IplStorageIcons.TileRowOffset(13,8)+IplStorageIcons.TileRowBytes<512*512*4,"Tiled atlas rows stay within native allocation");
        var catalog=new CatalogSource();using var lazy=new IplBrowser(catalog);lazy.Open(IplBrowserKind.MemoryCards);
        Check(lazy.Count==105&&catalog.Materialized==1&&Retained(lazy)==1,"Native grid materializes only selected metadata for 105 visible icons");
        lazy.Direction(1,0);Check(lazy.Selected!.Path=="save1"&&Retained(lazy)==1&&catalog.Materialized==2,"Selection replaces rather than accumulates managed metadata");
        lazy.Artwork(9);Check(Retained(lazy)==1&&lazy.Selected!.Path=="save1","An off-selection icon uses transient metadata");
        int content=lazy.ContentRevision;lazy.ToggleView();Check(lazy.ContentRevision==content,"View changes preserve native card artwork identity");Check(lazy.Count==9&&Retained(lazy)==9&&catalog.Materialized==12,"Native list retains at most nine row records");
        lazy.Refresh();Check(lazy.ContentRevision>content,"Explicit refresh invalidates native card artwork");
        using var records=new IplSdPage(105);records.Set(0,"\u65e5\u672c-file.txt",IplItemKind.File,4294967295,"GAME01");var roundtrip=records.Item(0)!;
        Check(roundtrip.Path=="\u65e5\u672c-file.txt"&&roundtrip.Bytes==4294967295&&roundtrip.GameId=="GAME01","Native page format preserves filename code units, 64-bit lengths and private launch identity");
        Console.WriteLine("Dense storage: 105-cell grid, nine-row list, async selection, boundaries, slot/view memory, SD parent paths, sizes and read-only contents passed.");
    }
    private static int Retained(IplBrowser browser){int count=0;foreach(var item in browser.Items)if(item!=null)count++;return count;}
    private static void Finish(IplSdDirectory sd){for(int i=0;sd.Busy&&i<1000;i++)sd.Tick();Check(!sd.Busy,"SD fixture enumeration completes");}
}
