using System;
using Dolphin.Graphics;
using Dolphin.Timing;

internal sealed partial class GameCubeCanvas : IIplCanvas, IIplBrowserCanvas, IDisposable
{
    private const int GlassVertices=25*25, PanelRowVertices=36*3,ThreePanelVertices=3*PanelRowVertices,PanelVertices=4*PanelRowVertices,BlurAxis=9,BlurTaps=BlurAxis*BlurAxis;
    private readonly GraphicsDevice device;
    private readonly Draw2D draw;
    private readonly SealedGlassShell sealedGlass=new SealedGlassShell();
    internal int GlassBlur;
#if IPL_CACHED_SELECTORS
    private readonly IndexedRims[] selectors=new IndexedRims[3];
    private readonly string[] selectorValues=new string[3];
    private readonly float[] selectorCells=new float[3];
    private readonly uint[] selectorColors=new uint[3];
    private int recordingSelector=-1;
#endif
    private readonly Texture2D atlas=Texture2D.FromPng(IplFontData.Png,true);
    private readonly Renderer renderer;
    private readonly CachedFooter footer;
    private readonly CachedFooter browserCards;
    private readonly DynamicMesh browserMask;
    private int browserMaskVertices;
    private readonly UvTransform[] browserBlurOffsets=new UvTransform[9];
    private readonly Color32[] browserBlurColors=new Color32[9];
#if IPL_BATCHED_BLUR
    private readonly MeshPasses browserBlurPasses;
#endif
    private readonly Mesh browserBannerQuad,browserIconQuad;
    private readonly Material browserArtworkMaterial=new Material{Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Transform browserArtworkTransform=new Transform();
    private readonly Texture2D?[] artwork=new Texture2D?[48];
    private readonly int[] artworkCounts=new int[6],artworkFrames=new int[6],artworkDurations=new int[48];
    private readonly bool[] artworkBounces=new bool[6];
    private int artworkRevision=-1,artworkCursor;
#if IPL_CACHED_GRID
    private readonly CachedFooter editorGrid;
#endif
    private readonly IndexedIcons icons=new IndexedIcons();
#if IPL_CACHED_FOOTER_TEXT
    private readonly CachedFooterText footerText;
#endif
#if IPL_BATCHED_TEXT
    private const int GlyphCapacity=16;
    private readonly Texture2D[] glyphTextures=new Texture2D[GlyphCapacity];
    private readonly float[] glyphVertices=new float[GlyphCapacity*24];
#endif
    private readonly Camera camera=new Camera(60,4f/3,.1f,10);
    private readonly Transform identity=new Transform();
#if !IPL_HARDWARE_RUNNER
    private readonly Mesh[] overlayLabels=new Mesh[8];
    private readonly ushort[] overlayIndices=new ushort[156*6];
    private readonly Material overlayMaterial=new Material {Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Transform overlayTransform=new Transform();
    private int overlayMeshCount,overlayVertices;
    private uint overlayColor;
#if IPL_CACHED_NUMBERS
    private readonly Mesh[] numberMeshes=new Mesh[4];
    private readonly int[] numberMeshCounts=new int[2];
    private int numberGroup,numberVertices,numberChunk;
    private uint numberColor;
#endif
#endif
    private readonly Material frostMaterial=new Material {Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly VertexPositionColorTexture[] glass=new VertexPositionColorTexture[GlassVertices];

    private readonly IplPoint[] panelPoints=new IplPoint[PanelVertices];
    private readonly VertexPositionColorTexture[] panelVertices=new VertexPositionColorTexture[PanelVertices];
    private readonly DynamicMesh threePanelMesh,fourPanelMesh;
    private DynamicMesh panelMesh;
#if IPL_RETAINED_TINT
    private readonly MeshColorView threePanelTintMesh,fourPanelTintMesh;
    private MeshColorView panelTintMesh;
    private readonly Color32[] panelTints=new Color32[PanelVertices];
    private readonly Material panelTintMaterial=new Material {Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private int tintRows=-1,tintSelected=-1;
#endif
#if IPL_BATCHED_BLUR
    #if IPL_HYPERCUBE_PASSES
    private readonly Hypercube.GameCube.OrderedMeshPasses threeBlurPasses,fourBlurPasses;
    private Hypercube.GameCube.OrderedMeshPasses blurPasses;
#else
    private readonly MeshPasses threeBlurPasses,fourBlurPasses;
    private MeshPasses blurPasses;
#endif
#endif
    private readonly UvTransform[] blurOffsets=new UvTransform[BlurTaps];
    private readonly Color32[] blurColors=new Color32[BlurTaps];
    private SceneCapture? capture;
    private int panelCount,captureWidth,captureHeight,copyWidth,copyHeight;
    private float scale=1,offset,logicalWidth=640,logicalHeight=480;
    private float blurScaleX,blurScaleY;
    internal GameCubeCanvas(GraphicsDevice device)
    {
        this.device=device;
        renderer=new Renderer(device);
        footer=new CachedFooter(glass);
        browserCards=new CachedFooter(glass,7,24);
#if IPL_CACHED_GRID
        editorGrid=new CachedFooter(glass,1,32);
#endif
#if IPL_CACHED_FOOTER_TEXT
        footerText=new CachedFooterText(glass,atlas);
#endif
        #if IPL_BATCHED_TEXT
        draw=new Draw2D(device,new Draw2DOptions {VertexCapacity=32760,SpriteCapacity=GlyphCapacity});
        for(int i=0;i<GlyphCapacity;i++)glyphTextures[i]=atlas;
#else
        draw=new Draw2D(device,new Draw2DOptions {VertexCapacity=32760});
#endif
        // Both capacities retain uniform white, including unused vertices:
        // native pass validation examines the entire retained vertex stream.
        for(int i=0;i<PanelVertices;i++)panelVertices[i]=new VertexPositionColorTexture(default,Color32.White,default);
        var threePanelIndices=new ushort[ThreePanelVertices];var fourPanelIndices=new ushort[PanelVertices];
        for(int i=0;i<PanelVertices;i++){fourPanelIndices[i]=(ushort)i;if(i<ThreePanelVertices)threePanelIndices[i]=(ushort)i;}
        threePanelMesh=DynamicMesh.Create(panelVertices,threePanelIndices);fourPanelMesh=DynamicMesh.Create(panelVertices,fourPanelIndices);
        panelMesh=threePanelMesh;
        var browserIndices=new ushort[576];for(int i=0;i<browserIndices.Length;i++)browserIndices[i]=(ushort)i;
        browserMask=DynamicMesh.Create(glass,browserIndices);
        browserBannerQuad=BrowserArtworkMesh(8f/142,8f/48);browserIconQuad=BrowserArtworkMesh(8f/48,8f/48);
#if IPL_BATCHED_BLUR
        browserBlurPasses=new MeshPasses(browserMask,9);
#endif
        float browserWeight=0;for(int i=0;i<9;i++){float weight=(i%3==1?2:1)*(i/3==1?2:1);browserWeight+=weight;browserBlurColors[i]=new Color32(255,255,255,(byte)(255*weight/browserWeight+.5f));}
#if IPL_RETAINED_TINT
        threePanelTintMesh=new MeshColorView(threePanelMesh,panelTints);fourPanelTintMesh=new MeshColorView(fourPanelMesh,panelTints);
        panelTintMesh=threePanelTintMesh;
#endif
#if IPL_BATCHED_BLUR
#if IPL_HYPERCUBE_PASSES
        threeBlurPasses=new Hypercube.GameCube.OrderedMeshPasses(threePanelMesh,BlurTaps);
        fourBlurPasses=new Hypercube.GameCube.OrderedMeshPasses(fourPanelMesh,BlurTaps);
#else
        threeBlurPasses=new MeshPasses(threePanelMesh,BlurTaps);fourBlurPasses=new MeshPasses(fourPanelMesh,BlurTaps);
#endif
        blurPasses=threeBlurPasses;
#endif
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
        IplFrameProfiler.ExtraDraws=IplFrameProfiler.ExtraTriangles=0;
        var size=draw.LogicalSize;logicalWidth=size.X;logicalHeight=size.Y;
        scale=size.Y/480f;offset=(size.X-640*scale)/2;
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
            for(int i=0;i<9;i++){browserBlurOffsets[i]=new UvTransform(new Vector2((i%3-1)*4*sx,(i/3-1)*4*sy),new Vector2(1,1));
#if IPL_BATCHED_BLUR
                browserBlurPasses.Set(i,browserBlurColors[i],browserBlurOffsets[i]);
#endif
            }
            for(int tap=0;tap<BlurTaps;tap++){
                blurOffsets[tap]=new UvTransform(new Vector2((tap%BlurAxis-BlurAxis/2)*3*sx,(tap/BlurAxis-BlurAxis/2)*3*sy),new Vector2(1,1));
#if IPL_BATCHED_BLUR
                threeBlurPasses.Set(tap,blurColors[tap],blurOffsets[tap]);fourBlurPasses.Set(tap,blurColors[tap],blurOffsets[tap]);
#endif
            }
        }
    }
    internal void Begin()=>draw.Begin();
    internal void End()=>draw.End();
    public void Section(IplRenderPhase phase)=>IplPerformanceOverlay.Section(phase);
    private static Color32 Color(uint c)=>new Color32((byte)(c>>24),(byte)(c>>16),(byte)(c>>8),(byte)c);
    private Vector3 World(IplPoint p)=>new Vector3((p.X-320)*scale,(240-p.Y)*scale,0);
    public bool PrepareSealedGlass(IplPoint x,IplPoint y,IplPoint z,float sceneScale,float centerY)
    {sealedGlass.Prepare(x,y,z,sceneScale,centerY,logicalWidth,logicalHeight,scale,offset,device.Width,device.Height,GlassBlur);return true;}
    public void BeginGlass() { }
    public void GlassVertex(int index,IplPoint point,uint color,float u,float v,float reflection) { }
    public void EndGlassFace() { }
    public void EndGlass() { }
    public void DrawGlassRear(){draw.End();sealedGlass.DrawRear(device);draw.Begin();}
    public void DrawGlassFront(){draw.End();sealedGlass.DrawFront(device);draw.Begin();}
    public void BeginPanels(){panelCount=0;}
    public void PanelTriangle(IplPoint a,IplPoint b,IplPoint d)
    {panelPoints[panelCount++]=a;panelPoints[panelCount++]=b;panelPoints[panelCount++]=d;}
    public void EndPanels()
    {
        if(panelCount==0)return;
        bool fourRows=panelCount>ThreePanelVertices;
        int activeCount=fourRows?PanelVertices:ThreePanelVertices;
        panelMesh=fourRows?fourPanelMesh:threePanelMesh;
#if IPL_RETAINED_TINT
        panelTintMesh=fourRows?fourPanelTintMesh:threePanelTintMesh;
#endif
#if IPL_BATCHED_BLUR
        blurPasses=fourRows?fourBlurPasses:threeBlurPasses;
#endif
        // Upload the rounded mask once. GX texture coordinates supply each
        // sampling offset, instead of rebuilding 81 copies of identical geometry.
        for(int i=0;i<activeCount;i++)
        {
            if(i>=panelCount){panelVertices[i]=new VertexPositionColorTexture(default,Color32.White,default);continue;}
            var p=panelPoints[i];
            var uv=new Vector2((offset+p.X*scale)/logicalWidth*device.Width/copyWidth,p.Y*scale/logicalHeight*device.Height/copyHeight);
            panelVertices[i]=new VertexPositionColorTexture(World(p),Color32.White,uv);
        }
        panelMesh.SetVertices(panelVertices,count:activeCount);
    }
    // Reuse the already-projected blur mask. Only row/selection changes upload
    // its independent color stream; moving the cube needs no second geometry.
    public void PreparePanelTint(int selected)
    {
#if IPL_RETAINED_TINT
        if(panelCount==0)return;
        int rows=panelCount/PanelRowVertices;
        if(rows==tintRows&&selected==tintSelected)return;
        tintRows=rows;tintSelected=selected;
        for(int i=0;i<PanelVertices;i++)panelTints[i]=Color(i/PanelRowVertices==selected?IplRenderer.SelectedPanelTint:IplRenderer.IdlePanelTint);
        panelTintMesh.SetColors(panelTints);
#endif
    }
    public bool DrawPanelTint()
    {
#if IPL_RETAINED_TINT
        if(panelCount==0)return true;
        draw.End();renderer.Begin3D(camera);renderer.DrawMesh(panelTintMesh,panelTintMaterial,identity);renderer.End3D();draw.Begin();return true;
#else
        return false;
#endif
    }
    public void CaptureBackdrop()
    {
        long start=IplFrameProfiler.Active?Clock.Timestamp:0;
        draw.End();capture!.Capture(new Viewport(0,0,copyWidth,copyHeight));draw.Begin();
        if(IplFrameProfiler.Active)IplFrameProfiler.CaptureUs+=IplFrameProfiler.Since(start);
    }
    public void DrawFrostedPanels()
    {
        if(panelCount==0)return;
        long start=IplFrameProfiler.Active?Clock.Timestamp:0;
        draw.End();renderer.Begin3D(camera);
#if IPL_BATCHED_BLUR
#if IPL_HYPERCUBE_PASSES
        blurPasses.Draw(renderer,frostMaterial,identity);
#else
        renderer.DrawMeshPasses(blurPasses,frostMaterial,identity);
#endif
#else
        for(int tap=0;tap<BlurTaps;tap++)
        {
            // Incremental Gaussian average of the immutable captured backdrop.
            // Pass zero replaces the row; later taps normalize by running weight.
            frostMaterial.Color=blurColors[tap];frostMaterial.TextureTransform=blurOffsets[tap];
            renderer.DrawMesh(panelMesh,frostMaterial,identity);
        }
#endif
        renderer.End3D();draw.Begin();
        if(IplFrameProfiler.Active)IplFrameProfiler.BlurUs+=IplFrameProfiler.Since(start);
    }
    public void Triangle(float ax,float ay,float bx,float by,float cx,float cy,uint color)
    {
        if((color&255)==0)return;
        if(browserCards.Recording){browserCards.Triangle(ax,ay,bx,by,cx,cy,color);return;}
        if(footer.Recording){footer.Triangle(ax,ay,bx,by,cx,cy,color);return;}
        if(icons.Recording){icons.Triangle(ax,ay,bx,by,cx,cy,color);return;}
        draw.FillTriangle(new Vector2(offset+ax*scale,ay*scale),new Vector2(offset+bx*scale,by*scale),new Vector2(offset+cx*scale,cy*scale),Color(color));
    }
    public bool BeginSelectorCache(int part,string value,float cell,uint color)
    {
#if IPL_CACHED_SELECTORS
        var mesh=selectors[part];
        if(mesh!=null&&selectorValues[part]==value&&selectorCells[part]==cell&&selectorColors[part]==color)return false;
        if(mesh==null)selectors[part]=mesh=new IndexedRims(1024);
        selectorValues[part]=value;selectorCells[part]=cell;selectorColors[part]=color;
        recordingSelector=part;mesh.Begin();return true;
#else
        return false;
#endif
    }
    public void EndSelectorCache()
    {
#if IPL_CACHED_SELECTORS
        selectors[recordingSelector].End();recordingSelector=-1;
#endif
    }
    public void PrepareSelector(int part,IplSelectorPose pose,float centerX,float centerY)
    {
#if IPL_CACHED_SELECTORS
        selectors[part].PrepareSelector(pose,centerX,centerY,logicalWidth,logicalHeight,scale,offset);
#endif
    }
    public bool DrawSelectorCache(int part)
    {
#if IPL_CACHED_SELECTORS
        if(selectors[part]==null||!selectors[part].Ready)return false;
        draw.End();selectors[part].Draw(device);draw.Begin();return true;
#else
        return false;
#endif
    }
    public bool BeginGridCache()
    {
#if IPL_CACHED_GRID
        editorGrid.Begin(0);return true;
#else
        return false;
#endif
    }
    public void EndGridCache()
    {
#if IPL_CACHED_GRID
        editorGrid.End();
#endif
    }
    public bool DrawGridCache()
    {
#if IPL_CACHED_GRID
        if(!editorGrid.Ready(0))return false;
        draw.End();renderer.Begin3D(camera);editorGrid.Draw(renderer,0,scale);renderer.End3D();draw.Begin();return true;
#else
        return false;
#endif
    }
    public void ResetFooterTextCache()
    {
#if IPL_CACHED_FOOTER_TEXT
        footerText.Reset();
#endif
    }
    public bool BeginFooterTextCache()
    {
#if IPL_CACHED_FOOTER_TEXT
        footerText.Begin();return true;
#else
        return false;
#endif
    }
    public void EndFooterTextCache()
    {
#if IPL_CACHED_FOOTER_TEXT
        footerText.End();
#endif
    }
    public bool DrawFooterTextCache()
    {
#if IPL_CACHED_FOOTER_TEXT
        if(!footerText.Ready)return false;
        draw.End();renderer.Begin3D(camera);footerText.Draw(renderer);renderer.End3D();draw.Begin();return true;
#else
        return false;
#endif
    }
    internal void UpdateArtwork(IplBrowser? browser)
    {
        if(browser?.Compact==true){if(artworkRevision!=-1){for(int i=0;i<artwork.Length;i++){artwork[i]?.Dispose();artwork[i]=null;}artworkRevision=-1;}UpdateStorageArtwork(browser);return;}
        if(storageMode)ReleaseStorageArtwork();
        int revision=browser?.Revision??-1;
        if(revision!=artworkRevision){
            for(int i=0;i<artwork.Length;i++){artwork[i]?.Dispose();artwork[i]=null;}
            Array.Clear(artworkCounts,0,6);Array.Clear(artworkFrames,0,6);artworkRevision=revision;artworkCursor=0;
            if(browser!=null)for(int i=0;i<browser.Count;i++){artworkCounts[i]=browser.ArtworkFrames(i);artworkBounces[i]=browser.ArtworkBounces(i);for(int f=0;f<artworkCounts[i];f++)artworkDurations[i*8+f]=browser.ArtworkDuration(i,f);}
        }
        if(browser==null)return;
        for(int i=0;i<browser.Count;i++){
            int count=artworkCounts[i],period=artworkBounces[i]&&count>1?count*2-2:count,total=0;
            for(int n=0;n<period;n++)total+=artworkDurations[i*8+(n<count?n:period-n)];
            int time=total>0?(int)(browser.AnimationTime*60)%total:0,frame=0;
            if(!browser.ReducedMotion)for(int n=0;n<period;n++){frame=n<count?n:period-n;if(time<artworkDurations[i*8+frame])break;time-=artworkDurations[i*8+frame];}
            artworkFrames[i]=frame;
        }
        if(browser.Busy)return;
        while(artworkCursor<48){
            int cursor=artworkCursor++,slot=cursor%6,frame=cursor/6;
            if(slot>=browser.Count||frame>=artworkCounts[slot])continue;
            var image=browser.Artwork(slot,frame);if(image==null)break;int target=slot*8+frame;
            try{
                if(image.Format==IplArtworkFormat.Rgb5A3){var texture=Texture2D.CreateNative(image.Width,image.Height,NativeTextureFormat.Rgb5A3,true);try{texture.ReplaceNative(image.Bytes);artwork[target]=texture;}catch{texture.Dispose();throw;}}
                else artwork[target]=image.Format==IplArtworkFormat.Png?Texture2D.FromPng(image.Bytes,true):Texture2D.Create(image.Width,image.Height,image.Bytes,true);
            }catch(ArgumentException){}catch(OutOfMemoryException){}
            break; // At most one bounded image read/upload, between frames.
        }
    }
    private Texture2D? ArtworkTexture(int slot)=>slot<0||slot>=6?null:artwork[slot*8+artworkFrames[slot]]??artwork[slot*8];
    private Mesh BrowserArtworkMesh(float rx,float ry)
    {
        int used=0;var center=new IplPoint(.5f,.5f);IplPoint first=default,previous=default;
        for(int corner=0;corner<4;corner++)for(int j=0;j<=8;j++){float a=(180-corner*90-j*90f/8)*.017453293f;float x=corner==0||corner==3?rx:1-rx,y=corner<2?1-ry:ry;var point=new IplPoint(x+(float)Math.Cos(a)*rx,y+(float)Math.Sin(a)*ry);if(corner==0&&j==0)first=point;else{BrowserArtVertex(center,ref used);BrowserArtVertex(previous,ref used);BrowserArtVertex(point,ref used);}previous=point;}
        BrowserArtVertex(center,ref used);BrowserArtVertex(previous,ref used);BrowserArtVertex(first,ref used);
        var indices=new ushort[used];for(int i=0;i<used;i++)indices[i]=(ushort)i;for(int i=used;i<glass.Length;i++)glass[i]=new VertexPositionColorTexture(default,Color32.White,default);return Mesh.Create(glass,indices);
    }
    private void BrowserArtVertex(IplPoint p,ref int at){glass[at++]=new VertexPositionColorTexture(new Vector3(p.X-.5f,.5f-p.Y,0),Color32.White,new Vector2(p.X,p.Y));}
    public bool BrowserColorTriangle(IplPoint a,uint ca,IplPoint b,uint cb,IplPoint d,uint cd)
    {if(!browserCards.Recording)return false;browserCards.ColoredTriangle(a.X,a.Y,b.X,b.Y,d.X,d.Y,ca,cb,cd);return true;}
    public bool HasArtwork(int slot)=>ArtworkTexture(slot)!=null;
    private void DrawRoundedArtwork(Texture2D image,float x,float y,float width,float height)
    {
        float aspect=(float)image.Width/image.Height;if(width/height>aspect){float w=height*aspect;x+=(width-w)/2;width=w;}else{float h=width/aspect;y+=(height-h)/2;height=h;}
        browserArtworkMaterial.Texture=image;browserArtworkTransform.LocalPosition=new Vector3((x+width/2-320)*scale,(240-y-height/2)*scale,0);browserArtworkTransform.LocalScale=new Vector3(width*scale,height*scale,1);
        renderer.DrawMesh(aspect>1.5f?browserBannerQuad:browserIconQuad,browserArtworkMaterial,browserArtworkTransform);
    }
    public void DrawArtworks(){if(storageMode){DrawStorageArtwork();return;}bool began=false;for(int i=0;i<6;i++){var image=ArtworkTexture(i);if(image==null)continue;if(!began){draw.End();renderer.Begin3D(camera);began=true;}DrawRoundedArtwork(image,40+i%3*192+17,100+i/3*146+16,142,48);}if(began){renderer.End3D();draw.Begin();}}
    public bool DrawArtwork(int slot,float x,float y,float width,float height){var image=ArtworkTexture(slot);if(image==null)return false;draw.End();renderer.Begin3D(camera);DrawRoundedArtwork(image,x,y,width,height);renderer.End3D();draw.Begin();return true;}
    public void BeginBrowserMask(){browserMaskVertices=0;}
    private void BrowserMaskVertex(IplPoint p){glass[browserMaskVertices++]=new VertexPositionColorTexture(World(p),Color32.White,new Vector2((offset+p.X*scale)/logicalWidth*device.Width/copyWidth,p.Y*scale/logicalHeight*device.Height/copyHeight));}
    public void BrowserMaskTriangle(IplPoint a,IplPoint b,IplPoint d){BrowserMaskVertex(a);BrowserMaskVertex(b);BrowserMaskVertex(d);}
    public void EndBrowserMask(int expectedVertices=576){if(expectedVertices<3||expectedVertices>576||expectedVertices%3!=0||browserMaskVertices!=expectedVertices)throw new InvalidOperationException("Incomplete browser mask.");for(int i=browserMaskVertices;i<glass.Length;i++)glass[i]=new VertexPositionColorTexture(default,Color32.White,default);browserMask.SetVertices(glass);}
    public void DrawBrowserFrost()
    {
        long began=IplFrameProfiler.Active?Clock.Timestamp:0;draw.End();renderer.Begin3D(camera);
#if IPL_BATCHED_BLUR
        renderer.DrawMeshPasses(browserBlurPasses,frostMaterial,identity);
#else
        for(int i=0;i<9;i++){frostMaterial.Color=browserBlurColors[i];frostMaterial.TextureTransform=browserBlurOffsets[i];renderer.DrawMesh(browserMask,frostMaterial,identity);}
        frostMaterial.Color=Color32.White;frostMaterial.TextureTransform=UvTransform.Identity;
#endif
        renderer.End3D();draw.Begin();if(IplFrameProfiler.Active)IplFrameProfiler.BlurUs+=IplFrameProfiler.Since(began);
    }
    public bool BeginBrowserCache(int style){if(browserCards.Ready(style))return false;browserCards.Begin(style);return true;}
    public void EndBrowserCache()=>browserCards.End();
    public bool DrawBrowserCache(int style){if(!browserCards.Ready(style))return false;draw.End();renderer.Begin3D(camera);browserCards.Draw(renderer,style,scale);renderer.End3D();draw.Begin();return true;}
    public bool BeginFooterCache(int style){footer.Begin(style);return true;}
    public void EndFooterCache()=>footer.End();
    public bool DrawFooterCache(int style)
    {
#if IPL_CACHED_FOOTER_TEXT
        if(footerText.Recording)return true;
#endif
        if(!footer.Ready(style))return false;
        draw.End();renderer.Begin3D(camera);footer.Draw(renderer,style,scale);renderer.End3D();draw.Begin();return true;
    }
    public bool BeginIconCache(int style){icons.Begin(style);return true;}
    public void EndIconCache()=>icons.End();
    public void PrepareIcon(IplPoint origin,IplPoint x,IplPoint y,float cubeScale,float centerY)=>icons.Prepare(origin,x,y,cubeScale,centerY,logicalWidth,logicalHeight,scale,offset);
    public bool DrawIconCache(int style){if(!icons.Ready(style))return false;draw.End();icons.Draw(device,style);draw.Begin();return true;}
    public void Rectangle(float x,float y,float width,float height,uint color)
    {
        if((color&255)==0)return;
        if(browserCards.Recording){browserCards.Triangle(x,y,x+width,y,x+width,y+height,color);browserCards.Triangle(x,y,x+width,y+height,x,y+height,color);return;}
#if IPL_CACHED_SELECTORS
        if(recordingSelector>=0){selectors[recordingSelector].Quad(new IplPoint(x,y),new IplPoint(x+width,y),new IplPoint(x,y+height),new IplPoint(x+width,y+height),color);return;}
#endif
#if IPL_CACHED_GRID
        if(editorGrid.Recording){editorGrid.Triangle(x,y,x+width,y,x+width,y+height,color);editorGrid.Triangle(x,y,x+width,y+height,x,y+height,color);return;}
#endif
        draw.FillRectangle(new RectF(offset+x*scale,y*scale,width*scale,height*scale),Color(color));
    }
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
#if IPL_CACHED_NUMBERS
    internal void BeginNumberGroup(int group)
    {numberGroup=group;numberVertices=numberChunk=0;numberColor=group==0?IplOverlayLayout.TotalColor:IplOverlayLayout.DetailColor;}
    internal void CacheNumber(byte[] text,int length,float right,float y,float size)
    {
        float factor=size/28,width=0;
        for(int i=0;i<length;i++)width+=IplFontData.Widths[text[i]-32]*factor;
        float x=right-width,du=32f/atlas.Width,dv=40f/atlas.Height;var tint=Color(numberColor);
        for(int i=0;i<length;i++){
            if(numberVertices+4>624)FlushNumberGroup();
            int ch=text[i]-32;float u=ch%16*32f/atlas.Width,v=ch/16*40f/atlas.Height;
            float r=x+32*factor,b=y+40*factor;
            glass[numberVertices++]=new VertexPositionColorTexture(new Vector3(x-320,240-y,0),tint,new Vector2(u,v));
            glass[numberVertices++]=new VertexPositionColorTexture(new Vector3(r-320,240-y,0),tint,new Vector2(u+du,v));
            glass[numberVertices++]=new VertexPositionColorTexture(new Vector3(r-320,240-b,0),tint,new Vector2(u+du,v+dv));
            glass[numberVertices++]=new VertexPositionColorTexture(new Vector3(x-320,240-b,0),tint,new Vector2(u,v+dv));
            x+=IplFontData.Widths[ch]*factor;
        }
    }
    private void FlushNumberGroup()
    {
        if(numberVertices==0)return;
        if(numberChunk==2)throw new InvalidOperationException("Number cache capacity exceeded.");
        var tint=Color(numberColor);
        for(int i=numberVertices;i<glass.Length;i++)glass[i]=new VertexPositionColorTexture(default,tint,default);
        var indices=new ushort[numberVertices/4*6];Array.Copy(overlayIndices,indices,indices.Length);
        int slot=numberGroup*2+numberChunk++;var replacement=Mesh.Create(glass,indices);
        numberMeshes[slot]?.Dispose();numberMeshes[slot]=replacement;numberVertices=0;
    }
    internal void EndNumberGroup()
    {
        FlushNumberGroup();
        for(int i=numberChunk;i<numberMeshCounts[numberGroup];i++){int slot=numberGroup*2+i;numberMeshes[slot].Dispose();numberMeshes[slot]=null!;}
        numberMeshCounts[numberGroup]=numberChunk;
    }
    internal void DrawOverlayNumbers()
    {
        draw.End();renderer.Begin3D(camera);overlayTransform.LocalScale=new Vector3(scale,scale,1);
        for(int group=0;group<2;group++)for(int i=0;i<numberMeshCounts[group];i++)renderer.DrawMesh(numberMeshes[group*2+i],overlayMaterial,overlayTransform);
        renderer.End3D();draw.Begin();
    }
#endif
    internal void DrawOverlayLabels()
    {
        draw.End();renderer.Begin3D(camera);
        overlayTransform.LocalScale=new Vector3(scale,scale,1);
        for(int i=0;i<overlayMeshCount;i++)renderer.DrawMesh(overlayLabels[i],overlayMaterial,overlayTransform);
        renderer.End3D();draw.Begin();
    }
#endif
#if IPL_BATCHED_TEXT
    // Six XYUV vertices per glyph, using the SDK's ordered packed-quad path.
    // Small fixed chunks keep the total managed heap below 256 KiB.
    private void Glyph(int index,int ch,float x,float y,float width,float height,float co,float si)
    {
        float u=(ch%16*32f)/atlas.Width,v=(ch/16*40f)/atlas.Height,du=32f/atlas.Width,dv=40f/atlas.Height;
        float rx=x+width*co,ry=y+width*si,bx=x-height*si,by=y+height*co,ex=rx-height*si,ey=ry+height*co;
        int at=index*24;
        GlyphVertex(at,x,y,u,v);GlyphVertex(at+4,rx,ry,u+du,v);GlyphVertex(at+8,ex,ey,u+du,v+dv);
        GlyphVertex(at+12,x,y,u,v);GlyphVertex(at+16,ex,ey,u+du,v+dv);GlyphVertex(at+20,bx,by,u,v+dv);
    }
    private void GlyphVertex(int at,float x,float y,float u,float v)
    {glyphVertices[at]=x;glyphVertices[at+1]=y;glyphVertices[at+2]=u;glyphVertices[at+3]=v;}
    public void Text(string value,float x,float y,float size,uint color,float angle,float stretch=1)
    {
#if IPL_CACHED_FOOTER_TEXT
        if(footerText.Recording){footerText.Add(value,x,y,size,color,angle,stretch,scale);return;}
#endif
        float factor=size/28,co=(float)Math.Cos(angle*.017453293f),si=(float)Math.Sin(angle*.017453293f),advance=0;
        float rotation=angle*.01745329252f,quadCo=angle==0?1:MathF.Cos(rotation),quadSi=angle==0?0:MathF.Sin(rotation);
        var tint=Color(color);int used=0;
        for(int i=0;i<value.Length;i++){
            int ch=value[i]-32;if(ch<0||ch>=96)continue;
            Glyph(used++,ch,offset+(x+advance*co)*scale,(y+advance*si)*scale,32*factor*stretch*scale,40*factor*scale,quadCo,quadSi);
            advance+=IplFontData.Widths[ch]*factor*stretch;
            if(used==GlyphCapacity){draw.DrawTexturedQuads(glyphTextures,glyphVertices,used,tint);used=0;}
        }
        if(used!=0)draw.DrawTexturedQuads(glyphTextures,glyphVertices,used,tint);
    }
    internal void TextAscii(byte[] value,int length,float right,float y,float size,uint color)
    {
        float factor=size/28,width=0;
        for(int i=0;i<length;i++)width+=IplFontData.Widths[value[i]-32]*factor;
        float x=right-width;var tint=Color(color);int used=0;
        for(int i=0;i<length;i++){
            int ch=value[i]-32;Glyph(used++,ch,offset+x*scale,y*scale,32*factor*scale,40*factor*scale,1,0);
            x+=IplFontData.Widths[ch]*factor;
            if(used==GlyphCapacity){draw.DrawTexturedQuads(glyphTextures,glyphVertices,used,tint);used=0;}
        }
        if(used!=0)draw.DrawTexturedQuads(glyphTextures,glyphVertices,used,tint);
    }
#else
    public void Text(string value,float x,float y,float size,uint color,float angle,float stretch=1)
    {
#if IPL_CACHED_FOOTER_TEXT
        if(footerText.Recording){footerText.Add(value,x,y,size,color,angle,stretch,scale);return;}
#endif
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
#endif
    public void Dispose()
    {
#if IPL_CACHED_SELECTORS
        for(int i=0;i<selectors.Length;i++)selectors[i]?.Dispose();
#endif
        ReleaseStorageArtwork();browserMask.Dispose();browserBannerQuad.Dispose();browserIconQuad.Dispose();
        footer.Dispose();browserCards.Dispose();for(int i=0;i<artwork.Length;i++)artwork[i]?.Dispose();icons.Dispose();
#if IPL_CACHED_GRID
        editorGrid.Dispose();
#endif
#if IPL_CACHED_FOOTER_TEXT
        footerText.Dispose();
#endif
#if !IPL_HARDWARE_RUNNER
        for(int i=0;i<overlayMeshCount;i++)overlayLabels[i].Dispose();
#if IPL_CACHED_NUMBERS
        for(int i=0;i<numberMeshes.Length;i++)numberMeshes[i]?.Dispose();
#endif
#endif
        capture?.Dispose();
#if IPL_RETAINED_TINT
        threePanelTintMesh.Dispose();fourPanelTintMesh.Dispose();
#endif
        threePanelMesh.Dispose();fourPanelMesh.Dispose();
        sealedGlass.Dispose();draw.Dispose();atlas.Dispose();
    }
}
