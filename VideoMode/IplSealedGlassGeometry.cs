using System;

// Two closed, welded boundary surfaces of ONE thick-walled rounded glass solid.
// Shared integer lattice keys eliminate duplicate edge/corner vertices.
internal static class IplSealedGlassGeometry
{
    internal const int Divisions=24,Side=25,SurfaceVertices=3458,Vertices=SurfaceVertices*2,Indices=6*24*24*6;
    internal const float Radius=.16f,Thickness=.080f,Core=1-Radius;
    internal static int Index(int x,int y,int z)
    {
        if(z==24)return x+25*y;
        if(z==0)return 625+x+25*y;
        if(y==24)return 1250+x+25*(z-1);
        if(y==0)return 1825+x+25*(z-1);
        if(x==24)return 2400+y-1+23*(z-1);
        return 2929+y-1+23*(z-1);
    }
    internal static int FaceIndex(int face,int u,int v)
    {
        if(face==0)return Index(u,v,24);
        if(face==1)return Index(24-u,v,0);
        if(face==2)return Index(24,v,24-u);
        if(face==3)return Index(0,v,u);
        if(face==4)return Index(u,24,24-v);
        return Index(u,0,v);
    }
    private static float Coordinate(int i)=>i<10?-1+Radius*(1-(float)Math.Cos(i/10f*1.5707963f)):i>14?-Coordinate(24-i):-Core+(i-10)*2*Core/4;
    internal static void Vertex(int index,out IplPoint position,out IplPoint normal)
    {
        bool inner=index>=SurfaceVertices;if(inner)index-=SurfaceVertices;
        int x,y,z;
        if(index<625){x=index%25;y=index/25;z=24;}
        else if(index<1250){index-=625;x=index%25;y=index/25;z=0;}
        else if(index<1825){index-=1250;x=index%25;z=index/25+1;y=24;}
        else if(index<2400){index-=1825;x=index%25;z=index/25+1;y=0;}
        else if(index<2929){index-=2400;y=index%23+1;z=index/23+1;x=24;}
        else{index-=2929;y=index%23+1;z=index/23+1;x=0;}
        float px=Coordinate(x),py=Coordinate(y),pz=Coordinate(z);
        float cx=Math.Max(-Core,Math.Min(Core,px)),cy=Math.Max(-Core,Math.Min(Core,py)),cz=Math.Max(-Core,Math.Min(Core,pz));
        float nx=px-cx,ny=py-cy,nz=pz-cz;
        float length=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz);nx/=length;ny/=length;nz/=length;
        float radius=inner?Radius-Thickness:Radius;
        position=new IplPoint(cx+nx*radius,cy+ny*radius,cz+nz*radius);
        normal=new IplPoint(inner?-nx:nx,inner?-ny:ny,inner?-nz:nz);
    }
    internal static int TriangleIndex(int face,int x,int y,int vertex,bool inner)
    {
        int corner=vertex==0?0:vertex==1?(inner?2:1):vertex==2?(inner?1:2):vertex==3?1:vertex==4?(inner?2:3):(inner?3:2);
        return FaceIndex(face,x+(corner&1),y+(corner>>1))+(inner?SurfaceVertices:0);
    }
    // GX's front-face convention with our right-handed projection is opposite
    // the mathematical outward winding. Keep the manifold definition intact
    // and adapt only the retained native command stream.
    internal static int NativeTriangleIndex(int face,int x,int y,int vertex,bool inner)
    {
        int slot=vertex==1?2:vertex==2?1:vertex==4?5:vertex==5?4:vertex;
        return TriangleIndex(face,x,y,slot,inner);
    }
}
