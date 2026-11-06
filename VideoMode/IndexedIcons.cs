using System;
using Dolphin.Graphics;
using Dolphin.Graphics.Raw;
using static Dolphin.Graphics.Raw.GXConstants;

// Canonical face-plane triangles never change. GX performs the cube transform
// and perspective divide; only two small matrices are uploaded between frames.
internal sealed class IndexedIcons : IDisposable
{
    private const int Styles=6,Triangles=128,Capacity=2048;
    private readonly NativeBuffer[] positions=new NativeBuffer[Styles],colors=new NativeBuffer[Styles];
    private readonly int[] chunks=new int[Styles];
    private readonly float[] xy=new float[Triangles*6],projection=new float[16],model=new float[12];
    private readonly byte[] rgba=new byte[Triangles*4];
    private readonly NativeBuffer projectionBuffer=NativeBuffer.Allocate(64),modelBuffer=NativeBuffer.Allocate(48),commands;
    private int style,pending,commandBytes;
    internal bool Recording {get;private set;}
    internal IndexedIcons()
    {
        var list=new byte[(3+Triangles*3*4+31)/32*32];list[0]=0x90;list[1]=(byte)(Triangles*3>>8);list[2]=(byte)(Triangles*3&255);int at=3;
        for(int i=0;i<Triangles;i++)for(int j=0;j<3;j++){
            int vertex=i*3+j;list[at++]=(byte)(vertex>>8);list[at++]=(byte)vertex;list[at++]=(byte)(i>>8);list[at++]=(byte)i;
        }
        commandBytes=list.Length;commands=NativeBuffer.Allocate(list.Length);commands.Write(list);commands.Flush();
    }
    internal void Begin(int variant)
    {
        if(Recording||variant<0||variant>=Styles||positions[variant]!=null)throw new InvalidOperationException("Invalid icon cache setup.");
        style=variant;pending=0;Recording=true;
        positions[variant]=NativeBuffer.Allocate(Capacity*24);colors[variant]=NativeBuffer.Allocate(Capacity*4);
    }
    internal void Triangle(float ax,float ay,float bx,float by,float cx,float cy,uint color)
    {
        if(chunks[style]*Triangles+pending>=Capacity)throw new InvalidOperationException("Icon cache capacity exceeded.");
        int at=pending*6;xy[at]=ax;xy[at+1]=ay;xy[at+2]=bx;xy[at+3]=by;xy[at+4]=cx;xy[at+5]=cy;
        at=pending*4;rgba[at]=(byte)(color>>24);rgba[at+1]=(byte)(color>>16);rgba[at+2]=(byte)(color>>8);rgba[at+3]=(byte)color;
        if(++pending==Triangles)Flush();
    }
    private void Flush()
    {
        if(pending==0)return;
        Array.Clear(xy,pending*6,xy.Length-pending*6);Array.Clear(rgba,pending*4,rgba.Length-pending*4);
        positions[style].Write(xy,0,chunks[style]*xy.Length*4);colors[style].Write(rgba,0,chunks[style]*rgba.Length);chunks[style]++;pending=0;
    }
    internal void End(){Flush();positions[style].Flush();colors[style].Flush();Recording=false;}
    internal void Prepare(IplPoint o,IplPoint x,IplPoint y,float cubeScale,float centerY,float width,float height,float scale,float offset)
    {
        model[0]=x.X-o.X;model[1]=y.X-o.X;model[3]=o.X;
        model[4]=x.Y-o.Y;model[5]=y.Y-o.Y;model[7]=o.Y;
        model[8]=x.Z-o.Z;model[9]=y.Z-o.Z;model[11]=o.Z-3.8f;
        projection[0]=2*cubeScale*.975f*3.8f*scale/width;
        projection[2]=1-2*(offset+322*scale)/width;
        projection[5]=2*cubeScale*3.8f*scale/height;
        projection[6]=2*centerY*scale/height-1;
        projection[11]=-1;projection[14]=-1;
        modelBuffer.Write(model);projectionBuffer.Write(projection);modelBuffer.Flush();projectionBuffer.Flush();
    }
    internal bool Ready(int variant)=>chunks[variant]>0;
    internal void Draw(GraphicsDevice device,int variant)
    {
        device.BeginRaw();GX.GX_SetViewport(0,0,device.Width,device.Height,0,1);GX.GX_SetScissor(0,0,(uint)device.Width,(uint)device.Height);
        GX.GX_LoadProjectionMtx(projectionBuffer.Address,GX_PERSPECTIVE);GX.GX_LoadPosMtxImm(modelBuffer.Address,GX_PNMTX0);GX.GX_SetCurrentMtx(GX_PNMTX0);
        GX.GX_ClearVtxDesc();GX.GX_SetVtxDesc(GX_VA_POS,GX_INDEX16);GX.GX_SetVtxDesc(GX_VA_CLR0,GX_INDEX16);
        GX.GX_SetVtxAttrFmt(0,GX_VA_POS,GX_POS_XY,GX_F32,0);GX.GX_SetVtxAttrFmt(0,GX_VA_CLR0,GX_CLR_RGBA,GX_RGBA8,0);
        GX.GX_SetNumChans(1);GX.GX_SetChanCtrl(GX_COLOR0A0,0,GX_SRC_VTX,GX_SRC_VTX,0,GX_DF_NONE,GX_AF_NONE);
        GX.GX_SetNumTexGens(0);GX.GX_SetNumTevStages(1);GX.GX_SetNumIndStages(0);GX.GX_SetTevDirect(0);GX.GX_SetTevOrder(0,GX_TEXCOORDNULL,GX_TEXMAP_NULL,GX_COLOR0A0);GX.GX_SetTevOp(0,GX_PASSCLR);
        GX.GX_SetCullMode(GX_CULL_NONE);GX.GX_SetZMode(0,GX_ALWAYS,0);GX.GX_SetZCompLoc(0);GX.GX_SetAlphaCompare(GX_ALWAYS,0,GX_AOP_AND,GX_ALWAYS,0);
        GX.GX_SetBlendMode(GX_BM_BLEND,GX_BL_SRCALPHA,GX_BL_INVSRCALPHA,GX_LO_CLEAR);GX.GX_SetDstAlpha(0,0);GX.GX_SetColorUpdate(1);GX.GX_SetAlphaUpdate(1);
        for(int i=0;i<chunks[variant];i++){
            GX.GX_InvVtxCache();GX.GX_SetArray(GX_VA_POS,positions[variant].Address.Add(i*xy.Length*4),8);GX.GX_SetArray(GX_VA_CLR0,colors[variant].Address.Add(i*rgba.Length),4);GX.GX_CallDispList(commands.Address,(uint)commandBytes);
        }
        device.EndRaw();IplFrameProfiler.ExtraDraws+=chunks[variant];IplFrameProfiler.ExtraTriangles+=chunks[variant]*Triangles;
    }
    public void Dispose()
    {for(int i=0;i<Styles;i++){positions[i]?.Dispose();colors[i]?.Dispose();}commands.Dispose();projectionBuffer.Dispose();modelBuffer.Dispose();}
}
