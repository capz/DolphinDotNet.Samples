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
#if IPL_HARDWARE_RUNNER
    private static async System.Threading.Tasks.Task<int> Main()
    {
        // Finish BBA/DHCP initialization before warmup and measurement. Leaving
        // it to the post-window sender lets native auto-network setup race frames.
        await IplHardwareReporter.Initialize();
        int result=RunFrames();
        if(result==0)await IplHardwareReporter.Send();
        return result;
    }
    private static int RunFrames()
#else
    private static int Main()
#endif
    {
        var device=GraphicsDevice.Default;
        DebugOverlay.TextVisible=false;
        DebugOverlay.PerformanceVisible=false;
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
#if IPL_VISUAL_REVIEW || IPL_PERFORMANCE_REVIEW || IPL_BROWSER_REVIEW || IPL_STORAGE_REVIEW
        device.ConfigureDisplay(new DisplayOptions {Resolution=GraphicsResolution.R640x480,Scan=ScanMode.Interlaced,Widescreen=false});
#endif
#if IPL_HARDWARE_RUNNER
#if IPL_RUNNER_576
        const GraphicsResolution runnerResolution=GraphicsResolution.R640x576;
#else
        const GraphicsResolution runnerResolution=GraphicsResolution.R640x480;
#endif
#if IPL_RUNNER_PROGRESSIVE
        const ScanMode runnerScan=ScanMode.Progressive;
#else
        const ScanMode runnerScan=ScanMode.Interlaced;
#endif
        device.ConfigureDisplay(new DisplayOptions {Resolution=runnerResolution,Scan=runnerScan,Widescreen=false});
#endif
#if IPL_HARDWARE_AA
        const bool hardwareAntialiasing=true;
#else
        const bool hardwareAntialiasing=false;
#endif
#if IPL_STORAGE_REVIEW
        using var browser=new IplBrowser(new IplStorageBrowser(Dolphin.Storage.StorageDevice.DvdDrive));
#elif IPL_BROWSER_REVIEW
        using var browser=new IplBrowser(new IplBrowserFixture());
#else
        using var browser=new IplBrowser(new IplStorageBrowser());
#endif
        var output=new VideoModeOutput(device,hardwareAntialiasing);
#if IPL_VISUAL_REVIEW || IPL_PERFORMANCE_REVIEW || IPL_VIDEO_MODE_REVIEW || IPL_HARDWARE_RUNNER
        var shell=new IplShell(output);
#elif IPL_STORAGE_REVIEW
        var shell=new IplShell(output,browser:browser);
#else
        Controller.Update();bool restoreVideo=!Controller.GetState(0).X.IsHeld;
        var shell=new IplShell(output,new IplPreferencesStore(),new IplConsoleCalendar(),restoreVideo,new IplConsoleSound(),browser);
#endif
#if IPL_VIDEO_MODE_REVIEW
        var scanReview=new VideoModeScanReview(shell,output,reviewResolution);
#endif
#if IPL_VISUAL_REVIEW || IPL_PERFORMANCE_REVIEW || IPL_HARDWARE_RUNNER
        // Repeatable native review pose; normal builds retain the interactive IPL.
#if !IPL_RUNNER_CUBE && !IPL_REVIEW_CUBE
        shell.TurnTo(ShellFace.Options);shell.Row=1;shell.Tick(1);shell.Time=0;
#if IPL_RUNNER_DETAIL || IPL_REVIEW_DETAIL
        shell.TurnTo(ShellFace.Video);shell.Row=1;shell.Tick(1);
        shell.Confirm();shell.Confirm();
#endif
#endif
#endif
        using var canvas=new GameCubeCanvas(device);
        var renderer=new IplRenderer(canvas);
        var clock=new FrameClock {MaximumDeltaSeconds=.05f};
#if IPL_PERFORMANCE_REVIEW || IPL_HARDWARE_RUNNER
        IplFrameProfiler.Start();
#endif
#if IPL_OVERLAY_REVIEW
        IplPerformanceOverlay.Stats.Toggle();
#endif
#if IPL_OVERLAY_TIMERS_ONLY
        IplPerformanceOverlay.DrawEnabled=false;
#endif
#if IPL_VISUAL_REVIEW && !IPL_AA_BENCHMARK
        int reviewFrames=0;
#endif
        while(true)
        {
            long frameStart=Clock.Timestamp;
            IplFrameProfiler.CaptureUs=IplFrameProfiler.BlurUs=0;
            IplFrameProfiler.ExtraDraws=IplFrameProfiler.ExtraTriangles=0;
            Controller.Update();var pad=Controller.GetState(0);float dt=clock.Tick();
            shell.Tick(dt);
            if(browser.LaunchRequested)return 0;
#if IPL_VISUAL_REVIEW || IPL_PERFORMANCE_REVIEW || IPL_HARDWARE_RUNNER
            shell.Time=0;
#endif
#if IPL_VIDEO_MODE_REVIEW
            scanReview.Tick();
            scanReview.CheckAntialiasing(device.Display.Antialiasing,hardwareAntialiasing&&shell.Video.Applied.Resolution!=2);
#elif !IPL_HARDWARE_RUNNER
            if(pad.Z.WasPressed)
            {
                // The SDK also toggles its generic panel in Controller.Update.
                // Use the app's detailed panel instead of drawing both.
                DebugOverlay.PerformanceVisible=false;
                IplPerformanceOverlay.DrawEnabled=true;
                IplPerformanceOverlay.Stats.Toggle();
            }
            if(pad.Y.WasPressed){if(shell.Page==ShellPage.Browser)browser.Refresh();else if(!IplFrameProfiler.Active)IplFrameProfiler.Start();}
            if(pad.Start.WasPressed) {shell.Video.Revert();if(!shell.Video.Pending)return 0;}
            if(pad.X.WasPressed)
            {
                shell.SafeMode();
            }
            else if(pad.B.WasPressed)shell.Back();
            else if(pad.A.WasPressed)shell.Confirm();
            else if(pad.R.WasPressed){if(shell.Page==ShellPage.Cube)shell.TurnTo(ShellFace.Memory);else if(shell.Page==ShellPage.Browser)browser.SwitchSlot(1);}
            else if(pad.L.WasPressed&&shell.Page==ShellPage.Browser){if(browser.Kind==IplBrowserKind.Sd)browser.ParentDirectory();else browser.SwitchSlot(0);}
            else
            {
                int x=pad.DPadRight.IsHeld?1:pad.DPadLeft.IsHeld?-1:0;
                int y=pad.DPadUp.IsHeld?1:pad.DPadDown.IsHeld?-1:0;
                bool stick=x==0&&y==0;
                if(stick){x=pad.Stick.X>.55f?1:pad.Stick.X<-.55f?-1:0;y=pad.Stick.Y>.55f?1:pad.Stick.Y<-.55f?-1:0;}
                shell.Direction(x,y,dt,stick);
            }
#endif
            if(browser.LaunchRequested)return 0;
            IplPerformanceOverlay.BeginFrame();
            bool timing=IplFrameProfiler.Active||IplPerformanceOverlay.Stats.Visible;
            long phaseStart=timing?Clock.Timestamp:0;
#if !IPL_HARDWARE_RUNNER
            canvas.UpdateArtwork(shell.Page==ShellPage.Browser?browser:null);
#endif
            canvas.Prepare();renderer.Prepare(shell);
#if !IPL_HARDWARE_RUNNER
            IplPerformanceOverlay.PrepareNumbers(canvas);
#endif
            int prepare=timing?IplFrameProfiler.Since(phaseStart):0;
            phaseStart=timing?Clock.Timestamp:0;
            device.BeginFrame(Color32.Black);canvas.Begin();renderer.Present(shell);
            if(!IplPerformanceOverlay.Stats.Visible&&!IplFrameProfiler.Active&&IplFrameProfiler.Status.Length>0)
            {
                canvas.Text(IplFrameProfiler.Status,12,6,12,0x98d8c9ffu,0);
                canvas.Text(IplFrameProfiler.Summary,12,21,12,0x98d8c9ffu,0);
            }
#if IPL_VIDEO_MODE_REVIEW
            canvas.Text(scanReview.Caption,12,6,16,scanReview.Failed?0xff4545ffu:0x7bffb0ffu,0);
#endif
            canvas.End();
            int submit=timing?IplFrameProfiler.Since(phaseStart):0;
#if !IPL_HARDWARE_RUNNER
            if(IplPerformanceOverlay.Stats.Visible&&IplPerformanceOverlay.DrawEnabled)
            {
                long overlayStart=Clock.Timestamp;
                canvas.Begin();IplPerformanceOverlay.Draw(canvas);
#if IPL_PERFORMANCE_REVIEW
                long overlayFlushStart=Clock.Timestamp;
#endif
                canvas.End();
#if IPL_PERFORMANCE_REVIEW
                IplPerformanceOverlay.FlushUs=IplFrameProfiler.Since(overlayFlushStart);
#endif
                IplPerformanceOverlay.Stats.Current[IplPerformanceStats.Overlay]=IplFrameProfiler.Since(overlayStart);
            }
#endif
            phaseStart=timing?Clock.Timestamp:0;
            device.EndFrame();
            int end=timing?IplFrameProfiler.Since(phaseStart):0;
            IplPerformanceOverlay.CompleteFrame(frameStart,prepare,submit,end);
            if(IplFrameProfiler.Active)
            {
                IplFrameProfiler.Record(frameStart,prepare,submit,end,device,shell);
            }
            if(IplFrameProfiler.NeedsExport&&!shell.Video.Pending)
            {IplFrameProfiler.NeedsExport=false;IplFrameProfiler.Export();}
#if IPL_PERFORMANCE_REVIEW
            // Keep the completed buffer stable for read-only debugger extraction.
            if(IplFrameProfiler.Ready)while(Clock.Timestamp>=0) { }
#endif
#if IPL_HARDWARE_RUNNER
            // All rendering and timing finish before BBA reporting starts.
            // Returning from the async entry point restores the resident loader.
            if(IplFrameProfiler.Ready)return 0;
#endif
#if IPL_VISUAL_REVIEW && !IPL_AA_BENCHMARK
            // Freeze only after a complete native presentation, so window
            // captures cannot stop between the cube and footer drawing passes.
            if(++reviewFrames==45)while(Clock.Timestamp>=0) { }
#endif
        }
    }
}
