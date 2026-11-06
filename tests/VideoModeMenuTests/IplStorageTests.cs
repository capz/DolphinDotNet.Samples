using System;
using System.IO;
internal static class IplStorageTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private sealed class Cards : IIplBrowserSource,IIplCardOperations,IIplBrowserNotifications
    {
        internal bool Format,Protected;internal int Actions,Cancelled,Frames,Slot;internal IplCardAction Last;
        public int Version {get;set;}
        public int Total=>1;public bool Busy=>Frames>0;public string Status=>"Fixture cards";public bool LaunchRequested=>false;
        public int Progress=>Frames==0?100:50;
        public string SlotCaption(int slot)=>slot==0?"Slot A":"Slot B";
        public bool CanCopy(IplBrowserItem item)=>!Protected;public bool CanMove(IplBrowserItem item)=>!Protected;
        public void Begin(IplBrowserKind kind,string path,int slot,bool rescan){Slot=slot;Version++;}
        public void Tick(){if(Frames>0&&--Frames==0)Version++;}
        public int ReadPage(int start,IplBrowserItem?[] output){output[0]=new IplBrowserItem("fixture","Original save","2 blocks",Format?IplItemKind.CardFormat:IplItemKind.Save);return 1;}
        public IplArtwork? Artwork(IplBrowserItem item)=>null;public string Describe(IplBrowserItem item)=>"Original fixture";
        public void Open(IplBrowserItem item,VideoModeSettings video)=>throw new Exception("Save actions must not launch");
        public void Execute(IplCardAction action,IplBrowserItem? item){Last=action;Actions++;if(action==IplCardAction.Copy||action==IplCardAction.Move)Frames=2;else Version++;}
        public void Cancel(){Cancelled++;Frames=0;Version++;}public void Dispose(){}
    }
    private static void Finish(IplSdDirectory dir){for(int n=0;dir.Busy&&n<1000;n++)dir.Tick();Check(!dir.Busy,"Bounded fixture enumeration finished");}
    internal static void Run()
    {
        string root=Path.Combine(Path.GetTempPath(),"ipl-sd-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{
            Directory.CreateDirectory(Path.Combine(root,"folder"));File.WriteAllText(Path.Combine(root,"folder","notes.txt"),"Original text preview\nSecond line");File.WriteAllBytes(Path.Combine(root,"binary.bin"),new byte[]{0,1,0xfe,0xff});
            for(int i=0;i<9;i++)File.WriteAllText(Path.Combine(root,"file"+i+".txt"),"Original read-only fixture "+i);
            using var dir=new IplSdDirectory(root);dir.Begin("");Check(dir.Busy,"SD enumeration starts incrementally");dir.Tick();Check(dir.Busy,"A tick does not enumerate the complete directory");Finish(dir);Check(dir.Count==11,"SD directory counts all files without retaining all entries");var page=new IplBrowserItem?[6];Check(dir.ReadPage(0,page)==6&&page[0]?.Kind==IplItemKind.Directory,"Directories appear first");
            Check(dir.ReadPage(6,page)==0&&dir.Busy,"A new page is loaded incrementally");Finish(dir);Check(dir.ReadPage(6,page)==5,"Final SD page contains remaining entries");
            dir.Begin("folder");Finish(dir);Check(dir.Count==1&&dir.ReadPage(0,page)==1,"Subfolder can be browsed");string text=dir.Describe(page[0]!);Check(text.Contains("Original text preview")&&text.Contains("Second line"),"Opening text gives a bounded preview");
            Check(dir.Describe(new IplBrowserItem("binary.bin","binary","",IplItemKind.File)).Contains("00 01 FE FF"),"Binary files have a bounded hex preview");
            bool rejected=false;try{dir.Begin("../outside");}catch(InvalidDataException){rejected=true;}Check(rejected,"SD navigation cannot escape its mounted root");Check(File.ReadAllText(Path.Combine(root,"folder","notes.txt"))=="Original text preview\nSecond line"&&Directory.GetFiles(root).Length==10,"Browsing/opening preserves file content and directory entries");
        }finally{Directory.Delete(root,true);}
        var source=new Cards();using var browser=new IplBrowser(source);var shell=new IplShell(new FakeOutput(),browser:browser);shell.TurnTo(ShellFace.Memory);shell.Tick(1);Check(shell.Rows==2&&shell.Title=="Storage"&&shell.Label(1)=="SD Storage","Storage has distinct card and SD buttons");shell.Confirm();Check(browser.Kind==IplBrowserKind.MemoryCards&&shell.Page==ShellPage.Browser,"Memory card button opens a flat screen");browser.SwitchSlot(1);Check(source.Slot==1&&browser.Slot==1,"L/R slot selection changes the source snapshot");
        browser.Confirm(shell.Video.Applied);Check(browser.Panel==IplBrowserPanel.CardActions&&browser.Choice==0,"Save menu defaults to Cancel");browser.Confirm(shell.Video.Applied);Check(source.Actions==0&&browser.Panel==IplBrowserPanel.Grid,"Cancel never changes a card");
        browser.Confirm(shell.Video.Applied);browser.Direction(1,0);source.Protected=true;browser.Confirm(shell.Video.Applied);Check(source.Actions==0&&browser.Panel==IplBrowserPanel.CardActions,"Copy protection is enforced before confirmation");source.Protected=false;browser.Confirm(shell.Video.Applied);Check(browser.Panel==IplBrowserPanel.CardConfirm&&browser.Choice==0,"Copy confirmation defaults to Cancel");browser.Direction(1,0);browser.Confirm(shell.Video.Applied);Check(source.Actions==1&&source.Last==IplCardAction.Copy&&browser.Panel==IplBrowserPanel.Progress,"Explicit confirmation starts incremental copy");browser.Tick(.016f);browser.Back();Check(source.Cancelled==1&&!browser.Busy&&browser.Panel==IplBrowserPanel.Grid,"B safely cancels an incremental transfer");
        browser.Confirm(shell.Video.Applied);browser.Direction(-1,0);browser.Confirm(shell.Video.Applied);browser.Direction(1,0);browser.Confirm(shell.Video.Applied);Check(source.Last==IplCardAction.Delete,"Delete requires its own explicit confirmation");
        source.Format=true;source.Version++;browser.Tick(.016f);browser.Confirm(shell.Video.Applied);Check(browser.FormatStage==1&&browser.Choice==0,"Format starts at the first Cancel");browser.Direction(1,0);browser.Confirm(shell.Video.Applied);Check(browser.FormatStage==2&&browser.Choice==0&&source.Last==IplCardAction.Delete,"Second format confirmation resets to Cancel");browser.Direction(1,0);browser.Confirm(shell.Video.Applied);Check(source.Last==IplCardAction.Format,"Only the second explicit confirmation formats");
        source.Format=false;source.Version++;browser.Tick(.016f);browser.Confirm(shell.Video.Applied);source.Version++;browser.Tick(.016f);Check(browser.Panel==IplBrowserPanel.Grid,"Media revision dismisses a stale destructive selection");shell.Back();Check(shell.Page==ShellPage.Face,"B leaves card grid for Storage");shell.Back();Check(shell.Page==ShellPage.Cube,"Direct Storage entry returns to cube");
        Console.WriteLine("Storage: bounded read-only SD browsing/previews/root bounds, card slot routing, protected actions, default Cancel, transfer cancellation, two-stage format and media revision passed.");
    }
}
