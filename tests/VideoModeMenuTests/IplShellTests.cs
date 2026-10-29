using System;

internal static class IplShellTests
{
    private static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    private static void Move(IplShell s,int x,int y){s.Direction(0,0,0);s.Direction(x,y,.016f);}
    private static void Face(IplShell s,ShellFace face){s.TurnTo(face);s.Tick(.7f);}
    internal static void Run()
    {
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
        Console.WriteLine("IPL shell: face routing, A/B, vertical draft selection, hardware rollback, segmented calendar and reduced motion passed.");
    }
}
