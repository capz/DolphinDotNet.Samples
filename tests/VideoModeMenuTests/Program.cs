using System;

internal sealed class FakeOutput : IVideoModeOutput
{
    internal VideoModeSettings Current=new VideoModeSettings(1,1,false);
    internal int Failure;
    internal int Changes;
    public VideoModeSettings Read()=>Current;
    public void Configure(VideoModeSettings settings)
    {
        if(Failure==1 || settings.Resolution==0 && settings.Scan==1) throw new InvalidOperationException();
        if(Failure==2) throw new OutOfMemoryException();
        Current=new VideoModeSettings(settings.Resolution,
            settings.Scan==0?(settings.Resolution==0?2:1):settings.Scan,settings.Widescreen);
        Changes++;
    }
}

internal static class Program
{
    private static void Check(bool value,string message)
    { if(!value) throw new Exception(message); }
    private static void Wide(VideoModeMenu menu) { while(menu.Row!=2) menu.Move(1); menu.Edit(1); }

    private static void Main()
    {
        var output=new FakeOutput(); var menu=new VideoModeMenu(output);
        Check(output.Changes==0 && !menu.Pending,"Opening the menu must retain the boot mode");
        menu.Move(-1); Check(menu.Row==2,"Selection wraps up"); menu.Move(1);
        menu.Edit(-1); Check(menu.Selected.Resolution==0,"Resolution changes");
        menu.Edit(-1); Check(menu.Selected.Resolution==2,"Resolution wraps");
        menu.Edit(1); menu.Apply();
        Check(!menu.Pending && output.Changes==0 && menu.Applied.Resolution==1,"Unsupported 240i preserves current mode");
        Check(menu.Status=="UNSUPPORTED MODE OR CABLE","Unsupported mode feedback");
        menu.Edit(1); Wide(menu); menu.Apply();
        Check(menu.Pending && output.Current.Widescreen && menu.Remaining==12,"A successful change starts preview");
        int row=menu.Row; menu.Move(1); menu.Edit(1); menu.Apply();
        Check(menu.Row==row && menu.Selected.Widescreen && output.Changes==1,"Preview locks editing and duplicate apply");
        menu.Tick(11.9f); Check(menu.Pending,"Preview remains before deadline");
        menu.Tick(.2f); Check(!menu.Pending && !output.Current.Widescreen,"Timeout restores exact previous mode");

        Wide(menu); menu.Apply(); menu.Keep(); menu.Tick(20);
        Check(output.Current.Widescreen && !menu.Pending,"Confirmation survives timeout");
        menu.Edit(-1); menu.Apply(); menu.Revert();
        Check(output.Current.Widescreen && !menu.Pending && menu.Selected.Widescreen,"B restores the last confirmed mode");

        menu.Edit(1); output.Failure=2; menu.Apply();
        Check(!menu.Pending && menu.Applied.Widescreen,"Allocation failure does not start preview");
        Check(menu.Status=="NOT ENOUGH MEMORY FOR MODE","Allocation failure feedback");
        output.Failure=0; menu.Apply(); output.Failure=2; menu.Tick(13);
        Check(menu.Pending && menu.Remaining==1,"Failed rollback remains pending and retries");
        output.Failure=0; menu.Tick(1);
        Check(!menu.Pending && output.Current.Widescreen,"Rollback retry restores confirmed settings");

        menu.SafeMode();
        Check(!menu.Pending && output.Current.Resolution==1 && output.Current.Scan==1 && !output.Current.Widescreen,"Safe mode is 640x480i 4:3");
        menu.Move(1); menu.Move(1); menu.Edit(-1); // Scan: Interlaced -> Auto.
        menu.Apply(); Check(menu.Pending && menu.Applied.Scan==1,"Keep the resolved scan mode for rollback");
        menu.Revert();
        Check(output.Current.Scan==1,"Rollback restores resolved scan mode");
        Console.WriteLine("Video mode menu: navigation, apply/keep/revert, timeout, invalid modes, allocation rollback and safe mode passed.");
    }
}
