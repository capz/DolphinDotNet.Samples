using System;
using Dolphin.Graphics;

internal sealed class GameCubeCanvas : IIplCanvas, IDisposable
{
    private const int GlassVertices=6*24*24*6, PanelVertices=3*36*3;
    private readonly GraphicsDevice device;
    private readonly Draw2D draw;
    private readonly Texture2D atlas=Texture2D.FromPng(IplFontData.Png,true);
    private readonly Renderer renderer;
    private readonly Camera camera=new Camera(60,4f/3,.1f,10);
    private readonly Transform identity=new Transform();
    private readonly Material glassMaterial=new Material {Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Material frostMaterial=new Material {Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly VertexPositionColor[] glass=new VertexPositionColor[GlassVertices];
    private readonly DynamicMesh glassMesh;
    private readonly IplPoint[] panelPoints=new IplPoint[PanelVertices];
    private readonly VertexPositionColorTexture[] panelVertices=new VertexPositionColorTexture[PanelVertices];
    private readonly DynamicMesh[] panelMeshes=new DynamicMesh[9];
    private SceneCapture? capture;
    private int glassCount,panelCount,captureWidth,captureHeight;
    private float scale=1,offset,logicalWidth=640,logicalHeight=480;
    internal GameCubeCanvas(GraphicsDevice device)
    {
        this.device=device;renderer=new Renderer(device);
        draw=new Draw2D(device,new Draw2DOptions {VertexCapacity=32760});
        glassMesh=DynamicMesh.Create(glass);
        for(int i=0;i<9;i++)panelMeshes[i]=DynamicMesh.Create(panelVertices);
        camera.LookAt(new Vector3(0,0,1),new Vector3(0,0,0),new Vector3(0,1,0));
    }
    internal void Prepare()
    {
        var size=draw.LogicalSize;logicalWidth=size.X;logicalHeight=size.Y;
        scale=size.Y/480f;offset=(size.X-640*scale)/2;
        camera.SetOrthographic(size.X,size.Y,.1f,10);
        if(captureWidth!=device.Width||captureHeight!=device.Height)
        {
            // Standard VI modes all permit an aligned GX half-size copy.
            // Allocate between frames; a display change may resize the EFB.
            var replacement=new SceneCapture(device,device.Width/2,device.Height/2,true);
            capture?.Dispose();capture=replacement;
            captureWidth=device.Width;captureHeight=device.Height;frostMaterial.Texture=capture.Texture;
        }
    }
    internal void Begin()=>draw.Begin();
    internal void End()=>draw.End();
    private static Color32 Color(uint c)=>new Color32((byte)(c>>24),(byte)(c>>16),(byte)(c>>8),(byte)c);
    private Vector3 World(IplPoint p)=>new Vector3((p.X-320)*scale,(240-p.Y)*scale,0);
    public void BeginGlass(){glassCount=0;}
    public void GlassTriangle(IplPoint a,uint ca,IplPoint b,uint cb,IplPoint d,uint cd)
    {
        glass[glassCount++]=new VertexPositionColor(World(a),Color(ca));
        glass[glassCount++]=new VertexPositionColor(World(b),Color(cb));
        glass[glassCount++]=new VertexPositionColor(World(d),Color(cd));
    }
    public void EndGlass()
    {
        if(glassCount!=GlassVertices)throw new InvalidOperationException("Incomplete glass geometry.");
        glassMesh.SetVertices(glass);
    }
    public void DrawGlass()
    {
        draw.End();renderer.Begin3D(camera);renderer.DrawMesh(glassMesh,glassMaterial,identity);renderer.End3D();draw.Begin();
    }
    public void BeginPanels(){panelCount=0;}
    public void PanelTriangle(IplPoint a,IplPoint b,IplPoint d)
    {panelPoints[panelCount++]=a;panelPoints[panelCount++]=b;panelPoints[panelCount++]=d;}
    public void EndPanels()
    {
        // All nine meshes sample one immutable snapshot. Offsetting only UVs
        // keeps the rounded silhouette fixed while averaging the backdrop.
        for(int tap=0;tap<9;tap++)
        {
            float dx=(tap%3-1)*4,dy=(tap/3-1)*4;
            for(int i=0;i<PanelVertices;i++)
            {
                if(i>=panelCount){panelVertices[i]=default;continue;}
                var p=panelPoints[i];
                var uv=new Vector2((offset+(p.X+dx)*scale)/logicalWidth,(p.Y+dy)*scale/logicalHeight);
                panelVertices[i]=new VertexPositionColorTexture(World(p),Color32.White,uv);
            }
            panelMeshes[tap].SetVertices(panelVertices);
        }
    }
    public void CaptureBackdrop()
    {
        draw.End();capture!.Capture(new Viewport(0,0,device.Width,device.Height));draw.Begin();
    }
    public void DrawFrostedPanels()
    {
        if(panelCount==0)return;
        draw.End();renderer.Begin3D(camera);
        for(int tap=0;tap<9;tap++)
        {
            // Incremental normalized average: each pass contributes 1/(tap+1).
            // Pass zero replaces the unblurred row; later taps blend into it.
            frostMaterial.Color=new Color32(255,255,255,(byte)(255/(tap+1)));
            renderer.DrawMesh(panelMeshes[tap],frostMaterial,identity);
        }
        renderer.End3D();draw.Begin();
    }
    public void Triangle(float ax,float ay,float bx,float by,float cx,float cy,uint color)
    {draw.FillTriangle(new Vector2(offset+ax*scale,ay*scale),new Vector2(offset+bx*scale,by*scale),new Vector2(offset+cx*scale,cy*scale),Color(color));}
    public void Text(string value,float x,float y,float size,uint color,float angle)
    {
        float factor=size/28,co=(float)Math.Cos(angle*.017453293f),si=(float)Math.Sin(angle*.017453293f),advance=0;
        for(int i=0;i<value.Length;i++)
        {
            int ch=value[i]-32;if(ch<0||ch>=96)continue;
            draw.DrawTexture(atlas,new RectF(ch%16*32,ch/16*40,32,40),
                new RectF(offset+(x+advance*co)*scale,(y+advance*si)*scale,32*factor*scale,40*factor*scale),Color(color),angle,new Vector2(0,0));
            advance+=IplFontData.Widths[ch]*factor;
        }
    }
    public void Dispose()
    {
        capture?.Dispose();for(int i=0;i<panelMeshes.Length;i++)panelMeshes[i].Dispose();
        glassMesh.Dispose();draw.Dispose();atlas.Dispose();
    }
}
