using System;
using Dolphin.Graphics;

// Immutable geometry, warmed between frames. Reuse the canvas's small staging
// grid; only native meshes and a bounded index array survive initialization.
internal sealed class CachedFooter : IDisposable
{
    private const int Vertices=624;
    private readonly int styles,slots;
    private readonly VertexPositionColorTexture[] staging;
    private readonly ushort[] indices=new ushort[Vertices];
    private readonly Mesh[] meshes;
    private readonly int[] counts;
    private readonly Material material=new Material{Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Transform transform=new Transform();
    private int style,used;
    internal bool Recording {get;private set;}
    internal CachedFooter(VertexPositionColorTexture[] staging,int styles=3,int slots=8)
    {this.staging=staging;this.styles=styles;this.slots=slots;meshes=new Mesh[styles*slots];counts=new int[styles];for(int i=0;i<indices.Length;i++)indices[i]=(ushort)i;}
    internal void Begin(int variant)
    {
        if(Recording||variant<0||variant>=styles||counts[variant]!=0)throw new InvalidOperationException("Invalid footer cache setup.");
        style=variant;used=0;Recording=true;
    }
    internal void Triangle(float ax,float ay,float bx,float by,float cx,float cy,uint rgba)
    {
        ColoredTriangle(ax,ay,bx,by,cx,cy,rgba,rgba,rgba);
    }
    private static Color32 Color(uint color)=>new Color32((byte)(color>>24),(byte)(color>>16),(byte)(color>>8),(byte)color);
    internal void ColoredTriangle(float ax,float ay,float bx,float by,float cx,float cy,uint ca,uint cb,uint cc)
    {
        staging[used++]=new VertexPositionColorTexture(new Vector3(ax-320,240-ay,0),Color(ca),Vector2.Zero);
        staging[used++]=new VertexPositionColorTexture(new Vector3(bx-320,240-by,0),Color(cb),Vector2.Zero);
        staging[used++]=new VertexPositionColorTexture(new Vector3(cx-320,240-cy,0),Color(cc),Vector2.Zero);
        if(used==Vertices)Flush();
    }
    private void Flush()
    {
        if(used==0)return;
        if(counts[style]==slots)throw new InvalidOperationException("Footer cache capacity exceeded.");
        var prefix=indices;
        if(used!=indices.Length){prefix=new ushort[used];Array.Copy(indices,prefix,used);}
        for(int i=used;i<staging.Length;i++)staging[i]=default;
        meshes[style*slots+counts[style]++]=Mesh.Create(staging,prefix);used=0;
    }
    internal void End(){Flush();Recording=false;}
    internal bool Ready(int variant)=>counts[variant]!=0;
    internal void Draw(Renderer renderer,int variant,float scale)
    {
        transform.LocalScale=new Vector3(scale,scale,1);
        for(int i=0;i<counts[variant];i++)renderer.DrawMesh(meshes[variant*slots+i],material,transform);
    }
    public void Dispose(){for(int s=0;s<styles;s++)for(int i=0;i<counts[s];i++)meshes[s*slots+i].Dispose();}
}
