using System;
using Dolphin.Graphics;
using Dolphin.Graphics.Raw;
using Dolphin.Timing;
using static Dolphin.Graphics.Raw.GXConstants;

// One static native indexed object. No per-frame vertex uploads, per-face frames
// or managed triangle arrays. Outer and cavity interfaces share one vertex store.
internal sealed class SealedGlassShell : IDisposable
{
#if IPL_OPTICS_DIAGNOSTIC
    // Read-only render probes in an explicitly selected native review build.
    internal static int DiagnosticMode;
#endif
    private readonly NativeBuffer vertices=NativeBuffer.Allocate(IplSealedGlassGeometry.Vertices*24);
    private readonly NativeBuffer outer,inner;
    private readonly NativeBuffer viewOffsets=NativeBuffer.Allocate(128*128*4),offsetTexture=NativeBuffer.Allocate(32),positionMatrix=NativeBuffer.Allocate(48);
    private readonly float[] positionTransform=new float[12];
    private readonly NativeBuffer indirectMatrix=NativeBuffer.Allocate(32);
    private const int ReflectionSize=256;
    private readonly NativeBuffer environment=NativeBuffer.Allocate(ReflectionSize*ReflectionSize*4),texture=NativeBuffer.Allocate(32);
    private readonly NativeBuffer rearEnvironment=NativeBuffer.Allocate(ReflectionSize*ReflectionSize*4);
    private readonly NativeBuffer innerEnvironment=NativeBuffer.Allocate(ReflectionSize*ReflectionSize*4);
    private readonly NativeBuffer normalMatrix=NativeBuffer.Allocate(48);
    private readonly NativeBuffer projectionBuffer=NativeBuffer.Allocate(64),modelBuffer=NativeBuffer.Allocate(48),textureMatrix=NativeBuffer.Allocate(48);
    private readonly float[] projection=new float[16],model=new float[12],texMatrix=new float[12];
    private readonly NativeBuffer reversedTextureMatrix=NativeBuffer.Allocate(48);
    private readonly float[] reversedTexMatrix=new float[12];
    private readonly NativeBuffer fresnel=NativeBuffer.Allocate(128*128*4),fresnelTexture=NativeBuffer.Allocate(32),fresnelMatrix=NativeBuffer.Allocate(48);
    private readonly NativeBuffer innerFresnelMatrix=NativeBuffer.Allocate(48);
    private readonly float[] innerFresnelTransform=new float[12];
    private readonly NativeBuffer rearImage=NativeBuffer.Allocate(640*576*4),rearTexture=NativeBuffer.Allocate(32),screenMatrix=NativeBuffer.Allocate(48),refractionMatrix=NativeBuffer.Allocate(32);
    private readonly float[] fresnelTransform=new float[12],screenTransform=new float[12],refraction=new float[6];
    private readonly NativeBuffer bendImage=NativeBuffer.Allocate(128*128*4),bendTexture=NativeBuffer.Allocate(32),bendMatrix=NativeBuffer.Allocate(48),echoMatrix=NativeBuffer.Allocate(32);
    private readonly float[] bendTransform=new float[12],echoTransform=new float[6];
    private readonly NativeBuffer fieldImage=NativeBuffer.Allocate(128*128*4),fieldTexture=NativeBuffer.Allocate(32);
    private readonly NativeBuffer reflectionPositionMatrix=NativeBuffer.Allocate(48);
    private readonly float[] reflectionPosition=new float[12];
    private int copyWidth,copyHeight;
    private InnerGlassBlur? innerBlur;
    private int blurLevel;
    private readonly int commandBytes=(3+IplSealedGlassGeometry.Indices*4+31)/32*32;
    internal SealedGlassShell()
    {
        outer=NativeBuffer.Allocate(commandBytes);inner=NativeBuffer.Allocate(commandBytes);
        var indirect=new float[6];indirect[0]=.125f*(ReflectionSize/128f);indirect[4]=.25f*(ReflectionSize/128f);indirectMatrix.Write(indirect);indirectMatrix.Flush();
        var identity=new float[12];identity[0]=identity[5]=identity[10]=1;normalMatrix.Write(identity);normalMatrix.Flush();
        var stage=new float[192];int used=0,offset=0;
        for(int i=0;i<IplSealedGlassGeometry.Vertices;i++)
        {
            IplSealedGlassGeometry.Vertex(i,out var p,out var n);
            stage[used++]=p.X;stage[used++]=p.Y;stage[used++]=p.Z;
            stage[used++]=n.X;stage[used++]=n.Y;stage[used++]=n.Z;
            if(used==stage.Length||i==IplSealedGlassGeometry.Vertices-1){vertices.Write(stage,0,offset,used);offset+=used*4;used=0;}
        }
        vertices.Flush();BuildCommands(outer,false);BuildCommands(inner,true);BuildEnvironment();BuildField();BuildViewOffsets();BuildFresnel();BuildRefraction();rearImage.Flush();
    }
    private void BuildCommands(NativeBuffer target,bool cavity)
    {
        var stage=new byte[4096];int used=3,offset=0;
        stage[0]=0x90;stage[1]=(byte)(IplSealedGlassGeometry.Indices>>8);stage[2]=(byte)(IplSealedGlassGeometry.Indices&255);
        for(int f=0;f<6;f++)for(int y=0;y<24;y++)for(int x=0;x<24;x++)for(int v=0;v<6;v++)
        {
            int index=IplSealedGlassGeometry.NativeTriangleIndex(f,x,y,v,cavity);
            for(int stream=0;stream<2;stream++)
            {
                stage[used++]=(byte)(index>>8);if(used==stage.Length){target.Write(stage,0,offset,used);offset+=used;used=0;}
                stage[used++]=(byte)index;if(used==stage.Length){target.Write(stage,0,offset,used);offset+=used;used=0;}
            }
        }
        while(offset+used<commandBytes){stage[used++]=0;if(used==stage.Length){target.Write(stage,0,offset,used);offset+=used;used=0;}}
        if(used>0)target.Write(stage,0,offset,used);target.Flush();
    }
    private void BuildEnvironment()
    {
        // Tiled GX RGBA8; profiled studio returns, not detached glowing edge strips.
        var tile=new byte[64];
        for(int bank=0;bank<3;bank++)for(int ty=0;ty<ReflectionSize/4;ty++)for(int tx=0;tx<ReflectionSize/4;tx++)
        {
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                int px=tx*4+x,py=ty*4+y,at=(y*4+x)*2;
#if IPL_OPTICS_DIAGNOSTIC
                int red=px*255/(ReflectionSize-1),green=py*255/(ReflectionSize-1);
                uint color=((uint)red<<24)|((uint)green<<16)|(128u<<8)|255u;
#else
                uint color=IplGlassEnvironment.Reflection(px/(float)(ReflectionSize-1),py/(float)(ReflectionSize-1),bank!=0,bank==2);
#endif
                tile[at]=(byte)color;tile[at+1]=(byte)(color>>24);tile[32+at]=(byte)(color>>16);tile[33+at]=(byte)(color>>8);
            }
            (bank==0?rearEnvironment:bank==1?environment:innerEnvironment).Write(tile,0,(ty*(ReflectionSize/4)+tx)*64);
        }
        environment.Flush();rearEnvironment.Flush();innerEnvironment.Flush();
    }
    private void BuildField()
    {
        var tile=new byte[64];
        for(int ty=0;ty<32;ty++)for(int tx=0;tx<32;tx++)
        {
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
#if IPL_OPTICS_DIAGNOSTIC
                int red=(tx*4+x)*255/127,green=(ty*4+y)*255/127;
                uint color=((uint)red<<24)|((uint)green<<16)|(128u<<8)|255u;
#else
                uint color=IplGlassEnvironment.Field((tx*4+x-63.5f)/32,(ty*4+y-63.5f)/32);
#endif
                int at=(y*4+x)*2;tile[at]=255;tile[at+1]=(byte)(color>>24);tile[32+at]=(byte)(color>>16);tile[33+at]=(byte)(color>>8);
            }
            fieldImage.Write(tile,0,(ty*32+tx)*64);
        }
        fieldImage.Flush();
    }
    private void BuildViewOffsets()
    {
        var tile=new byte[64];
        for(int ty=0;ty<32;ty++)for(int tx=0;tx<32;tx++)
        {
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                int px=tx*4+x,py=ty*4+y,at=(y*4+x)*2;
                // Indirect GX offsets are read in A,B,G order (S,T,U).
                // Biasing around 128 gives continuous signed view-position offsets.
                tile[at]=(byte)(px*2);tile[at+1]=128;tile[32+at]=128;tile[33+at]=(byte)(py*2);
            }
            viewOffsets.Write(tile,0,(ty*32+tx)*64);
        }
        viewOffsets.Flush();
    }
    private void BuildFresnel()
    {
        var tile=new byte[64];
        for(int ty=0;ty<32;ty++)for(int tx=0;tx<32;tx++)
        {
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                float cosine=Math.Abs((tx*4+x-63.5f)/63.5f);
                float r=IplGlassOptics.Reflectance(cosine,IplGlassOptics.RefractiveIndex);
                int py=ty*4+y;
                // The upper bank retains physical R^2 and the outer opacity.
                // The lower bank stores bounded internal-return radiance in
                // RGB, while alpha keeps that same outer opacity response.
                float insideCosine=((py+.5f)/128-.755f)/.235f;
                float radiance=py<64?r*r:IplGlassOptics.BoundedInternalReturn(cosine,insideCosine,IplGlassOptics.RefractiveIndex);
                int alpha=(int)(255*r),weight=(int)(255*radiance);
                int at=(y*4+x)*2;tile[at]=(byte)alpha;tile[at+1]=(byte)weight;tile[32+at]=(byte)weight;tile[33+at]=(byte)weight;
            }
            fresnel.Write(tile,0,(ty*32+tx)*64);
        }
        fresnel.Flush();
    }
    private void BuildRefraction()
    {
        var tile=new byte[64];
        for(int ty=0;ty<32;ty++)for(int tx=0;tx<32;tx++)
        {
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                float nx=(tx*4+x-63.5f)/63.5f,ny=(ty*4+y-63.5f)/63.5f;
                IplGlassOptics.WallOffset(nx,ny,1,IplGlassOptics.RefractiveIndex,out float dx,out float dy);
                int sx=128+(int)(127*dx+(dx>=0?.5f:-.5f)),sy=128+(int)(127*dy+(dy>=0?.5f:-.5f));
                int at=(y*4+x)*2;tile[at]=(byte)sx;tile[at+1]=128;tile[32+at]=128;tile[33+at]=(byte)sy;
            }
            bendImage.Write(tile,0,(ty*32+tx)*64);
        }
        bendImage.Flush();
    }
    internal void Prepare(IplPoint x,IplPoint y,IplPoint z,float sceneScale,float centerY,float width,float height,float canvasScale,float offset,int physicalWidth,int physicalHeight,int glassBlur=0)
    {
        float sx=(IplCamera.CenterX*canvasScale+offset),sy=centerY*canvasScale,scale=sceneScale*canvasScale;
        projection[0]=2*IplCamera.Distance*scale*IplCamera.HorizontalScale/width;projection[2]=1-2*sx/width;
        projection[5]=2*IplCamera.Distance*scale/height;projection[6]=2*sy/height-1;
        projection[11]=-1;projection[14]=-1;
        model[0]=x.X;model[1]=y.X;model[2]=z.X;model[3]=0;
        model[4]=x.Y;model[5]=y.Y;model[6]=z.Y;model[7]=0;
        model[8]=x.Z;model[9]=y.Z;model[10]=z.Z;model[11]=-IplCamera.Distance;
        copyWidth=physicalWidth/4*4;copyHeight=physicalHeight/4*4;
        if(copyWidth>640||copyHeight>576)throw new InvalidOperationException("Glass capture exceeds the demo's video modes.");
        // Normal-based environment coordinates rotate with the entire shell.
        texMatrix[0]=x.X*.35f;texMatrix[1]=y.X*.35f;texMatrix[2]=z.X*.35f;texMatrix[3]=.5f;
        texMatrix[4]=-x.Y*.45f;texMatrix[5]=-y.Y*.45f;texMatrix[6]=-z.Y*.45f;texMatrix[7]=.5f;
        // Reflection is invariant under N -> -N. Each interface's cull mode
        // chooses its visible hemisphere; reverse normal coefficients only,
        // preserving the environment center and the independent depth offset.
        reversedTexMatrix[0]=-texMatrix[0];reversedTexMatrix[1]=-texMatrix[1];reversedTexMatrix[2]=-texMatrix[2];reversedTexMatrix[3]=.5f;
        reversedTexMatrix[4]=-texMatrix[4];reversedTexMatrix[5]=-texMatrix[5];reversedTexMatrix[6]=-texMatrix[6];reversedTexMatrix[7]=.5f;
        reversedTextureMatrix.Write(reversedTexMatrix);reversedTextureMatrix.Flush();
        positionTransform[0]=.25f*x.X;positionTransform[1]=.25f*y.X;positionTransform[2]=.25f*z.X;positionTransform[3]=.5f;
        // Oppose normal-Y in the reflection lookup: a top wall must not be
        // pushed entirely into the long light strip or the clamped image border.
        positionTransform[4]=.25f*x.Y;positionTransform[5]=.25f*y.Y;positionTransform[6]=.25f*z.Y;positionTransform[7]=.5f;
        // Include optical depth: grazing flat walls must cross narrow light
        // sources instead of sampling one white band across their whole area.
        reflectionPosition[0]=.18f*x.X+.28f*x.Z;reflectionPosition[1]=.18f*y.X+.28f*y.Z;reflectionPosition[2]=.18f*z.X+.28f*z.Z;reflectionPosition[3]=.5f;
        reflectionPosition[4]=.18f*x.Y+.28f*x.Z;reflectionPosition[5]=.18f*y.Y+.28f*y.Z;reflectionPosition[6]=.18f*z.Y+.28f*z.Z;reflectionPosition[7]=.5f;
        reflectionPositionMatrix.Write(reflectionPosition);reflectionPositionMatrix.Flush();
        // Direct view-normal Z avoids reconstructing it from a sampled unit disk.
        fresnelTransform[0]=x.Z*.5f;fresnelTransform[1]=y.Z*.5f;fresnelTransform[2]=z.Z*.5f;fresnelTransform[3]=.5f;
        fresnelTransform[7]=.25f;
        // Approximate the incident path through the view-center entry point.
        // Separate view/internal cosines preserve opacity while curved cavity
        // normals can collect a stronger internal reflected light return.
        IplPoint localEntry=IplGlassOptics.EntryNormal(new IplPoint(x.Z,y.Z,z.Z),IplSealedGlassGeometry.Core,IplSealedGlassGeometry.Radius);
        IplPoint entry=new IplPoint(x.X*localEntry.X+y.X*localEntry.Y+z.X*localEntry.Z,x.Y*localEntry.X+y.Y*localEntry.Y+z.Y*localEntry.Z,x.Z*localEntry.X+y.Z*localEntry.Y+z.Z*localEntry.Z);
        IplPoint transmitted=IplGlassOptics.TransmittedViewRay(entry,IplGlassOptics.RefractiveIndex);
        innerFresnelTransform[0]=fresnelTransform[0];innerFresnelTransform[1]=fresnelTransform[1];innerFresnelTransform[2]=fresnelTransform[2];innerFresnelTransform[3]=.5f;
        innerFresnelTransform[4]=.235f*(transmitted.X*x.X+transmitted.Y*x.Y+transmitted.Z*x.Z);
        innerFresnelTransform[5]=.235f*(transmitted.X*y.X+transmitted.Y*y.Y+transmitted.Z*y.Z);
        innerFresnelTransform[6]=.235f*(transmitted.X*z.X+transmitted.Y*z.Y+transmitted.Z*z.Z);innerFresnelTransform[7]=.755f;
        innerFresnelMatrix.Write(innerFresnelTransform);innerFresnelMatrix.Flush();
        float cx=sx/width,cy=sy/height,fx=IplCamera.Distance*scale*IplCamera.HorizontalScale/width,fy=IplCamera.Distance*scale/height;
        float cropX=physicalWidth/(float)copyWidth,cropY=physicalHeight/(float)copyHeight;
        screenTransform[0]=(fx*x.X-cx*x.Z)*cropX;screenTransform[1]=(fx*y.X-cx*y.Z)*cropX;screenTransform[2]=(fx*z.X-cx*z.Z)*cropX;screenTransform[3]=IplCamera.Distance*cx*cropX;
        screenTransform[4]=(-fy*x.Y-cy*x.Z)*cropY;screenTransform[5]=(-fy*y.Y-cy*y.Z)*cropY;screenTransform[6]=(-fy*z.Y-cy*z.Z)*cropY;screenTransform[7]=IplCamera.Distance*cy*cropY;
        screenTransform[8]=-x.Z;screenTransform[9]=-y.Z;screenTransform[10]=-z.Z;screenTransform[11]=IplCamera.Distance;
        bendTransform[0]=x.X*.5f;bendTransform[1]=y.X*.5f;bendTransform[2]=z.X*.5f;bendTransform[3]=.5f;
        bendTransform[4]=-x.Y*.5f;bendTransform[5]=-y.Y*.5f;bendTransform[6]=-z.Y*.5f;bendTransform[7]=.5f;
        float wallPixels=IplSealedGlassGeometry.Thickness*IplCamera.Distance*scale/(IplCamera.Distance-(1-IplSealedGlassGeometry.Thickness));
        refraction[0]=wallPixels*IplCamera.HorizontalScale*physicalWidth/width/127;refraction[4]=wallPixels*physicalHeight/height/127;
        // The secondary transmitted path makes one additional internal round trip.
        echoTransform[0]=3*refraction[0];echoTransform[4]=3*refraction[4];
        bendMatrix.Write(bendTransform);echoMatrix.Write(echoTransform);bendMatrix.Flush();echoMatrix.Flush();
        fresnelMatrix.Write(fresnelTransform);screenMatrix.Write(screenTransform);refractionMatrix.Write(refraction);
        fresnelMatrix.Flush();screenMatrix.Flush();refractionMatrix.Flush();
        projectionBuffer.Write(projection);modelBuffer.Write(model);textureMatrix.Write(texMatrix);positionMatrix.Write(positionTransform);
        projectionBuffer.Flush();modelBuffer.Flush();textureMatrix.Flush();positionMatrix.Flush();
        blurLevel=glassBlur;
        if(blurLevel!=0)
        {
            // Native allocation and matrix uploads happen between frames.
            if(innerBlur==null)innerBlur=new InnerGlassBlur();
            innerBlur.Prepare(blurLevel,screenTransform,canvasScale/width*cropX,canvasScale/height*cropY,copyWidth,copyHeight);
        }
    }
    private void BeginInterfacePass(GraphicsDevice device,bool front)
    {
        device.BeginRaw();
        GX.GX_SetViewport(0,0,device.Width,device.Height,0,1);GX.GX_SetScissor(0,0,(uint)device.Width,(uint)device.Height);
        GX.GX_LoadProjectionMtx(projectionBuffer.Address,GX_PERSPECTIVE);GX.GX_LoadPosMtxImm(modelBuffer.Address,GX_PNMTX0);GX.GX_SetCurrentMtx(GX_PNMTX0);
        GX.GX_LoadNrmMtxImm(normalMatrix.Address,GX_PNMTX0);
        GX.GX_ClearVtxDesc();GX.GX_SetVtxDesc(GX_VA_POS,GX_INDEX16);GX.GX_SetVtxDesc(GX_VA_NRM,GX_INDEX16);
        GX.GX_SetVtxAttrFmt(0,GX_VA_POS,GX_POS_XYZ,GX_F32,0);GX.GX_SetVtxAttrFmt(0,GX_VA_NRM,GX_NRM_XYZ,GX_F32,0);
        GX.GX_SetArray(GX_VA_POS,vertices.Address,24);GX.GX_SetArray(GX_VA_NRM,vertices.Address.Add(12),24);
        GX.GX_LoadTexMtxImm(textureMatrix.Address,GX_TEXMTX0,GX_MTX2x4);
        GX.GX_SetNumChans(0);GX.GX_SetNumTexGens(6);GX.GX_SetTexCoordGen(GX_TEXCOORD0,GX_TG_MTX2x4,GX_TG_NRM,GX_TEXMTX0);
        GX.GX_InitTexObj(texture.Address,(front?innerEnvironment:rearEnvironment).Address,ReflectionSize,ReflectionSize,GX_TF_RGBA8,GX_CLAMP,GX_CLAMP,0);
        GX.GX_InitTexObjFilterMode(texture.Address,GX_LINEAR,GX_LINEAR);GX.GX_LoadTexObj(texture.Address,GX_TEXMAP0);
        GX.GX_LoadTexMtxImm(positionMatrix.Address,GX_TEXMTX1,GX_MTX2x4);
        GX.GX_SetTexCoordGen(GX_TEXCOORD1,GX_TG_MTX2x4,GX_TG_POS,GX_TEXMTX1);
        GX.GX_LoadTexMtxImm(reflectionPositionMatrix.Address,GX_TEXMTX5,GX_MTX2x4);
        GX.GX_SetTexCoordGen(GX_TEXCOORD5,GX_TG_MTX2x4,GX_TG_POS,GX_TEXMTX5);
        GX.GX_SetTexCoordScaleManually(5,1,128,128);
        GX.GX_InitTexObj(offsetTexture.Address,viewOffsets.Address,128,128,GX_TF_RGBA8,GX_CLAMP,GX_CLAMP,0);
        GX.GX_InitTexObjFilterMode(offsetTexture.Address,GX_LINEAR,GX_LINEAR);GX.GX_LoadTexObj(offsetTexture.Address,GX_TEXMAP1);
        GX.GX_LoadTexMtxImm(fresnelMatrix.Address,GX_TEXMTX2,GX_MTX2x4);
        GX.GX_SetTexCoordGen(GX_TEXCOORD2,GX_TG_MTX2x4,GX_TG_NRM,GX_TEXMTX2);
        GX.GX_LoadTexMtxImm(bendMatrix.Address,GX_TEXMTX4,GX_MTX2x4);
        GX.GX_SetTexCoordGen(GX_TEXCOORD4,GX_TG_MTX2x4,GX_TG_NRM,GX_TEXMTX4);
        GX.GX_InitTexObj(bendTexture.Address,bendImage.Address,128,128,GX_TF_RGBA8,GX_CLAMP,GX_CLAMP,0);
        GX.GX_InitTexObjFilterMode(bendTexture.Address,GX_LINEAR,GX_LINEAR);GX.GX_LoadTexObj(bendTexture.Address,GX_TEXMAP4);
        GX.GX_SetTexCoordScaleManually(0,1,ReflectionSize,ReflectionSize);GX.GX_SetTexCoordScaleManually(1,1,128,128);
        GX.GX_SetTexCoordScaleManually(2,1,128,128);GX.GX_SetTexCoordScaleManually(4,1,128,128);
        GX.GX_LoadTexMtxImm(screenMatrix.Address,GX_TEXMTX3,GX_MTX3x4);
        GX.GX_SetTexCoordGen(GX_TEXCOORD3,GX_TG_MTX3x4,GX_TG_POS,GX_TEXMTX3);
        GX.GX_InitTexObj(fieldTexture.Address,fieldImage.Address,128,128,GX_TF_RGBA8,GX_CLAMP,GX_CLAMP,0);
        GX.GX_InitTexObjFilterMode(fieldTexture.Address,GX_LINEAR,GX_LINEAR);GX.GX_LoadTexObj(fieldTexture.Address,GX_TEXMAP5);
        GX.GX_InitTexObj(fresnelTexture.Address,fresnel.Address,128,128,GX_TF_RGBA8,GX_CLAMP,GX_CLAMP,0);
        GX.GX_InitTexObjFilterMode(fresnelTexture.Address,GX_LINEAR,GX_LINEAR);GX.GX_LoadTexObj(fresnelTexture.Address,GX_TEXMAP2);
        GX.GX_SetNumTevStages(2);GX.GX_SetNumIndStages(1);
        GX.GX_SetIndTexOrder(0,GX_TEXCOORD5,GX_TEXMAP1);GX.GX_SetIndTexCoordScale(0,GX_ITS_1,GX_ITS_1);
        GX.GX_SetIndTexMatrix(GX_ITM_0,indirectMatrix.Address,0);
        GX.GX_SetTevSwapModeTable(GX_TEV_SWAP0,GX_CH_RED,GX_CH_GREEN,GX_CH_BLUE,GX_CH_ALPHA);
        GX.GX_SetTevSwapMode(0,GX_TEV_SWAP0,GX_TEV_SWAP0);GX.GX_SetTevSwapMode(1,GX_TEV_SWAP0,GX_TEV_SWAP0);GX.GX_SetTevSwapMode(2,GX_TEV_SWAP0,GX_TEV_SWAP0);
        GX.GX_SetTevIndirect(0,0,GX_ITF_8,GX_ITB_ST,GX_ITM_0,GX_ITW_OFF,GX_ITW_OFF,0,0,GX_ITBA_OFF);
        GX.GX_SetTevOrder(0,GX_TEXCOORD0,GX_TEXMAP0,GX_COLORNULL);
        GX.GX_SetTevKColorSel(0,GX_TEV_KCSEL_K0);GX.GX_SetTevKAlphaSel(0,GX_TEV_KASEL_K0_A);
        GX.GX_SetTevColorIn(0,GX_CC_ZERO,GX_CC_TEXC,GX_CC_KONST,GX_CC_ZERO);
        GX.GX_SetTevAlphaIn(0,GX_CA_ZERO,GX_CA_ZERO,GX_CA_ZERO,GX_CA_KONST);
        GX.GX_SetTevColorOp(0,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVPREV);GX.GX_SetTevAlphaOp(0,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVPREV);
        GX.GX_SetTevDirect(1);GX.GX_SetTevOrder(1,GX_TEXCOORD2,GX_TEXMAP2,GX_COLORNULL);
        GX.GX_SetTevColorIn(1,GX_CC_ZERO,GX_CC_CPREV,GX_CC_TEXA,GX_CC_ZERO);
        GX.GX_SetTevAlphaIn(1,GX_CA_ZERO,GX_CA_APREV,GX_CA_TEXA,GX_CA_ZERO);
        // Premultiplied interface radiance: baseline sources are stored at
        // half intensity, while corner sources retain their full range. This
        // supplies highlight exposure without 11-bit values wrapping when
        // later TEV A/B/C inputs are reduced to eight bits.
        GX.GX_SetTevColorOp(1,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_4,1,GX_TEVPREV);GX.GX_SetTevAlphaOp(1,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_2,1,GX_TEVPREV);
        GX.GX_SetZMode(0,GX_ALWAYS,0);GX.GX_SetZCompLoc(0);GX.GX_SetAlphaCompare(GX_ALWAYS,0,GX_AOP_AND,GX_ALWAYS,0);
        GX.GX_SetBlendMode(GX_BM_BLEND,GX_BL_ONE,GX_BL_INVSRCALPHA,GX_LO_CLEAR);GX.GX_SetDstAlpha(0,0);GX.GX_SetColorUpdate(1);GX.GX_SetAlphaUpdate(1);
        GX.GX_InvVtxCache();GX.GX_InvalidateTexAll();
    }
    internal void DrawRear(GraphicsDevice device)
    {
        BeginInterfacePass(device,false);
        GX.GX_LoadTexMtxImm(reversedTextureMatrix.Address,GX_TEXMTX0,GX_MTX2x4);
        GX.GX_SetCullMode(GX_CULL_FRONT);GX.GX_SetTevKColor(0,new GXColor(82,88,95,165));GX.GX_CallDispList(outer.Address,(uint)commandBytes);
        GX.GX_LoadTexMtxImm(textureMatrix.Address,GX_TEXMTX0,GX_MTX2x4);
        GX.GX_SetCullMode(GX_CULL_BACK);GX.GX_SetTevKColor(0,new GXColor(98,102,109,170));GX.GX_CallDispList(inner.Address,(uint)commandBytes);
        EndInterfacePass(device);IplFrameProfiler.ExtraDraws+=2;IplFrameProfiler.ExtraTriangles+=IplSealedGlassGeometry.Indices/3*2;
    }
    internal void DrawFront(GraphicsDevice device)
    {
        if(blurLevel!=0)
            innerBlur!.Draw(device,rearImage,rearTexture,vertices,inner,commandBytes,projectionBuffer,modelBuffer,copyWidth,copyHeight);
        BeginInterfacePass(device,true);
        // The cavity's front interface must cover the centered icon before
        // the transmitted scene is captured through the outer front wall.
        GX.GX_LoadTexMtxImm(reversedTextureMatrix.Address,GX_TEXMTX0,GX_MTX2x4);
        GX.GX_LoadTexMtxImm(innerFresnelMatrix.Address,GX_TEXMTX2,GX_MTX2x4);
        GX.GX_SetTevColorIn(1,GX_CC_ZERO,GX_CC_CPREV,GX_CC_TEXC,GX_CC_ZERO);
        // Lower artistic exposure keeps this near interface from becoming a
        // second inset frame; the stronger far-cavity return reveals depth.
        GX.GX_SetCullMode(GX_CULL_FRONT);GX.GX_SetTevKColor(0,new GXColor(24,26,30,55));GX.GX_CallDispList(inner.Address,(uint)commandBytes);
        GX.GX_LoadTexMtxImm(textureMatrix.Address,GX_TEXMTX0,GX_MTX2x4);
        GX.GX_LoadTexMtxImm(fresnelMatrix.Address,GX_TEXMTX2,GX_MTX2x4);
        GX.GX_InitTexObj(texture.Address,environment.Address,ReflectionSize,ReflectionSize,GX_TF_RGBA8,GX_CLAMP,GX_CLAMP,0);
        GX.GX_InitTexObjFilterMode(texture.Address,GX_LINEAR,GX_LINEAR);GX.GX_LoadTexObj(texture.Address,GX_TEXMAP0);
        long copyStart=IplFrameProfiler.Active?Clock.Timestamp:0;
        GX.GX_SetTexCopySrc(0,0,(ushort)copyWidth,(ushort)copyHeight);GX.GX_SetTexCopyDst((ushort)copyWidth,(ushort)copyHeight,GX_TF_RGBA8,0);
        GX.GX_CopyTex(rearImage.Address,0);GX.GX_PixModeSync();GX.GX_InvalidateTexAll();
        if(IplFrameProfiler.Active)IplFrameProfiler.CaptureUs+=IplFrameProfiler.Since(copyStart);
        GX.GX_InitTexObj(rearTexture.Address,rearImage.Address,(ushort)copyWidth,(ushort)copyHeight,GX_TF_RGBA8,GX_CLAMP,GX_CLAMP,0);
        GX.GX_InitTexObjFilterMode(rearTexture.Address,GX_LINEAR,GX_LINEAR);GX.GX_LoadTexObj(rearTexture.Address,GX_TEXMAP3);
        GX.GX_SetTexCoordScaleManually(3,1,(ushort)copyWidth,(ushort)copyHeight);
        GX.GX_SetNumIndStages(2);GX.GX_SetIndTexOrder(1,GX_TEXCOORD4,GX_TEXMAP4);GX.GX_SetIndTexCoordScale(1,GX_ITS_1,GX_ITS_1);
        GX.GX_SetIndTexMatrix(GX_ITM_1,refractionMatrix.Address,0);
        GX.GX_SetIndTexMatrix(GX_ITM_2,echoMatrix.Address,0);
        // A2 is an independent light-field weight. Stage 4 reduces collected
        // light only modestly at grazing angles: 180/255 - (36/255)*Fresnel.
        GX.GX_SetTevColor(GX_TEVREG2,new GXColor(0,0,0,180));
        // RGB carries physical R^2 into REG2; PREV retains reflected RGB and
        // the exposed Fresnel alpha. No extra Fresnel texture fetch is needed.
        GX.GX_SetTevColorIn(1,GX_CC_ZERO,GX_CC_ZERO,GX_CC_ZERO,GX_CC_TEXC);
        GX.GX_SetTevColorOp(1,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVREG2);
        // Environment alpha is a shadow on collected illumination, stored in
        // A1. Fresnel opacity is independently reconstructed from K0 and the
        // dielectric lookup, so a dark reflection cannot make glass opaque.
        GX.GX_SetTevAlphaIn(0,GX_CA_ZERO,GX_CA_ZERO,GX_CA_ZERO,GX_CA_TEXA);
        GX.GX_SetTevAlphaOp(0,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVREG1);
        GX.GX_SetTevKAlphaSel(1,GX_TEV_KASEL_K0_A);
        GX.GX_SetTevAlphaIn(1,GX_CA_ZERO,GX_CA_TEXA,GX_CA_KONST,GX_CA_ZERO);
        // Artistic front reflection exposure; physical R^2 in RGB remains
        // unchanged for the secondary transmitted path. Face-on transmission
        // remains about 85%, with stronger curved-wall studio highlights.
        GX.GX_SetTevAlphaOp(1,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_4,1,GX_TEVPREV);
        GX.GX_SetTevOrder(2,GX_TEXCOORD3,GX_TEXMAP3,GX_COLORNULL);
        GX.GX_SetTevIndirect(2,1,GX_ITF_8,GX_ITB_ST,GX_ITM_1,GX_ITW_OFF,GX_ITW_OFF,0,0,GX_ITBA_OFF);
        GX.GX_SetTevColorIn(2,GX_CC_ZERO,GX_CC_ZERO,GX_CC_ZERO,GX_CC_TEXC);GX.GX_SetTevKAlphaSel(2,GX_TEV_KASEL_K2_A);
        GX.GX_SetTevAlphaIn(2,GX_CA_ZERO,GX_CA_APREV,GX_CA_KONST,GX_CA_A2);
        GX.GX_SetTevColorOp(2,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVREG0);GX.GX_SetTevAlphaOp(2,GX_TEV_SUB,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVREG0);
        GX.GX_SetTevSwapMode(3,GX_TEV_SWAP0,GX_TEV_SWAP0);GX.GX_SetTevOrder(3,GX_TEXCOORD3,GX_TEXMAP3,GX_COLORNULL);
        GX.GX_SetTevIndirect(3,1,GX_ITF_8,GX_ITB_ST,GX_ITM_2,GX_ITW_OFF,GX_ITW_OFF,0,0,GX_ITBA_OFF);
        GX.GX_SetTevColorIn(3,GX_CC_C0,GX_CC_TEXC,GX_CC_C2,GX_CC_ZERO);GX.GX_SetTevAlphaIn(3,GX_CA_ZERO,GX_CA_A0,GX_CA_A1,GX_CA_ZERO);
        GX.GX_SetTevColorOp(3,GX_TEV_ADD,GX_TB_ZERO,GX_CS_DIVIDE_2,1,GX_TEVREG0);GX.GX_SetTevAlphaOp(3,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVREG0);
        // Quarter transmission before the final mix, keeping its intermediate
        // channels within eight bits. Four-times studio exposure can then
        // produce white glass highlights without raising Fresnel opacity.
        GX.GX_SetTevDirect(4);GX.GX_SetTevOrder(4,GX_TEXCOORDNULL,GX_TEXMAP_NULL,GX_COLORNULL);
        GX.GX_SetTevColorIn(4,GX_CC_ZERO,GX_CC_ZERO,GX_CC_ZERO,GX_CC_C0);GX.GX_SetTevAlphaIn(4,GX_CA_ZERO,GX_CA_ZERO,GX_CA_ZERO,GX_CA_A0);
        GX.GX_SetTevColorOp(4,GX_TEV_ADD,GX_TB_ZERO,GX_CS_DIVIDE_2,1,GX_TEVREG0);GX.GX_SetTevAlphaOp(4,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVREG0);
        GX.GX_SetTevDirect(5);GX.GX_SetTevOrder(5,GX_TEXCOORDNULL,GX_TEXMAP_NULL,GX_COLORNULL);
        GX.GX_SetTevColorIn(5,GX_CC_C0,GX_CC_CPREV,GX_CC_APREV,GX_CC_ZERO);
        GX.GX_SetTevKColor(2,new GXColor(0,0,0,36));
        GX.GX_SetTevAlphaIn(5,GX_CA_ZERO,GX_CA_ZERO,GX_CA_ZERO,GX_CA_A0);
        GX.GX_SetTevColorOp(5,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_4,1,GX_TEVREG0);GX.GX_SetTevAlphaOp(5,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVREG0);
        GX.GX_SetTevDirect(6);GX.GX_SetTevSwapMode(6,GX_TEV_SWAP0,GX_TEV_SWAP0);GX.GX_SetTevOrder(6,GX_TEXCOORD1,GX_TEXMAP5,GX_COLORNULL);
        GX.GX_SetTevColorIn(6,GX_CC_ZERO,GX_CC_TEXC,GX_CC_A0,GX_CC_C0);
#if IPL_OPTICS_DIAGNOSTIC
        // Bypass the final composite without changing the earlier texture
        // coordinate, indirect or Fresnel setup. Alpha below remains opaque.
        if(DiagnosticMode==0)GX.GX_SetTevColorIn(6,GX_CC_ZERO,GX_CC_ZERO,GX_CC_ZERO,GX_CC_APREV);
        else if(DiagnosticMode==1)GX.GX_SetTevColorIn(6,GX_CC_ZERO,GX_CC_ZERO,GX_CC_ZERO,GX_CC_CPREV);
        else GX.GX_SetTevColorIn(6,GX_CC_ZERO,GX_CC_ZERO,GX_CC_ZERO,GX_CC_TEXC);
#endif
        GX.GX_SetTevKAlphaSel(6,GX_TEV_KASEL_1);GX.GX_SetTevAlphaIn(6,GX_CA_ZERO,GX_CA_ZERO,GX_CA_ZERO,GX_CA_KONST);
        GX.GX_SetTevColorOp(6,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVPREV);GX.GX_SetTevAlphaOp(6,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVPREV);
        GX.GX_SetNumTevStages(7);GX.GX_SetCullMode(GX_CULL_BACK);GX.GX_SetTevKColor(0,new GXColor(255,255,255,245));GX.GX_CallDispList(outer.Address,(uint)commandBytes);
        EndInterfacePass(device);IplFrameProfiler.ExtraDraws+=2;IplFrameProfiler.ExtraTriangles+=IplSealedGlassGeometry.Indices/3*2;
    }
    private void EndInterfacePass(GraphicsDevice device)
    {
        GX.GX_SetNumIndStages(0);GX.GX_SetTevDirect(0);GX.GX_SetTevDirect(1);GX.GX_SetTevDirect(2);
        GX.GX_SetTevDirect(3);GX.GX_SetTevDirect(4);GX.GX_SetTevDirect(5);GX.GX_SetTevDirect(6);
        GX.GX_SetTexCoordScaleManually(0,0,1,1);GX.GX_SetTexCoordScaleManually(1,0,1,1);GX.GX_SetTexCoordScaleManually(2,0,1,1);
        GX.GX_SetTexCoordScaleManually(3,0,1,1);GX.GX_SetTexCoordScaleManually(4,0,1,1);GX.GX_SetTexCoordScaleManually(5,0,1,1);
        device.EndRaw();
    }
    public void Dispose(){innerBlur?.Dispose();vertices.Dispose();outer.Dispose();inner.Dispose();environment.Dispose();rearEnvironment.Dispose();innerEnvironment.Dispose();texture.Dispose();projectionBuffer.Dispose();modelBuffer.Dispose();textureMatrix.Dispose();reversedTextureMatrix.Dispose();normalMatrix.Dispose();viewOffsets.Dispose();offsetTexture.Dispose();positionMatrix.Dispose();indirectMatrix.Dispose();fresnel.Dispose();fresnelTexture.Dispose();fresnelMatrix.Dispose();innerFresnelMatrix.Dispose();rearImage.Dispose();rearTexture.Dispose();screenMatrix.Dispose();refractionMatrix.Dispose();bendImage.Dispose();bendTexture.Dispose();bendMatrix.Dispose();echoMatrix.Dispose();fieldImage.Dispose();fieldTexture.Dispose();reflectionPositionMatrix.Dispose();}
}
