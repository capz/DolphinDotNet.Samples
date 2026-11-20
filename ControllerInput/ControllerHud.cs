using System;
using System.Collections.Generic;
using Dolphin.Graphics;

// Fixed text and chrome are uploaded once. Only changed input glyphs/meters
// update two small preallocated meshes; no frame-owned text or shape arrays.
internal sealed class ControllerHud : IDisposable
{
    private static readonly float[] PortCenters={467.8f,509.2f,548.7f,588.7f};
    private static readonly string[] PortDigits={"1","2","3","4"};
    private static readonly Color32 White=new Color32(238,235,247),Dim=new Color32(151,143,176),
        Mint=new Color32(103,227,166),Line=new Color32(104,94,136),Purple=new Color32(139,108,242);
    private readonly Texture2D atlas=Texture2D.FromPng(IplFontData.Png,true);
    private readonly Material glyphMaterial,shapeMaterial=new Material{Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private readonly Transform identity=new Transform();
    private readonly Mesh text,chrome;
    private readonly DynamicMesh dynamicText,dynamicShapes;
    private readonly VertexPositionColorTexture[] glyphs=new VertexPositionColorTexture[1024];
    private readonly VertexPositionColor[] shapes=new VertexPositionColor[510];
    private int glyphCount,shapeCount,oldGlyphCount,oldShapeCount;
    private int oldMainX=int.MinValue,oldMainY,oldCX,oldCY,oldL,oldR,oldPorts,oldSelected,oldExit;
    private string oldButtons="",oldEvent="",oldStatus="";
    internal ControllerHud()
    {
        glyphMaterial=new Material{Texture=atlas,Blend=BlendMode.Alpha,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
        // The approved plate owns the title, labels, panel, grid and shell.
        // Only live values and port state are drawn over its cleared fields.
        var labels=new TextBuilder();
        labels.Text("Hold START + Z (2s): exit",470,459,7,Dim);
        text=labels.Build();
        var border=new ShapeBuilder();
        border.Rounded(368,394,94,7.7f,3.8f,new Color32(56,47,76));
        border.Rounded(368,408,94,7.7f,3.8f,new Color32(56,47,76));
        chrome=border.Build();
        dynamicText=DynamicMesh.Create(glyphs,QuadIndices(glyphs.Length));
        // Shape primitives are unindexed triangles, so unused capacity is
        // degenerate. No stale meter or connection pixels survive shorter text.
        dynamicShapes=DynamicMesh.Create(shapes);
    }
    internal void Update(ControllerSession session)
    {
        var state=session.State;
        int x=Quantize(state.Stick.X),y=Quantize(state.Stick.Y),cx=Quantize(state.CStick.X),cy=Quantize(state.CStick.Y);
        int l=Percent(state.LeftTrigger),r=Percent(state.RightTrigger),ports=0;
        for(int i=0;i<4;i++)if(session.IsConnected(i))ports|=1<<i;
        int exit=(int)(session.ExitHoldProgress*100);
        if(x==oldMainX&&y==oldMainY&&cx==oldCX&&cy==oldCY&&l==oldL&&r==oldR&&ports==oldPorts&&session.SelectedPort==oldSelected&&exit==oldExit&&session.ButtonsText==oldButtons&&session.LastEventText==oldEvent&&session.StatusText==oldStatus)return;
        oldMainX=x;oldMainY=y;oldCX=cx;oldCY=cy;oldL=l;oldR=r;oldPorts=ports;oldSelected=session.SelectedPort;oldExit=exit;
        oldButtons=session.ButtonsText;oldEvent=session.LastEventText;oldStatus=session.StatusText;
        glyphCount=shapeCount=0;
        Number(x,83,395,10.5f);Number(y,124,395,10.5f);Number(cx,237,395,10.5f);Number(cy,277,395,10.5f);
        Integer(l,480,391,9);Integer(r,480,405,9);
        DynamicText(session.StatusText,605-Measure(session.StatusText,9),51,9,ports==0?Dim:Mint);
        DynamicText(session.ButtonsText=="NONE"?"No buttons held":session.ButtonsText,521,392,9,White,75);
        float[] centers=PortCenters;
        if(session.SelectedPort>=0)Rounded(centers[session.SelectedPort]-15.7f,30.1f,31.4f,16.2f,7.8f,new Color32(76,52,140));
        for(int i=0;i<4;i++){
            float px=centers[i];
            DynamicText(PortDigits[i],px-5.5f,30.5f,12,session.IsConnected(i)?White:Dim);
            if(session.IsConnected(i))Rounded(px+7,35.5f,4,4,2,Mint);
        }
        if(l>0)Rounded(368,394,94*l/100f,7.7f,Math.Min(3.8f,47*l/100f),Purple);
        if(r>0)Rounded(368,408,94*r/100f,7.7f,Math.Min(3.8f,47*r/100f),Purple);
        if(exit>0)Rect(38,451,566*exit/100f,1.5f,Mint);
        int used=Math.Max(glyphCount,oldGlyphCount);for(int i=glyphCount;i<used;i++)glyphs[i]=default;
        if(used>0)dynamicText.SetVertices(glyphs,0,used);oldGlyphCount=glyphCount;
        used=Math.Max(shapeCount,oldShapeCount);for(int i=shapeCount;i<used;i++)shapes[i]=default;
        if(used>0)dynamicShapes.SetVertices(shapes,0,used);oldShapeCount=shapeCount;
    }
    internal void Draw(Renderer renderer)
    {
        renderer.DrawMesh(chrome,shapeMaterial,identity);renderer.DrawMesh(dynamicShapes,shapeMaterial,identity);
        renderer.DrawMesh(text,glyphMaterial,identity);renderer.DrawMesh(dynamicText,glyphMaterial,identity);
    }
    private static int Quantize(float value)=>(int)Math.Round(value*100);
    private static int Percent(float value)=>Math.Clamp((int)Math.Round(value*100),0,100);
    private void Number(int value,float x,float y,float size)
    {
        int n=Math.Abs(value);Glyph(value<0?'-':'+',ref x,y,size,White);Glyph((char)('0'+n/100),ref x,y,size,White);Glyph('.',ref x,y,size,White);
        Glyph((char)('0'+n/10%10),ref x,y,size,White);Glyph((char)('0'+n%10),ref x,y,size,White);
    }
    private void Integer(int value,float x,float y,float size)
    {
        if(value>=100)Glyph('1',ref x,y,size,White);if(value>=10)Glyph((char)('0'+value/10%10),ref x,y,size,White);
        Glyph((char)('0'+value%10),ref x,y,size,White);Glyph('%',ref x,y,size,White);
    }
    private void DynamicText(string value,float x,float y,float size,Color32 color,float maxWidth=640)
    {
        float start=x;
        for(int i=0;i<value.Length;i++){
            float width=Advance(value[i],size);
            if(x+width>start+maxWidth){x=start;y+=size+3;if(y>418&&start==523)return;}
            Glyph(value[i],ref x,y,size,color);
        }
    }
    private void Glyph(char value,ref float x,float y,float size,Color32 color)
    {
        int ch=value-32;if(ch<0||ch>=96)return;
        if(glyphCount+4>glyphs.Length)throw new InvalidOperationException("Controller HUD glyph capacity");
        float s=size/28,u=ch%16/16f,v=ch/16*40f/256;
        glyphs[glyphCount++]=TextVertex(x,y,u,v,color);glyphs[glyphCount++]=TextVertex(x+32*s,y,u+32f/512,v,color);
        glyphs[glyphCount++]=TextVertex(x+32*s,y+40*s,u+32f/512,v+40f/256,color);glyphs[glyphCount++]=TextVertex(x,y+40*s,u,v+40f/256,color);
        x+=IplFontData.Widths[ch]*s;
    }
    private static float Advance(char value,float size)=>value>=32&&value<128?IplFontData.Widths[value-32]*size/28:0;
    private static float Measure(string text,float size){float width=0;for(int i=0;i<text.Length;i++)width+=Advance(text[i],size);return width;}
    private static VertexPositionColorTexture TextVertex(float x,float y,float u,float v,Color32 color)=>new VertexPositionColorTexture(new Vector3(x-320,240-y,0),color,new Vector2(u,v));
    private static ushort[] QuadIndices(int vertices){var result=new ushort[vertices/4*6];for(int i=0;i<vertices/4;i++){int at=i*6;ushort v=(ushort)(i*4);result[at]=v;result[at+1]=(ushort)(v+1);result[at+2]=(ushort)(v+2);result[at+3]=v;result[at+4]=(ushort)(v+2);result[at+5]=(ushort)(v+3);}return result;}
    private void Rect(float x,float y,float w,float h,Color32 color){Triangle(x,y,x+w,y,x+w,y+h,color);Triangle(x,y,x+w,y+h,x,y+h,color);}
    private void Triangle(float ax,float ay,float bx,float by,float cx,float cy,Color32 color)
    {
        if(shapeCount+3>shapes.Length)throw new InvalidOperationException("Controller HUD shape capacity");
        shapes[shapeCount++]=Vertex(ax,ay,color);shapes[shapeCount++]=Vertex(bx,by,color);shapes[shapeCount++]=Vertex(cx,cy,color);
    }
    private void Rounded(float x,float y,float w,float h,float radius,Color32 color)
    {
        radius=Math.Min(radius,Math.Min(w,h)/2);Rect(x+radius,y,w-2*radius,h,color);Rect(x,y+radius,radius,h-2*radius,color);Rect(x+w-radius,y+radius,radius,h-2*radius,color);
        for(int corner=0;corner<4;corner++){float ox=corner==0||corner==3?x+radius:x+w-radius,oy=corner<2?y+radius:y+h-radius;
            for(int i=0;i<4;i++){float a=(corner*90+180+i*22.5f)*.01745329252f,b=a+22.5f*.01745329252f;Triangle(ox,oy,ox+(float)Math.Cos(a)*radius,oy+(float)Math.Sin(a)*radius,ox+(float)Math.Cos(b)*radius,oy+(float)Math.Sin(b)*radius,color);}}
    }
    private static VertexPositionColor Vertex(float x,float y,Color32 color)=>new VertexPositionColor(new Vector3(x-320,240-y,0),color);
    public void Dispose(){dynamicShapes.Dispose();dynamicText.Dispose();chrome.Dispose();text.Dispose();atlas.Dispose();}
    private sealed class TextBuilder
    {
        private readonly List<VertexPositionColorTexture> vertices=new List<VertexPositionColorTexture>();
        internal void Center(string value,float x,float y,float size,Color32 color)=>Text(value,x-Measure(value,size)/2,y,size,color);
        internal void Text(string value,float x,float y,float size,Color32 color){for(int i=0;i<value.Length;i++){int ch=value[i]-32;if(ch<0||ch>=96)continue;float s=size/28,u=ch%16/16f,v=ch/16*40f/256;vertices.Add(TextVertex(x,y,u,v,color));vertices.Add(TextVertex(x+32*s,y,u+32f/512,v,color));vertices.Add(TextVertex(x+32*s,y+40*s,u+32f/512,v+40f/256,color));vertices.Add(TextVertex(x,y+40*s,u,v+40f/256,color));x+=IplFontData.Widths[ch]*s;}}
        internal Mesh Build(){var array=vertices.ToArray();return Mesh.Create(array,QuadIndices(array.Length));}
    }
    private sealed class ShapeBuilder
    {
        private readonly List<VertexPositionColor> vertices=new List<VertexPositionColor>();
        internal void GradientQuad(float x,float y,float w,float h,Color32 a,Color32 b,Color32 c,Color32 d){vertices.Add(Vertex(x,y,a));vertices.Add(Vertex(x+w,y,b));vertices.Add(Vertex(x+w,y+h,c));vertices.Add(Vertex(x,y,a));vertices.Add(Vertex(x+w,y+h,c));vertices.Add(Vertex(x,y+h,d));}
        internal void Rect(float x,float y,float w,float h,Color32 color)=>GradientQuad(x,y,w,h,color,color,color,color);
        internal void Line(float ax,float ay,float bx,float by,Color32 color,float width){float dx=bx-ax,dy=by-ay,n=(float)Math.Sqrt(dx*dx+dy*dy);if(n==0)return;float ox=-dy/n*width*.5f,oy=dx/n*width*.5f;vertices.Add(Vertex(ax+ox,ay+oy,color));vertices.Add(Vertex(bx+ox,by+oy,color));vertices.Add(Vertex(bx-ox,by-oy,color));vertices.Add(Vertex(ax+ox,ay+oy,color));vertices.Add(Vertex(bx-ox,by-oy,color));vertices.Add(Vertex(ax-ox,ay-oy,color));}
        internal void Rounded(float x,float y,float w,float h,float r,Color32 color){r=Math.Min(r,Math.Min(w,h)/2);Rect(x+r,y,w-2*r,h,color);Rect(x,y+r,r,h-2*r,color);Rect(x+w-r,y+r,r,h-2*r,color);for(int corner=0;corner<4;corner++){float ox=corner==0||corner==3?x+r:x+w-r,oy=corner<2?y+r:y+h-r;for(int i=0;i<6;i++){float a=(corner*90+180+i*15)*.01745329252f,b=a+15*.01745329252f;vertices.Add(Vertex(ox,oy,color));vertices.Add(Vertex(ox+(float)Math.Cos(a)*r,oy+(float)Math.Sin(a)*r,color));vertices.Add(Vertex(ox+(float)Math.Cos(b)*r,oy+(float)Math.Sin(b)*r,color));}}}
        internal Mesh Build()=>Mesh.Create(vertices.ToArray());
    }
}
