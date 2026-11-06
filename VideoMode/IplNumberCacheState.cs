using System;
// Cache the values actually printed (hundredths of a millisecond), not raw
// microseconds. Sub-digit timing changes must not rebuild native geometry.
internal sealed class IplNumberCacheState
{
    private readonly int[] printed=new int[IplOverlayLayout.Metrics.Length];
    private bool ready;
    internal int Update(int[] values)
    {
        int dirty=ready?0:3;
        for(int i=0;i<printed.Length;i++){
            int metric=IplOverlayLayout.Metrics[i],value=Math.Max(0,values[metric])/10;
            if(!ready||printed[i]!=value){printed[i]=value;dirty|=IplOverlayLayout.Color(metric)==IplOverlayLayout.TotalColor?1:2;}
        }
        ready=true;return dirty;
    }
}
