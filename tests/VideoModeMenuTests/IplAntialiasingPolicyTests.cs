using System;

internal static class IplAntialiasingPolicyTests
{
    private static void Check(bool pass,string message)
    {if(!pass)throw new Exception("Native AA page policy: "+message);}
    internal static void Run()
    {
        var policy=new IplAntialiasingPolicy(true);
        Check(policy.Enabled&&policy.CubeVisible,"interactive startup selects the cube");
        Check(!policy.SetCubeVisible(true),"the initial cube does not request a redundant mode change");
        for(int resolution=0;resolution<3;resolution++)for(int scan=0;scan<3;scan++)foreach(bool wide in new[]{false,true})
        {
            var video=new VideoModeSettings(resolution,scan,wide);
            Check(policy.Wanted(video)==(resolution!=2),"cube AA follows supported resolution, independently of scan and aspect");
        }

        Check(policy.SetCubeVisible(false),"opening a flat page reports one transition");
        for(int resolution=0;resolution<3;resolution++)for(int scan=0;scan<3;scan++)
            Check(!policy.Wanted(new VideoModeSettings(resolution,scan,false)),"flat settings and storage retain full EFB resolution");
        for(int frame=0;frame<600;frame++)
            Check(!policy.SetCubeVisible(false),"idle flat frames do not repeat a display transition");
        Check(policy.SetCubeVisible(true)&&policy.Wanted(new VideoModeSettings(1,2,true)),"returning to the cube restores AA");
        Check(!policy.SetCubeVisible(true),"idle cube frames do not repeat a display transition");

        policy.Disable();
        Check(!policy.Enabled&&!policy.Wanted(new VideoModeSettings(1,1,false)),"allocation failure disables AA immediately");
        policy.SetCubeVisible(false);policy.SetCubeVisible(true);policy.Disable();
        for(int resolution=0;resolution<3;resolution++)for(int scan=0;scan<3;scan++)
            Check(!policy.Wanted(new VideoModeSettings(resolution,scan,true)),"page or video changes cannot re-enable a failed session");

        var disabled=new IplAntialiasingPolicy(false);
        Check(!disabled.Enabled&&!disabled.Wanted(new VideoModeSettings(1,1,false)),"explicit AA-off builds retain the control path");
        disabled.SetCubeVisible(false);disabled.SetCubeVisible(true);
        Check(!disabled.Wanted(new VideoModeSettings(0,2,false)),"page navigation preserves an explicit AA-off choice");
        Console.WriteLine("Native AA page policy: cube-only coverage, sharp flat pages, scan/aspect compatibility, edge-only transitions and failure latch passed.");
    }
}
