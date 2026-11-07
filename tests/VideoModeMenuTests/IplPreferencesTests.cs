using System;
internal static class IplPreferencesTests
{
    private sealed class Store : IIplPreferencesStore
    {
        internal IplPreferences Value=new IplPreferences(new VideoModeSettings(2,2,true),1,true);
        internal bool Present=true,Writable=true;internal int Saves;
        public string Status=>Writable?"Saved":"Write protected";
        public bool Load(out IplPreferences value){value=Value;return Present;}
        public bool Save(IplPreferences value){Saves++;if(Writable)Value=value;return Writable;}
    }
    private sealed class Calendar : IIplConsoleCalendar
    {
        internal IplCalendarValue Now=new IplCalendarValue(2026,11,6,14,27,43);
        internal int Writes;internal bool Writable=true;
        public string Status=>Writable?"Clock ready":"Clock unavailable";
        public bool Read(out IplCalendarValue value){value=Now;return true;}
        public bool Write(IplCalendarValue value){Writes++;if(Writable)Now=value;return Writable;}
    }
    private sealed class ConsoleSound : IIplConsoleSound
    {
        internal int Mode,Writes;internal bool Writable=true;
        public string Status=>Writable?"Sound ready":"Sound write failed";
        public bool Read(out int mode){mode=Mode;return true;}
        public bool Write(int mode){Writes++;if(Writable)Mode=mode;return Writable;}
    }
    private static void Check(bool pass,string text){if(!pass)throw new Exception(text);}
    private static void Face(IplShell s,ShellFace face){s.TurnTo(face);s.Tick(1);s.Confirm();}
    private static void Move(IplShell s,int x,int y){s.Direction(0,0,0);s.Direction(x,y,.016f);}
    internal static void Run()
    {
        var settings=new IplPreferences(new VideoModeSettings(1,2,true),1,true);
        byte[] data=IplPreferencesCodec.Encode(settings,uint.MaxValue);
        Check(IplPreferencesCodec.Decode(data,out var parsed,out uint generation)&&generation==uint.MaxValue&&parsed.Video.Widescreen&&parsed.Sound==1&&parsed.ReducedMotion,"Preferences roundtrip");
        for(int i=0;i<data.Length;i++){var corrupt=(byte[])data.Clone();corrupt[i]^=1;Check(!IplPreferencesCodec.Decode(corrupt,out _,out _),"Every damaged preference byte is rejected");}
        Check(!IplPreferencesCodec.Decode(new byte[31],out _,out _)&&IplPreferencesCodec.Newer(0,uint.MaxValue)&&!IplPreferencesCodec.Newer(uint.MaxValue,0),"Truncation and generation wrap");
        var output=new FakeOutput();var store=new Store();var clock=new Calendar();var s=new IplShell(output,store,clock);
        Check(output.Current.Resolution==2&&output.Current.Scan==2&&output.Current.Widescreen&&s.Sound==1&&s.ReducedMotion&&s.Hour==14&&s.Day==6,"Startup applies persisted mode/options and reads real calendar");
        var skipped=new FakeOutput();new IplShell(skipped,store,null,false);Check(skipped.Changes==0,"Startup video restore can be bypassed");
        var unavailable=new FakeOutput {Failure=1};var fallback=new IplShell(unavailable,store);Check(unavailable.Current.Resolution==1&&fallback.SystemStatus.Contains("unavailable"),"Unsupported saved video leaves boot output");
        Face(s,ShellFace.Video);s.Row=1;s.Confirm();Move(s,0,-1);s.Confirm();Check(store.Saves==0,"Hardware preview is not persisted");s.Back();Check(store.Saves==0,"Rollback is not persisted");
        Face(s,ShellFace.Video);s.Row=1;s.Confirm();Move(s,0,-1);s.Confirm();s.Confirm();Check(store.Saves==1&&store.Value.Video.Scan==1,"Only confirmed hardware output is saved");
        Face(s,ShellFace.Calendar);s.Confirm();Move(s,0,1);Move(s,1,0);Move(s,0,1);s.Confirm();
        Check(clock.Now.Hour==15&&clock.Now.Minute==28&&clock.Now.Second==43&&clock.Now.Day==6&&!s.Editing,"Time commit preserves current date and seconds");
        s.Row=1;s.Confirm();Move(s,1,0);Move(s,0,-1);clock.Now=new IplCalendarValue(2026,11,6,16,30,12);s.Confirm();
        Check(clock.Now.Month==10&&clock.Now.Hour==16&&clock.Now.Minute==30&&clock.Now.Second==12,"Date commit preserves current live time");
        s.Confirm();Move(s,0,1);s.Back();Check(clock.Writes==2,"B never writes the console clock");
        Face(s,ShellFace.Calendar);s.Confirm();clock.Writable=false;s.Confirm();Check(s.Editing&&s.SystemStatus=="Clock unavailable","Clock failure stays editable and visible");
        s.Back();Face(s,ShellFace.Options);s.Row=1;s.Confirm();Check(store.Saves==2,"Motion preference is saved");
        store.Writable=false;s.Row=0;s.Confirm();Move(s,0,1);s.Confirm();Check(!s.Editing&&s.SystemStatus=="Write protected","Persistence failure does not hide a retained applied setting");
        s.SafeMode();Check(store.Saves==4&&output.Current.Resolution==1&&output.Current.Scan==1&&!output.Current.Widescreen,"Explicit safe mode persists safe output");
        var sound=new ConsoleSound();var soundStore=new Store();var withSound=new IplShell(new FakeOutput(),soundStore,null,true,sound);
        Check(sound.Mode==1&&sound.Writes==1,"Startup saved sound is applied to console SRAM");
        Face(withSound,ShellFace.Options);withSound.Confirm();Move(withSound,0,1);withSound.Back();Check(sound.Writes==1,"Discarded sound draft never writes SRAM");
        Face(withSound,ShellFace.Options);withSound.Confirm();Move(withSound,0,1);sound.Writable=false;withSound.Confirm();Check(withSound.Editing&&soundStore.Saves==0&&withSound.Sound==1,"Failed SRAM sound write preserves edit and does not persist draft");
        sound.Writable=true;withSound.Confirm();Check(!withSound.Editing&&sound.Mode==0&&soundStore.Value.Sound==0,"Confirmed console sound is persisted on SD");
        var freshSound=new ConsoleSound{Mode=1};var freshShell=new IplShell(new FakeOutput(),new Store{Present=false},null,true,freshSound);Check(freshShell.Sound==1&&freshSound.Writes==0,"Without saved settings read existing console sound");
        var failedSound=new ConsoleSound{Writable=false};var failedShell=new IplShell(new FakeOutput(),new Store(),null,true,failedSound);Check(failedShell.Sound==0&&failedShell.SystemStatus=="Sound write failed","Startup SRAM failure reconciles real sound");
        Console.WriteLine("Preferences/calendar: CRC, generations, startup/fallback, confirmed-only saves, live clock part commits, cancellation and errors passed.");
    }
}
