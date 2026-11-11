using System;
using System.Collections.Generic;

internal static class IplSealedGlassTests
{
    internal static void Run()
    {
        int count=IplSealedGlassGeometry.SurfaceVertices;
        IplSealedGlassGeometry.Vertex(IplSealedGlassGeometry.Index(24,12,12),out var flat,out _);
        IplSealedGlassGeometry.Vertex(IplSealedGlassGeometry.Index(24,24,24),out var corner,out _);
        IplSealedGlassGeometry.Vertex(IplSealedGlassGeometry.Index(24,24,24)+count,out var insideCorner,out _);
        float expected=IplSealedGlassGeometry.Core+IplSealedGlassGeometry.Radius/(float)Math.Sqrt(3);
        float innerExpected=IplSealedGlassGeometry.Core+(IplSealedGlassGeometry.Radius-IplSealedGlassGeometry.Thickness)/(float)Math.Sqrt(3);
        if(Math.Abs(flat.X-1)+Math.Abs(flat.Y)+Math.Abs(flat.Z)>.00001f||
           Math.Abs(corner.X-expected)+Math.Abs(corner.Y-expected)+Math.Abs(corner.Z-expected)>.00001f||
           Math.Abs(insideCorner.X-innerExpected)+Math.Abs(insideCorner.Y-innerExpected)+Math.Abs(insideCorner.Z-innerExpected)>.00001f)
            throw new Exception("Glass must retain flat wall centers and pronounced spherical outer/inner corner roundovers.");
        for(int i=0;i<count;i++)
        {
            IplSealedGlassGeometry.Vertex(i,out var a,out var n);
            IplSealedGlassGeometry.Vertex(i+count,out var b,out var m);
            float length=n.X*n.X+n.Y*n.Y+n.Z*n.Z;
            float thickness=(a.X-b.X)*n.X+(a.Y-b.Y)*n.Y+(a.Z-b.Z)*n.Z;
            if(!float.IsFinite(length)||Math.Abs(length-1)>.00001f||Math.Abs(thickness-IplSealedGlassGeometry.Thickness)>.00001f||
               Math.Abs(n.X+m.X)+Math.Abs(n.Y+m.Y)+Math.Abs(n.Z+m.Z)>.00001f)
                throw new Exception("Sealed glass requires finite unit normals, an inward cavity and constant normal wall thickness.");
        }
        for(int boundary=0;boundary<2;boundary++)
        {
            var edges=new Dictionary<(int,int),(int count,int balance)>();
            var vertices=new HashSet<int>();double volume=0;
            for(int face=0;face<6;face++)for(int y=0;y<24;y++)for(int x=0;x<24;x++)for(int t=0;t<2;t++)
            {
                int a=IplSealedGlassGeometry.TriangleIndex(face,x,y,t*3,boundary==1);
                int b=IplSealedGlassGeometry.TriangleIndex(face,x,y,t*3+1,boundary==1);
                int c=IplSealedGlassGeometry.TriangleIndex(face,x,y,t*3+2,boundary==1);
                foreach(int v in new[]{a,b,c})
                {
                    if(v<boundary*count||v>=(boundary+1)*count)throw new Exception("Glass index outside its closed interface.");
                    vertices.Add(v);
                }
                Edge(a,b);Edge(b,c);Edge(c,a);
                IplSealedGlassGeometry.Vertex(a,out var pa,out _);IplSealedGlassGeometry.Vertex(b,out var pb,out _);IplSealedGlassGeometry.Vertex(c,out var pc,out _);
                volume+=pa.X*(pb.Y*pc.Z-pb.Z*pc.Y)+pa.Y*(pb.Z*pc.X-pb.X*pc.Z)+pa.Z*(pb.X*pc.Y-pb.Y*pc.X);
            }
            foreach(var edge in edges.Values)if(edge.count!=2||edge.balance!=0)throw new Exception("Glass has an open seam or inconsistent winding.");
            if(vertices.Count!=count||vertices.Count-edges.Count+6912!=2||(boundary==0?volume<=0:volume>=0))
                throw new Exception("Glass must have a closed spherical outer boundary and oppositely wound sealed cavity.");
            void Edge(int a,int b)
            {
                var key=a<b?(a,b):(b,a);edges.TryGetValue(key,out var old);
                edges[key]=(old.count+1,old.balance+(a<b?1:-1));
            }
        }
        Console.WriteLine("Sealed glass: welded outer/cavity manifolds, zero boundary seams, winding and constant normal wall thickness passed.");
    }
}
