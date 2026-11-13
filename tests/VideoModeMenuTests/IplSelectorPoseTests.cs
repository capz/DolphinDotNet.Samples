using System;

internal static class IplSelectorPoseTests
{
    private static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    private static bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
    private static void Move(IplShell s,int x,int y){s.Direction(0,0,0);s.Direction(x,y,.016f);}
    private static void Flat(IplSelectorPose pose)
    {
        var point=pose.Project(17,-13,320,201);
        Check(!pose.Moving&&Near(point.X,337)&&Near(point.Y,188),"Inactive selector must keep its exact flat layout");
    }
    internal static void Run()
    {
        var output=new FakeOutput();var s=new IplShell(output);
        s.TurnTo(ShellFace.Calendar);s.Tick(1);s.Confirm();Flat(IplSelectorPose.For(s,0,66));
        s.Confirm();Flat(IplSelectorPose.For(s,0,66));s.Tick(.18f);
        var first=IplSelectorPose.For(s,0,66);Check(first.Moving,"Hour rocks while its editor is open");Flat(IplSelectorPose.For(s,1,55));
        var a=first.Project(33,-21,255,201);s.Tick(.1f);var b=IplSelectorPose.For(s,0,66).Project(33,-21,255,201);
        Check(Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)>.05f,"The active selector keeps moving without held input");
        Move(s,1,0);Flat(IplSelectorPose.For(s,0,55));Flat(IplSelectorPose.For(s,1,66));s.Tick(.18f);
        Check(IplSelectorPose.For(s,1,66).Moving,"Motion follows the independently selected minute after easing in");
        Move(s,0,1);Check(s.Segment(1)=="01"&&s.Segment(0)=="12","Animation preserves independent minute editing");
        s.ReducedMotion=true;Flat(IplSelectorPose.For(s,1,66));s.ReducedMotion=false;
        s.Confirm();Check(!s.Editing&&s.Minute==1,"A commits and exits the animated editor");Flat(IplSelectorPose.For(s,1,66));
        s.Confirm();s.Tick(.18f);Move(s,0,1);s.Back();Check(s.Hour==12&&s.Minute==1,"B stops motion and discards the draft");Flat(IplSelectorPose.For(s,0,66));
        s.TurnTo(ShellFace.Calendar);s.Tick(1);s.Confirm();s.Row=1;s.Confirm();Move(s,1,0);Move(s,1,0);s.Tick(.18f);
        Check(IplSelectorPose.For(s,2,138).Moving,"Year selector can rock independently");Flat(IplSelectorPose.For(s,0,55));Flat(IplSelectorPose.For(s,1,55));
        s.Back();s.TurnTo(ShellFace.Video);s.Tick(1);s.Confirm();Move(s,1,0);s.Tick(.18f);
        for(int step=0;step<600;step++){
            s.Time=step*.1f;var pose=IplSelectorPose.For(s,1,510);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2){
                var point=pose.Project(x*255,y*27.5f,320,209.5f);
                Check(point.X>35&&point.X<605&&point.Y>154&&point.Y<262,"Wide selectors must clear arrows and viewport margins throughout the wobble");
            }
        }
        Move(s,0,1);s.Confirm();Check(s.Video.Pending,"A still previews the progressive scan mode");Flat(IplSelectorPose.For(s,1,510));s.Back();
        Check(!s.Editing&&output.Current.Scan==1,"B rolls back the scan preview and stops selector motion");
        Console.WriteLine("Selector motion: active part, ease-in, idle continuation, A/B, reduced motion, preview suppression and wide-value bounds passed.");
    }
}
