using Dolphin.Diagnostics;
using Dolphin.Timing;

internal static class IplPerformanceOverlay
{
    internal static readonly IplPerformanceStats Stats=new IplPerformanceStats();
    internal static bool DrawEnabled=true;
    private static readonly byte[] number=new byte[16];
#if IPL_CACHED_NUMBERS
    private static readonly IplNumberCacheState numberState=new IplNumberCacheState();
#endif
    private static IplRenderPhase phase;
    private static long phaseStart;
#if IPL_PERFORMANCE_REVIEW
    internal static int LabelsUs,NumbersUs,FormatUs,FlushUs,PublishUs;
#endif

    internal static void BeginFrame()
    {
#if IPL_PERFORMANCE_REVIEW
        LabelsUs=NumbersUs=FormatUs=FlushUs=PublishUs=0;
#endif
        if(Stats.Visible){Stats.BeginFrame();phase=IplRenderPhase.None;}
    }
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
#if IPL_PERFORMANCE_REVIEW
        long publishStart=Clock.Timestamp;
#endif
        Stats.Current[IplPerformanceStats.Frame]=IplFrameProfiler.Since(start);
        Stats.Current[IplPerformanceStats.Prepare]=prepare;
        Stats.Current[IplPerformanceStats.Submit]=submit;
        Stats.Current[IplPerformanceStats.Finish]=finish;
        Stats.Current[IplPerformanceStats.GpuWait]=GpuProfiler.GpuWaitMicroseconds;
        Stats.Current[IplPerformanceStats.Retrace]=GpuProfiler.RetraceWaitMicroseconds;
        Stats.CompleteFrame();
#if IPL_PERFORMANCE_REVIEW
        PublishUs=IplFrameProfiler.Since(publishStart);
#endif
    }
#if !IPL_HARDWARE_RUNNER
    internal static void PrepareLabels(GameCubeCanvas canvas)
    {
        canvas.OverlayLabel("PERFORMANCE / Z to hide",22,20,15,0xc7f8efffu);
        canvas.FlushOverlayLabels();
        canvas.OverlayLabel("Mean ~250 ms / CPU submission",22,43,11,0x9eaebfffu);
        canvas.OverlayLabel("GX wait != full GPU time. Waits include overlay.",22,432,10,0x9eaebfffu);
        canvas.FlushOverlayLabels();
        for(int group=0;group<2;group++)
        {
            uint color=group==0?IplOverlayLayout.TotalColor:IplOverlayLayout.DetailColor;
            for(int i=0;i<IplOverlayLayout.Labels.Length;i++)
                if(IplOverlayLayout.Color(IplOverlayLayout.Metrics[i])==color)
                    canvas.OverlayLabel(IplOverlayLayout.Labels[i],22,IplOverlayLayout.Y(i),13,color);
            canvas.FlushOverlayLabels();
        }
    }
    internal static void PrepareNumbers(GameCubeCanvas canvas)
    {
#if IPL_CACHED_NUMBERS
        if(!Stats.Visible||!Stats.HasSample||!DrawEnabled)return;
        int dirty=numberState.Update(Stats.Display);
        for(int group=0;group<2;group++)if((dirty&(1<<group))!=0){
#if IPL_PERFORMANCE_REVIEW
            long start=Clock.Timestamp;
#endif
            canvas.BeginNumberGroup(group);
            for(int i=0;i<IplOverlayLayout.Metrics.Length;i++){
                int metric=IplOverlayLayout.Metrics[i];
                if((IplOverlayLayout.Color(metric)==IplOverlayLayout.TotalColor?0:1)!=group)continue;
                int length=IplPerformanceStats.FormatMilliseconds(Stats.Display[metric],number);
                canvas.CacheNumber(number,length,264,IplOverlayLayout.Y(i),13);
            }
            canvas.EndNumberGroup();
#if IPL_PERFORMANCE_REVIEW
            NumbersUs+=IplFrameProfiler.Since(start);
#endif
        }
#endif
    }
    private static void Value(GameCubeCanvas canvas,int metric,float y)
    {
        uint color=IplOverlayLayout.Color(metric);
        if(Stats.HasSample)
        {
#if IPL_PERFORMANCE_REVIEW
            long start=Clock.Timestamp;
#endif
            int length=IplPerformanceStats.FormatMilliseconds(Stats.Display[metric],number);
#if IPL_PERFORMANCE_REVIEW
            FormatUs+=IplFrameProfiler.Since(start);start=Clock.Timestamp;
#endif
            canvas.TextAscii(number,length,264,y,13,color);
#if IPL_PERFORMANCE_REVIEW
            NumbersUs+=IplFrameProfiler.Since(start);
#endif
        }
        else canvas.Text("...",232,y,13,color,0);
    }
    internal static void Draw(GameCubeCanvas canvas)
    {
        canvas.Triangle(12,12,278,12,12,453,0x07111bf5u);
        canvas.Triangle(278,12,278,453,12,453,0x07111bf5u);
#if IPL_PERFORMANCE_REVIEW
        long labelsStart=Clock.Timestamp;
#endif
        canvas.DrawOverlayLabels();
#if IPL_PERFORMANCE_REVIEW
        LabelsUs=IplFrameProfiler.Since(labelsStart);
#endif
#if IPL_CACHED_NUMBERS
        if(Stats.HasSample){
#if IPL_PERFORMANCE_REVIEW
            long start=Clock.Timestamp;
#endif
            canvas.DrawOverlayNumbers();
#if IPL_PERFORMANCE_REVIEW
            NumbersUs+=IplFrameProfiler.Since(start);
#endif
            return;
        }
#endif
        for(int i=0;i<IplOverlayLayout.Metrics.Length;i++)Value(canvas,IplOverlayLayout.Metrics[i],IplOverlayLayout.Y(i));
    }
#endif
}
