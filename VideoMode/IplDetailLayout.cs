using System;

// Calendar, Video and single-value editors share one readable block scale and
// the same arrow/label baseline. Selection changes the active part, not the grid.
internal static class IplDetailLayout
{
    internal const float ValueY=180,LabelY=285,ArrowTop=151,ArrowBottom=263;
    internal static int Count(IplShell shell)=>shell.Face==ShellFace.Video?3:shell.Face==ShellFace.Calendar?(shell.Row==0?2:3):1;
    internal static int Active(IplShell shell)=>shell.Face==ShellFace.Video?shell.Row:shell.Face==ShellFace.Calendar?shell.Part:0;
    internal static float CenterX(IplShell shell,int part)
    {
        if(shell.Face==ShellFace.Video)return 136+184*part;
        if(shell.Face!=ShellFace.Calendar)return 320;
        return shell.Row==0?255+112*part:part==0?186:part==1?298:431;
    }
    internal static float Cell(IplShell shell,int part)=>part==Active(shell)?6:5;
    internal static string Value(IplShell shell,int part)
    {
        if(shell.Face==ShellFace.Calendar)return shell.Segment(part);
        if(shell.Face!=ShellFace.Video)return shell.Value(shell.Row).ToUpperInvariant();
        if(part==1)return shell.Video.Selected.Scan==0?"AUTO":shell.Video.Selected.Scan==1?"I":"P";
        return shell.Value(part);
    }
    internal static string Label(IplShell shell,int part)
        =>shell.Face==ShellFace.Video?(part==2?"Aspect":shell.Label(part)):
          shell.Face==ShellFace.Calendar?(shell.Row==0?(part==0?"Hour":"Minute"):(part==0?"Day":part==1?"Month":"Year")):shell.Label(shell.Row);
}
