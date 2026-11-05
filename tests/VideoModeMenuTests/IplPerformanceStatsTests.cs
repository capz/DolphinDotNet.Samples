using System;
using System.Text;

internal static class IplPerformanceStatsTests
{
    private static void Check(bool value,string message)
    {if(!value)throw new Exception(message);}
    internal static void Run()
    {
        var stats=new IplPerformanceStats();
        Check(!stats.Visible&&!stats.HasSample,"Live performance overlay defaults off");
        stats.Toggle();
        stats.BeginFrame();stats.Current[IplPerformanceStats.Frame]=100000;
        stats.Current[(int)IplRenderPhase.Frost]=1200;stats.CompleteFrame();
        Check(stats.HasSample&&stats.Display[(int)IplRenderPhase.Frost]==1200,"Publish the first completed frame");
        stats.BeginFrame();stats.Current[IplPerformanceStats.Frame]=100000;
        Check(stats.Current[(int)IplRenderPhase.Frost]==0,"Entering the flat editor must clear unused glass/blur scopes");
        stats.CompleteFrame();
        Check(stats.Display[(int)IplRenderPhase.Frost]==1200,"Keep displayed readings stable within the averaging window");
        stats.BeginFrame();stats.Current[IplPerformanceStats.Frame]=100000;stats.CompleteFrame();
        Check(stats.Display[(int)IplRenderPhase.Frost]==400,"Average all completed frames in the window");
        stats.BeginFrame();stats.Current[IplPerformanceStats.Frame]=300000;stats.CompleteFrame();
        Check(stats.Display[(int)IplRenderPhase.Frost]==0,"A completed new window must drop stale scope times");
        stats.Toggle();stats.Current[(int)IplRenderPhase.Frost]=999;stats.CompleteFrame();
        Check(!stats.Visible&&!stats.HasSample,"A second press hides the overlay and stops collection");
        stats.Toggle();
        Check(stats.Visible&&!stats.HasSample&&stats.Current[(int)IplRenderPhase.Frost]==0,"Reopening begins a fresh timing window");
        var buffer=new byte[16];
        foreach(var pair in new[]{(0,"0.00 ms"),(999,"0.99 ms"),(123456,"123.45 ms"),(int.MaxValue,"2147483.64 ms")})
            Check(Encoding.ASCII.GetString(buffer,0,IplPerformanceStats.FormatMilliseconds(pair.Item1,buffer))==pair.Item2,"Allocation-free timing formatter: "+pair.Item1);
        Console.WriteLine("Live performance overlay: toggle, fresh scopes, averaging and bounded formatting passed.");
    }
}
