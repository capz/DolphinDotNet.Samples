using System;

// CPU scopes are exclusive; totals and presentation waits are listed separately.
internal enum IplRenderPhase
{
    None, GlassGeometry, FrostGeometry, RimGeometry, Transmission, Rims, Reflections, Icons,
    Capture, Frost, PanelTint, Labels, Footer, Editor, Count
}

internal sealed class IplPerformanceStats
{
    internal const int Frame=(int)IplRenderPhase.Count, Prepare=Frame+1, Submit=Frame+2,
        Finish=Frame+3, GpuWait=Frame+4, Retrace=Frame+5, Overlay=Frame+6, Count=Frame+7;
    internal readonly int[] Current=new int[Count], Display=new int[Count];
    private readonly long[] sums=new long[Count];
    private int frames;
    private long elapsed;
    internal bool Visible {get;private set;}
    internal bool HasSample {get;private set;}

    internal void Toggle()
    {
        Visible=!Visible;HasSample=false;frames=0;elapsed=0;
        Array.Clear(Current,0,Count);Array.Clear(Display,0,Count);Array.Clear(sums,0,Count);
    }
    internal void BeginFrame()=>Array.Clear(Current,0,Count);
    internal void CompleteFrame()
    {
        if(!Visible)return;
        frames++;elapsed+=Current[Frame];
        for(int i=0;i<Count;i++)sums[i]+=Current[i];
        // Show the first completed frame immediately, then refresh about 4 Hz.
        if(!HasSample||elapsed>=250000)
        {
            for(int i=0;i<Count;i++)Display[i]=(int)(sums[i]/frames);
            HasSample=true;
        }
        if(elapsed>=250000)
        {Array.Clear(sums,0,Count);frames=0;elapsed=0;}
    }

    // Reuse a tiny ASCII buffer: no per-frame strings on the 256 KiB managed heap.
    internal static int FormatMilliseconds(int microseconds,byte[] buffer)
    {
        int value=Math.Max(0,microseconds)/10,whole=value/100,at=0;
        do {buffer[at++]=(byte)('0'+whole%10);whole/=10;}while(whole>0);
        for(int i=0;i<at/2;i++){byte ch=buffer[i];buffer[i]=buffer[at-i-1];buffer[at-i-1]=ch;}
        buffer[at++]=(byte)'.';buffer[at++]=(byte)('0'+value/10%10);buffer[at++]=(byte)('0'+value%10);
        buffer[at++]=(byte)' ';buffer[at++]=(byte)'m';buffer[at++]=(byte)'s';return at;
    }
}
