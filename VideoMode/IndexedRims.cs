using System;
using Dolphin.Graphics;
using Dolphin.Graphics.Raw;
using static Dolphin.Graphics.Raw.GXConstants;

// Dynamic positions/colors live in native memory. Only one small chunk is staged
// on the 256 KiB managed heap; GX replays an immutable indexed command list.
internal sealed class IndexedRims : IDisposable
{
    private const int MaxQuads=32768;
    private readonly float[] positions=new float[IplRimCommands.PositionFloats];
    private readonly byte[] colors=new byte[IplRimCommands.ColorBytes];
    private readonly float[] projection=new float[16],transform=new float[12];
    private readonly NativeBuffer positionBuffer=NativeBuffer.Allocate(MaxQuads*32);
    private readonly NativeBuffer colorBuffer=NativeBuffer.Allocate(MaxQuads*4);
    private readonly NativeBuffer projectionBuffer=NativeBuffer.Allocate(64),transformBuffer=NativeBuffer.Allocate(48);
    private readonly NativeBuffer commands;
    private readonly int commandBytes;
    private int pending,chunks;
    private float previousWidth,previousHeight,previousScale,previousOffset;
    internal IndexedRims()
    {
        var data=IplRimCommands.Create();commandBytes=data.Length;
        commands=NativeBuffer.Allocate(data.Length);commands.Write(data);commands.Flush();
    }
    internal void Prepare(float width,float height,float scale,float offset)
    {
        if(width==previousWidth&&height==previousHeight&&scale==previousScale&&offset==previousOffset)return;
        previousWidth=width;previousHeight=height;previousScale=scale;previousOffset=offset;
        // Match the SDK's top-left 2D guOrtho(...,-1,1) and logical layout.
        projection[0]=2/width;projection[3]=-1;projection[5]=-2/height;projection[7]=1;
        projection[10]=-.5f;projection[11]=-.5f;projection[15]=1;
        transform[0]=transform[5]=scale;transform[3]=offset;transform[10]=1;
        projectionBuffer.Write(projection);transformBuffer.Write(transform);
        projectionBuffer.Flush();transformBuffer.Flush();
    }
    internal void Begin(){pending=chunks=0;}
    internal void Quad(IplPoint a,IplPoint b,IplPoint c,IplPoint d,uint color)
    {
        if((color&255)==0)return;
        if(chunks*IplRimCommands.Quads+pending>=MaxQuads)throw new InvalidOperationException("Optical rim capacity exceeded.");
        int at=pending*8;
        positions[at]=a.X;positions[at+1]=a.Y;positions[at+2]=b.X;positions[at+3]=b.Y;
        positions[at+4]=c.X;positions[at+5]=c.Y;positions[at+6]=d.X;positions[at+7]=d.Y;
        at=pending*4;colors[at]=(byte)(color>>24);colors[at+1]=(byte)(color>>16);
        colors[at+2]=(byte)(color>>8);colors[at+3]=(byte)color;
        if(++pending==IplRimCommands.Quads)Upload();
    }
    private void Upload()
    {
        // Degenerate transparent padding keeps one fixed immutable command list.
        if(pending<IplRimCommands.Quads)
        {
            Array.Clear(positions,pending*8,positions.Length-pending*8);
            Array.Clear(colors,pending*4,colors.Length-pending*4);
        }
        positionBuffer.Write(positions,0,chunks*positions.Length*4);
        colorBuffer.Write(colors,0,chunks*colors.Length);
        chunks++;pending=0;
    }
    internal void End()
    {
        if(pending>0)Upload();
        if(chunks==0)return;
        positionBuffer.Flush(0,chunks*positions.Length*4);colorBuffer.Flush(0,chunks*colors.Length);
    }
    internal void Draw(GraphicsDevice device)
    {
        if(chunks==0)return;
        device.BeginRaw();
        GX.GX_SetViewport(0,0,device.Width,device.Height,0,1);
        GX.GX_SetScissor(0,0,(uint)device.Width,(uint)device.Height);
        GX.GX_LoadProjectionMtx(projectionBuffer.Address,GX_ORTHOGRAPHIC);
        GX.GX_LoadPosMtxImm(transformBuffer.Address,GX_PNMTX0);GX.GX_SetCurrentMtx(GX_PNMTX0);
        GX.GX_ClearVtxDesc();GX.GX_SetVtxDesc(GX_VA_POS,GX_INDEX16);GX.GX_SetVtxDesc(GX_VA_CLR0,GX_INDEX16);
        GX.GX_SetVtxAttrFmt(0,GX_VA_POS,GX_POS_XY,GX_F32,0);GX.GX_SetVtxAttrFmt(0,GX_VA_CLR0,GX_CLR_RGBA,GX_RGBA8,0);
        GX.GX_SetNumChans(1);GX.GX_SetChanCtrl(GX_COLOR0A0,0,GX_SRC_VTX,GX_SRC_VTX,0,GX_DF_NONE,GX_AF_NONE);
        GX.GX_SetNumTexGens(0);GX.GX_SetNumTevStages(1);GX.GX_SetNumIndStages(0);
        GX.GX_SetTevDirect(0);GX.GX_SetTevOrder(0,GX_TEXCOORDNULL,GX_TEXMAP_NULL,GX_COLOR0A0);GX.GX_SetTevOp(0,GX_PASSCLR);
        GX.GX_SetCullMode(GX_CULL_NONE);GX.GX_SetZMode(0,GX_ALWAYS,0);GX.GX_SetZCompLoc(0);
        GX.GX_SetAlphaCompare(GX_ALWAYS,0,GX_AOP_AND,GX_ALWAYS,0);
        GX.GX_SetBlendMode(GX_BM_BLEND,GX_BL_SRCALPHA,GX_BL_INVSRCALPHA,GX_LO_CLEAR);
        GX.GX_SetDstAlpha(0,0);GX.GX_SetColorUpdate(1);GX.GX_SetAlphaUpdate(1);
        for(int i=0;i<chunks;i++)
        {
            GX.GX_InvVtxCache();
            GX.GX_SetArray(GX_VA_POS,positionBuffer.Address.Add(i*positions.Length*4),8);
            GX.GX_SetArray(GX_VA_CLR0,colorBuffer.Address.Add(i*colors.Length),4);
            GX.GX_CallDispList(commands.Address,(uint)commandBytes);
        }
        device.EndRaw();
        // SDK counters track managed draws. Include our raw batches and their
        // degenerate padding without changing the hardware runner's CSV schema.
        IplFrameProfiler.ExtraDraws+=chunks;
        IplFrameProfiler.ExtraTriangles+=chunks*IplRimCommands.Quads*2;
    }
    public void Dispose()
    {commands.Dispose();positionBuffer.Dispose();colorBuffer.Dispose();projectionBuffer.Dispose();transformBuffer.Dispose();}
}
