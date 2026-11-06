using System;
using Dolphin.Graphics;

// Fixed storage labels survive the live blur/atlas passes as immutable native
// text meshes. The selected row name remains live for its smooth focus scale.
internal sealed class CachedStorageText : IDisposable
{
    private readonly VertexPositionColorTexture[] staging;
    private readonly ushort[] indices=new ushort[156*6];
    private readonly Mesh?[] meshes=new Mesh?[8];
    private readonly Material material=new Material{Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Transform transform=new Transform();
    private readonly Texture2D atlas;
    private int used,count;internal bool Recording {get;private set;}
    internal bool Ready {get;private set;}
    internal CachedStorageText(VertexPositionColorTexture[] staging,Texture2D atlas)
    {this.staging=staging;this.atlas=atlas;material.Texture=atlas;for(int i=0;i<156;i++){int a=i*6,b=i*4;indices[a]=(ushort)b;indices[a+1]=(ushort)(b+1);indices[a+2]=(ushort)(b+2);indices[a+3]=(ushort)b;indices[a+4]=(ushort)(b+2);indices[a+5]=(ushort)(b+3);}}
    internal void Begin(){Dispose();used=count=0;Recording=true;Ready=false;}
    internal void Add(string text,float x,float y,float size,uint rgba,float angle,float stretch,float scale)
    {
        float factor=size/28,co=(float)Math.Cos(angle*.017453293f),si=(float)Math.Sin(angle*.017453293f),advance=0;
        var color=new Color32((byte)(rgba>>24),(byte)(rgba>>16),(byte)(rgba>>8),(byte)rgba);
        float du=32f/atlas.Width,dv=40f/atlas.Height;
        for(int i=0;i<text.Length;i++){
            int ch=text[i]-32;if(ch<0||ch>=96)continue;if(used+4>624)Flush();
            float left=x+advance*co,top=y+advance*si,w=32*factor*stretch,h=40*factor;
            float right=left+w*co,ry=top+w*si,bx=left-h*si,by=top+h*co,ex=right-h*si,ey=ry+h*co;
            float u=ch%16*32f/atlas.Width,v=ch/16*40f/atlas.Height;
            staging[used++]=new VertexPositionColorTexture(new Vector3(left-320,240-top,0),color,new Vector2(u,v));
            staging[used++]=new VertexPositionColorTexture(new Vector3(right-320,240-ry,0),color,new Vector2(u+du,v));
            staging[used++]=new VertexPositionColorTexture(new Vector3(ex-320,240-ey,0),color,new Vector2(u+du,v+dv));
            staging[used++]=new VertexPositionColorTexture(new Vector3(bx-320,240-by,0),color,new Vector2(u,v+dv));
            advance+=IplFontData.Widths[ch]*factor*stretch;
        }
        transform.LocalScale=new Vector3(scale,scale,1);
    }
    private void Flush()
    {
        if(used==0)return;if(count==meshes.Length)throw new InvalidOperationException("Storage text capacity exceeded.");
        for(int i=used;i<staging.Length;i++)staging[i]=default;
        var prefix=indices;if(used!=624){prefix=new ushort[used/4*6];Array.Copy(indices,prefix,prefix.Length);}
        meshes[count++]=Mesh.Create(staging,prefix);used=0;
    }
    internal void End(){Flush();Recording=false;Ready=true;}
    internal void Draw(Renderer renderer){for(int i=0;i<count;i++)renderer.DrawMesh(meshes[i]!,material,transform);}
    public void Dispose(){for(int i=0;i<count;i++){meshes[i]?.Dispose();meshes[i]=null;}count=0;Ready=false;}
}
