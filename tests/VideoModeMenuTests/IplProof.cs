using System;
using System.Globalization;
using System.IO;
using System.Text;

// Inspection aid, not a Dolphin screenshot: exact shared scene commands and atlas.
internal sealed class IplProof : IIplCanvas
{
    private readonly StringBuilder shapes=new StringBuilder();
    private int ink;
    private static string N(float f)=>f.ToString("0.###",CultureInfo.InvariantCulture);
    private static string Color(uint c)=>"#"+(c>>8).ToString("x6");
    public void Triangle(float ax,float ay,float bx,float by,float cx,float cy,uint color)
    {shapes.Append("<path d='M"+N(ax)+" "+N(ay)+"L"+N(bx)+" "+N(by)+"L"+N(cx)+" "+N(cy)+"Z' fill='"+Color(color)+"' fill-opacity='"+N((color&255)/255f)+"'/>");}
    public void Text(string text,float x,float y,float size,uint color,float angle)
    {
        string id="ink"+(ink++).ToString();
        shapes.Append("<filter id='"+id+"'><feFlood flood-color='"+Color(color)+"'/><feComposite in2='SourceAlpha' operator='in'/></filter><g filter='url(#"+id+")' transform='translate("+N(x)+" "+N(y)+") rotate("+N(angle)+") scale("+N(size/28)+")' opacity='"+N((color&255)/255f)+"'>");
        float advance=0;
        for(int i=0;i<text.Length;i++)
        {
            int ch=text[i]-32;if(ch<0||ch>=96)continue;
            shapes.Append("<svg x='"+N(advance)+"' width='32' height='40' viewBox='"+(ch%16*32)+" "+(ch/16*40)+" 32 40'><use href='#atlas'/></svg>");
            advance+=IplFontData.Widths[ch];
        }
        shapes.Append("</g>");
    }
    private void Save(string path,IplShell state)
    {
        shapes.Clear();new IplRenderer(this).Present(state);
        string atlas=Convert.ToBase64String(IplFontData.Png);
        File.WriteAllText(path,"<svg xmlns='http://www.w3.org/2000/svg' width='640' height='480' viewBox='0 0 640 480'><defs><image id='atlas' width='512' height='256' href='data:image/png;base64,"+atlas+"'/></defs>"+shapes+"</svg>");
    }
    internal static void Write(string dir)
    {
        Directory.CreateDirectory(dir);var p=new IplProof();var s=new IplShell(new FakeOutput());s.Time=1;
        p.Save(Path.Combine(dir,"ipl-menu.svg"),s);
        s.TurnTo(ShellFace.Video);s.Tick(.7f);p.Save(Path.Combine(dir,"ipl-video.svg"),s);
        s.Row=2;s.Confirm();p.Save(Path.Combine(dir,"ipl-aspect.svg"),s);
        s.Back();s.TurnTo(ShellFace.Calendar);s.Tick(.7f);s.Row=1;s.Confirm();s.Direction(1,0,.016f);
        p.Save(Path.Combine(dir,"ipl-date.svg"),s);
        File.WriteAllText(Path.Combine(dir,"index.html"),"<!doctype html><meta charset='utf-8'><title>Native IPL scene proofs</title><style>body{background:#161616;color:white;font:16px sans-serif}section{display:inline-block;margin:12px}img{width:640px;height:480px}</style><h1>Native scene geometry proofs</h1><p>Shared C# renderer and glyph atlas. These are host exports, not emulator captures.</p><section><h2>Cube</h2><img src='ipl-menu.svg'></section><section><h2>Video face</h2><img src='ipl-video.svg'></section><section><h2>Flat selector</h2><img src='ipl-aspect.svg'></section><section><h2>Independent date segments</h2><img src='ipl-date.svg'></section>");
    }
}
