internal static class IplOverlayLayout
{
    internal const uint TotalColor=0x9ce7dcffu,DetailColor=0xd6dde7ffu;
    internal static readonly string[] Labels={
        "Frame (with overlay)","Prepare total","  Glass matrices","  Row geometry",
        "Submit (app only)","  Sealed glass + TEV","  Menu icons",
        "  Backdrop copy","  Frosted row blur","  Row tint","  Menu text","  Footer","  Flat editor",
        "Finish / present","  GX completion wait","  VSync wait","Overlay CPU"};
    internal static readonly int[] Metrics={
        IplPerformanceStats.Frame,IplPerformanceStats.Prepare,(int)IplRenderPhase.GlassGeometry,
        (int)IplRenderPhase.FrostGeometry,IplPerformanceStats.Submit,
        (int)IplRenderPhase.Transmission,
        (int)IplRenderPhase.Icons,(int)IplRenderPhase.Capture,(int)IplRenderPhase.Frost,
        (int)IplRenderPhase.PanelTint,(int)IplRenderPhase.Labels,(int)IplRenderPhase.Footer,
        (int)IplRenderPhase.Editor,IplPerformanceStats.Finish,IplPerformanceStats.GpuWait,
        IplPerformanceStats.Retrace,IplPerformanceStats.Overlay};
    internal static float Y(int row)=>65+row*18;
    internal static uint Color(int metric)=>metric==IplPerformanceStats.Frame||metric==IplPerformanceStats.Prepare||
        metric==IplPerformanceStats.Submit||metric==IplPerformanceStats.Finish||metric==IplPerformanceStats.Overlay?TotalColor:DetailColor;
}
