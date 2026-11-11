using System;

// Dielectric helpers for retained lookups and one transmitted ray per frame.
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
    internal static float InterfaceReflectance(float cosine,float incidentIndex,float transmittedIndex)
    {
        if(!(incidentIndex>0&&incidentIndex<=float.MaxValue))throw new ArgumentOutOfRangeException("incidentIndex");
        if(!(transmittedIndex>0&&transmittedIndex<=float.MaxValue))throw new ArgumentOutOfRangeException("transmittedIndex");
        if(incidentIndex==transmittedIndex)return 0;
        float c=Math.Min(1,Math.Abs(cosine));
        if(c==0)return 1;
        // Normalize the indices at normal incidence to avoid overflow for
        // otherwise valid finite pairs with very different magnitudes.
        if(c==1)
        {
            float largest=Math.Max(incidentIndex,transmittedIndex);
            float a=incidentIndex/largest,b=transmittedIndex/largest;
            float normal=(a-b)/(a+b);return normal*normal;
        }
        float ratio=incidentIndex/transmittedIndex;
        float sineSquared=ratio*ratio*(1-c*c);
        if(sineSquared>=1)return 1;
        float transmitted=(float)Math.Sqrt(Math.Max(0,1-sineSquared));
        float s=(ratio*c-transmitted)/(ratio*c+transmitted);
        float p=(c-ratio*transmitted)/(c+ratio*transmitted);
        return (s*s+p*p)*.5f;
    }
    internal static float BoundedInternalReturn(float viewCos,float insideCos,float index)
    {
        float external=Reflectance(viewCos,index);
        float cavity=InterfaceReflectance(insideCos,index,1);
        // Collected internal light affects radiance only; the material keeps
        // the external Fresnel opacity rather than making the cavity opaque.
        return external+.20f*Math.Max(0,cavity-external);
    }
    internal static IplPoint TransmittedViewRay(IplPoint outwardNormal,float index)
    {
        if(index<1)throw new ArgumentOutOfRangeException("index");
        if(index==1)return new IplPoint(0,0,-1);
        // The caller supplies a unit entry-plane normal. Orient it toward
        // the camera, then refract incident direction (0,0,-1) into glass.
        if(outwardNormal.Z<0)outwardNormal=new IplPoint(-outwardNormal.X,-outwardNormal.Y,-outwardNormal.Z);
        float cosine=Math.Min(1,outwardNormal.Z),eta=1/index;
        float transmitted=(float)Math.Sqrt(Math.Max(0,1-eta*eta*(1-cosine*cosine)));
        float normalWeight=eta*cosine-transmitted;
        return new IplPoint(normalWeight*outwardNormal.X,normalWeight*outwardNormal.Y,-eta+normalWeight*outwardNormal.Z);
    }
    internal static IplPoint EntryNormal(IplPoint direction,float core,float radius)
    {
        // Intersect the view-center ray with the actual rounded-box boundary.
        // Solving its one/two/three active coordinates avoids an arbitrary
        // entry-plane switch, and its reflection pop, during a 45-degree turn.
        float a=Math.Abs(direction.X),b=Math.Abs(direction.Y),c=Math.Abs(direction.Z),swap;
        if(a<b){swap=a;a=b;b=swap;}if(b<c){swap=b;b=c;c=swap;}if(a<b){swap=a;a=b;b=swap;}
        float distance=(core+radius)/a;
        if(distance*b>core)
        {
            float squared=a*a+b*b,sum=a+b,center=core*sum;
            distance=(center+(float)Math.Sqrt(Math.Max(0,center*center-squared*(2*core*core-radius*radius))))/squared;
            if(distance*c>core)
            {
                squared+=c*c;sum+=c;center=core*sum;
                distance=(center+(float)Math.Sqrt(Math.Max(0,center*center-squared*(3*core*core-radius*radius))))/squared;
            }
        }
        float nx=Math.Max(0,distance*Math.Abs(direction.X)-core)*(direction.X<0?-1:1);
        float ny=Math.Max(0,distance*Math.Abs(direction.Y)-core)*(direction.Y<0?-1:1);
        float nz=Math.Max(0,distance*Math.Abs(direction.Z)-core)*(direction.Z<0?-1:1);
        float length=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz);
        return new IplPoint(nx/length,ny/length,nz/length);
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
