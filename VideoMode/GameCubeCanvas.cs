using System;
using Dolphin.Graphics;

internal sealed class GameCubeCanvas : IIplCanvas, IDisposable
{
    private readonly Draw2D draw;
    private readonly Texture2D atlas=Texture2D.FromPng(IplFontData.Png);
    private float scale=1,offset;
    internal GameCubeCanvas(GraphicsDevice device)
    { draw=new Draw2D(device,new Draw2DOptions {VertexCapacity=32760}); }
    internal void Begin()
    { draw.Begin();scale=draw.LogicalSize.Y/480f;offset=(draw.LogicalSize.X-640*scale)/2; }
    internal void End()=>draw.End();
    private static Color32 Color(uint c)=>new Color32((byte)(c>>24),(byte)(c>>16),(byte)(c>>8),(byte)c);
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
    public void Dispose(){draw.Dispose();atlas.Dispose();}
}
