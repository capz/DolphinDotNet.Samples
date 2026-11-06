using System;
using Dolphin.Graphics;

// Immutable geometry, warmed between frames. Reuse the canvas's small staging
// grid; only native meshes and a bounded index array survive initialization.
internal sealed class CachedFooter : IDisposable
{
    private const int Styles=3,Slots=8,Vertices=624;
    private readonly VertexPositionColorTexture[] staging;
    private readonly ushort[] indices=new ushort[Vertices];
    private readonly Mesh[] meshes=new Mesh[Styles*Slots];
    private readonly int[] counts=new int[Styles];
    private readonly Material material=new Material{Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Transform transform=new Transform();
    private int style,used;
    internal bool Recording {get;private set;}
    internal CachedFooter(VertexPositionColorTexture[] staging)
    {this.staging=staging;for(int i=0;i<indices.Length;i++)indices[i]=(ushort)i;}
    internal void Begin(int variant)
    {
        if(Recording||variant<0||variant>=Styles||counts[variant]!=0)throw new InvalidOperationException("Invalid footer cache setup.");
        style=variant;used=0;Recording=true;
    }
    internal void Triangle(float ax,float ay,float bx,float by,float cx,float cy,uint rgba)
    {
        var color=new Color32((byte)(rgba>>24),(byte)(rgba>>16),(byte)(rgba>>8),(byte)rgba);
        staging[used++]=new VertexPositionColorTexture(new Vector3(ax-320,240-ay,0),color,Vector2.Zero);
        staging[used++]=new VertexPositionColorTexture(new Vector3(bx-320,240-by,0),color,Vector2.Zero);
        staging[used++]=new VertexPositionColorTexture(new Vector3(cx-320,240-cy,0),color,Vector2.Zero);
        if(used==Vertices)Flush();
    }
    private void Flush()
    {
        if(used==0)return;
        if(counts[style]==Slots)throw new InvalidOperationException("Footer cache capacity exceeded.");
        var prefix=indices;
        if(used!=indices.Length){prefix=new ushort[used];Array.Copy(indices,prefix,used);}
        for(int i=used;i<staging.Length;i++)staging[i]=default;
        meshes[style*Slots+counts[style]++]=Mesh.Create(staging,prefix);used=0;
    }
    internal void End(){Flush();Recording=false;}
    internal bool Ready(int variant)=>counts[variant]!=0;
    internal void Draw(Renderer renderer,int variant,float scale)
    {
        transform.LocalScale=new Vector3(scale,scale,1);
        for(int i=0;i<counts[variant];i++)renderer.DrawMesh(meshes[variant*Slots+i],material,transform);
    }
    public void Dispose(){for(int s=0;s<Styles;s++)for(int i=0;i<counts[s];i++)meshes[s*Slots+i].Dispose();}
}
