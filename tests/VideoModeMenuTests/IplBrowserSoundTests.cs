using System;
using System.Collections.Generic;

internal static class IplBrowserSoundTests
{
    private sealed class Sounds : IIplUiSoundSink
    {
        internal readonly List<IplUiSound> Cues=new List<IplUiSound>();
        public void Emit(IplUiSound cue){Cues.Add(cue);}
        internal void Expect(string action,params IplUiSound[] expected)
        {
            Check(Cues.Count==expected.Length,action+": unexpected cue count "+Cues.Count);
            for(int i=0;i<expected.Length;i++)Check(Cues[i]==expected[i],action+": unexpected cue "+Cues[i]);
            Cues.Clear();
        }
    }

    private sealed class Source : IIplBrowserSource,IIplCardOperations,IIplBrowserNotifications,IIplCardOperationNotifications
    {
        internal IplBrowserItem[] Entries=Array.Empty<IplBrowserItem>();
        internal bool FailBegin,FailRead,FailDescribe,FailOpen,FailExecute,ReturnFailedExecute,ScanOnBegin,FinishScan,Probe,CopyAllowed=true,MoveAllowed=true;
        internal int Begins,Executions,Cancellations;
        internal IplCardOperationResult FinishOperation;
        public int Total=>Entries.Length;
        public bool Busy {get;private set;}
        public string Status=>"Fixture storage";
        public bool LaunchRequested {get;private set;}
        public int Version {get;private set;}
        public int OperationVersion {get;private set;}
        public IplCardOperationResult OperationResult {get;private set;}
        public int Progress=>Busy?50:100;
        public void Begin(IplBrowserKind kind,string path,int slot,bool rescan)
        {Begins++;if(FailBegin)throw new InvalidOperationException("Unavailable fixture");Busy=ScanOnBegin;Version++;}
        public void Tick()
        {
            if(FinishOperation!=IplCardOperationResult.None){Busy=false;OperationResult=FinishOperation;FinishOperation=IplCardOperationResult.None;OperationVersion++;Version++;}
            if(FinishScan){Busy=false;FinishScan=false;Version++;}
            if(Probe){Probe=false;Version++;}
        }
        public int ReadPage(int start,IplBrowserItem?[] output)
        {
            if(FailRead)throw new InvalidOperationException("Read failed");
            int count=Math.Min(output.Length,Total-start);
            for(int i=0;i<count;i++)output[i]=Entries[start+i];return count;
        }
        public IplArtwork? Artwork(IplBrowserItem item)=>null;
        public string Describe(IplBrowserItem item){if(FailDescribe)throw new InvalidOperationException("Info failed");return item.Title;}
        public void Open(IplBrowserItem item,VideoModeSettings video){if(FailOpen)throw new InvalidOperationException("Launch failed");LaunchRequested=true;}
        public string SlotCaption(int slot)=>slot==0?"Slot A":"Slot B";
        public bool CanCopy(IplBrowserItem item)=>CopyAllowed;
        public bool CanMove(IplBrowserItem item)=>MoveAllowed;
        public void Execute(IplCardAction action,IplBrowserItem? item)
        {
            if(FailExecute){OperationResult=IplCardOperationResult.Failed;OperationVersion++;throw new InvalidOperationException("Operation failed");}
            if(ReturnFailedExecute){OperationResult=IplCardOperationResult.Failed;OperationVersion++;return;}
            Executions++;OperationResult=IplCardOperationResult.None;
            if(action==IplCardAction.Copy||action==IplCardAction.Move)Busy=true;
            else{OperationResult=IplCardOperationResult.Succeeded;OperationVersion++;Version++;}
        }
        public void Cancel(){Cancellations++;Busy=false;OperationResult=IplCardOperationResult.Cancelled;OperationVersion++;Version++;}
        public void Dispose(){Busy=false;}
    }

    private static readonly VideoModeSettings Video=new VideoModeSettings(1,1,false);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static IplBrowserItem Item(IplItemKind kind,string path="fixture")=>new IplBrowserItem(path,path,"Fixture",kind);
    private static void BeginCopy(IplBrowser browser,Sounds sounds)
    {
        browser.Confirm(Video);sounds.Expect("Open save actions",IplUiSound.OpenEditor);
        browser.Direction(1,0);sounds.Expect("Choose Copy",IplUiSound.ChangeRow);
        browser.Confirm(Video);sounds.Expect("Open copy confirmation",IplUiSound.OpenEditor);
        browser.Direction(1,0);sounds.Expect("Choose confirm",IplUiSound.ChangeRow);
        browser.Confirm(Video);sounds.Expect("Accept copy",IplUiSound.ConfirmEdit);
        Check(browser.Busy,"Copy is asynchronous");
    }

    internal static void Run()
    {
        var sounds=new Sounds();
        var files=new Source{Entries=new[]{Item(IplItemKind.Directory,"folder"),Item(IplItemKind.File,"notes.txt"),Item(IplItemKind.Dol,"app.dol")}};
        using(var browser=new IplBrowser(files,sounds)){
            browser.Open(IplBrowserKind.Sd);sounds.Expect("Open SD",IplUiSound.EnterMenu);
            browser.Direction(0,0);sounds.Expect("Neutral input");
            browser.Direction(1,0);sounds.Expect("Select file",IplUiSound.ChangeRow);
            browser.Confirm(Video);sounds.Expect("Open file info",IplUiSound.EnterMenu);
            browser.Direction(1,0);sounds.Expect("Info ignores navigation");
            browser.Confirm(Video);sounds.Expect("Close file info",IplUiSound.Back);
            browser.Direction(1,0);sounds.Expect("Select executable",IplUiSound.ChangeRow);
            browser.Confirm(Video);sounds.Expect("Open launch prompt",IplUiSound.OpenEditor);
            browser.Direction(-1,0);sounds.Expect("Already on Cancel");
            browser.Confirm(Video);sounds.Expect("Cancel launch",IplUiSound.CancelEdit);
            browser.Confirm(Video);sounds.Expect("Reopen launch",IplUiSound.OpenEditor);
            browser.Direction(1,0);sounds.Expect("Choose launch",IplUiSound.ChangeRow);
            browser.Direction(1,0);sounds.Expect("Already on launch");
            files.FailOpen=true;browser.Confirm(Video);sounds.Expect("Failed launch",IplUiSound.CancelEdit);
            files.FailOpen=false;browser.Confirm(Video);sounds.Expect("Launch accepted",IplUiSound.ConfirmEdit);
            Check(files.LaunchRequested,"Accepted executable launches");
            browser.Exit();sounds.Expect("Exit browser",IplUiSound.Back);
            browser.Exit();sounds.Expect("Already exited");
        }

        files=new Source{Entries=new[]{Item(IplItemKind.Directory,"folder"),Item(IplItemKind.File,"notes.txt")}};
        using(var browser=new IplBrowser(files,sounds)){
            browser.Open(IplBrowserKind.Sd);sounds.Expect("Open folders",IplUiSound.EnterMenu);
            browser.Confirm(Video);sounds.Expect("Open folder once",IplUiSound.EnterMenu);
            Check(browser.Path=="folder","Folder entered");
            browser.ParentDirectory();sounds.Expect("Parent once",IplUiSound.Back);
            browser.ParentDirectory();sounds.Expect("Root has no parent");
            browser.ToggleView();sounds.Expect("Grid/list view",IplUiSound.ChangeField);
            browser.Refresh();sounds.Expect("Manual refresh",IplUiSound.ChangeField);
            files.ScanOnBegin=true;browser.Refresh();sounds.Expect("Begin scan",IplUiSound.ChangeField);
            browser.Direction(1,0);browser.Confirm(Video);browser.ToggleView();browser.ParentDirectory();sounds.Expect("Busy scan ignores input");
            int begins=files.Begins;browser.Refresh();sounds.Expect("Explicit rescan while scanning",IplUiSound.ChangeField);Check(files.Begins==begins+1,"Manual rescan retains restart behavior");
            files.ScanOnBegin=false;files.FinishScan=true;browser.Tick(.016f);sounds.Expect("Background scan completes silently");
            files.Probe=true;browser.Tick(.016f);sounds.Expect("Background revision is silent");
            files.FailRead=true;browser.Refresh();sounds.Expect("Failed refresh",IplUiSound.CancelEdit);
            files.FailRead=false;browser.Refresh();sounds.Expect("Refresh recovery",IplUiSound.ChangeField);
            browser.Direction(1,0);sounds.Expect("Select info",IplUiSound.ChangeRow);
            files.FailDescribe=true;browser.Confirm(Video);sounds.Expect("Failed info",IplUiSound.CancelEdit);
            browser.Direction(-1,0);sounds.Expect("New navigation after failure",IplUiSound.ChangeRow);
        }

        using(var browser=new IplBrowser(new Source(),sounds)){
            browser.Open(IplBrowserKind.Sd);sounds.Expect("Open empty storage",IplUiSound.EnterMenu);
            browser.Direction(1,0);browser.Confirm(Video);sounds.Expect("Empty selection");
        }
        using(var browser=new IplBrowser(new Source{Entries=new[]{Item(IplItemKind.File)}},sounds)){
            browser.Open(IplBrowserKind.Sd);sounds.Expect("Single item storage",IplUiSound.EnterMenu);
            browser.Direction(1,0);browser.Direction(0,-1);sounds.Expect("Single-item wrap stays silent");
        }
        using(var browser=new IplBrowser(new Source{FailBegin=true},sounds)){
            browser.Open(IplBrowserKind.Sd);sounds.Expect("Failed storage open has no success cue",IplUiSound.CancelEdit);
        }

        var cards=new Source{Entries=new[]{Item(IplItemKind.Save)}};
        using(var browser=new IplBrowser(cards,sounds)){
            browser.Open(IplBrowserKind.MemoryCards);sounds.Expect("Open card",IplUiSound.EnterMenu);
            int begins=cards.Begins;browser.SwitchSlot(0);sounds.Expect("Same slot stays silent");Check(cards.Begins==begins+1,"Existing same-slot reload remains available");
            browser.SwitchSlot(1);sounds.Expect("Switch card slot",IplUiSound.ChangeField);
            browser.SwitchSlot(0);sounds.Expect("Switch card back",IplUiSound.ChangeField);
            cards.Probe=true;browser.Tick(.016f);sounds.Expect("Card insertion/probe is not completion");
            browser.Confirm(Video);sounds.Expect("Save actions",IplUiSound.OpenEditor);
            browser.Direction(1,0);sounds.Expect("Copy row",IplUiSound.ChangeRow);
            cards.CopyAllowed=false;browser.Confirm(Video);sounds.Expect("Protected save",IplUiSound.CancelEdit);Check(cards.Executions==0,"Protected save never executes");
            cards.CopyAllowed=true;browser.Confirm(Video);sounds.Expect("Copy prompt",IplUiSound.OpenEditor);
            browser.Direction(-1,0);sounds.Expect("Default confirmation stays selected");
            browser.Confirm(Video);sounds.Expect("Cancel copy proposal",IplUiSound.CancelEdit);

            BeginCopy(browser,sounds);
            browser.Direction(1,0);browser.Confirm(Video);browser.Refresh();browser.ToggleView();browser.SwitchSlot(1);sounds.Expect("Busy transfer input is silent");
            cards.FinishOperation=IplCardOperationResult.Succeeded;browser.Tick(.016f);sounds.Expect("Verified transfer completion",IplUiSound.ConfirmEdit);
            browser.Tick(.016f);cards.Probe=true;browser.Tick(.016f);sounds.Expect("Completion is one-shot, independent of probe revisions");
            BeginCopy(browser,sounds);
            cards.FinishOperation=IplCardOperationResult.Failed;browser.Tick(.016f);sounds.Expect("Failed transfer",IplUiSound.CancelEdit);
            browser.Tick(.016f);sounds.Expect("Failure is one-shot");
            BeginCopy(browser,sounds);
            browser.Exit();sounds.Expect("B cancels active transfer",IplUiSound.CancelEdit);Check(cards.Cancellations==1,"Transfer cancellation occurs once");
            browser.Tick(.016f);sounds.Expect("No completion after leaving browser");
        }

        cards=new Source{Entries=new[]{Item(IplItemKind.Save)}};
        using(var browser=new IplBrowser(cards,sounds)){
            browser.Open(IplBrowserKind.MemoryCards);sounds.Expect("Open deletion fixture",IplUiSound.EnterMenu);
            browser.Confirm(Video);sounds.Expect("Delete actions",IplUiSound.OpenEditor);
            browser.Direction(-1,0);sounds.Expect("Select Delete",IplUiSound.ChangeRow);
            browser.Confirm(Video);sounds.Expect("Delete confirmation",IplUiSound.OpenEditor);
            browser.Direction(1,0);sounds.Expect("Confirm delete choice",IplUiSound.ChangeRow);
            cards.FailExecute=true;browser.Confirm(Video);sounds.Expect("Rejected delete",IplUiSound.CancelEdit);Check(cards.Executions==0,"Failed operation has no success cue");
            cards.FailExecute=false;browser.Confirm(Video);sounds.Expect("Reopen delete actions",IplUiSound.OpenEditor);
            browser.Direction(-1,0);sounds.Expect("Select Delete again",IplUiSound.ChangeRow);
            browser.Confirm(Video);sounds.Expect("Reopen delete prompt",IplUiSound.OpenEditor);
            browser.Direction(1,0);sounds.Expect("Confirm delete again",IplUiSound.ChangeRow);
            browser.Confirm(Video);sounds.Expect("Synchronous delete completes once",IplUiSound.ConfirmEdit);
            browser.Tick(.016f);sounds.Expect("No duplicate synchronous result");
            cards.Entries=new[]{Item(IplItemKind.CardFormat)};cards.Probe=true;browser.Tick(.016f);sounds.Expect("Format availability is silent");
            browser.Confirm(Video);sounds.Expect("First format prompt",IplUiSound.OpenEditor);
            browser.Direction(1,0);sounds.Expect("First format choice",IplUiSound.ChangeRow);
            browser.Confirm(Video);sounds.Expect("First format guard accepted",IplUiSound.ConfirmEdit);
            Check(browser.FormatStage==2&&cards.Executions==1,"First format guard does not modify card");
            browser.Confirm(Video);sounds.Expect("Cancel second format guard",IplUiSound.CancelEdit);
            Check(cards.Executions==1,"Cancellation leaves card unchanged");
        }
        cards=new Source{Entries=new[]{Item(IplItemKind.Save)},ReturnFailedExecute=true};
        using(var browser=new IplBrowser(cards,sounds)){
            browser.Open(IplBrowserKind.MemoryCards);sounds.Expect("Open returned-error fixture",IplUiSound.EnterMenu);
            browser.Confirm(Video);sounds.Expect("Returned-error actions",IplUiSound.OpenEditor);
            browser.Direction(-1,0);sounds.Expect("Returned-error Delete",IplUiSound.ChangeRow);
            browser.Confirm(Video);sounds.Expect("Returned-error confirmation",IplUiSound.OpenEditor);
            browser.Direction(1,0);sounds.Expect("Returned-error choice",IplUiSound.ChangeRow);
            browser.Confirm(Video);sounds.Expect("Typed failure overrides success without exception",IplUiSound.CancelEdit);
            browser.Tick(.016f);sounds.Expect("Returned error is one-shot");
        }
        Console.WriteLine("Browser sounds: real selection/open/change/confirm/cancel cues, no-op and background silence, async terminal outcomes and one-shot completion passed.");
    }
}
