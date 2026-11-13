using System;
using Dolphin.Graphics;
using Dolphin.Graphics.Raw;
using Dolphin.Timing;
using static Dolphin.Graphics.Raw.GXConstants;

// Separable nine-tap Gaussian, fused into five bilinear samples per axis and
// clipped to the existing inner
// boundary. The final exterior reflection and all UI are drawn afterwards.
internal sealed class InnerGlassBlur : IDisposable
{
    // One half-resolution horizontal intermediate. Reuse the shell's full-size
    // transmission capture for source/restore, then its usual front capture.
    private readonly NativeBuffer horizontalImage=NativeBuffer.Allocate(320*288*4),horizontalTexture=NativeBuffer.Allocate(32);
    private readonly NativeBuffer horizontalMatrices=NativeBuffer.Allocate(5*48),verticalMatrices=NativeBuffer.Allocate(5*48);
    private readonly NativeBuffer quadVertices=NativeBuffer.Allocate(80),quadCommands=NativeBuffer.Allocate(32);
    private readonly NativeBuffer quadProjection=NativeBuffer.Allocate(64),identityMatrix=NativeBuffer.Allocate(48);
    private readonly float[] horizontal=new float[60],vertical=new float[60];
    private readonly float[] offsets=new float[10];
    private readonly byte[] weights=new byte[10];
    private GXColor horizontalWeights,verticalWeights;
    private int lastLevel,lastWidth,lastHeight;
    private float lastPixelX,lastPixelY;
    internal InnerGlassBlur()
    {
        // Allocation zeroes CPU memory; flush before GX becomes its writer so
        // dirty zero-filled cache lines cannot later overwrite the GPU copy.
        horizontalImage.Flush();
        quadVertices.Write(new float[]{-1,1,0,0,0, 1,1,0,1,0, 1,-1,0,1,1, -1,-1,0,0,1});quadVertices.Flush();
        quadCommands.Write(new byte[]{0x80,0,4,0,0,1,1,2,2,3,3});quadCommands.Flush();
        quadProjection.Write(new float[]{1,0,0,0, 0,1,0,0, 0,0,-1,0, 0,0,0,1});quadProjection.Flush();
        identityMatrix.Write(new float[]{1,0,0,0, 0,1,0,0, 0,0,1,0});identityMatrix.Flush();
    }
    internal void Prepare(int level,float[] screen,float pixelX,float pixelY,int width,int height)
    {
        if(level!=lastLevel||width!=lastWidth||height!=lastHeight||pixelX!=lastPixelX||pixelY!=lastPixelY)
        {
            float sigma=IplGlassBlur.Spacing(level);
            float effective=IplGlassBlur.BuildKernel(sigma*pixelX*width,offsets,weights,0);
            // Match the actual horizontal radius after its finite support and
            // byte quantization, even across different EFB pixel aspects.
            IplGlassBlur.BuildKernel(effective/(pixelX*width)*pixelY*(height/2),offsets,weights,5);
            horizontalWeights=new GXColor(weights[0],weights[1],weights[2],255);
            verticalWeights=new GXColor(weights[5],weights[6],weights[7],255);
            for(int tap=0;tap<5;tap++)
            {
                int at=tap*12;horizontal[at]=horizontal[at+5]=1;horizontal[at+3]=offsets[tap]/width;
            }
            horizontalMatrices.Write(horizontal);horizontalMatrices.Flush();
            lastLevel=level;lastWidth=width;lastHeight=height;lastPixelX=pixelX;lastPixelY=pixelY;
        }
        for(int tap=0;tap<5;tap++)
        {
            int at=tap*12;float dy=offsets[5+tap]/(height/2);
            // Projective screen T offset is dy*Q, including its translation.
            // Adding a constant to T alone would shrink the radius with depth.
            IplGlassBlur.OffsetScreenT(screen,vertical,at,dy);
        }
        verticalMatrices.Write(vertical);verticalMatrices.Flush();
    }
    private static void Copy(NativeBuffer image,int width,int height)
    {
        long start=IplFrameProfiler.Active?Clock.Timestamp:0;
        GX.GX_SetTexCopySrc(0,0,(ushort)width,(ushort)height);GX.GX_SetTexCopyDst((ushort)width,(ushort)height,GX_TF_RGBA8,0);
        GX.GX_CopyTex(image.Address,0);GX.GX_PixModeSync();GX.GX_InvalidateTexAll();
        if(IplFrameProfiler.Active)IplFrameProfiler.CaptureUs+=IplFrameProfiler.Since(start);
    }
    private static void LoadTexture(NativeBuffer image,NativeBuffer texture,int width,int height,bool linear)
    {
        GX.GX_InitTexObj(texture.Address,image.Address,(ushort)width,(ushort)height,GX_TF_RGBA8,GX_CLAMP,GX_CLAMP,0);
        GX.GX_InitTexObjFilterMode(texture.Address,(byte)(linear?GX_LINEAR:GX_NEAR),(byte)(linear?GX_LINEAR:GX_NEAR));GX.GX_LoadTexObj(texture.Address,GX_TEXMAP0);
    }
    private static void Filter(NativeBuffer matrices,int width,int height,bool projective,bool restore,GXColor weights)
    {
        GX.GX_SetNumChans(0);GX.GX_SetNumIndStages(0);GX.GX_SetNumTexGens((byte)(restore?1:5));GX.GX_SetNumTevStages((byte)(restore?1:5));
        // Symmetric paired Gaussian weights, normalized to exactly 255.
        GX.GX_SetTevKColor(0,weights);
        for(int tap=0;tap<(restore?1:5);tap++)
        {
            int matrix=GX_TEXMTX0+tap*3;
            GX.GX_LoadTexMtxImm((restore?matrices.Address.Add(2*48):matrices.Address.Add(tap*48)),(uint)matrix,(byte)(projective?GX_MTX3x4:GX_MTX2x4));
            GX.GX_SetTexCoordGen((ushort)tap,(uint)(projective?GX_TG_MTX3x4:GX_TG_MTX2x4),(uint)(projective?GX_TG_POS:GX_TG_TEX0),(uint)matrix);
            GX.GX_SetTexCoordScaleManually((byte)tap,1,(ushort)width,(ushort)height);
            GX.GX_SetTevDirect((byte)tap);GX.GX_SetTevSwapMode((byte)tap,GX_TEV_SWAP0,GX_TEV_SWAP0);
            GX.GX_SetTevOrder((byte)tap,(byte)tap,GX_TEXMAP0,GX_COLORNULL);
            GX.GX_SetTevKColorSel((byte)tap,(byte)(restore?GX_TEV_KCSEL_1:tap==2?GX_TEV_KCSEL_K0_B:tap==1||tap==3?GX_TEV_KCSEL_K0_G:GX_TEV_KCSEL_K0_R));
            GX.GX_SetTevColorIn((byte)tap,GX_CC_ZERO,GX_CC_TEXC,GX_CC_KONST,(byte)(tap==0?GX_CC_ZERO:GX_CC_CPREV));
            GX.GX_SetTevColorOp((byte)tap,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVPREV);
            GX.GX_SetTevKAlphaSel((byte)tap,GX_TEV_KASEL_1);GX.GX_SetTevAlphaIn((byte)tap,GX_CA_ZERO,GX_CA_ZERO,GX_CA_ZERO,GX_CA_KONST);
            GX.GX_SetTevAlphaOp((byte)tap,GX_TEV_ADD,GX_TB_ZERO,GX_CS_SCALE_1,1,GX_TEVPREV);
        }
        GX.GX_SetZMode(0,GX_ALWAYS,0);GX.GX_SetZCompLoc(0);GX.GX_SetAlphaCompare(GX_ALWAYS,0,GX_AOP_AND,GX_ALWAYS,0);
        GX.GX_SetBlendMode(GX_BM_NONE,GX_BL_ONE,GX_BL_ZERO,GX_LO_CLEAR);GX.GX_SetDstAlpha(0,0);GX.GX_SetColorUpdate(1);GX.GX_SetAlphaUpdate(1);
    }
    private void Quad(int width,int height)
    {
        GX.GX_SetViewport(0,0,width,height,0,1);GX.GX_SetScissor(0,0,(uint)width,(uint)height);
        GX.GX_LoadProjectionMtx(quadProjection.Address,GX_ORTHOGRAPHIC);GX.GX_LoadPosMtxImm(identityMatrix.Address,GX_PNMTX0);GX.GX_SetCurrentMtx(GX_PNMTX0);
        GX.GX_SetCullMode(GX_CULL_NONE);GX.GX_ClearVtxDesc();GX.GX_SetVtxDesc(GX_VA_POS,GX_INDEX8);GX.GX_SetVtxDesc(GX_VA_TEX0,GX_INDEX8);
        GX.GX_SetVtxAttrFmt(0,GX_VA_POS,GX_POS_XYZ,GX_F32,0);GX.GX_SetVtxAttrFmt(0,GX_VA_TEX0,GX_TEX_ST,GX_F32,0);
        GX.GX_SetArray(GX_VA_POS,quadVertices.Address,20);GX.GX_SetArray(GX_VA_TEX0,quadVertices.Address.Add(12),20);GX.GX_InvVtxCache();
        GX.GX_CallDispList(quadCommands.Address,32);
    }
    internal void Draw(GraphicsDevice device,NativeBuffer scene,NativeBuffer sceneTexture,NativeBuffer shellVertices,NativeBuffer shellCommands,int commandBytes,NativeBuffer projection,NativeBuffer model,int width,int height)
    {
        long start=IplFrameProfiler.Active?Clock.Timestamp:0;
        int previousCaptures=IplFrameProfiler.CaptureUs;
        device.BeginRaw();
        Copy(scene,width,height);
        GX.GX_SetTevSwapModeTable(GX_TEV_SWAP0,GX_CH_RED,GX_CH_GREEN,GX_CH_BLUE,GX_CH_ALPHA);
        LoadTexture(scene,sceneTexture,width,height,true);Filter(horizontalMatrices,width,height,false,false,horizontalWeights);Quad(width/2,height/2);
        Copy(horizontalImage,width/2,height/2);
        // The scratch quad overlaps the scene. Restore its exact full-size
        // source before the cavity mask, so blur cannot create an outer halo.
        LoadTexture(scene,sceneTexture,width,height,false);Filter(horizontalMatrices,width,height,false,true,horizontalWeights);Quad(width,height);
        LoadTexture(horizontalImage,horizontalTexture,width/2,height/2,true);Filter(verticalMatrices,width/2,height/2,true,false,verticalWeights);
        GX.GX_SetViewport(0,0,device.Width,device.Height,0,1);GX.GX_SetScissor(0,0,(uint)device.Width,(uint)device.Height);
        GX.GX_LoadProjectionMtx(projection.Address,GX_PERSPECTIVE);GX.GX_LoadPosMtxImm(model.Address,GX_PNMTX0);GX.GX_SetCurrentMtx(GX_PNMTX0);
        GX.GX_ClearVtxDesc();GX.GX_SetVtxDesc(GX_VA_POS,GX_INDEX16);GX.GX_SetVtxDesc(GX_VA_NRM,GX_INDEX16);
        GX.GX_SetVtxAttrFmt(0,GX_VA_POS,GX_POS_XYZ,GX_F32,0);GX.GX_SetVtxAttrFmt(0,GX_VA_NRM,GX_NRM_XYZ,GX_F32,0);
        GX.GX_SetArray(GX_VA_POS,shellVertices.Address,24);GX.GX_SetArray(GX_VA_NRM,shellVertices.Address.Add(12),24);GX.GX_InvVtxCache();
        GX.GX_SetCullMode(GX_CULL_FRONT);GX.GX_CallDispList(shellCommands.Address,(uint)commandBytes);
        for(int i=0;i<5;i++){GX.GX_SetTexCoordScaleManually((byte)i,0,1,1);GX.GX_SetTevDirect((byte)i);}
        device.EndRaw();
        IplFrameProfiler.ExtraDraws+=3;IplFrameProfiler.ExtraTriangles+=4+IplSealedGlassGeometry.Indices/3;
        if(IplFrameProfiler.Active)IplFrameProfiler.BlurUs+=IplFrameProfiler.Since(start)-(IplFrameProfiler.CaptureUs-previousCaptures);
    }
    public void Dispose()
    {
        horizontalImage.Dispose();horizontalTexture.Dispose();horizontalMatrices.Dispose();verticalMatrices.Dispose();
        quadVertices.Dispose();quadCommands.Dispose();quadProjection.Dispose();identityMatrix.Dispose();
    }
}
