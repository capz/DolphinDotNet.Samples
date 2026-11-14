using System;

internal static class IplUiSoundPreferencesTests
{
    private static void Check(bool pass,string text){if(!pass)throw new Exception(text);}
    internal static void Run()
    {
        var video=new VideoModeSettings(1,2,true);
        Check(new IplPreferences(video,1,false).UiSoundVolume==2&&new IplPreferences(video,1,2).UiSoundVolume==2,"Both preference constructors default UI sound volume to Medium");
        for(int level=0;level<=3;level++){
            var settings=new IplPreferences(video,1,2,3,level);
            byte[] bytes=IplPreferencesCodec.Encode(settings,23);
            Check(bytes.Length==32&&bytes[4]==4&&bytes[11]==level&&IplPreferencesCodec.Decode(bytes,out var restored,out uint generation)&&restored.UiSoundVolume==level&&restored.MotionLevel==2&&restored.GlassBlur==3&&restored.Sound==1&&generation==23,"Every UI sound volume survives v4 storage without changing existing settings");
        }
        foreach(int invalid in new[]{-1,4}){
            var settings=new IplPreferences(video,1,1,0,invalid);bool rejected=false;
            try{IplPreferencesCodec.Encode(settings,23);}catch(ArgumentException){rejected=true;}
            Check(!settings.Valid&&rejected,"Invalid UI sound volume cannot be persisted");
        }
        // Independently generated legacy records with IEEE CRC32. v3 includes
        // each Motion level, so migration cannot silently collapse Pronounced.
        string[] legacy={
            "49504c53010102010100000017000000000000000000000000000000d9cb17a5",
            "49504c5302010203010300001700000000000000000000000000000025e59bba",
            "49504c530301020301030000170000000000000000000000000000000280be3b",
            "49504c530301020101030100170000000000000000000000000000009e372830",
            "49504c53030102010103020017000000000000000000000000000000fceaaeda"};
        int[] motion={1,0,0,1,2};
        for(int i=0;i<legacy.Length;i++){
            Check(IplPreferencesCodec.Decode(Convert.FromHexString(legacy[i]),out var settings,out uint generation)&&settings.UiSoundVolume==2&&settings.MotionLevel==motion[i]&&settings.GlassBlur==(i==0?0:3)&&settings.Video.Widescreen&&settings.Sound==1&&generation==23,"v1/v2/v3 migrate UI sounds to Medium while preserving video, sound, blur and motion");
        }
        foreach(string invalid in new[]{
            "49504c5304010201010301041700000000000000000000000000000026273872", // v4 invalid volume
            "49504c5304010201010301021700000001000000000000000000000041dca94e", // v4 reserved byte16
            "49504c530401020301030102170000000000000000000000000000005391c831", // v4 contradictory Disabled bit
            "49504c530101020101000001170000000000000000000000000000009a00b122", // v1 reserved volume byte
            "49504c53020102030103000117000000000000000000000000000000662e3d3d", // v2 reserved volume byte
            "49504c53030102010102010117000000000000000000000000000000857c6c60", // v3 reserved volume byte
            "49504c53050102010103010017000000000000000000000000000000ce651580"}) // unsupported v5
            Check(!IplPreferencesCodec.Decode(Convert.FromHexString(invalid),out _,out _),"Valid CRC cannot admit invalid volume, reserved legacy bytes or unknown versions");
        byte[] damaged=IplPreferencesCodec.Encode(new IplPreferences(video,1,false,3,2),23);damaged[11]^=1;
        Check(!IplPreferencesCodec.Decode(damaged,out _,out _),"Volume corruption is covered by the record checksum");
        Console.WriteLine("UI sound preferences: Medium defaults, all volumes, v1/v2/v3 migration, reserved fields and checksum passed.");
    }
}
