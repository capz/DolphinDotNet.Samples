using System;

internal static class IplGlassBlur
{
    internal static float Spacing(int level)
    {
        if(level==0)return 0;
        if(level==1)return 1;
        if(level==2)return 2;
        if(level==3)return 3;
        throw new ArgumentOutOfRangeException(nameof(level));
    }
    // A narrow optical return on the existing cavity silhouette. Perspective
    // moves its visible contour slightly away from N.Z=0; keep frontal walls
    // dark and reserve the return for normals approaching that contour.
    internal static float BoundaryHighlight(float cosine)
    {
        float t=Math.Max(0,Math.Min(1,(.34f-Math.Abs(cosine))/.18f));
        return .55f*t*t*(3-2*t);
    }
    // A dark return immediately inward from the bright cavity contour. Both
    // ramps meet with zero slope, so the absorption does not form a hard line
    // or darken either the frontal glass or the grazing highlight itself.
    internal static float BoundaryDarkening(float cosine)
    {
        float c=Math.Abs(cosine);
        float rise=Math.Max(0,Math.Min(1,(c-.24f)/.12f));
        float fall=Math.Max(0,Math.Min(1,(.50f-c)/.14f));
        return .32f*rise*rise*(3-2*rise)*fall*fall*(3-2*fall);
    }
    // A dense nine-sample Gaussian expressed as five bilinear texture fetches.
    // Each adjacent pair retains its weighted position; quantized symmetric
    // weights total 255 exactly. Called only when strength or pixel size changes.
    // Return the actual discrete impulse's standard deviation after quantization.
    internal static float BuildKernel(float sigma,float[] offsets,byte[] weights,int at)
    {
        if(!(sigma>=0&&sigma<=float.MaxValue))throw new ArgumentOutOfRangeException(nameof(sigma));
        double w1=0,w2=0,w3=0,w4=0;
        if(sigma>0)
        {
            double inverse=-.5/((double)sigma*sigma);
            w1=Math.Exp(inverse);w2=Math.Exp(inverse*4);
            w3=Math.Exp(inverse*9);w4=Math.Exp(inverse*16);
        }
        double inner=w1+w2,outer=w3+w4,total=1+2*(inner+outer);
        float innerOffset=inner>0?(float)((w1+2*w2)/inner):1;
        float outerOffset=outer>0?(float)((3*w3+4*w4)/outer):3;
        int innerWeight=(int)(255*inner/total+.5),outerWeight=(int)(255*outer/total+.5);
        offsets[at]=-outerOffset;offsets[at+1]=-innerOffset;offsets[at+2]=0;
        offsets[at+3]=innerOffset;offsets[at+4]=outerOffset;
        weights[at]=weights[at+4]=(byte)outerWeight;
        weights[at+1]=weights[at+3]=(byte)innerWeight;
        weights[at+2]=(byte)(255-2*(innerWeight+outerWeight));
        // Bilinear reconstruction distributes the pair over adjacent integer
        // texels. Its second moment differs from the fused offset squared.
        double moment=2*(innerWeight*(1+3*(innerOffset-1))+outerWeight*(9+7*(outerOffset-3)))/255.0;
        return (float)Math.Sqrt(moment);
    }
    // Translate normalized screen T by dy at every depth: (T + dy*Q)/Q.
    // Preserve S and Q so the existing projected cavity geometry stays aligned.
    internal static void OffsetScreenT(float[] screen,float[] target,int at,float dy)
    {
        for(int i=0;i<12;i++)target[at+i]=screen[i];
        for(int i=0;i<4;i++)target[at+4+i]+=dy*screen[8+i];
    }
}
