using System;

internal static class IplShellTests
{
    private static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    private static void Move(IplShell s,int x,int y){s.Direction(0,0,0);s.Direction(x,y,.016f);}
    private static void Face(IplShell s,ShellFace face){s.TurnTo(face);s.Tick(.7f);}
    private static void ScanRoundTrip()
    {
        for(int resolution=1;resolution<=2;resolution++)
        {
            var output=new FakeOutput {Current=new VideoModeSettings(resolution,1,false)};var shell=new IplShell(output,resolution==2);
            var review=new VideoModeScanReview(shell,output,resolution);
            while(!review.Complete&&!review.Failed)review.Step();
            Check(review.Complete&&!review.Failed&&review.Phase==14,"480/576 i/p previews, keeps, both B rollbacks, timeout to p and safe i");
            Check(output.Current.Resolution==1&&output.Current.Scan==1&&output.Changes==10,"Scan sequence performs the expected output changes");
        }
        var palOutput=new FakeOutput {Current=new VideoModeSettings(2,1,false)};
        var pal=new IplShell(palOutput);Face(pal,ShellFace.Video);pal.Row=1;
        pal.Confirm();Move(pal,0,1);pal.Confirm();
        Check(pal.Editing&&!pal.Video.Pending&&palOutput.Changes==0&&pal.Video.Status=="576P NOT QUALIFIED","Release policy rejects 576p before changing hardware");
        pal.Back();pal.Confirm();Move(pal,0,-1);pal.Confirm();
        Check(pal.Video.Pending&&palOutput.Current.Scan==1,"576 Auto resolves to qualified interlaced timing");
        pal.Confirm();pal.Video.SafeMode();
        Check(palOutput.Current.Resolution==1&&palOutput.Current.Scan==1,"Safe mode leaves 576i for 480i");
        var rejectedOutput=new FakeOutput();var rejectedShell=new IplShell(rejectedOutput);
        var rejected=new VideoModeScanReview(rejectedShell,rejectedOutput);
        rejected.Step();rejectedOutput.Failure=1;rejected.Step();
        Check(rejected.Failed&&!rejectedShell.Video.Pending&&rejectedOutput.Current.Scan==1&&rejectedShell.Video.Status=="UNSUPPORTED MODE OR CABLE","Unsupported progressive output preserves interlaced mode");
        Console.WriteLine("Scan modes: i/p round trip, keep, both B rollbacks, timeout, safe mode and rejected output passed.");
    }
    internal static void Run()
    {
        ScanRoundTrip();
        var output=new FakeOutput();var s=new IplShell(output);
        Check(s.Page==ShellPage.Cube&&output.Changes==0,"Boot preserves active display");
        Move(s,1,0);Check(s.Face==ShellFace.Video,"Right turns to video");
        s.Confirm();Check(!s.Editing,"A is suppressed during rotation");s.Tick(.7f);
        Move(s,1,0);Check(s.Row==0&&s.Video.Selected.Resolution==1,"Horizontal face input cannot change a value");
        s.Confirm();Check(s.Editing,"A opens a flat editor");
        Move(s,1,0);Check(s.Video.Selected.Resolution==1,"Horizontal editor input cannot change a value");
        Move(s,0,1);Check(s.Video.Selected.Resolution==2&&output.Changes==0,"Up increments the draft without changing the display");
        s.Back();Check(!s.Editing&&s.Video.Selected.Resolution==1&&output.Changes==0,"B discards draft without applying");
        s.Confirm();Move(s,0,1);s.Confirm();Check(s.Video.Pending&&output.Current.Resolution==2,"A previews output");
        s.Back();Check(!s.Video.Pending&&output.Current.Resolution==1,"B reverts a pending hardware preview");
        s.Confirm();Move(s,0,1);s.Confirm();s.Tick(13);Check(!s.Editing&&output.Current.Resolution==1,"Preview timeout returns to the cube");
        s.Confirm();Move(s,0,1);output.Failure=2;s.Confirm();
        Check(s.Editing&&!s.Video.Pending&&output.Current.Resolution==1,"Failed allocation keeps current mode and editor");
        output.Failure=0;s.Confirm();s.Confirm();Check(output.Current.Resolution==2&&!s.Editing&&!s.Video.Pending,"Second A confirms hardware mode");
        s.Back();Check(s.Page==ShellPage.Cube,"B from face returns home");
        Face(s,ShellFace.Calendar);s.Hour=23;s.Minute=59;s.Confirm();
        Move(s,0,1);Check(s.Segment(0)=="00"&&s.Segment(1)=="59","Hour wraps independently");
        Move(s,1,0);Move(s,0,1);Check(s.Segment(1)=="00"&&s.Segment(0)=="00","Minute wraps independently");
        s.Back();Check(s.Hour==23&&s.Minute==59,"B discards all time segments");
        s.Year=2024;s.Month=2;s.Day=29;s.Row=1;s.Confirm();
        Move(s,1,0);Move(s,1,0);Move(s,0,1);s.Confirm();
        Check(s.Year==2025&&s.Month==2&&s.Day==28,"Year change clamps a leap day");
        Check(output.Changes==5,"Calendar never changes the hardware display");
        Face(s,ShellFace.Options);s.Row=1;s.Confirm();Check(s.ReducedMotion,"Reduced motion can disable idle movement");
        s.Row=2;s.Confirm();Check(s.Face==ShellFace.Memory,"Memory has its own face");s.Tick(.7f);s.Back();Check(s.Face==ShellFace.Options,"Memory B returns to its parent");
        var retryOutput=new FakeOutput();var retry=new IplShell(retryOutput);
        Face(retry,ShellFace.Video);retry.Confirm();Move(retry,0,1);retry.Confirm();
        retryOutput.Failure=2;retry.Tick(13);retry.Confirm();
        Check(retry.Video.Pending&&retry.Video.Reverting&&retry.Editing,"A cannot cancel a failed timeout rollback");
        retryOutput.Failure=0;retry.Tick(1);
        Check(!retry.Video.Pending&&!retry.Video.Reverting&&!retry.Editing&&retryOutput.Current.Resolution==1,"Failed rollback retries and returns safely");
        var focus=new IplShell(new FakeOutput());Face(focus,ShellFace.Video);
        Check(focus.Focus(0)==1&&focus.Focus(1)==0,"Initial focus grows to full size");
        Move(focus,0,-1);focus.Tick(.09f);
        Check(focus.Focus(0)>0&&focus.Focus(0)<1&&focus.Focus(1)>0&&focus.Focus(1)<1,"Outgoing and incoming row animate together");
        focus.Tick(.09f);Check(focus.Focus(0)==0&&focus.Focus(1)==1,"Outgoing row returns to base size");
        focus.ReducedMotion=true;Move(focus,0,-1);focus.Tick(.001f);
        Check(focus.Focus(1)==0&&focus.Focus(2)==1,"Reduced motion changes focus without interpolation");
        Console.WriteLine("IPL shell: face routing, A/B, vertical draft selection, hardware rollback, segmented calendar and reduced motion passed.");
    }
}
