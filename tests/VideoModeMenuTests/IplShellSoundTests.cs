using System;
using System.Collections.Generic;

internal static class IplShellSoundTests
{
    private sealed class Sink : IIplUiSoundSink
    {
        internal readonly List<IplUiSound> Cues=new List<IplUiSound>();
        public void Emit(IplUiSound cue)=>Cues.Add(cue);
        internal void Expect(params IplUiSound[] expected)
        {
            Check(Cues.Count==expected.Length,"Expected "+string.Join(",",expected)+"; received "+string.Join(",",Cues));
            for(int i=0;i<expected.Length;i++)Check(Cues[i]==expected[i],"Unexpected shell sound ordering");
            Cues.Clear();
        }
    }
    private sealed class Store : IIplPreferencesStore
    {
        internal IplPreferences Value=new IplPreferences(new VideoModeSettings(1,1,false),0,1,2,2);
        internal bool Present=true,Writable=true;
        internal int Saves;
        public string Status=>Writable?"Saved":"Write protected";
        public bool Load(out IplPreferences value){value=Value;return Present;}
        public bool Save(IplPreferences value){Saves++;if(Writable)Value=value;return Writable;}
    }
    private sealed class Clock : IIplConsoleCalendar
    {
        internal IplCalendarValue Value=new IplCalendarValue(2026,11,14,12,34,56);
        internal bool Readable=true,Writable=true;
        public string Status=>"Clock unavailable";
        public bool Read(out IplCalendarValue value){value=Value;return Readable;}
        public bool Write(IplCalendarValue value){if(Writable)Value=value;return Writable;}
    }
    private sealed class ConsoleSound : IIplConsoleSound
    {
        internal bool Readable=true,Writable=true;
        internal int Mode;
        public string Status=>"Console sound unavailable";
        public bool Read(out int mode){mode=Mode;return Readable;}
        public bool Write(int mode){if(Writable)Mode=mode;return Writable;}
    }
    private sealed class Files : IIplBrowserSource
    {
        internal bool IsBusy;
        public int Total=>2;
        public bool Busy=>IsBusy;
        public string Status=>"Ready";
        public bool LaunchRequested=>false;
        public void Begin(IplBrowserKind kind,string path,int slot,bool rescan){}
        public void Tick(){}
        public int ReadPage(int start,IplBrowserItem?[] output){for(int i=0;i<Total;i++)output[i]=new IplBrowserItem("file"+i,"File "+i,"",IplItemKind.File);return Total;}
        public IplArtwork? Artwork(IplBrowserItem item)=>null;
        public string Describe(IplBrowserItem item)=>item.Title;
        public void Open(IplBrowserItem item,VideoModeSettings video){}
        public void Dispose(){IsBusy=false;}
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Move(IplShell shell,int x,int y,bool stick=false)
    {shell.Direction(0,0,0,stick);shell.Direction(x,y,.016f,stick);}
    private static void Open(IplShell shell,ShellFace face)
    {shell.TurnTo(face);shell.Tick(1);shell.Confirm();}

    private static void Navigation()
    {
        var sink=new Sink();var shell=new IplShell(new FakeOutput(),uiSounds:sink);
        shell.Tick(3);shell.Confirm();shell.Back();Move(shell,0,0);sink.Expect();
        Move(shell,-1,0,true);sink.Expect(IplUiSound.TurnSide);
        shell.Confirm();shell.Back();Move(shell,0,1);sink.Expect(); // Turn animation locks all actions.
        shell.Tick(1);Move(shell,0,1);sink.Expect(); // Cube side previews are read-only.
        shell.Confirm();sink.Expect(IplUiSound.EnterMenu);
        Move(shell,1,0);Move(shell,0,0);sink.Expect();
        Move(shell,0,-1);sink.Expect(IplUiSound.ChangeRow);
        shell.Direction(0,-1,.016f);sink.Expect(); // A held direction waits before its next accepted repeat.
        shell.Direction(0,-1,.4f);sink.Expect(IplUiSound.ChangeRow);
        shell.Back();sink.Expect(IplUiSound.Back);
        shell.Back();sink.Expect(IplUiSound.TurnCenter);
        shell.Back();shell.Confirm();sink.Expect();
        shell.Tick(1);Move(shell,1,0,true);shell.Tick(1);sink.Expect(IplUiSound.TurnSide);
        Move(shell,-1,0,true);sink.Expect(IplUiSound.TurnCenter);
        shell.Direction(-1,0,1,true);sink.Expect(); // Reverse-stick latch never crosses another side.
        shell.Tick(1);Move(shell,0,1,true);shell.Tick(1);sink.Expect(IplUiSound.TurnSide);
        shell.TurnTo(ShellFace.Memory);sink.Expect(); // No false turn cue when the face is unchanged.
        shell.Tick(1);shell.Confirm();sink.Expect(IplUiSound.EnterMenu);
        shell.Confirm();sink.Expect(IplUiSound.CancelEdit); // Unavailable browser service.
    }

    private static void Volume()
    {
        var sink=new Sink();var store=new Store();var shell=new IplShell(new FakeOutput(),store,uiSounds:sink);
        sink.Expect();Check(shell.UiSoundVolume==2,"Menu audio defaults to Medium without a startup cue");
        Open(shell,ShellFace.Options);sink.Expect(IplUiSound.TurnSide,IplUiSound.EnterMenu);
        Check(shell.Rows==4&&shell.Label(3)=="UI Sounds"&&shell.Value(3)=="Medium","UI audio is a separate fourth option");
        Move(shell,0,1);sink.Expect(IplUiSound.ChangeRow);Check(shell.Row==3,"Options wraps to its fourth row");
        shell.Confirm();sink.Expect(IplUiSound.OpenEditor);
        Move(shell,0,1);sink.Expect(IplUiSound.ChangeValue);
        Check(shell.UiSoundVolume==3&&shell.Value(3)=="Full"&&store.Saves==0&&store.Value.UiSoundVolume==2,"Full volume previews without saving");
        Move(shell,0,1);sink.Expect(IplUiSound.ChangeValue);Check(shell.UiSoundVolume==0&&shell.Value(3)=="Off","Volume wraps to Off");
        Move(shell,0,-1);sink.Expect(IplUiSound.ChangeValue);Check(shell.UiSoundVolume==3,"Volume wraps back to Full");
        Move(shell,1,0);sink.Expect();
        shell.Back();sink.Expect(IplUiSound.CancelEdit);
        Check(shell.UiSoundVolume==2&&store.Saves==0&&shell.Page==ShellPage.Face,"B cancels the volume draft");
        Open(shell,ShellFace.Options);sink.Expect(IplUiSound.EnterMenu);shell.Row=3;shell.Confirm();sink.Expect(IplUiSound.OpenEditor);
        Move(shell,0,-1);sink.Expect(IplUiSound.ChangeValue);shell.Confirm();sink.Expect(IplUiSound.ConfirmEdit);
        Check(shell.FlatMenu&&shell.UiSoundVolume==1&&store.Value.UiSoundVolume==1&&store.Value.GlassBlur==2&&store.Value.MotionLevel==1,"Confirm persists only the accepted volume alongside other settings");
        var restored=new IplShell(new FakeOutput(),store,uiSounds:sink);sink.Expect();Check(restored.UiSoundVolume==1,"Saved UI volume restores silently");
        shell.Row=1;shell.Confirm();sink.Expect(IplUiSound.OpenEditor);Move(shell,0,1);shell.Confirm();sink.Expect(IplUiSound.ChangeValue,IplUiSound.ConfirmEdit);
        Check(store.Value.MotionLevel==2&&store.Value.UiSoundVolume==1,"Saving another option retains UI audio volume");
        shell.Row=3;shell.Confirm();sink.Expect(IplUiSound.OpenEditor);Move(shell,0,1);sink.Expect(IplUiSound.ChangeValue);
        store.Writable=false;shell.Confirm();sink.Expect(IplUiSound.CancelEdit);
        Check(shell.FlatMenu&&shell.UiSoundVolume==2&&store.Value.UiSoundVolume==1&&shell.SystemStatus=="Write protected","Persistence failure retains session volume and reports failure");
        var absent=new IplShell(new FakeOutput(),new Store{Present=false},uiSounds:sink);sink.Expect();Check(absent.UiSoundVolume==2,"Missing preferences use Medium");
    }

    private static void CalendarAndSound()
    {
        var sink=new Sink();var clock=new Clock();var console=new ConsoleSound();
        var shell=new IplShell(new FakeOutput(),calendar:clock,soundService:console,uiSounds:sink);
        sink.Expect();Open(shell,ShellFace.Calendar);sink.Expect(IplUiSound.TurnSide,IplUiSound.EnterMenu);
        shell.Confirm();sink.Expect(IplUiSound.OpenEditor);
        Move(shell,1,0);sink.Expect(IplUiSound.ChangeField);Move(shell,0,1);sink.Expect(IplUiSound.ChangeValue);
        shell.Confirm();sink.Expect(IplUiSound.ConfirmEdit);Check(clock.Value.Minute==35,"Accepted clock changes still write real time");
        Move(shell,0,-1);sink.Expect(IplUiSound.ChangeRow);shell.Confirm();sink.Expect(IplUiSound.OpenEditor);
        Move(shell,1,0);Move(shell,1,0);Move(shell,0,-1);sink.Expect(IplUiSound.ChangeField,IplUiSound.ChangeField,IplUiSound.ChangeValue);
        clock.Writable=false;shell.Confirm();sink.Expect(IplUiSound.CancelEdit);Check(shell.Editing,"Clock write failure stays editable");
        clock.Readable=false;shell.Confirm();sink.Expect(IplUiSound.CancelEdit);shell.Back();sink.Expect(IplUiSound.CancelEdit);
        Open(shell,ShellFace.Options);sink.Expect(IplUiSound.TurnSide,IplUiSound.EnterMenu);shell.Confirm();sink.Expect(IplUiSound.OpenEditor);
        Move(shell,0,1);sink.Expect(IplUiSound.ChangeValue);console.Writable=false;shell.Confirm();sink.Expect(IplUiSound.CancelEdit);
        Check(shell.Editing&&shell.Sound==0,"Console SRAM failure preserves current sound and edit");
        console.Writable=true;shell.Confirm();sink.Expect(IplUiSound.ConfirmEdit);Check(shell.Sound==1,"Console SRAM success commits sound");
        var missingClock=new IplShell(new FakeOutput(),calendar:new Clock{Readable=false},uiSounds:sink);
        sink.Expect();Open(missingClock,ShellFace.Calendar);sink.Expect(IplUiSound.TurnSide,IplUiSound.EnterMenu);missingClock.Confirm();sink.Expect(IplUiSound.CancelEdit);
        var missingSound=new IplShell(new FakeOutput(),soundService:new ConsoleSound{Readable=false},uiSounds:sink);
        sink.Expect();Open(missingSound,ShellFace.Options);sink.Expect(IplUiSound.TurnSide,IplUiSound.EnterMenu);missingSound.Confirm();sink.Expect(IplUiSound.CancelEdit);
    }

    private static void VideoPreview()
    {
        var sink=new Sink();var output=new FakeOutput();var shell=new IplShell(output,uiSounds:sink);
        Open(shell,ShellFace.Video);sink.Expect(IplUiSound.TurnSide,IplUiSound.OpenEditor);
        Move(shell,1,0);Move(shell,1,0);sink.Expect(IplUiSound.ChangeField,IplUiSound.ChangeField);
        Move(shell,0,1);sink.Expect(IplUiSound.ChangeValue);shell.Confirm();sink.Expect(IplUiSound.ConfirmEdit);
        Check(shell.Video.Pending,"Applying mode starts the unchanged 12-second preview");
        Move(shell,1,0);Move(shell,0,1);sink.Expect();shell.Tick(11);sink.Expect();
        shell.Confirm();sink.Expect(IplUiSound.ConfirmEdit);Check(!shell.Video.Pending&&shell.Editing,"Keep accepts once and returns to grouped editor");
        Move(shell,0,1);shell.Confirm();sink.Expect(IplUiSound.ChangeValue,IplUiSound.ConfirmEdit);
        shell.Tick(13);sink.Expect(IplUiSound.CancelEdit);shell.Tick(13);sink.Expect();
        Move(shell,0,1);shell.Confirm();sink.Expect(IplUiSound.ChangeValue,IplUiSound.ConfirmEdit);
        output.Failure=1;shell.Tick(13);sink.Expect(IplUiSound.CancelEdit);
        Check(shell.Video.Pending&&shell.Video.Reverting,"Failed automatic rollback remains pending");
        shell.Tick(2);shell.Confirm();shell.Back();sink.Expect(); // Repeated rollback/Keep guards are silent.
        output.Failure=0;shell.Tick(2);sink.Expect();Check(!shell.Video.Pending&&shell.Page==ShellPage.Face,"Retry completes the requested return without a second cue");
        Open(shell,ShellFace.Video);sink.Expect(IplUiSound.OpenEditor);Move(shell,1,0);Move(shell,1,0);Move(shell,0,1);shell.Confirm();sink.Expect(IplUiSound.ChangeField,IplUiSound.ChangeField,IplUiSound.ChangeValue,IplUiSound.ConfirmEdit);
        shell.Back();sink.Expect(IplUiSound.CancelEdit);Check(shell.Page==ShellPage.Face&&!shell.Video.Pending,"B preview reverts and returns to cube");
        Open(shell,ShellFace.Video);sink.Expect(IplUiSound.OpenEditor);output.Failure=2;shell.Confirm();sink.Expect(IplUiSound.CancelEdit);
        Check(shell.Editing&&!shell.Video.Pending,"Failed mode apply keeps the editor");
    }

    private static void SafeModeAndSaveErrors()
    {
        var sink=new Sink();var output=new FakeOutput();var store=new Store();var shell=new IplShell(output,store,uiSounds:sink);
        shell.SafeMode();sink.Expect(IplUiSound.ConfirmEdit);Check(shell.Face==ShellFace.Video&&store.Saves==1,"Safe mode succeeds without an extra turn cue");
        shell.SafeMode();sink.Expect(IplUiSound.ConfirmEdit); // It is still an accepted hardware command.
        output.Failure=1;shell.SafeMode();sink.Expect(IplUiSound.CancelEdit);
        output.Failure=0;store.Writable=false;shell.SafeMode();sink.Expect(IplUiSound.CancelEdit);
        shell.Tick(1);shell.Confirm();sink.Expect(IplUiSound.OpenEditor);Move(shell,1,0);Move(shell,1,0);Move(shell,0,1);shell.Confirm();sink.Expect(IplUiSound.ChangeField,IplUiSound.ChangeField,IplUiSound.ChangeValue,IplUiSound.ConfirmEdit);
        shell.Confirm();sink.Expect(IplUiSound.CancelEdit);Check(!shell.Video.Pending&&shell.SystemStatus=="Write protected","Failed Keep persistence reports failure while retaining the applied session mode");
    }

    private static void BrowserDelegation()
    {
        var sink=new Sink();var files=new Files();var browser=new IplBrowser(files,sink);
        var shell=new IplShell(new FakeOutput(),browser:browser,uiSounds:sink);
        Open(shell,ShellFace.Memory);sink.Expect(IplUiSound.TurnSide,IplUiSound.EnterMenu);
        Move(shell,0,-1);sink.Expect(IplUiSound.ChangeRow);shell.Confirm();sink.Expect(IplUiSound.EnterMenu);
        Move(shell,1,0);sink.Expect(IplUiSound.ChangeRow);shell.Confirm();sink.Expect(IplUiSound.EnterMenu);
        shell.Confirm();sink.Expect(IplUiSound.Back);
        files.IsBusy=true;Move(shell,1,0);shell.Confirm();shell.Tick(1);sink.Expect();
        shell.Back();sink.Expect(IplUiSound.Back);Check(shell.Page==ShellPage.Face,"Shell delegates browser cues exactly once and preserves browser exit");
    }

    internal static void Run()
    {
        Navigation();Volume();CalendarAndSound();VideoPreview();SafeModeAndSaveErrors();BrowserDelegation();
        Console.WriteLine("Shell audio: accepted navigation/field/value cues, silent guards/startup, volume draft/save/reload, hardware errors, preview keep/revert/timeout retries and safe mode passed.");
    }
}
