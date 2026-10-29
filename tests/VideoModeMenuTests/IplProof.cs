using System;
using System.Globalization;
using System.IO;
using System.Text;

// Inspection aid, not a Dolphin screenshot: exact shared scene commands and atlas.
internal sealed class IplProof : IIplCanvas
{
    private readonly StringBuilder shapes=new StringBuilder();
    private readonly StringBuilder glass=new StringBuilder(),panels=new StringBuilder();
    private string backdrop="";
    private readonly IplPoint[] facePoints=new IplPoint[625];
    private readonly uint[] faceColors=new uint[625];
    private int ink;
    private static string N(float f)=>f.ToString("0.###",CultureInfo.InvariantCulture);
    private static string Color(uint c)=>"#"+(c>>8).ToString("x6");
    public void Triangle(float ax,float ay,float bx,float by,float cx,float cy,uint color)
    {shapes.Append("<path d='M"+N(ax)+" "+N(ay)+"L"+N(bx)+" "+N(by)+"L"+N(cx)+" "+N(cy)+"Z' fill='"+Color(color)+"' fill-opacity='"+N((color&255)/255f)+"'/>");}
    public void BeginGlass()=>glass.Clear();
    private static IplPoint Mid(IplPoint a,IplPoint b)=>new IplPoint((a.X+b.X)/2,(a.Y+b.Y)/2);
    private static uint Mix(uint a,uint b)=>(((a>>24)+(b>>24))/2<<24)|((((a>>16)&255)+((b>>16)&255))/2<<16)|((((a>>8)&255)+((b>>8)&255))/2<<8)|(((a&255)+(b&255))/2);
    private void GlassPiece(IplPoint a,uint ca,IplPoint b,uint cb,IplPoint d,uint cd)
    {
        uint color=Mix(Mix(ca,cb),cd);
        glass.Append("<path d='M"+N(a.X)+" "+N(a.Y)+"L"+N(b.X)+" "+N(b.Y)+"L"+N(d.X)+" "+N(d.Y)+"Z' fill='"+Color(color)+"' fill-opacity='"+N((color&255)/255f)+"'/>");
    }
    public void GlassVertex(int index,IplPoint point,uint color){facePoints[index]=point;faceColors[index]=color;}
    public void EndGlassFace()
    {
        for(int y=0;y<24;y++)for(int x=0;x<24;x++)
        {
            int a=y*25+x;
            GlassTriangle(facePoints[a],faceColors[a],facePoints[a+1],faceColors[a+1],facePoints[a+25],faceColors[a+25]);
            GlassTriangle(facePoints[a+1],faceColors[a+1],facePoints[a+26],faceColors[a+26],facePoints[a+25],faceColors[a+25]);
        }
    }
    private void GlassTriangle(IplPoint a,uint ca,IplPoint b,uint cb,IplPoint d,uint cd)
    {
        var ab=Mid(a,b);var bd=Mid(b,d);var da=Mid(d,a);uint cab=Mix(ca,cb),cbd=Mix(cb,cd),cda=Mix(cd,ca);
        GlassPiece(a,ca,ab,cab,da,cda);GlassPiece(ab,cab,b,cb,bd,cbd);GlassPiece(da,cda,bd,cbd,d,cd);GlassPiece(ab,cab,bd,cbd,da,cda);
    }
    public void EndGlass() { }
    public void DrawGlass()=>shapes.Append(glass);
    public void BeginPanels()=>panels.Clear();
    public void PanelTriangle(IplPoint a,IplPoint b,IplPoint d)
    {panels.Append("M"+N(a.X)+" "+N(a.Y)+"L"+N(b.X)+" "+N(b.Y)+"L"+N(d.X)+" "+N(d.Y)+"Z");}
    public void EndPanels() { }
    public void CaptureBackdrop()=>backdrop=shapes.ToString();
    public void DrawFrostedPanels()
    {
        string id="backdrop"+(ink++).ToString();
        shapes.Append("<defs><g id='"+id+"'>"+backdrop+"</g><filter id='"+id+"-blur'><feGaussianBlur stdDeviation='3.3'/></filter><clipPath id='"+id+"-clip'><path d='"+panels+"'/></clipPath></defs><g clip-path='url(#"+id+"-clip)'><use href='#"+id+"' filter='url(#"+id+"-blur)'/></g>");
    }
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
        shapes.Clear();var renderer=new IplRenderer(this);renderer.Prepare(state);renderer.Present(state);
        string atlas=Convert.ToBase64String(IplFontData.Png);
        File.WriteAllText(path,"<svg xmlns='http://www.w3.org/2000/svg' width='640' height='480' viewBox='0 0 640 480' style='background:black'><defs><image id='atlas' width='512' height='256' href='data:image/png;base64,"+atlas+"'/></defs>"+shapes+"</svg>");
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
