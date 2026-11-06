using System;
using System.IO;
using Dolphin.Storage;
using Store=Dolphin.Storage.Storage;

internal sealed class IplPreferencesStore : IIplPreferencesStore
{
    private readonly StorageDevice device;
    private uint generation;
    private int slot=-1;
    internal const string Folder="videomode",SlotA="videomode/settings-a.bin",SlotB="videomode/settings-b.bin";
    public string Status {get;private set;}="Settings not loaded.";
    internal IplPreferencesStore(StorageDevice device=StorageDevice.GcLoader){this.device=device;}
    private bool Mount()=>Store.IsMounted(device)||Store.Mount(device);
    private byte[]? Read(string path)
    {
        try{
            if(!Store.FileExists(device,path))return null;
            using var file=Store.Open(device,path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(file.Length!=IplPreferencesCodec.Size)return null;
            var data=new byte[IplPreferencesCodec.Size];int done=0;
            while(done<data.Length){int n=file.Read(data,done,data.Length-done);if(n<=0)return null;done+=n;}
            return data;
        }catch(Exception){return null;}
    }
    public bool Load(out IplPreferences value)
    {
        value=default;
        try{
            if(!Mount()){Status="GC Loader SD unavailable. Using boot settings.";return false;}
            bool a=IplPreferencesCodec.Decode(Read(SlotA),out var first,out uint ag),b=IplPreferencesCodec.Decode(Read(SlotB),out var second,out uint bg);
            if(!a&&!b){Status="No valid saved settings. Using boot settings.";return false;}
            slot=b&&(!a||IplPreferencesCodec.Newer(bg,ag))?1:0;generation=slot==0?ag:bg;value=slot==0?first:second;
            Status="Settings loaded from GC Loader SD.";return true;
        }catch(Exception){Status="Could not read settings. Using boot settings.";return false;}
    }
    public bool Save(IplPreferences value)
    {
        try{
            if(!Mount()){Status="Settings not saved: GC Loader SD unavailable.";return false;}
            Store.CreateDirectory(device,Folder);
            int target=slot==0?1:0;uint next=unchecked(generation+1);string path=target==0?SlotA:SlotB;
            byte[] data=IplPreferencesCodec.Encode(value,next);
            using(var file=Store.Open(device,path,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(data,0,data.Length);file.Flush();}
            byte[]? actual=Read(path);bool same=actual!=null&&actual.Length==data.Length;
            if(same)for(int i=0;i<data.Length;i++)if(actual![i]!=data[i]){same=false;break;}
            if(!same||!IplPreferencesCodec.Decode(actual,out _,out uint verified)||verified!=next){Status="Settings write could not be verified.";return false;}
            generation=next;slot=target;Status="Settings saved on GC Loader SD.";return true;
        }catch(Exception){Status="Settings not saved. Check SD access or write protection.";return false;}
    }
}

internal sealed class IplConsoleCalendar : IIplConsoleCalendar
{
    public string Status {get;private set;}="System clock unavailable.";
    public bool Read(out IplCalendarValue value)
    {
        value=default;
#if IPL_CONSOLE_SERVICES
        try{var now=Dolphin.Timing.SystemClock.Read();value=new IplCalendarValue(now.Year,now.Month,now.Day,now.Hour,now.Minute,now.Second);Status="GameCube system clock.";return true;}
        catch(Exception){Status="Could not read the GameCube clock.";}
#endif
        return false;
    }
    public bool Write(IplCalendarValue value)
    {
#if IPL_CONSOLE_SERVICES
        try{Dolphin.Timing.SystemClock.Write(new Dolphin.Timing.CalendarTime(value.Year,value.Month,value.Day,value.Hour,value.Minute,value.Second));Status="System clock saved for IPL.";return true;}
        catch(Exception){Status="Clock write failed or SRAM is pending. Read again before retrying.";}
#endif
        return false;
    }
}
internal sealed class IplConsoleSound : IIplConsoleSound
{
    public string Status {get;private set;}="Console sound unavailable.";
    public bool Read(out int mode)
    {
        mode=0;
#if IPL_CONSOLE_SERVICES
        try{mode=(int)Dolphin.System.ConsolePreferences.SoundMode;Status="Console sound loaded.";return true;}
        catch(Exception){Status="Could not read console sound.";}
#endif
        return false;
    }
    public bool Write(int mode)
    {
#if IPL_CONSOLE_SERVICES
        try{Dolphin.System.ConsolePreferences.SoundMode=(Dolphin.System.ConsoleSoundMode)mode;Status="Console sound saved for IPL.";return true;}
        catch(Exception){Status="Console sound write failed or SRAM is pending.";}
#endif
        return false;
    }
}
