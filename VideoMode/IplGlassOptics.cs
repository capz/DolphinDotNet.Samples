using System;

// Parallel-wall approximation, evaluated only while creating native lookups.
internal static class IplGlassOptics
{
    internal const float RefractiveIndex=1.50f;
    internal static float Reflectance(float cosine,float index)
    {
        if(index<1)throw new ArgumentOutOfRangeException("index");
        if(index==1)return 0;
        float c=Math.Min(1,Math.Abs(cosine)),eta=1/index;
        float transmitted=(float)Math.Sqrt(Math.Max(0,1-eta*eta*(1-c*c)));
        float s=(c-index*transmitted)/(c+index*transmitted);
        float p=(index*c-transmitted)/(index*c+transmitted);
        return (s*s+p*p)*.5f;
    }
    internal static void WallOffset(float nx,float ny,float thickness,float index,out float x,out float y)
    {
        if(index<1||thickness<0)throw new ArgumentOutOfRangeException("index/thickness");
        if(index==1||thickness==0){x=y=0;return;}
        float squared=nx*nx+ny*ny;
        if(squared>1){float length=(float)Math.Sqrt(squared);nx/=length;ny/=length;squared=1;}
        float cosine=(float)Math.Sqrt(Math.Max(0,1-squared)),eta=1/index;
        float transmitted=(float)Math.Sqrt(1-eta*eta*(1-cosine*cosine));
        float lateral=thickness*(eta*cosine/transmitted-1);
        x=lateral*nx;y=lateral*ny;
    }
}
