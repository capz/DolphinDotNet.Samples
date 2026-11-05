using System;

internal static class IplOverlayLayoutTests
{
    internal static void Run()
    {
        if(IplOverlayLayout.Labels.Length!=IplOverlayLayout.Metrics.Length)throw new Exception("Overlay labels and values must align");
        var seen=new bool[IplPerformanceStats.Count];
        for(int i=0;i<IplOverlayLayout.Metrics.Length;i++)
        {
            int metric=IplOverlayLayout.Metrics[i];
            if(metric<=0||metric>=seen.Length||seen[metric])throw new Exception("Overlay metrics must occur exactly once");
            seen[metric]=true;
            if(IplOverlayLayout.Y(i)<65||IplOverlayLayout.Y(i)+19>432)throw new Exception("Overlay rows exceed the panel");
        }
        for(int i=1;i<seen.Length;i++)if(!seen[i])throw new Exception("A measured phase has no displayed row");
        if(IplOverlayLayout.Color(IplPerformanceStats.GpuWait)!=IplOverlayLayout.DetailColor||
           IplOverlayLayout.Color(IplPerformanceStats.Retrace)!=IplOverlayLayout.DetailColor)
            throw new Exception("Wait rows must retain the subordinate detail color");
        int meshes=3; // Header, muted text, total-color group.
        int details=0;
        for(int i=0;i<IplOverlayLayout.Labels.Length;i++)
            if(IplOverlayLayout.Color(IplOverlayLayout.Metrics[i])==IplOverlayLayout.DetailColor)details+=IplOverlayLayout.Labels[i].Length;
        meshes+=(details+155)/156;
        if(meshes>8)throw new Exception("Static labels exceed bounded native mesh slots");
        Console.WriteLine("Overlay layout: complete unique metrics, aligned rows, wait colors and bounded mesh budget passed.");
    }
}
