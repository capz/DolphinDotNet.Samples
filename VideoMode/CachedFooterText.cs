using System;
using Dolphin.Graphics;

// Combine the fixed footer runs into one uniform-color indexed mesh. Retaining
// individual moving labels was slower overall in native profiling: CPU savings
// moved into GPU waits. Keep the live menu on the packed-quad path.
internal sealed class CachedFooterText : IDisposable
{
    private const int Slots=8;
    private readonly VertexPositionColorTexture[] staging;
    private readonly string[] keys=new string[Slots];
    private readonly float[] layout=new float[Slots*5];
    private readonly Material material=new Material{Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Transform transform=new Transform();
    private readonly Texture2D atlas;
    private Mesh? mesh;
    private int used,count;
    private bool dirty;
    private uint tint;
    private float scale;
    internal bool Recording {get;private set;}
    internal bool Ready {get;private set;}
    internal CachedFooterText(VertexPositionColorTexture[] staging,Texture2D atlas)
    {this.staging=staging;this.atlas=atlas;material.Texture=atlas;}
    internal void Reset()=>Ready=false;
    internal void Begin(){Recording=true;Ready=false;used=0;dirty=mesh==null;}
    internal void End()
    {
        dirty|=used!=count;count=used;
        if(dirty){
            int n=0;
            var color=new Color32((byte)(tint>>24),(byte)(tint>>16),(byte)(tint>>8),(byte)tint);
            float du=32f/atlas.Width,dv=40f/atlas.Height;
            for(int run=0;run<count;run++){
                int at=run*5;float x=layout[at],y=layout[at+1],factor=layout[at+2]/28,angle=layout[at+3],stretch=layout[at+4];
                float co=(float)Math.Cos(angle*.017453293f),si=(float)Math.Sin(angle*.017453293f),advance=0;
                float rotation=angle*.01745329252f,qco=angle==0?1:MathF.Cos(rotation),qsi=angle==0?0:MathF.Sin(rotation);
                var text=keys[run];
                for(int i=0;i<text.Length;i++){
                    int ch=text[i]-32;if(ch<0||ch>=96)continue;
                    if(n+4>624)throw new InvalidOperationException("Footer text exceeds shared staging capacity.");
                    float left=x+advance*co,top=y+advance*si,width=32*factor*stretch,height=40*factor;
                    float right=left+width*qco,ry=top+width*qsi,bx=left-height*qsi,by=top+height*qco,ex=right-height*qsi,ey=ry+height*qco;
                    float u=ch%16*32f/atlas.Width,v=ch/16*40f/atlas.Height;
                    staging[n++]=new VertexPositionColorTexture(new Vector3(left-320,240-top,0),color,new Vector2(u,v));
                    staging[n++]=new VertexPositionColorTexture(new Vector3(right-320,240-ry,0),color,new Vector2(u+du,v));
                    staging[n++]=new VertexPositionColorTexture(new Vector3(ex-320,240-ey,0),color,new Vector2(u+du,v+dv));
                    staging[n++]=new VertexPositionColorTexture(new Vector3(bx-320,240-by,0),color,new Vector2(u,v+dv));
                    advance+=IplFontData.Widths[ch]*factor*stretch;
                }
            }
            for(int i=n;i<staging.Length;i++)staging[i]=new VertexPositionColorTexture(default,color,default);
            if(n==0){mesh?.Dispose();mesh=null;}
            else{
                var indices=new ushort[n/4*6];
                for(int i=0;i<n/4;i++){int at=i*6,b=i*4;indices[at]=(ushort)b;indices[at+1]=(ushort)(b+1);indices[at+2]=(ushort)(b+2);indices[at+3]=(ushort)b;indices[at+4]=(ushort)(b+2);indices[at+5]=(ushort)(b+3);}
                var replacement=Mesh.Create(staging,indices);mesh?.Dispose();mesh=replacement;
            }
        }
        transform.LocalScale=new Vector3(scale,scale,1);Recording=false;Ready=true;
    }
    internal void Add(string text,float x,float y,float size,uint color,float angle,float stretch,float canvasScale)
    {
        if(used==Slots)throw new InvalidOperationException("Footer run capacity exceeded.");
        if(used==0){dirty|=tint!=color;tint=color;}
        else if(tint!=color)throw new InvalidOperationException("Footer text must have one color.");
        if(keys[used]!=text){keys[used]=text;dirty=true;}
        int at=used*5;
        if(layout[at]!=x||layout[at+1]!=y||layout[at+2]!=size||layout[at+3]!=angle||layout[at+4]!=stretch)dirty=true;
        layout[at]=x;layout[at+1]=y;layout[at+2]=size;layout[at+3]=angle;layout[at+4]=stretch;
        scale=canvasScale;used++;
    }
    internal void Draw(Renderer renderer){if(mesh!=null)renderer.DrawMesh(mesh,material,transform);}
    public void Dispose()=>mesh?.Dispose();
}
