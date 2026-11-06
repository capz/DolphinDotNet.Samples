using System;

internal readonly struct IplPreferences
{
    internal readonly VideoModeSettings Video;
    internal readonly int Sound;
    internal readonly bool ReducedMotion;
    internal IplPreferences(VideoModeSettings video,int sound,bool reducedMotion)
    {Video=video;Sound=sound;ReducedMotion=reducedMotion;}
    internal bool Valid=>Video.Resolution>=0&&Video.Resolution<=2&&Video.Scan>=0&&Video.Scan<=2&&Sound>=0&&Sound<=1;
}
internal interface IIplPreferencesStore
{
    bool Load(out IplPreferences value);
    bool Save(IplPreferences value);
    string Status {get;}
}
// Two independently checksummed slots retain the previous generation if a
// power loss tears the current write. Only confirmed applied settings are saved.
internal static class IplPreferencesCodec
{
    internal const int Size=32;
    private static uint Read(byte[] data,int at)=>(uint)data[at]|(uint)data[at+1]<<8|(uint)data[at+2]<<16|(uint)data[at+3]<<24;
    private static void Write(byte[] data,int at,uint value){for(int i=0;i<4;i++)data[at+i]=(byte)(value>>(i*8));}
    private static uint Crc(byte[] data)
    {
        uint crc=0xffffffff;for(int i=0;i<28;i++){crc^=data[i];for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}
        return ~crc;
    }
    internal static bool Newer(uint candidate,uint current)=>unchecked((int)(candidate-current))>0;
    internal static byte[] Encode(IplPreferences value,uint generation)
    {
        if(!value.Valid)throw new ArgumentException("Invalid preferences.");
        var data=new byte[Size];data[0]=73;data[1]=80;data[2]=76;data[3]=83;data[4]=1;
        data[5]=(byte)value.Video.Resolution;data[6]=(byte)value.Video.Scan;data[7]=(byte)((value.Video.Widescreen?1:0)|(value.ReducedMotion?2:0));data[8]=(byte)value.Sound;
        Write(data,12,generation);Write(data,28,Crc(data));return data;
    }
    internal static bool Decode(byte[]? data,out IplPreferences value,out uint generation)
    {
        value=default;generation=0;
        if(data==null||data.Length!=Size||data[0]!=73||data[1]!=80||data[2]!=76||data[3]!=83||data[4]!=1||data[7]>3||Read(data,28)!=Crc(data))return false;
        for(int i=9;i<28;i++)if((i<12||i>15)&&data[i]!=0)return false;
        var decoded=new IplPreferences(new VideoModeSettings(data[5],data[6],(data[7]&1)!=0),data[8],(data[7]&2)!=0);
        if(!decoded.Valid)return false;
        value=decoded;generation=Read(data,12);return true;
    }
}

internal readonly struct IplCalendarValue
{
    internal readonly int Year,Month,Day,Hour,Minute,Second;
    internal IplCalendarValue(int year,int month,int day,int hour,int minute,int second=0)
    {Year=year;Month=month;Day=day;Hour=hour;Minute=minute;Second=second;}
}
internal interface IIplConsoleCalendar
{
    bool Read(out IplCalendarValue value);
    bool Write(IplCalendarValue value);
    string Status {get;}
}
internal interface IIplConsoleSound
{
    bool Read(out int mode);
    bool Write(int mode);
    string Status {get;}
}
