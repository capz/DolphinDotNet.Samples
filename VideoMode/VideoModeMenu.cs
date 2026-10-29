using System;

// No rendering or input calls here: mode changes always happen between frames.
internal readonly struct VideoModeSettings
{
    internal readonly int Resolution; // 0: 320x240, 1: 640x480, 2: 640x576.
    internal readonly int Scan;       // 0: Auto, 1: Interlaced, 2: Progressive.
    internal readonly bool Widescreen;
    internal VideoModeSettings(int resolution, int scan, bool widescreen)
    { Resolution=resolution; Scan=scan; Widescreen=widescreen; }
}

internal interface IVideoModeOutput
{
    VideoModeSettings Read();
    void Configure(VideoModeSettings settings);
}

internal sealed class VideoModeMenu
{
    internal const float PreviewSeconds=12;
    private readonly IVideoModeOutput output;
    private VideoModeSettings previous;
    internal VideoModeSettings Selected { get; private set; }
    internal VideoModeSettings Applied { get; private set; }
    internal int Row { get; private set; }
    internal bool Pending { get; private set; }
    internal float Remaining { get; private set; }
    internal string Status { get; private set; }="CHOOSE SETTINGS THEN PRESS A";

    internal VideoModeMenu(IVideoModeOutput output)
    { this.output=output; Applied=Selected=output.Read(); }

    internal void Move(int direction)
    { if(!Pending) Row=(Row+direction+3)%3; }

    internal void Edit(int direction)
    {
        if(Pending) return;
        var value=Selected;
        Selected=Row==0
            ? new VideoModeSettings((value.Resolution+direction+3)%3,value.Scan,value.Widescreen)
            : Row==1
            ? new VideoModeSettings(value.Resolution,(value.Scan+direction+3)%3,value.Widescreen)
            : new VideoModeSettings(value.Resolution,value.Scan,!value.Widescreen);
    }

    internal void Apply()
    {
        if(Pending) return;
        var before=Applied;
        if(!TryConfigure(Selected)) return;
        previous=before; Pending=true; Remaining=PreviewSeconds;
        Status="A KEEP - B REVERT";
    }

    internal void Keep()
    {
        if(!Pending) return;
        Pending=false; Remaining=0; Status="MODE KEPT";
    }

    internal void Revert()
    {
        if(!Pending) return;
        if(!TryConfigure(previous)) { Remaining=1; return; }
        Selected=Applied; Pending=false; Remaining=0; Status="PREVIOUS MODE RESTORED";
    }

    internal void SafeMode()
    {
        if(!TryConfigure(new VideoModeSettings(1,1,false))) return;
        Selected=Applied; Pending=false; Remaining=0; Status="SAFE MODE - 640X480I 4:3";
    }

    internal void Tick(float seconds)
    {
        if(!Pending) return;
        Remaining-=seconds;
        if(Remaining<=0) Revert();
    }

    private bool TryConfigure(VideoModeSettings settings)
    {
        try { output.Configure(settings); Applied=output.Read(); return true; }
        catch(InvalidOperationException)
        { Status="UNSUPPORTED MODE OR CABLE"; return false; }
        catch(OutOfMemoryException)
        { Status="NOT ENOUGH MEMORY FOR MODE"; return false; }
    }
}
