using Dolphin.Graphics;
using Dolphin.Input;
using Dolphin.Timing;

internal sealed class VideoModeOutput : IVideoModeOutput
{
    private readonly GraphicsDevice device;
    internal VideoModeOutput(GraphicsDevice device) { this.device=device; }
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
            Scan=renderingOnly?ScanMode.Auto:(ScanMode)settings.Scan,Widescreen=settings.Widescreen
        });
    }
}
internal static class Program
{
    private static int Main()
    {
        var device=GraphicsDevice.Default;
        var shell=new IplShell(new VideoModeOutput(device));
        using var canvas=new GameCubeCanvas(device);
        var renderer=new IplRenderer(canvas);
        var clock=new FrameClock {MaximumDeltaSeconds=.05f};
        while(true)
        {
            Controller.Update();var pad=Controller.GetState(0);float dt=clock.Tick();
            shell.Tick(dt);
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
            canvas.Prepare();renderer.Prepare(shell);
            device.BeginFrame(Color32.Black);canvas.Begin();renderer.Present(shell);canvas.End();device.EndFrame();
        }
    }
}
