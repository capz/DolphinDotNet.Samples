using System;

// Deterministic acceptance driver. Program invokes it only in an explicit
// IplVideoModeReview build; host tests exercise the same shell actions.
internal sealed class VideoModeScanReview
{
    private readonly IplShell shell;
    private readonly IVideoModeOutput output;
    private int frames;
    private readonly int resolution;
    internal int Phase { get; private set; }
    internal bool Failed { get; private set; }
    internal bool Complete { get; private set; }
    internal string Caption { get; private set; }="SCAN REVIEW STARTING";
    internal VideoModeScanReview(IplShell shell,IVideoModeOutput output,int resolution=1)
    {
        this.shell=shell;this.output=output;this.resolution=resolution;
        shell.TurnTo(ShellFace.Video);shell.Tick(1);shell.Confirm();shell.Row=1;
    }
    internal void Tick()
    {
        if(!Failed&&!Complete&&frames++%16==0)Step();
    }
    internal void CheckAntialiasing(bool actual,bool expected)
    {
        if(actual==expected)return;
        Failed=true;Caption="SCAN "+Phase.ToString()+" FAIL AA STATE";
    }
    internal void Step()
    {
        if(Failed||Complete)return;
        string label="";
        switch(Phase)
        {
            case 0:
                label="BASELINE I";
                CheckMode(1,false);break;
            case 1: label="PREVIEW P";Preview(2);break;
            case 2: label="KEEP P";shell.Confirm();CheckMode(2,false);break;
            case 3: label="PREVIEW I";Preview(1);break;
            case 4: label="KEEP I";shell.Confirm();CheckMode(1,false);break;
            case 5: label="PREVIEW P";Preview(2);break;
            case 6: label="BACK TO I";shell.Back();CheckMode(1,false);break;
            case 7: label="PREVIEW P";Preview(2);break;
            case 8: label="KEEP P";shell.Confirm();CheckMode(2,false);break;
            case 9: label="PREVIEW I";Preview(1);break;
            case 10: label="BACK TO P";shell.Back();CheckMode(2,false);break;
            case 11: label="PREVIEW I";Preview(1);break;
            case 12:
                label="TIMEOUT TO P";
                shell.Tick(VideoModeMenu.PreviewSeconds+.1f);
                CheckMode(2,false);break;
            case 13:
                label="PREVIEW OTHER P";PreviewResolution(resolution==1?2:1);break;
            case 14:
                label="KEEP OTHER P";shell.Confirm();CheckMode(2,false,resolution==1?2:1);break;
            case 15:
                label="PREVIEW ORIGINAL P";PreviewResolution(resolution);break;
            case 16:
                label="BACK OTHER P";shell.Back();CheckMode(2,false,resolution==1?2:1);break;
            case 17:
                label="PASS SAFE I";
                shell.Video.SafeMode();CheckMode(1,false,1);
                if(!Failed){shell.TurnTo(ShellFace.Video);shell.Tick(1);shell.Confirm();shell.Row=1;Complete=true;}
                break;
        }
        Phase++;
        string number=Phase<10?"0"+Phase.ToString():Phase.ToString();
        Caption="SCAN "+number+" "+(Failed?"FAIL ":"")+label+" ACTUAL "+ModeName(output.Read());
    }
    private static string ModeName(VideoModeSettings mode)
        =>(mode.Resolution==2?"576":mode.Resolution==0?"240":"480")+(mode.Scan==2?"P":"I");
    private void OpenList(){if(!shell.FlatMenu){shell.TurnTo(ShellFace.Video);shell.Tick(1);shell.Confirm();}}
    private void Preview(int scan)
    {
        OpenList();shell.Row=1;shell.Confirm();
        shell.Direction(0,0,0);shell.Direction(0,scan==2?1:-1,.016f);
        if(!shell.Editing||shell.Video.Selected.Scan!=scan){Failed=true;return;}
        shell.Confirm();CheckMode(scan,true);
    }
    private void PreviewResolution(int target)
    {
        OpenList();shell.Row=0;shell.Confirm();
        for(int i=0;i<2&&shell.Video.Selected.Resolution!=target;i++)
        {
            shell.Direction(0,0,0);shell.Direction(0,1,.016f);
        }
        if(!shell.Editing||shell.Video.Selected.Resolution!=target){Failed=true;return;}
        shell.Confirm();CheckMode(2,true,target);
    }
    private void CheckMode(int scan,bool pending,int expectedResolution=-1)
    {
        var actual=output.Read();
        if(actual.Resolution!=(expectedResolution<0?resolution:expectedResolution)||actual.Scan!=scan||actual.Widescreen||
            shell.Video.Applied.Scan!=scan||shell.Video.Pending!=pending||shell.Video.Reverting)
            Failed=true;
    }
}
