using System;
using Dolphin.Graphics;
using Dolphin.System;

internal sealed class IplText : IDisposable
{
    private readonly SampleText fallback=new SampleText();
    private readonly SystemFont? font;
    internal bool Original=>font!=null;
    internal IplText()
    {try{font=SystemFont.Load();}catch(NotSupportedException){}catch(InvalidOperationException){}}
    internal void Draw(Draw2D draw,string value,float x,float y,float height,Color32 color)
    {
        if(font!=null)font.Draw(draw,value,new Vector2(x,y),color,height/font.CellHeight);
        else fallback.Draw(draw,value,x,y,height/5,color);
    }
    public void Dispose(){font?.Dispose();fallback.Dispose();}
}

