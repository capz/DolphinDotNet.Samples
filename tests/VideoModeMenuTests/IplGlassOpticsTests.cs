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
        CheckInterfaces();
        CheckTransmittedRays();
        CheckEntryNormals();
        for(int x=0;x<=40;x++)for(int y=0;y<=40;y++)
        {
            float view=x/40f,inside=y/40f;
            float external=IplGlassOptics.Reflectance(view,index),internalR=IplGlassOptics.InterfaceReflectance(inside,index,1);
            float collected=IplGlassOptics.BoundedInternalReturn(view,inside,index);
            if(!float.IsFinite(collected)||collected<external||collected>external+.20f*(1-external)+.000001f||
               (internalR<=external&&collected!=external))
                throw new Exception("The internal return must preserve the external baseline and add only its bounded radiance gain.");
            if(IplGlassOptics.BoundedInternalReturn(view,inside,1)!=0)
                throw new Exception("Matched indices cannot produce an internal light return.");
        }
        if(Math.Abs(IplGlassOptics.BoundedInternalReturn(.7f,.7f,index)-.2407336f)>.000001f)
            throw new Exception("The reference supercritical cavity return must remain bounded at approximately 0.240734.");
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
        Console.WriteLine("Glass optics: reciprocal interfaces, total internal reflection, bounded internal returns, transmitted-ray Snell symmetry and wall offsets passed.");
    }
    private static void CheckInterfaces()
    {
        const float index=1.5f;
        if(Math.Abs(IplGlassOptics.InterfaceReflectance(1,index,1)-.04f)>.000001f)
            throw new Exception("Glass-to-air normal reflectance must match air-to-glass reflectance.");
        float critical=(float)Math.Sqrt(1-1/(index*index));
        if(IplGlassOptics.InterfaceReflectance(critical-.0001f,index,1)!=1||
           IplGlassOptics.InterfaceReflectance(critical+.0001f,index,1)>=1)
            throw new Exception("The glass-to-air interface must cross the Snell critical angle.");
        for(int a=1;a<=5;a++)for(int b=1;b<=5;b++)for(int step=0;step<=100;step++)
        {
            float incident=a*.5f,transmitted=b*.5f,cosine=step/100f;
            float r=IplGlassOptics.InterfaceReflectance(cosine,incident,transmitted);
            if(!float.IsFinite(r)||r<0||r>1||r!=IplGlassOptics.InterfaceReflectance(-cosine,incident,transmitted))
                throw new Exception("Interface reflectance must be finite, bounded and invariant under the normal sign.");
            if(a==b&&r!=0)throw new Exception("Matched interfaces must transmit without reflection at every angle.");
            float ratio=incident/transmitted,sineSquared=ratio*ratio*(1-cosine*cosine);
            // Very near grazing, float-quantizing the refracted cosine loses
            // the incident angle; those cases still have boundedness checks.
            if(sineSquared<1&&cosine>=.05f)
            {
                float refractedCosine=(float)Math.Sqrt(1-sineSquared);
                float reciprocal=IplGlassOptics.InterfaceReflectance(refractedCosine,transmitted,incident);
                if(Math.Abs(r-reciprocal)>.0001f)
                    throw new Exception($"Snell-related incident and transmitted angles must have reciprocal Fresnel reflectance ({incident}/{transmitted}, cos={cosine}, R={r}, reciprocal={reciprocal}).");
            }
            else if(sineSquared>1&&r!=1)throw new Exception("A supercritical internal ray must be totally reflected.");
        }
    }
    private static void CheckTransmittedRays()
    {
        for(int material=0;material<3;material++)for(int tilt=0;tilt<=18;tilt++)for(int azimuth=0;azimuth<8;azimuth++)
        {
            float index=material==0?1:material==1?1.5f:2.4f;
            float angle=tilt*(float)Math.PI/36,around=azimuth*(float)Math.PI/4;
            float x=(float)Math.Sin(angle)*(float)Math.Cos(around),y=(float)Math.Sin(angle)*(float)Math.Sin(around),z=(float)Math.Cos(angle);
            var ray=IplGlassOptics.TransmittedViewRay(new IplPoint(x,y,z),index);
            var flipped=IplGlassOptics.TransmittedViewRay(new IplPoint(-x,-y,-z),index);
            if(z<0){x=-x;y=-y;z=-z;}
            float lengthSquared=ray.X*ray.X+ray.Y*ray.Y+ray.Z*ray.Z;
            float transmittedCosine=-(ray.X*x+ray.Y*y+ray.Z*z);
            float expectedCosine=(float)Math.Sqrt(1-(1-z*z)/(index*index));
            if(!float.IsFinite(lengthSquared)||Math.Abs(lengthSquared-1)>.000001f||
               Math.Abs(transmittedCosine-expectedCosine)>.000001f||
               Math.Abs(ray.X-flipped.X)+Math.Abs(ray.Y-flipped.Y)+Math.Abs(ray.Z-flipped.Z)>.000001f)
                throw new Exception("The transmitted ray must remain unit length, obey Snell's law and orient either normal sign consistently.");
            if(index==1&&(ray.X!=0||ray.Y!=0||ray.Z!=-1))
                throw new Exception("Index-matched entry must preserve the incident view ray exactly.");
            if(z>.02f)
            {
                float external=IplGlassOptics.Reflectance(z,index),internalR=IplGlassOptics.InterfaceReflectance(transmittedCosine,index,1);
                if(Math.Abs(external-internalR)>.00001f||
                   Math.Abs(IplGlassOptics.BoundedInternalReturn(z,transmittedCosine,index)-external)>.00001f)
                    throw new Exception("Parallel entry and exit planes must keep reciprocal reflection without a fabricated internal-return gain.");
            }
        }
    }
    private static void CheckEntryNormals()
    {
        const float core=.84f,radius=.16f;
        for(int latitude=-8;latitude<=8;latitude++)for(int longitude=0;longitude<32;longitude++)
        {
            float pitch=latitude*(float)Math.PI/16,yaw=longitude*(float)Math.PI/16;
            var direction=new IplPoint((float)(Math.Cos(pitch)*Math.Sin(yaw)),(float)Math.Sin(pitch),(float)(Math.Cos(pitch)*Math.Cos(yaw)));
            var normal=IplGlassOptics.EntryNormal(direction,core,radius);
            float ax=Math.Abs(direction.X),ay=Math.Abs(direction.Y),az=Math.Abs(direction.Z);
            float distance=ax>=ay&&ax>=az?(core+radius*Math.Abs(normal.X))/ax:ay>=az?(core+radius*Math.Abs(normal.Y))/ay:(core+radius*Math.Abs(normal.Z))/az;
            float dx=Math.Max(0,distance*ax-core),dy=Math.Max(0,distance*ay-core),dz=Math.Max(0,distance*az-core);
            float length=normal.X*normal.X+normal.Y*normal.Y+normal.Z*normal.Z;
            if(!float.IsFinite(length)||Math.Abs(length-1)>.000002f||Math.Abs(dx*dx+dy*dy+dz*dz-radius*radius)>.000002f||
               Math.Abs(dx-radius*Math.Abs(normal.X))+Math.Abs(dy-radius*Math.Abs(normal.Y))+Math.Abs(dz-radius*Math.Abs(normal.Z))>.00001f)
                throw new Exception("The central entry normal must lie on the rounded-box boundary along the viewing ray.");
        }
        float before=44.99f*(float)Math.PI/180,after=45.01f*(float)Math.PI/180;
        var a=IplGlassOptics.EntryNormal(new IplPoint((float)Math.Sin(before),0,(float)Math.Cos(before)),core,radius);
        var b=IplGlassOptics.EntryNormal(new IplPoint((float)Math.Sin(after),0,(float)Math.Cos(after)),core,radius);
        if(Math.Abs(a.X-b.X)+Math.Abs(a.Z-b.Z)>.01f)
            throw new Exception("The entry normal must stay continuous across a 45-degree cube turn.");
    }
}
