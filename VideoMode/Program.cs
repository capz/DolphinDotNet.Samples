using Dolphin.Graphics;
using Dolphin.Diagnostics;
using Dolphin.Input;
using Dolphin.Timing;

internal sealed class VideoModeOutput : IVideoModeOutput
{
    private readonly GraphicsDevice device;
    private readonly bool hardwareAntialiasing;
    internal VideoModeOutput(GraphicsDevice device,bool hardwareAntialiasing)
    {
        this.device=device;this.hardwareAntialiasing=hardwareAntialiasing;
        try { device.ConfigureDisplay(new DisplayOptions {Antialiasing=hardwareAntialiasing&&Read().Resolution!=2,Widescreen=device.Display.Widescreen}); }
        catch(System.OutOfMemoryException) { this.hardwareAntialiasing=false; } // Keep the boot mode and its quality policy for this session.
    }
    public VideoModeSettings Read()
    {
        var display=device.Display;
        int resolution=display.Resolution==GraphicsResolution.R320x240?0:display.Resolution==GraphicsResolution.R640x576?2:1;
        return new VideoModeSettings(resolution,(int)display.Scan,display.Widescreen);
    }
    public void Configure(VideoModeSettings settings)
    {
        var current=Read();bool renderingOnly=settings.Resolution==current.Resolution&&settings.Scan==current.Scan;
        device.ConfigureDisplay(new DisplayOptions {
            Resolution=renderingOnly?GraphicsResolution.Auto:settings.Resolution==0?GraphicsResolution.R320x240:settings.Resolution==2?GraphicsResolution.R640x576:GraphicsResolution.R640x480,
            Scan=renderingOnly?ScanMode.Auto:(ScanMode)settings.Scan,Widescreen=settings.Widescreen,
            Antialiasing=renderingOnly?device.Display.Antialiasing:hardwareAntialiasing&&settings.Resolution!=2
        });
    }
}
internal static class Program
{
    private static int Main()
    {
        var device=GraphicsDevice.Default;
        DebugOverlay.TextVisible=false;
#if IPL_AA_BENCHMARK
        DebugOverlay.PerformanceVisible=true;
#endif
#if IPL_VIDEO_MODE_REVIEW
#if IPL_VIDEO_MODE_REVIEW_PAL
        const int reviewResolution=2;
        const GraphicsResolution reviewDisplay=GraphicsResolution.R640x576;
#else
        const int reviewResolution=1;
        const GraphicsResolution reviewDisplay=GraphicsResolution.R640x480;
#endif
        device.ConfigureDisplay(new DisplayOptions {Resolution=reviewDisplay,Scan=ScanMode.Interlaced,Widescreen=false});
#endif
#if IPL_VISUAL_REVIEW
        device.ConfigureDisplay(new DisplayOptions {Resolution=GraphicsResolution.R640x480,Scan=ScanMode.Interlaced,Widescreen=false});
#endif
#if IPL_HARDWARE_AA
        const bool hardwareAntialiasing=true;
#else
        const bool hardwareAntialiasing=false;
#endif
        var output=new VideoModeOutput(device,hardwareAntialiasing);
        var shell=new IplShell(output);
#if IPL_VIDEO_MODE_REVIEW
        var scanReview=new VideoModeScanReview(shell,output,reviewResolution);
#endif
#if IPL_VISUAL_REVIEW
        // Repeatable native review pose; normal builds retain the interactive IPL.
        shell.TurnTo(ShellFace.Options);shell.Row=1;shell.Tick(1);shell.Time=0;
#endif
        using var canvas=new GameCubeCanvas(device);
        var renderer=new IplRenderer(canvas);
        var clock=new FrameClock {MaximumDeltaSeconds=.05f};
#if IPL_VISUAL_REVIEW && !IPL_AA_BENCHMARK
        int reviewFrames=0;
#endif
        while(true)
        {
            Controller.Update();var pad=Controller.GetState(0);float dt=clock.Tick();
            shell.Tick(dt);
#if IPL_VISUAL_REVIEW
            shell.Time=0;
#endif
#if IPL_VIDEO_MODE_REVIEW
            scanReview.Tick();
            scanReview.CheckAntialiasing(device.Display.Antialiasing,hardwareAntialiasing&&shell.Video.Applied.Resolution!=2);
#else
            if(pad.Start.WasPressed) {shell.Video.Revert();if(!shell.Video.Pending)return 0;}
            if(pad.X.WasPressed)
            {
                shell.Video.SafeMode();
                if(!shell.Video.Pending)shell.TurnTo(ShellFace.Video);
            }
            else if(pad.B.WasPressed)shell.Back();
            else if(pad.A.WasPressed)shell.Confirm();
            else
            {
                int x=pad.DPadRight.IsHeld||pad.Stick.X>.55f?1:pad.DPadLeft.IsHeld||pad.Stick.X<-.55f?-1:0;
                int y=pad.DPadUp.IsHeld||pad.Stick.Y>.55f?1:pad.DPadDown.IsHeld||pad.Stick.Y<-.55f?-1:0;
                shell.Direction(x,y,dt);
            }
#endif
            canvas.Prepare();renderer.Prepare(shell);
            device.BeginFrame(Color32.Black);canvas.Begin();renderer.Present(shell);
#if IPL_VIDEO_MODE_REVIEW
            canvas.Text(scanReview.Caption,12,6,16,scanReview.Failed?0xff4545ffu:0x7bffb0ffu,0);
#endif
            canvas.End();device.EndFrame();
#if IPL_VISUAL_REVIEW && !IPL_AA_BENCHMARK
            // Freeze only after a complete native presentation, so window
            // captures cannot stop between the cube and footer drawing passes.
            if(++reviewFrames==45)while(Clock.Timestamp>=0) { }
#endif
        }
    }
}
