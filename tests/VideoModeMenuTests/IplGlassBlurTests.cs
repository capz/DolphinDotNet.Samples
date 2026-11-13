using System;

internal static class IplGlassBlurTests
{
    private static void Check(bool value,string message)
    {if(!value)throw new Exception(message);}
    private static float Project(float[] matrix,int at,int row,float x,float y,float z)
    {
        int r=at+row*4;
        return matrix[r]*x+matrix[r+1]*y+matrix[r+2]*z+matrix[r+3];
    }
    internal static void Run()
    {
        float[] spacings={0,1,2,3};
        for(int i=0;i<spacings.Length;i++)Check(IplGlassBlur.Spacing(i)==spacings[i],"Glass blur levels must retain their configured pixel spacing.");
        foreach(int level in new[]{-1,4})
        {
            bool rejected=false;
            try{IplGlassBlur.Spacing(level);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"Unknown glass blur levels must be rejected.");
        }
        float[] screen={.3f,.2f,-.1f,2, -.4f,.7f,.08f,1.2f, .12f,-.06f,-.25f,3.25f};
        var original=(float[])screen.Clone();var target=new float[36];
        foreach(float offset in new[]{-.04f,-.003f,0,.002f,.07f})
        {
            for(int i=0;i<target.Length;i++)target[i]=123;
            IplGlassBlur.OffsetScreenT(screen,target,12,offset);
            for(int i=0;i<screen.Length;i++)
            {
                Check(screen[i]==original[i],"Blur matrix construction must preserve its shared screen matrix.");
                if(i<4||i>=8)Check(target[12+i]==screen[i],"Blur must leave projected S and Q unchanged.");
            }
            for(int i=0;i<12;i++)Check(target[i]==123&&target[24+i]==123,"Blur must write only the selected matrix bank.");
            foreach(float x in new[]{-1.1f,0,.9f})foreach(float y in new[]{-.8f,.2f,1.3f})foreach(float z in new[]{-4f,0,4,10})
            {
                float q=Project(screen,0,2,x,y,z),t=Project(screen,0,1,x,y,z);
                Check(q>0,"The perspective test points must remain in front of the camera.");
                float shifted=Project(target,12,1,x,y,z)/Project(target,12,2,x,y,z);
                Check(Math.Abs(shifted-t/q-offset)<.000002f,"Projected blur radius must stay constant across screen position and positive depths.");
            }
        }
        CheckKernels();
        Console.WriteLine("Glass blur: dense Gaussian impulse, normalized symmetric bilinear kernels, effective variance, valid levels and constant projected radius passed.");
    }
    private static void CheckKernels()
    {
        var offsets=new float[15];var weights=new byte[15];
        foreach(float sigma in new[]{0,float.Epsilon,.00001f,.5f,1,2,3,100,float.MaxValue})
        {
            for(int i=0;i<15;i++){offsets[i]=123;weights[i]=123;}
            float effective=IplGlassBlur.BuildKernel(sigma,offsets,weights,5);
            Check(float.IsFinite(effective)&&effective>=0&&effective<2.7f,"Finite Gaussian strengths must yield a bounded finite variance.");
            int weight=0;
            for(int i=0;i<5;i++)
            {
                Check(offsets[i]==123&&offsets[10+i]==123&&weights[i]==123&&weights[10+i]==123,"Kernel construction must preserve neighboring banks.");
                Check(float.IsFinite(offsets[5+i])&&Math.Abs(offsets[5+i])<=4,"Fused Gaussian samples must stay inside their dense nine-texel support.");
                Check(offsets[5+i]==-offsets[9-i]&&weights[5+i]==weights[9-i],"Gaussian kernels must remain exactly symmetric.");
                weight+=weights[5+i];
            }
            Check(weight==255,"Gaussian weights must retain exact unit DC gain after byte quantization.");
            var impulse=new double[9];
            for(int tap=0;tap<5;tap++)
            {
                double position=offsets[5+tap]+4;
                int left=(int)Math.Floor(position);double fraction=position-left,w=weights[5+tap]/255.0;
                impulse[left]+=w*(1-fraction);if(fraction>0)impulse[left+1]+=w*fraction;
            }
            double sum=0,mean=0,moment=0;
            for(int i=0;i<9;i++)
            {
                sum+=impulse[i];mean+=(i-4)*impulse[i];moment+=(i-4)*(i-4)*impulse[i];
                Check(impulse[i]>=0&&Math.Abs(impulse[i]-impulse[8-i])<.0000001,"Reconstructed Gaussian impulse must be nonnegative and symmetric.");
                if(i<4)Check(impulse[i]<=impulse[i+1]+2.0/255+.0000001,"A Gaussian impulse must grow toward its central peak within byte quantization.");
            }
            Check(Math.Abs(sum-1)<.0000001&&Math.Abs(mean)<.0000001,"Bilinear fusion must preserve constant colors and zero positional drift.");
            Check(Math.Abs(effective*effective-moment)<.000002,"Reported effective sigma must match the actual reconstructed discrete impulse.");
            if(sigma==0||sigma<.001f)
                Check(effective==0&&impulse[4]==1,"Zero and underflow-small strengths must safely produce the identity filter.");
            if(sigma>=.5f&&sigma<=3)
            {
                var reference=new double[9];double referenceSum=0;
                for(int i=0;i<9;i++){reference[i]=Math.Exp(-(i-4)*(i-4)/(2*(double)sigma*sigma));referenceSum+=reference[i];}
                for(int i=0;i<9;i++)Check(Math.Abs(impulse[i]-reference[i]/referenceSum)<=2.0/255+.0000001,"Fused bilinear samples must reconstruct the dense Gaussian within byte-quantization error.");
            }
        }
        foreach(float sigma in new[]{-1,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            bool rejected=false;
            try{IplGlassBlur.BuildKernel(sigma,offsets,weights,0);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"Invalid Gaussian strengths must be rejected before producing nonfinite coordinates.");
        }
    }
}
