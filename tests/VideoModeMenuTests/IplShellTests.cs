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
    private static void VideoDetails()
    {
        var output=new FakeOutput();var s=new IplShell(output);
        Move(s,1,0);s.Confirm();Check(!s.Editing&&output.Changes==0,"A during a cube turn cannot open or apply Video");
        s.Tick(.7f);s.Confirm();
        Check(s.Editing&&!s.FlatMenu&&s.Row==0&&s.Part==0,"A opens all three Video fields directly");
        Move(s,1,0);Check(s.Row==1&&s.Part==1&&s.Video.Selected.Resolution==1,"Horizontal input selects Scan without editing Output");
        Move(s,0,1);Check(s.Video.Selected.Scan==2&&output.Changes==0,"Scan remains a draft before A");
        Move(s,1,0);Move(s,0,1);Check(s.Row==2&&s.Video.Selected.Widescreen&&s.Video.Selected.Scan==2,"Aspect edits retain the Scan draft");
        Move(s,1,0);Move(s,0,1);Check(s.Row==0&&s.Video.Selected.Resolution==2,"Selection wraps between independently editable fields");
        s.Confirm();Check(s.Video.Pending&&output.Current.Resolution==2&&output.Current.Scan==2&&output.Current.Widescreen,"A previews the complete drafted combination");
        Move(s,1,1);Check(s.Row==0&&s.Video.Selected.Scan==2,"Preview locks every field");s.Confirm();
        Check(s.Editing&&!s.Video.Pending&&!s.FlatMenu&&s.Value(0)=="576"&&s.Value(1)=="Progressive"&&s.Value(2)=="16:9","Keep returns to the complete grouped editor with applied values");
        s.Back();Check(s.Page==ShellPage.Face&&output.Current.Resolution==2,"B exits confirmed Video settings to its cube side");

        output=new FakeOutput();s=new IplShell(output);Side(s,ShellFace.Video);s.Confirm();Move(s,0,-1);s.Confirm();
        Check(s.Editing&&!s.Video.Pending&&s.Video.Status=="UNSUPPORTED MODE OR CABLE"&&output.Current.Resolution==1,"An unsupported combined mode stays editable without changing output");
        s.Back();Check(s.Page==ShellPage.Face&&s.Video.Selected.Resolution==1,"B cancels all unconfirmed fields");
        s.Confirm();Move(s,0,1);s.Confirm();s.Tick(13);
        Check(s.Editing&&!s.Video.Pending&&output.Current.Resolution==1&&s.Video.Selected.Resolution==1,"Timeout restores output and synchronizes every editor field");
        Move(s,0,1);output.Failure=2;s.Confirm();Check(s.Editing&&!s.Video.Pending,"Allocation failure keeps the grouped editor and current output");
        output.Failure=0;s.Confirm();output.Failure=2;s.Back();
        Check(s.Video.Pending&&s.Video.Reverting&&s.Editing,"Failed rollback keeps its guarded detail page");s.Confirm();Check(s.Video.Pending,"A cannot keep an unsuccessful rollback");
        output.Failure=0;s.Tick(1);Check(s.Page==ShellPage.Face&&!s.Video.Pending&&output.Current.Resolution==1,"Deferred B exits only after rollback succeeds");
    }
    internal static void Run()
    {
        ScanRoundTrip();
        foreach(var face in new[]{ShellFace.Video,ShellFace.Options,ShellFace.Memory,ShellFace.Calendar}){
            int x=face==ShellFace.Video?1:face==ShellFace.Options?-1:0,y=face==ShellFace.Memory?1:face==ShellFace.Calendar?-1:0;
            var nav=new IplShell(new FakeOutput());Move(nav,x,y,true);Check(nav.Face==face,"Each cube direction opens its advertised side");nav.Tick(.7f);Move(nav,-x,-y,true);Check(nav.Page==ShellPage.Cube,"Reverse stick still returns a preview to cube");
            nav.Tick(.7f);for(int i=0;i<60;i++)nav.Direction(-x,-y,.016f,true);Check(nav.Page==ShellPage.Cube,"Holding reverse cannot re-enter another side");Move(nav,-x,-y,true);Check(nav.Page==ShellPage.Face,"Neutral rearms preview navigation");
        }
        foreach(var face in new[]{ShellFace.Calendar,ShellFace.Options,ShellFace.Memory}){
            var preview=new IplShell(new FakeOutput());Side(preview,face);Move(preview,0,-1);Check(preview.Row==0&&preview.Page==ShellPage.Face,"Side previews do not select or edit options");
            preview.Confirm();Check(preview.FlatMenu&&!preview.Editing,"First A opens a flat list, not a field");Move(preview,0,-1);Check(preview.Row==1,"The flat list selects rows");preview.Back();Check(preview.Page==ShellPage.Face&&preview.Face==face,"B closes every flat list to the same cube side");
        }
        VideoDetails();
        var output=new FakeOutput();var s=new IplShell(output);
        Flat(s,ShellFace.Calendar);s.Hour=23;s.Minute=59;s.Confirm();Move(s,0,1);Check(s.Segment(0)=="00"&&s.Segment(1)=="59","Hour wraps independently");Move(s,1,0);Move(s,0,1);Check(s.Segment(1)=="00","Minute wraps independently");s.Back();Check(s.Page==ShellPage.Face&&s.Face==ShellFace.Calendar&&s.Hour==23&&s.Minute==59,"B discards all time parts and leaves to cube");
        Flat(s,ShellFace.Calendar);s.Year=2024;s.Month=2;s.Day=29;s.Row=1;s.Confirm();Move(s,1,0);Move(s,1,0);Move(s,0,1);s.Confirm();Check(s.FlatMenu&&s.Year==2025&&s.Day==28,"Date commit clamps leap day and returns to flat list");
        Side(s,ShellFace.Options);s.Confirm();Check(s.FlatMenu&&s.Rows==3&&s.Label(1)=="Motion"&&s.Label(2)=="Glass Blur","Options contains settings only after Storage moves to the top");s.Row=1;s.Confirm();Check(s.Editing&&!s.ReducedMotion,"Motion opens the common value editor without changing its default");s.Back();
        s.TurnTo(ShellFace.Menu);s.Tick(.7f);Move(s,0,1,true);s.Tick(.7f);Check(s.Face==ShellFace.Memory&&s.Title=="Storage"&&s.Rows==2,"Up opens the dedicated Storage side");s.Confirm();Check(s.FlatMenu&&s.Label(0)=="Memory Card"&&s.Label(1)=="SD Storage","Storage opens its two flat storage actions directly");s.Back();Check(s.Page==ShellPage.Face&&s.Face==ShellFace.Memory,"B returns directly from Storage options to the top cube side");Move(s,0,-1,true);Check(s.Page==ShellPage.Cube,"Down reverses the stick entry to the top Storage side");
        var focus=new IplShell(new FakeOutput());Side(focus,ShellFace.Options);Check(focus.Focus(0)==0,"Read-only previews have no active row animation");focus.Confirm();focus.Tick(.18f);Check(focus.Focus(0)==1,"Flat list focus eases in");Move(focus,0,-1);focus.Tick(.09f);Check(focus.Focus(0)>0&&focus.Focus(1)>0,"Flat row focus crossfades");focus.Tick(.09f);Check(focus.Focus(0)==0&&focus.Focus(1)==1,"Flat focus finishes its transition");
        Console.WriteLine("IPL shell: read-only sides, flat lists/editors, B-to-cube, guarded rollback, calendar parts, Motion and focus passed.");
    }
}
