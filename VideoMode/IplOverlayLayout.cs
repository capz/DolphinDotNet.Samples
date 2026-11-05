internal static class IplOverlayLayout
{
    internal const uint TotalColor=0x9ce7dcffu,DetailColor=0xd6dde7ffu;
    internal static readonly string[] Labels={
        "Frame (with overlay)","Prepare total","  Glass geometry","  Row geometry","  Rim geometry",
        "Submit (app only)","  Glass transmission","  Optical rims","  Gloss reflections","  Menu icons",
        "  Backdrop copy","  Frosted row blur","  Row tint","  Menu text","  Footer","  Flat editor",
        "Finish / present","  GX completion wait","  VSync wait","Overlay CPU"};
    internal static readonly int[] Metrics={
        IplPerformanceStats.Frame,IplPerformanceStats.Prepare,(int)IplRenderPhase.GlassGeometry,
        (int)IplRenderPhase.FrostGeometry,(int)IplRenderPhase.RimGeometry,IplPerformanceStats.Submit,
        (int)IplRenderPhase.Transmission,(int)IplRenderPhase.Rims,(int)IplRenderPhase.Reflections,
        (int)IplRenderPhase.Icons,(int)IplRenderPhase.Capture,(int)IplRenderPhase.Frost,
        (int)IplRenderPhase.PanelTint,(int)IplRenderPhase.Labels,(int)IplRenderPhase.Footer,
        (int)IplRenderPhase.Editor,IplPerformanceStats.Finish,IplPerformanceStats.GpuWait,
        IplPerformanceStats.Retrace,IplPerformanceStats.Overlay};
    internal static float Y(int row)=>65+row*18;
    internal static uint Color(int metric)=>metric==IplPerformanceStats.Frame||metric==IplPerformanceStats.Prepare||
        metric==IplPerformanceStats.Submit||metric==IplPerformanceStats.Finish||metric==IplPerformanceStats.Overlay?TotalColor:DetailColor;
}
