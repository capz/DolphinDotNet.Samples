using Dolphin.Diagnostics;
using Dolphin.Timing;

internal static class IplPerformanceOverlay
{
    internal static readonly IplPerformanceStats Stats=new IplPerformanceStats();
    private static readonly byte[] number=new byte[16];
    private static IplRenderPhase phase;
    private static long phaseStart;

    internal static void BeginFrame()
    {if(Stats.Visible){Stats.BeginFrame();phase=IplRenderPhase.None;}}
    internal static void Section(IplRenderPhase next)
    {
        if(!Stats.Visible)return;
        long now=Clock.Timestamp;
        if(phase!=IplRenderPhase.None)Stats.Current[(int)phase]+=(int)(now-phaseStart);
        phase=next;phaseStart=now;
    }
    internal static void CompleteFrame(long start,int prepare,int submit,int finish)
    {
        if(!Stats.Visible)return;
        Stats.Current[IplPerformanceStats.Frame]=IplFrameProfiler.Since(start);
        Stats.Current[IplPerformanceStats.Prepare]=prepare;
        Stats.Current[IplPerformanceStats.Submit]=submit;
        Stats.Current[IplPerformanceStats.Finish]=finish;
        Stats.Current[IplPerformanceStats.GpuWait]=GpuProfiler.GpuWaitMicroseconds;
        Stats.Current[IplPerformanceStats.Retrace]=GpuProfiler.RetraceWaitMicroseconds;
        Stats.CompleteFrame();
    }
    private static void Row(GameCubeCanvas canvas,string label,int metric,float y,bool total=false)
    {
        uint color=total?0x9ce7dcffu:0xd6dde7ffu;
        canvas.Text(label,22,y,13,color,0);
        if(Stats.HasSample)
            canvas.TextAscii(number,IplPerformanceStats.FormatMilliseconds(Stats.Display[metric],number),264,y,13,color);
        else canvas.Text("...",232,y,13,color,0);
    }
    internal static void Draw(GameCubeCanvas canvas)
    {
        canvas.Triangle(12,12,278,12,12,435,0x07111bf5u);
        canvas.Triangle(278,12,278,435,12,435,0x07111bf5u);
        canvas.Text("PERFORMANCE / Z to hide",22,20,15,0xc7f8efffu,0);
        canvas.Text("Mean ~250 ms / CPU submission",22,43,11,0x9eaebfffu,0);
        Row(canvas,"Frame (with overlay)",IplPerformanceStats.Frame,65,true);
        Row(canvas,"Prepare total",IplPerformanceStats.Prepare,83,true);
        Row(canvas,"  Glass geometry",(int)IplRenderPhase.GlassGeometry,101);
        Row(canvas,"  Row geometry",(int)IplRenderPhase.FrostGeometry,119);
        Row(canvas,"Submit (app only)",IplPerformanceStats.Submit,137,true);
        Row(canvas,"  Glass transmission",(int)IplRenderPhase.Transmission,155);
        Row(canvas,"  Optical rims",(int)IplRenderPhase.Rims,173);
        Row(canvas,"  Gloss reflections",(int)IplRenderPhase.Reflections,191);
        Row(canvas,"  Menu icons",(int)IplRenderPhase.Icons,209);
        Row(canvas,"  Backdrop copy",(int)IplRenderPhase.Capture,227);
        Row(canvas,"  Frosted row blur",(int)IplRenderPhase.Frost,245);
        Row(canvas,"  Row tint",(int)IplRenderPhase.PanelTint,263);
        Row(canvas,"  Menu text",(int)IplRenderPhase.Labels,281);
        Row(canvas,"  Footer",(int)IplRenderPhase.Footer,299);
        Row(canvas,"  Flat editor",(int)IplRenderPhase.Editor,317);
        Row(canvas,"Finish / present",IplPerformanceStats.Finish,335,true);
        Row(canvas,"  GX completion wait",IplPerformanceStats.GpuWait,353);
        Row(canvas,"  VSync wait",IplPerformanceStats.Retrace,371);
        Row(canvas,"Overlay CPU",IplPerformanceStats.Overlay,389,true);
        canvas.Text("GX wait != full GPU time. Waits include overlay.",22,414,10,0x9eaebfffu,0);
    }
}
