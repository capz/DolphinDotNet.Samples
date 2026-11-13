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
    private static void Motion()
    {
        var fresh=new IplShell(new FakeOutput());
        Check(fresh.MotionLevel==1&&!fresh.ReducedMotion&&fresh.MotionName=="Subtle","Motion defaults to the existing Subtle behavior");
        Check(new IplShell(new FakeOutput(),new Store{Present=false}).MotionLevel==1,"Missing settings retain the Subtle default");
        foreach(int level in new[]{0,1,2}){
            var value=new IplPreferences(new VideoModeSettings(1,2,true),1,level,3);
            byte[] bytes=IplPreferencesCodec.Encode(value,23);
            Check(bytes[4]==3&&bytes[10]==level&&IplPreferencesCodec.Decode(bytes,out var actual,out uint generation)&&actual.MotionLevel==level&&actual.GlassBlur==3&&generation==23,"Every motion level survives version-three storage");
        }
        foreach(int invalid in new[]{-1,3}){
            var value=new IplPreferences(new VideoModeSettings(1,1,false),0,invalid);
            Check(!value.Valid,"Invalid motion levels are rejected");bool rejected=false;
            try{IplPreferencesCodec.Encode(value,0);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Invalid motion cannot be saved");
        }
        // These pre-version-three slots were generated independently with IEEE CRC32.
        string[] legacy={
            "49504c53010102010100000017000000000000000000000000000000d9cb17a5",
            "49504c53010102030100000017000000000000000000000000000000a4cad341",
            "49504c5302010201010300001700000000000000000000000000000058e45f5e",
            "49504c5302010203010300001700000000000000000000000000000025e59bba"};
        for(int i=0;i<legacy.Length;i++){
            Check(IplPreferencesCodec.Decode(Convert.FromHexString(legacy[i]),out var actual,out uint generation)&&actual.MotionLevel==(i%2==0?1:0)&&actual.GlassBlur==(i<2?0:3)&&actual.Video.Widescreen&&actual.Sound==1&&generation==23,"Legacy Full maps to Subtle and Reduced maps to Disabled without losing other options");
        }
        foreach(string invalid in new[]{
            "49504c5303010201010203001700000000000000000000000000000045dc1ee2", // invalid level3
            "49504c53030102030102010017000000000000000000000000000000bbb60e03", // contradictory Disabled bit
            "49504c53030102010102010117000000000000000000000000000000857c6c60", // reserved byte11
            "49504c53040102010102010017000000000000000000000000000000b180d2d6"})
            Check(!IplPreferencesCodec.Decode(Convert.FromHexString(invalid),out _,out _),"Valid CRC cannot admit invalid motion fields, flags or versions");

        var store=new Store{Value=new IplPreferences(new VideoModeSettings(1,1,false),0,1,2)};
        var shell=new IplShell(new FakeOutput(),store);Face(shell,ShellFace.Options);shell.Row=1;shell.Confirm();
        Check(shell.Editing&&shell.MotionLevel==1&&shell.Value(1)=="Subtle"&&store.Saves==0,"Motion opens a value editor without changing or saving the option");
        Move(shell,0,1);Check(shell.MotionLevel==2&&shell.Value(1)=="Pronounced"&&!shell.ReducedMotion&&store.Value.MotionLevel==1,"Motion drafts preview Pronounced without persisting");
        Move(shell,0,1);Check(shell.MotionLevel==0&&shell.ReducedMotion&&shell.Value(1)=="Disabled","Motion wraps through Disabled and suppresses accessibility motion");
        Move(shell,0,-1);Check(shell.MotionLevel==2,"Motion wraps down from Disabled to Pronounced");
        Move(shell,1,0);Check(shell.MotionLevel==2,"Horizontal input does not change Motion");
        shell.Back();Check(shell.MotionLevel==1&&shell.Page==ShellPage.Face&&!shell.Editing&&store.Saves==0,"B discards a motion draft and returns to the cube");
        Face(shell,ShellFace.Options);shell.Row=1;shell.Confirm();Move(shell,0,-1);shell.Confirm();
        Check(shell.FlatMenu&&shell.MotionLevel==0&&store.Saves==1&&new IplShell(new FakeOutput(),store).MotionLevel==0,"Disabled commits and loads at the next startup");
        shell.Confirm();Move(shell,0,-1);shell.Confirm();
        Check(shell.MotionLevel==2&&store.Value.MotionLevel==2&&store.Value.GlassBlur==2&&new IplShell(new FakeOutput(),store).MotionLevel==2,"Pronounced commits and restores without resetting blur");
        store.Writable=false;shell.Confirm();Move(shell,0,1);shell.Confirm();
        Check(shell.FlatMenu&&shell.MotionLevel==0&&shell.SystemStatus=="Write protected"&&store.Value.MotionLevel==2,"Save failure retains the session motion and reports failure without overwriting the saved value");
        shell.ReducedMotion=true;Check(shell.MotionLevel==0,"Historical ReducedMotion=true maps to Disabled");
        shell.ReducedMotion=false;Check(shell.MotionLevel==1,"Historical ReducedMotion=false maps to Subtle");
    }
    private static void GlassBlur()
    {
        Check(new IplShell(new FakeOutput()).GlassBlur==0&&new IplPreferences(new VideoModeSettings(1,1,false),0,false).GlassBlur==0,"Glass blur defaults to Off");
        for(int level=0;level<=3;level++){
            var value=new IplPreferences(new VideoModeSettings(1,2,true),1,true,level);
            Check(IplPreferencesCodec.Decode(IplPreferencesCodec.Encode(value,19),out var decoded,out uint generation)&&decoded.GlassBlur==level&&generation==19,"Every blur level survives encoding");
        }
        foreach(int invalid in new[]{-1,4}){
            var value=new IplPreferences(new VideoModeSettings(1,1,false),0,false,invalid);
            Check(!value.Valid,"Out-of-range blur is invalid");bool rejected=false;
            try{IplPreferencesCodec.Encode(value,0);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Out-of-range blur cannot be persisted");
        }
        // Fixed version-one slots, independently checksummed, exercise migration
        // rather than round-tripping the new encoder back through itself.
        byte[] legacy=Convert.FromHexString("49504c530101020301000000ffffffff000000000000000000000000e5b84949");
        Check(IplPreferencesCodec.Decode(legacy,out var migrated,out uint oldGeneration)&&migrated.GlassBlur==0&&migrated.Video.Widescreen&&migrated.Sound==1&&migrated.ReducedMotion&&oldGeneration==uint.MaxValue,"Old settings load unchanged with blur Off");
        byte[] rewritten=IplPreferencesCodec.Encode(migrated,0);
        Check(rewritten[4]==3&&IplPreferencesCodec.Decode(rewritten,out var next,out uint nextGeneration)&&next.GlassBlur==0&&next.MotionLevel==0&&nextGeneration==0,"Saving legacy settings migrates to the new format across generation wrap");
        foreach(string fixture in new[]{
            "49504c530101020301010000ffffffff000000000000000000000000bd38ab9e", // v1 reserved byte
            "49504c530201020301040000ffffffff0000000000000000000000002f1c3ff8", // v2 blur outside 0..3
            "49504c530201020301010100ffffffff0000000000000000000000007427e729", // v2 reserved byte
            "49504c53040102010102010017000000000000000000000000000000b180d2d6"}) // unknown version
            Check(!IplPreferencesCodec.Decode(Convert.FromHexString(fixture),out _,out _),"Valid CRC does not permit invalid fields or unsupported versions");

        var store=new Store{Value=new IplPreferences(new VideoModeSettings(1,1,false),0,false,2)};
        var s=new IplShell(new FakeOutput(),store);Check(s.GlassBlur==2,"Startup restores glass blur");
        Face(s,ShellFace.Options);Check(s.Rows==3&&s.Label(2)=="Glass Blur"&&s.Value(2)=="Medium","Options retains blur after Storage moves to its own side");
        Move(s,0,1);s.Tick(.18f);Check(s.Row==2&&s.Focus(2)==1,"Final row wraps and gains focus");
        s.Confirm();Move(s,0,1);Check(s.Editing&&s.GlassBlur==3&&s.Value(2)=="High"&&store.Saves==0,"Blur edits preview without saving");
        Move(s,1,0);Check(s.GlassBlur==3,"Horizontal input does not change blur");
        Move(s,0,1);Check(s.GlassBlur==0&&s.Value(2)=="Off","Blur increments wrap to Off");
        Move(s,0,-1);Check(s.GlassBlur==3,"Blur decrements wrap to High");
        s.Back();Check(!s.Editing&&s.Page==ShellPage.Face&&s.GlassBlur==2&&store.Saves==0,"B restores the previous blur without saving");
        Face(s,ShellFace.Options);s.Row=2;s.Confirm();Move(s,0,-1);s.Confirm();
        Check(s.FlatMenu&&s.GlassBlur==1&&s.Value(2)=="Low"&&store.Saves==1&&store.Value.GlassBlur==1,"A commits the chosen blur and persists it");
        Check(new IplShell(new FakeOutput(),store).GlassBlur==1,"Confirmed blur loads on the next boot");
        s.Row=1;s.Confirm();Move(s,0,1);s.Confirm();Check(store.Saves==2&&store.Value.MotionLevel==2&&store.Value.GlassBlur==1,"Saving another preference retains blur");
        store.Writable=false;s.Row=2;s.Confirm();Move(s,0,1);s.Confirm();
        Check(s.FlatMenu&&s.GlassBlur==2&&s.SystemStatus=="Write protected"&&store.Value.GlassBlur==1,"Failed blur save keeps the applied effect and reports failure");
    }
    internal static void Run()
    {
        Motion();
        GlassBlur();
        var settings=new IplPreferences(new VideoModeSettings(1,2,true),1,true,3);
        byte[] data=IplPreferencesCodec.Encode(settings,uint.MaxValue);
        Check(IplPreferencesCodec.Decode(data,out var parsed,out uint generation)&&generation==uint.MaxValue&&parsed.Video.Widescreen&&parsed.Sound==1&&parsed.ReducedMotion&&parsed.GlassBlur==3,"Preferences roundtrip");
        for(int i=0;i<data.Length;i++){var corrupt=(byte[])data.Clone();corrupt[i]^=1;Check(!IplPreferencesCodec.Decode(corrupt,out _,out _),"Every damaged preference byte is rejected");}
        Check(!IplPreferencesCodec.Decode(new byte[31],out _,out _)&&IplPreferencesCodec.Newer(0,uint.MaxValue)&&!IplPreferencesCodec.Newer(uint.MaxValue,0),"Truncation and generation wrap");
        var output=new FakeOutput();var store=new Store();var clock=new Calendar();var s=new IplShell(output,store,clock);
        Check(output.Current.Resolution==2&&output.Current.Scan==2&&output.Current.Widescreen&&s.Sound==1&&s.ReducedMotion&&s.Hour==14&&s.Day==6,"Startup applies persisted mode/options and reads real calendar");
        var skipped=new FakeOutput();new IplShell(skipped,store,null,false);Check(skipped.Changes==0,"Startup video restore can be bypassed");
        var unavailable=new FakeOutput {Failure=1};var fallback=new IplShell(unavailable,store);Check(unavailable.Current.Resolution==1&&fallback.SystemStatus.Contains("unavailable"),"Unsupported saved video leaves boot output");
        Face(s,ShellFace.Video);Move(s,1,0);Move(s,0,-1);s.Confirm();Check(store.Saves==0,"Hardware preview is not persisted");s.Back();Check(store.Saves==0,"Rollback is not persisted");
        Face(s,ShellFace.Video);Move(s,1,0);Move(s,0,-1);s.Confirm();s.Confirm();Check(store.Saves==1&&store.Value.Video.Scan==1,"Only confirmed hardware output is saved");
        Face(s,ShellFace.Calendar);s.Confirm();Move(s,0,1);Move(s,1,0);Move(s,0,1);s.Confirm();
        Check(clock.Now.Hour==15&&clock.Now.Minute==28&&clock.Now.Second==43&&clock.Now.Day==6&&!s.Editing,"Time commit preserves current date and seconds");
        s.Row=1;s.Confirm();Move(s,1,0);Move(s,0,-1);clock.Now=new IplCalendarValue(2026,11,6,16,30,12);s.Confirm();
        Check(clock.Now.Month==10&&clock.Now.Hour==16&&clock.Now.Minute==30&&clock.Now.Second==12,"Date commit preserves current live time");
        s.Confirm();Move(s,0,1);s.Back();Check(clock.Writes==2,"B never writes the console clock");
        Face(s,ShellFace.Calendar);s.Confirm();clock.Writable=false;s.Confirm();Check(s.Editing&&s.SystemStatus=="Clock unavailable","Clock failure stays editable and visible");
        s.Back();Face(s,ShellFace.Options);s.Row=1;s.Confirm();Move(s,0,1);s.Confirm();Check(store.Saves==2,"Confirmed Motion preference is saved");
        store.Writable=false;s.Row=0;s.Confirm();Move(s,0,1);s.Confirm();Check(!s.Editing&&s.SystemStatus=="Write protected","Persistence failure does not hide a retained applied setting");
        s.SafeMode();Check(store.Saves==4&&output.Current.Resolution==1&&output.Current.Scan==1&&!output.Current.Widescreen,"Explicit safe mode persists safe output");
        var sound=new ConsoleSound();var soundStore=new Store();var withSound=new IplShell(new FakeOutput(),soundStore,null,true,sound);
        Check(sound.Mode==1&&sound.Writes==1,"Startup saved sound is applied to console SRAM");
        Face(withSound,ShellFace.Options);withSound.Confirm();Move(withSound,0,1);withSound.Back();Check(sound.Writes==1,"Discarded sound draft never writes SRAM");
        Face(withSound,ShellFace.Options);withSound.Confirm();Move(withSound,0,1);sound.Writable=false;withSound.Confirm();Check(withSound.Editing&&soundStore.Saves==0&&withSound.Sound==1,"Failed SRAM sound write preserves edit and does not persist draft");
        sound.Writable=true;withSound.Confirm();Check(!withSound.Editing&&sound.Mode==0&&soundStore.Value.Sound==0,"Confirmed console sound is persisted on SD");
        var freshSound=new ConsoleSound{Mode=1};var freshShell=new IplShell(new FakeOutput(),new Store{Present=false},null,true,freshSound);Check(freshShell.Sound==1&&freshSound.Writes==0,"Without saved settings read existing console sound");
        var failedSound=new ConsoleSound{Writable=false};var failedShell=new IplShell(new FakeOutput(),new Store(),null,true,failedSound);Check(failedShell.Sound==0&&failedShell.SystemStatus=="Sound write failed","Startup SRAM failure reconciles real sound");
        Console.WriteLine("Preferences/calendar: CRC, legacy migration, motion/blur draft/commit/cancel/reload, generations, startup/fallback, confirmed-only saves, live clock part commits and errors passed.");
    }
}
