using System;

internal static class IplShellTests
{
    private static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    private static void Move(IplShell s,int x,int y,bool stick=false){s.Direction(0,0,0);s.Direction(x,y,.016f,stick);}
    private static void Side(IplShell s,ShellFace face){s.TurnTo(face);s.Tick(.7f);}
    private static void Flat(IplShell s,ShellFace face){Side(s,face);s.Confirm();Check(s.FlatMenu,"A opens the complete flat options list");}
    private static void ScanRoundTrip()
    {
        for(int resolution=1;resolution<=2;resolution++){
            var output=new FakeOutput{Current=new VideoModeSettings(resolution,1,false)};var shell=new IplShell(output);var review=new VideoModeScanReview(shell,output,resolution);
            while(!review.Complete&&!review.Failed)review.Step();
            Check(review.Complete&&!review.Failed&&review.Phase==18,"480/576 i/p previews, keeps, both B rollbacks, timeout, resolution changes and safe mode");
            Check(output.Current.Resolution==1&&output.Current.Scan==1&&output.Changes==13,"Scan sequence preserves the expected output changes");
        }
        var automaticOutput=new FakeOutput{Current=new VideoModeSettings(2,2,false)};var automatic=new VideoModeMenu(automaticOutput);automatic.SelectRow(1);automatic.Edit(1);automatic.Apply();
        Check(automatic.Pending&&automaticOutput.LastRequested.Scan==0&&automatic.Applied.Scan==automaticOutput.Read().Scan,"Auto reaches SDK unchanged and resolves to actual output");automatic.Revert();Check(automaticOutput.Current.Scan==2,"Auto rollback restores progressive");
        var rejectedOutput=new FakeOutput();var rejectedShell=new IplShell(rejectedOutput);var rejected=new VideoModeScanReview(rejectedShell,rejectedOutput);rejected.Step();rejectedOutput.Failure=1;rejected.Step();
        Check(rejected.Failed&&!rejectedShell.Video.Pending&&rejectedOutput.Current.Scan==1,"Unsupported output preserves the confirmed mode");
    }
    internal static void Run()
    {
        ScanRoundTrip();
        foreach(var face in new[]{ShellFace.Video,ShellFace.Options,ShellFace.Play,ShellFace.Calendar}){
            int x=face==ShellFace.Video?1:face==ShellFace.Options?-1:0,y=face==ShellFace.Play?1:face==ShellFace.Calendar?-1:0;
            var nav=new IplShell(new FakeOutput());Move(nav,x,y,true);nav.Tick(.7f);Move(nav,-x,-y,true);Check(nav.Page==ShellPage.Cube,"Reverse stick still returns a preview to cube");
            nav.Tick(.7f);for(int i=0;i<60;i++)nav.Direction(-x,-y,.016f,true);Check(nav.Page==ShellPage.Cube,"Holding reverse cannot re-enter another side");Move(nav,-x,-y,true);Check(nav.Page==ShellPage.Face,"Neutral rearms preview navigation");
        }
        foreach(var face in new[]{ShellFace.Video,ShellFace.Calendar,ShellFace.Options,ShellFace.Memory}){
            var preview=new IplShell(new FakeOutput());Side(preview,face);Move(preview,0,-1);Check(preview.Row==0&&preview.Page==ShellPage.Face,"Side previews do not select or edit options");
            preview.Confirm();Check(preview.FlatMenu&&!preview.Editing,"First A opens a flat list, not a field");Move(preview,0,-1);Check(preview.Row==1,"The flat list selects rows");preview.Back();Check(preview.Page==ShellPage.Face&&preview.Face==face,"B closes every flat list to the same cube side");
        }
        var output=new FakeOutput();var s=new IplShell(output);Check(output.Changes==0,"Boot preserves display");Move(s,1,0);s.Confirm();Check(!s.FlatMenu,"A during rotation is suppressed");s.Tick(.7f);s.Confirm();Check(s.FlatMenu,"Video side opens flat video list");
        s.Confirm();Check(s.Editing,"A on a flat row opens its editor");Move(s,1,0);Check(s.Video.Selected.Resolution==1,"Horizontal field input does not edit values");Move(s,0,1);Check(s.Video.Selected.Resolution==2&&output.Changes==0,"Only the draft changes before preview");s.Back();Check(s.Page==ShellPage.Face&&s.Face==ShellFace.Video&&s.Video.Selected.Resolution==1&&output.Changes==0,"B discards field draft and returns directly to cube");
        Flat(s,ShellFace.Video);s.Confirm();Move(s,0,1);s.Confirm();Check(s.Video.Pending,"A starts video preview");s.Back();Check(s.Page==ShellPage.Face&&s.Face==ShellFace.Video&&!s.Video.Pending&&output.Current.Resolution==1,"B reverts preview before returning to cube");
        Flat(s,ShellFace.Video);s.Confirm();Move(s,0,1);s.Confirm();s.Tick(13);Check(s.FlatMenu&&!s.Video.Pending&&output.Current.Resolution==1,"Timeout restores output and returns to flat list");
        s.Confirm();Move(s,0,1);output.Failure=2;s.Confirm();Check(s.Editing&&!s.Video.Pending,"Allocation failure keeps editor and current output");output.Failure=0;s.Confirm();s.Confirm();Check(s.FlatMenu&&output.Current.Resolution==2&&!s.Video.Pending,"Keeping video returns to its flat list");
        Flat(s,ShellFace.Calendar);s.Hour=23;s.Minute=59;s.Confirm();Move(s,0,1);Check(s.Segment(0)=="00"&&s.Segment(1)=="59","Hour wraps independently");Move(s,1,0);Move(s,0,1);Check(s.Segment(1)=="00","Minute wraps independently");s.Back();Check(s.Page==ShellPage.Face&&s.Face==ShellFace.Calendar&&s.Hour==23&&s.Minute==59,"B discards all time parts and leaves to cube");
        Flat(s,ShellFace.Calendar);s.Year=2024;s.Month=2;s.Day=29;s.Row=1;s.Confirm();Move(s,1,0);Move(s,1,0);Move(s,0,1);s.Confirm();Check(s.FlatMenu&&s.Year==2025&&s.Day==28,"Date commit clamps leap day and returns to flat list");
        Side(s,ShellFace.Options);s.Confirm();Check(!s.ReducedMotion&&s.FlatMenu,"Opening Options does not toggle Motion");s.Row=1;s.Confirm();Check(s.ReducedMotion&&s.FlatMenu,"Motion changes only on the flat page");s.Row=2;s.Confirm();Check(s.Face==ShellFace.Memory&&s.FlatMenu,"Storage link opens flat Storage options");s.Back();Check(s.Page==ShellPage.Face&&s.Face==ShellFace.Memory,"Nested flat Storage returns straight to cube");
        var retryOutput=new FakeOutput();var retry=new IplShell(retryOutput);Flat(retry,ShellFace.Video);retry.Confirm();Move(retry,0,1);retry.Confirm();retryOutput.Failure=2;retry.Back();Check(retry.Video.Pending&&retry.Video.Reverting&&retry.Editing,"Failed rollback keeps the preview protected");retry.Confirm();Check(retry.Video.Pending,"A cannot cancel a failed rollback");retryOutput.Failure=0;retry.Tick(1);Check(retry.Page==ShellPage.Face&&retry.Face==ShellFace.Video&&!retry.Video.Pending&&retryOutput.Current.Resolution==1,"Deferred B return finishes only after safe rollback succeeds");
        var focus=new IplShell(new FakeOutput());Side(focus,ShellFace.Video);Check(focus.Focus(0)==0,"Read-only previews have no active row animation");focus.Confirm();focus.Tick(.18f);Check(focus.Focus(0)==1,"Flat list focus eases in");Move(focus,0,-1);focus.Tick(.09f);Check(focus.Focus(0)>0&&focus.Focus(1)>0,"Flat row focus crossfades");focus.Tick(.09f);Check(focus.Focus(0)==0&&focus.Focus(1)==1,"Flat focus finishes its transition");
        Console.WriteLine("IPL shell: read-only sides, flat lists/editors, B-to-cube, guarded rollback, calendar parts, Motion and focus passed.");
    }
}
