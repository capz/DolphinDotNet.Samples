using System;

internal static class IplGlassOpticsTests
{
    internal static void Run()
    {
        const float index=1.5f;
        if(Math.Abs(IplGlassOptics.Reflectance(1,index)-.04f)>.000001f||Math.Abs(IplGlassOptics.Reflectance(0,index)-1)>.000001f)
            throw new Exception("Dielectric normal/grazing Fresnel limits are wrong.");
        float previous=1;
        for(int i=0;i<=100;i++)
        {
            float c=i/100f,r=IplGlassOptics.Reflectance(c,index);
            if(!float.IsFinite(r)||r<0||r>1||r>previous+.000001f)throw new Exception("Fresnel must be finite, bounded and monotonic.");
            previous=r;
            if(IplGlassOptics.Reflectance(c,1)!=0)throw new Exception("Matched indices cannot reflect.");
        }
        for(int x=-10;x<=10;x++)for(int y=-10;y<=10;y++)
        {
            float nx=x/10f,ny=y/10f;
            IplGlassOptics.WallOffset(nx,ny,.14f,index,out var dx,out var dy);
            IplGlassOptics.WallOffset(nx,ny,.28f,index,out var twiceX,out var twiceY);
            IplGlassOptics.WallOffset(-nx,-ny,.14f,index,out var mirrorX,out var mirrorY);
            if(!float.IsFinite(dx)||!float.IsFinite(dy)||dx*nx+dy*ny>.000001f||dx*dx+dy*dy>.14f*.14f+.000001f||
               Math.Abs(twiceX-2*dx)+Math.Abs(twiceY-2*dy)>.000001f||Math.Abs(mirrorX+dx)+Math.Abs(mirrorY+dy)>.000001f)
                throw new Exception("Snell wall offsets must be finite, thickness-linear, symmetric and bounded by wall thickness.");
            IplGlassOptics.WallOffset(nx,ny,.14f,1,out dx,out dy);
            if(dx!=0||dy!=0)throw new Exception("Matched indices cannot bend light.");
        }
        IplGlassOptics.WallOffset(0,0,.14f,index,out var normalX,out var normalY);
        if(normalX!=0||normalY!=0)throw new Exception("Normal incidence must not displace the interior image.");
        var shell=new IplShell(new FakeOutput());shell.TurnTo(ShellFace.Calendar);shell.Tick(.7f);
        var proof=new IplProof();var renderer=new IplRenderer(proof);renderer.Prepare(shell);renderer.Present(shell);
        if(proof.OpticalLayers.ToString()!="rear>icon>front")throw new Exception("The centered icon must be captured between the rear and front glass interfaces.");
        Console.WriteLine("Glass optics: dielectric Fresnel limits, matching-index transmission, Snell direction, symmetry, finite bounds and thickness scaling passed.");
    }
}
