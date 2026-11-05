using System;

internal static class IplLighting
{
    // Linear interpolation of exp(-x), 0 <= x < 16. The interpolation error is
    // bounded by h*h/8 = 1/8192; the discarded tail is below 0.000000113.
    // 2052 bytes replace thousands of libm exponentials every moving frame.
    private static readonly float[] decay=CreateDecay();
    private static float[] CreateDecay()
    {
        var values=new float[513];
        for(int i=0;i<values.Length;i++)values[i]=(float)Math.Exp(-i/32.0);
        return values;
    }
    internal static float ExpNegative(float value)
    {
        if(value>=16)return 0;
        if(value<=0)return 1;
        float at=value*32;int i=(int)at;
        return decay[i]+(decay[i+1]-decay[i])*(at-i);
    }
}
