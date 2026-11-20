using Dolphin.Diagnostics;
using Dolphin.Timing;

internal static class Program
{
    private static int Main()
    {
        DebugOverlay.PerformanceVisible=false;
        DebugOverlay.TextVisible=false;
        var session=new ControllerSession();
        using var scene=new ControllerScene();
        var clock=new FrameClock{MaximumDeltaSeconds=.05f};
        DebugOverlay.WriteLine("CONTROLLER LAB READY");
        while(true){
            long start=Clock.Timestamp;
            float delta=clock.Tick();
            session.Poll(delta);
            if(session.ExitRequested)return 0;
            scene.Present(session,delta,start);
        }
    }
}
