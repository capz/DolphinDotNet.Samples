using System;
using Dolphin.Graphics;
using Dolphin.Timing;

internal sealed class GameCubeCanvas : IIplCanvas, IDisposable
{
    private const int GlassVertices=25*25, PanelVertices=3*36*3,BlurAxis=9,BlurTaps=BlurAxis*BlurAxis;
    private readonly GraphicsDevice device;
    private readonly Draw2D draw;
    private readonly IndexedRims rims=new IndexedRims();
    private readonly Texture2D atlas=Texture2D.FromPng(IplFontData.Png,true);
    private readonly Renderer renderer;
    private readonly Texture2D glossMap=Texture2D.FromPng(IplGlossData.Png,true);
    private readonly GxEffect glossEffect=GxEffect.FromPackage(IplGlossEffect.Package);
    private readonly Material glossMaterial=new Material {Blend=BlendMode.Additive,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Camera camera=new Camera(60,4f/3,.1f,10);
    private readonly Transform identity=new Transform();
#if !IPL_HARDWARE_RUNNER
    private readonly Mesh[] overlayLabels=new Mesh[8];
    private readonly ushort[] overlayIndices=new ushort[156*6];
    private readonly Material overlayMaterial=new Material {Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Transform overlayTransform=new Transform();
    private int overlayMeshCount,overlayVertices;
    private uint overlayColor;
#endif
    private readonly Material glassMaterial=new Material {Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Material frostMaterial=new Material {Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly VertexPositionColorTexture[] glass=new VertexPositionColorTexture[GlassVertices];
    private readonly DynamicMesh[] glassMeshes=new DynamicMesh[6];
    private readonly DynamicMesh[] glossMeshes=new DynamicMesh[6];
    private readonly Color32[] glossColors=new Color32[GlassVertices];
    private readonly IplPoint[] panelPoints=new IplPoint[PanelVertices];
    private readonly VertexPositionColorTexture[] panelVertices=new VertexPositionColorTexture[PanelVertices];
    private readonly DynamicMesh panelMesh;
    private readonly UvTransform[] blurOffsets=new UvTransform[BlurTaps];
    private readonly Color32[] blurColors=new Color32[BlurTaps];
    private SceneCapture? capture;
    private int glassCount,panelCount,captureWidth,captureHeight,copyWidth,copyHeight;
    private float scale=1,offset,logicalWidth=640,logicalHeight=480;
    private float blurScaleX,blurScaleY;
    internal GameCubeCanvas(GraphicsDevice device)
    {
        this.device=device;renderer=new Renderer(device);
        glossMaterial.Texture=glossMap;glossMaterial.Effect=glossEffect;
        draw=new Draw2D(device,new Draw2DOptions {VertexCapacity=32760});
        // One shared 15 KiB textured grid for both transmission and reflection:
        // the published SDK reserves only 256 KiB for the entire managed heap.
        var indices=new ushort[24*24*6];int at=0;
        for(int y=0;y<24;y++)for(int x=0;x<24;x++)
        {
            int a=y*25+x;indices[at++]=(ushort)a;indices[at++]=(ushort)(a+1);indices[at++]=(ushort)(a+25);
            indices[at++]=(ushort)(a+1);indices[at++]=(ushort)(a+26);indices[at++]=(ushort)(a+25);
        }
        for(int i=0;i<6;i++){glassMeshes[i]=DynamicMesh.Create(glass,indices);glossMeshes[i]=DynamicMesh.Create(glass,indices);}
        panelMesh=DynamicMesh.Create(panelVertices);
        // Allocate the optional native material options between frames, once.
        frostMaterial.TextureTransform=UvTransform.Identity;
        float accumulated=0;
        for(int tap=0;tap<BlurTaps;tap++)
        {
            float x=tap%BlurAxis-BlurAxis/2,y=tap/BlurAxis-BlurAxis/2;
            float weight=(float)Math.Exp(-(x*x+y*y)*.0555556f);accumulated+=weight;
            blurColors[tap]=new Color32(255,255,255,(byte)(255*weight/accumulated+.5f));
        }
        camera.LookAt(new Vector3(0,0,1),new Vector3(0,0,0),new Vector3(0,1,0));
#if !IPL_HARDWARE_RUNNER
        // Immutable labels are warmed during setup, using the existing shared
        // vertex grid. No per-toggle allocation or giant managed text buffer.
        for(int i=0;i<156;i++)
        {
            int index=i*6,vertex=i*4;
            overlayIndices[index]=(ushort)vertex;overlayIndices[index+1]=(ushort)(vertex+1);overlayIndices[index+2]=(ushort)(vertex+2);
            overlayIndices[index+3]=(ushort)vertex;overlayIndices[index+4]=(ushort)(vertex+2);overlayIndices[index+5]=(ushort)(vertex+3);
        }
        overlayMaterial.Texture=atlas;
#if IPL_PERFORMANCE_REVIEW
        long overlaySetupStart=Clock.Timestamp;
#endif
        IplPerformanceOverlay.PrepareLabels(this);
#if IPL_PERFORMANCE_REVIEW
        IplFrameProfiler.OverlaySetupUs=IplFrameProfiler.Since(overlaySetupStart);
#endif
#endif
    }
    internal void Prepare()
    {
        var size=draw.LogicalSize;logicalWidth=size.X;logicalHeight=size.Y;
        scale=size.Y/480f;offset=(size.X-640*scale)/2;
        rims.Prepare(size.X,size.Y,scale,offset);
        camera.SetOrthographic(size.X,size.Y,.1f,10);
        if(captureWidth!=device.Width||captureHeight!=device.Height)
        {
            // Hardware AA uses a 242-line EFB at 480 output. Crop its last two
            // lines so the half-size texture and source remain GX-aligned.
            copyWidth=device.Width/8*8;copyHeight=device.Height/8*8;
            var replacement=new SceneCapture(device,copyWidth/2,copyHeight/2,true);
            capture?.Dispose();capture=replacement;
            captureWidth=device.Width;captureHeight=device.Height;frostMaterial.Texture=capture.Texture;
        }
        float sx=scale/logicalWidth*device.Width/copyWidth,sy=scale/logicalHeight*device.Height/copyHeight;
        if(sx!=blurScaleX||sy!=blurScaleY)
        {
            blurScaleX=sx;blurScaleY=sy;
            for(int tap=0;tap<BlurTaps;tap++)
                blurOffsets[tap]=new UvTransform(new Vector2((tap%BlurAxis-BlurAxis/2)*3*sx,(tap/BlurAxis-BlurAxis/2)*3*sy),new Vector2(1,1));
        }
    }
    internal void Begin()=>draw.Begin();
    internal void End()=>draw.End();
    public void Section(IplRenderPhase phase)=>IplPerformanceOverlay.Section(phase);
    private static Color32 Color(uint c)=>new Color32((byte)(c>>24),(byte)(c>>16),(byte)(c>>8),(byte)c);
    private Vector3 World(IplPoint p)=>new Vector3((p.X-320)*scale,(240-p.Y)*scale,0);
    public void BeginGlass(){glassCount=0;}
    public void GlassVertex(int index,IplPoint point,uint color,float u,float v,float reflection)
    {
        glass[index]=new VertexPositionColorTexture(World(point),Color(color),new Vector2(u,v));
        // Store the complete struct: preview.103.1 mislowers direct float-to-byte
        // array stores to an eight-byte store, corrupting adjacent managed data.
        byte strength=(byte)Math.Min(255,(color&255)*reflection*1.35f);
        glossColors[index]=new Color32(strength,strength,strength,255);
    }
    public void EndGlassFace()
    {
        glassMeshes[glassCount].SetVertices(glass);
        for(int i=0;i<GlassVertices;i++)glass[i].Color=glossColors[i];
        glossMeshes[glassCount++].SetVertices(glass);
    }
    public void EndGlass()
    {
        if(glassCount<1||glassCount>3)throw new InvalidOperationException("Incomplete glass geometry.");
    }
    public void DrawGlass()
    {
        draw.End();renderer.Begin3D(camera);
        for(int i=0;i<glassCount;i++)renderer.DrawMesh(glassMeshes[i],glassMaterial,identity);
        renderer.End3D();draw.Begin();
    }
    public void DrawReflections()
    {
        draw.End();renderer.Begin3D(camera);
        for(int i=0;i<glassCount;i++)renderer.DrawMesh(glossMeshes[i],glossMaterial,identity);
        renderer.End3D();draw.Begin();
    }
    public void BeginRims()=>rims.Begin();
    public void RimQuad(IplPoint a,IplPoint b,IplPoint c,IplPoint d,uint color)=>rims.Quad(a,b,c,d,color);
    public void EndRims()=>rims.End();
    public void DrawRims(){draw.End();rims.Draw(device);draw.Begin();}
    public void BeginPanels(){panelCount=0;}
    public void PanelTriangle(IplPoint a,IplPoint b,IplPoint d)
    {panelPoints[panelCount++]=a;panelPoints[panelCount++]=b;panelPoints[panelCount++]=d;}
    public void EndPanels()
    {
        if(panelCount==0)return;
        // Upload the rounded mask once. GX texture coordinates supply each
        // sampling offset, instead of rebuilding 81 copies of identical geometry.
        for(int i=0;i<PanelVertices;i++)
        {
            if(i>=panelCount){panelVertices[i]=default;continue;}
            var p=panelPoints[i];
            var uv=new Vector2((offset+p.X*scale)/logicalWidth*device.Width/copyWidth,p.Y*scale/logicalHeight*device.Height/copyHeight);
            panelVertices[i]=new VertexPositionColorTexture(World(p),Color32.White,uv);
        }
        panelMesh.SetVertices(panelVertices);
    }
    public void CaptureBackdrop()
    {
        long start=IplFrameProfiler.Active?Clock.Timestamp:0;
        draw.End();capture!.Capture(new Viewport(0,0,copyWidth,copyHeight));draw.Begin();
        if(IplFrameProfiler.Active)IplFrameProfiler.CaptureUs=IplFrameProfiler.Since(start);
    }
    public void DrawFrostedPanels()
    {
        if(panelCount==0)return;
        long start=IplFrameProfiler.Active?Clock.Timestamp:0;
        draw.End();renderer.Begin3D(camera);
        for(int tap=0;tap<BlurTaps;tap++)
        {
            // Incremental Gaussian average of the immutable captured backdrop.
            // Pass zero replaces the row; later taps normalize by running weight.
            frostMaterial.Color=blurColors[tap];frostMaterial.TextureTransform=blurOffsets[tap];
            renderer.DrawMesh(panelMesh,frostMaterial,identity);
        }
        renderer.End3D();draw.Begin();
        if(IplFrameProfiler.Active)IplFrameProfiler.BlurUs=IplFrameProfiler.Since(start);
    }
    public void Triangle(float ax,float ay,float bx,float by,float cx,float cy,uint color)
    {if((color&255)!=0)draw.FillTriangle(new Vector2(offset+ax*scale,ay*scale),new Vector2(offset+bx*scale,by*scale),new Vector2(offset+cx*scale,cy*scale),Color(color));}
    public void Rectangle(float x,float y,float width,float height,uint color)
    {if((color&255)!=0)draw.FillRectangle(new RectF(offset+x*scale,y*scale,width*scale,height*scale),Color(color));}
#if !IPL_HARDWARE_RUNNER
    internal void OverlayLabel(string text,float x,float y,float size,uint color)
    {
        if(overlayVertices>0&&color!=overlayColor)FlushOverlayLabels();
        overlayColor=color;float factor=size/28,advance=0,du=32f/atlas.Width,dv=40f/atlas.Height;
        for(int i=0;i<text.Length;i++)
        {
            int ch=text[i]-32;if(ch<0||ch>=96)continue;
            if(overlayVertices+4>624)FlushOverlayLabels();
            float left=x+advance,right=left+32*factor,bottom=y+40*factor;
            float u=ch%16*32f/atlas.Width,v=ch/16*40f/atlas.Height;var tint=Color(color);
            glass[overlayVertices++]=new VertexPositionColorTexture(new Vector3(left-320,240-y,0),tint,new Vector2(u,v));
            glass[overlayVertices++]=new VertexPositionColorTexture(new Vector3(right-320,240-y,0),tint,new Vector2(u+du,v));
            glass[overlayVertices++]=new VertexPositionColorTexture(new Vector3(right-320,240-bottom,0),tint,new Vector2(u+du,v+dv));
            glass[overlayVertices++]=new VertexPositionColorTexture(new Vector3(left-320,240-bottom,0),tint,new Vector2(u,v+dv));
            advance+=IplFontData.Widths[ch]*factor;
        }
    }
    internal void FlushOverlayLabels()
    {
        if(overlayVertices==0)return;
        if(overlayMeshCount==overlayLabels.Length)throw new InvalidOperationException("Overlay label capacity exceeded.");
        // Uniform colors enable the SDK's fast indexed glyph path, including
        // unused staging vertices. Copy a short index prefix only during setup
        // so replay transforms exactly the real glyphs, with no padded draws.
        var tint=Color(overlayColor);
        for(int i=overlayVertices;i<glass.Length;i++)glass[i]=new VertexPositionColorTexture(default,tint,default);
        int count=overlayVertices/4*6;var indices=overlayIndices;
        if(count!=indices.Length){indices=new ushort[count];Array.Copy(overlayIndices,indices,count);}
        overlayLabels[overlayMeshCount++]=Mesh.Create(glass,indices);overlayVertices=0;
    }
    internal void DrawOverlayLabels()
    {
        draw.End();renderer.Begin3D(camera);
        overlayTransform.LocalScale=new Vector3(scale,scale,1);
        for(int i=0;i<overlayMeshCount;i++)renderer.DrawMesh(overlayLabels[i],overlayMaterial,overlayTransform);
        renderer.End3D();draw.Begin();
    }
#endif
    public void Text(string value,float x,float y,float size,uint color,float angle,float stretch=1)
    {
        float factor=size/28,co=(float)Math.Cos(angle*.017453293f),si=(float)Math.Sin(angle*.017453293f),advance=0;
        for(int i=0;i<value.Length;i++)
        {
            int ch=value[i]-32;if(ch<0||ch>=96)continue;
            draw.DrawTexture(atlas,new RectF(ch%16*32,ch/16*40,32,40),
                new RectF(offset+(x+advance*co)*scale,(y+advance*si)*scale,32*factor*stretch*scale,40*factor*scale),Color(color),angle,new Vector2(0,0));
            advance+=IplFontData.Widths[ch]*factor*stretch;
        }
    }
    internal void TextAscii(byte[] value,int length,float right,float y,float size,uint color)
    {
        float factor=size/28,width=0;
        for(int i=0;i<length;i++)width+=IplFontData.Widths[value[i]-32]*factor;
        float x=right-width;
        for(int i=0;i<length;i++)
        {
            int ch=value[i]-32;
            draw.DrawTexture(atlas,new RectF(ch%16*32,ch/16*40,32,40),
                new RectF(offset+x*scale,y*scale,32*factor*scale,40*factor*scale),Color(color),0,new Vector2(0,0));
            x+=IplFontData.Widths[ch]*factor;
        }
    }
    public void Dispose()
    {
        rims.Dispose();
#if !IPL_HARDWARE_RUNNER
        for(int i=0;i<overlayMeshCount;i++)overlayLabels[i].Dispose();
#endif
        capture?.Dispose();panelMesh.Dispose();
        for(int i=0;i<glassMeshes.Length;i++){glassMeshes[i].Dispose();glossMeshes[i].Dispose();}draw.Dispose();atlas.Dispose();glossMap.Dispose();glossEffect.Dispose();
    }
}
