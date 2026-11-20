using System;
using System.Collections.Generic;
using Dolphin.Graphics;

// Startup-only indexed geometry. Once uploaded, the builder and its arrays are
// collectible; the frame loop changes only the retained rigid transforms.
internal sealed class GeometryBuilder
{
    private readonly bool studioLighting,contactShadows;
    internal GeometryBuilder(bool studioLighting=true,bool contactShadows=false){this.studioLighting=studioLighting;this.contactShadows=contactShadows;}
    private readonly List<VertexPositionNormalColorTexture> vertices=new List<VertexPositionNormalColorTexture>();
    private readonly List<ushort> indices=new List<ushort>();
    internal int TriangleCount=>indices.Count/3;
    internal int VertexCount=>vertices.Count;
    internal int Vertex(Vector3 p,Vector3 n,Color32 color,bool shade=true)
    {
        if(vertices.Count>=65535)throw new InvalidOperationException("Controller mesh exceeds indexed vertex range.");
        n=Normalize(n);if(studioLighting&&shade)color=Studio(p,n,color,contactShadows);
        vertices.Add(new VertexPositionNormalColorTexture(p,n,color,Vector2.Zero));return vertices.Count-1;
    }
    internal void Triangle(int a,int b,int c){indices.Add((ushort)a);indices.Add((ushort)b);indices.Add((ushort)c);}
    internal void Quad(int a,int b,int c,int d){Triangle(a,b,c);Triangle(a,c,d);}
    internal Mesh Upload()
    {
        var mesh=Mesh.Create(vertices.ToArray(),indices.ToArray());
        // The native mesh owns its copy. Do not retain oversized startup List
        // arrays while subsequent parts/fonts are admitted.
        vertices.Clear();vertices.Capacity=0;indices.Clear();indices.Capacity=0;return mesh;
    }
    private static float Contact(float x,float y,float cx,float cy,float radius,float width)
    {
        float dx=x-cx,dy=y-cy,d=(float)Math.Sqrt(dx*dx+dy*dy);
        float q=Math.Max(0,1-Math.Abs(d-radius)/width);return q*q;
    }
    private static Color32 Studio(Vector3 p,Vector3 n,Color32 color,bool contact)
    {
        // Baked soft studio illumination: no texture, light loop, skinning or
        // geometry update is needed during input animation.
        float key=Math.Max(0,-n.X*.401f+n.Y*.573f+n.Z*.715f);
        float fill=Math.Max(0,n.X*.625f+n.Y*.156f+n.Z*.765f);
        float shade=.22f+.73f*key+.10f*fill;
        float half=Math.Max(0,-n.X*.214f+n.Y*.306f+n.Z*.928f);
        float h2=half*half,h4=h2*h2,h8=h4*h4;
        float spec=h8*h8*h4*.145f;
        if(contact&&n.Z>.35f&&p.Z>0){
            float ao=.16f*Contact(p.X,p.Y,-1.397f,.491f,.39f,.14f)
                +.11f*Contact(p.X,p.Y,.773f,-.539f,.355f,.13f)
                +.075f*Contact(p.X,p.Y,1.411f,.492f,.29f,.09f)
                +.045f*Contact(p.X,p.Y,.893f,.232f,.18f,.07f);
            shade*=1-Math.Min(.24f,ao);
        }
        return new Color32((byte)Math.Clamp(color.R*shade+spec*206,0,255),
            (byte)Math.Clamp(color.G*shade+spec*214,0,255),
            (byte)Math.Clamp(color.B*shade+spec*238,0,255),color.A);
    }
    internal static Vector3 Normalize(Vector3 n)
    {
        float length=(float)Math.Sqrt(n.X*n.X+n.Y*n.Y+n.Z*n.Z);
        return length>.000001f?n*(1/length):new Vector3(0,0,1);
    }
    internal static Vector2[] Circle(float radius,int count=48,float angle=0)
    {
        var points=new Vector2[count];
        for(int i=0;i<count;i++){float a=angle+i*6.283185307f/count;points[i]=new Vector2((float)Math.Cos(a)*radius,(float)Math.Sin(a)*radius);}
        return points;
    }
    internal static Vector2[] Rotate(Vector2[] points,float angle)
    {
        float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);var result=new Vector2[points.Length];
        for(int i=0;i<points.Length;i++)result[i]=new Vector2(points[i].X*c-points[i].Y*s,points[i].X*s+points[i].Y*c);
        return result;
    }
    internal static Vector2[] Spline(Vector2[] points,int count)
    {
        var result=new Vector2[count];
        for(int i=0;i<count;i++){
            float along=i*points.Length/(float)count;int k=(int)along;float t=along-k,t2=t*t,t3=t2*t;
            var a=points[(k+points.Length-1)%points.Length];var b=points[k%points.Length];
            var c=points[(k+1)%points.Length];var d=points[(k+2)%points.Length];
            result[i]=new Vector2(.5f*((2*b.X)+(-a.X+c.X)*t+(2*a.X-5*b.X+4*c.X-d.X)*t2+(-a.X+3*b.X-3*c.X+d.X)*t3),
                .5f*((2*b.Y)+(-a.Y+c.Y)*t+(2*a.Y-5*b.Y+4*c.Y-d.Y)*t2+(-a.Y+3*b.Y-3*c.Y+d.Y)*t3));
        }
        EnsureCounterClockwise(result);return result;
    }
    internal static void EnsureCounterClockwise(Vector2[] points)
    {
        float area=0;for(int i=0;i<points.Length;i++){var a=points[i];var b=points[(i+1)%points.Length];area+=a.X*b.Y-b.X*a.Y;}
        if(area<0)Array.Reverse(points);
    }
    internal static Vector2 Outward(Vector2[] p,int i)
    {
        var a=p[(i+p.Length-1)%p.Length];var b=p[(i+1)%p.Length];float x=b.Y-a.Y,y=a.X-b.X;
        float length=(float)Math.Sqrt(x*x+y*y);return new Vector2(x/length,y/length);
    }
    internal static Vector2[] Inset(Vector2[] p,float amount)
    {
        var result=new Vector2[p.Length];
        for(int i=0;i<p.Length;i++){var n=Outward(p,i);result[i]=new Vector2(p[i].X-n.X*amount,p[i].Y-n.Y*amount);}
        return result;
    }
    private static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
    internal static int[] Triangulate(Vector2[] p)
    {
        var remaining=new List<int>(p.Length);for(int i=0;i<p.Length;i++)remaining.Add(i);
        var triangles=new List<int>((p.Length-2)*3);int attempts=0;
        while(remaining.Count>3){
            bool found=false;
            for(int j=0;j<remaining.Count;j++){
                int a=remaining[(j+remaining.Count-1)%remaining.Count],b=remaining[j],c=remaining[(j+1)%remaining.Count];
                if(Cross(p[a],p[b],p[c])<=.0000001f)continue;
                bool occupied=false;
                for(int k=0;k<remaining.Count;k++){
                    int q=remaining[k];if(q==a||q==b||q==c)continue;
                    if(Cross(p[a],p[b],p[q])>=-.0000001f&&Cross(p[b],p[c],p[q])>=-.0000001f&&Cross(p[c],p[a],p[q])>=-.0000001f){occupied=true;break;}
                }
                if(occupied)continue;triangles.Add(a);triangles.Add(b);triangles.Add(c);remaining.RemoveAt(j);found=true;break;
            }
            if(!found||++attempts>p.Length*p.Length)throw new InvalidOperationException("Controller outline cannot be triangulated.");
        }
        triangles.Add(remaining[0]);triangles.Add(remaining[1]);triangles.Add(remaining[2]);return triangles.ToArray();
    }
    internal void FlatFace(Vector2[] outline,float z,Color32 color)
    {
        int start=vertices.Count;for(int i=0;i<outline.Length;i++)Vertex(new Vector3(outline[i].X,outline[i].Y,z),new Vector3(0,0,1),color);
        var triangles=Triangulate(outline);for(int i=0;i<triangles.Length;i+=3)Triangle(start+triangles[i],start+triangles[i+1],start+triangles[i+2]);
    }
    internal void Cap(Vector2[] outline,float top,Color32 color,float bevel=.018f)
    {
        EnsureCounterClockwise(outline);int count=outline.Length,start=vertices.Count;
        // Rounded cap, including a real sidewall and a narrower beveled top.
        for(int ring=0;ring<4;ring++)for(int i=0;i<count;i++){
            var p=outline[i];var n=Outward(outline,i);float inset=ring==0?bevel*.55f:ring==3?bevel:0;
            float z=ring==0?0:ring==1?bevel:ring==2?top-bevel:top;
            var normal=ring==0?new Vector3(n.X*.8f,n.Y*.8f,-.45f):ring==3?new Vector3(n.X*.45f,n.Y*.45f,.8f):new Vector3(n.X,n.Y,ring==2?.25f:0);
            Vertex(new Vector3(p.X-n.X*inset,p.Y-n.Y*inset,z),normal,color);
        }
        for(int ring=0;ring<3;ring++)for(int i=0;i<count;i++){int next=(i+1)%count;Quad(start+ring*count+i,start+ring*count+next,start+(ring+1)*count+next,start+(ring+1)*count+i);}
        var topOutline=Inset(outline,bevel);FlatFace(topOutline,top,color);
    }
    internal void Lathe(float[] radius,float[] height,int segments,Color32 color)
    {
        int start=vertices.Count;
        for(int ring=0;ring<radius.Length;ring++)for(int i=0;i<segments;i++){
            float a=i*6.283185307f/segments,c=(float)Math.Cos(a),s=(float)Math.Sin(a);
            int before=Math.Max(0,ring-1),after=Math.Min(radius.Length-1,ring+1);
            float dr=radius[after]-radius[before],dz=height[after]-height[before];
            Vertex(new Vector3(c*radius[ring],s*radius[ring],height[ring]),new Vector3(c*dz,s*dz,-dr),color);
        }
        for(int ring=0;ring<radius.Length-1;ring++)for(int i=0;i<segments;i++){int next=(i+1)%segments;Quad(start+ring*segments+i,start+ring*segments+next,start+(ring+1)*segments+next,start+(ring+1)*segments+i);}
    }
    internal void Stroke(float ax,float ay,float bx,float by,float z,float width,Color32 color,bool shell=false)
    {
        float dx=bx-ax,dy=by-ay,len=(float)Math.Sqrt(dx*dx+dy*dy);if(len<.000001f)return;
        float ox=-dy/len*width*.5f,oy=dx/len*width*.5f;int start=vertices.Count;
        StrokeVertex(ax+ox,ay+oy,z,color,shell);StrokeVertex(ax-ox,ay-oy,z,color,shell);
        StrokeVertex(bx-ox,by-oy,z,color,shell);StrokeVertex(bx+ox,by+oy,z,color,shell);Quad(start,start+1,start+2,start+3);
    }
    private void StrokeVertex(float x,float y,float z,Color32 color,bool shell)
    {
        Vertex(new Vector3(x,y,shell?ControllerModel.ShellHeight(x,y)+z:z),shell?ControllerModel.ShellNormal(x,y):new Vector3(0,0,1),color,false);
    }
    internal void Ring(float x,float y,float radius,float z,float width,Color32 color,int segments=48,bool shell=false)
    {
        for(int i=0;i<segments;i++){float a=i*6.283185307f/segments,b=(i+1)*6.283185307f/segments;Stroke(x+(float)Math.Cos(a)*radius,y+(float)Math.Sin(a)*radius,x+(float)Math.Cos(b)*radius,y+(float)Math.Sin(b)*radius,z,width,color,shell);}
    }
    internal void Outline(Vector2[] points,float z,float width,Color32 color,bool shell=false)
    {
        for(int i=0;i<points.Length;i++){var a=points[i];var b=points[(i+1)%points.Length];Stroke(a.X,a.Y,b.X,b.Y,z,width,color,shell);}
    }
    // Small vector legends remain attached to the actual cap mesh. No camera-
    // facing text, raster asset, per-frame projection, or z-fighting decal.
    internal void Text(string text,float x,float y,float height,float z,Color32 color,bool shell=false,float spacing=.25f)
    {
        float advance=height*(.64f+spacing),start=x-(text.Length-1)*advance*.5f-height*.32f;
        for(int i=0;i<text.Length;i++)Glyph(text[i],start+i*advance,y,height,z,color,shell);
    }
    private void Glyph(char ch,float x,float y,float h,float z,Color32 color,bool shell)
    {
        string lines=ch=='A'?"003660|1252":ch=='B'?"0006|064665644303|034362614000":
            ch=='C'?"6546160501104061":ch=='D'?"0006|063664623000":
            ch=='E'?"0006|0666|0353|0060":ch=='G'?"65461605011040616333":
            ch=='I'?"0666|3630|0060":ch=='L'?"060060":ch=='M'?"0006336660":
            ch=='N'?"00066066":ch=='O'?"264664624020020426":
            ch=='P'?"0006|064665644303":ch=='R'?"0006|064665644303|3360":
            ch=='S'?"654616050423436261501001":ch=='T'?"0666|3630":
            ch=='U'?"060220406266":ch=='X'?"0660|0066":ch=='Y'?"063366|3330":
            ch=='Z'?"06660060":ch=='/'?"0066":"";
        // Coordinates are in a simple 0..6 upright local glyph grid. Each
        // '|' begins another stroke; adjacent digit pairs form a polyline.
        int lastX=-1,lastY=-1;
        for(int i=0;i+1<lines.Length;){
            if(lines[i]=='|'){lastX=lastY=-1;i++;continue;}
            int px=lines[i]-'0',py=lines[i+1]-'0';i+=2;
            if(lastX>=0)Stroke(x+lastX*h*.1066667f,y+(lastY/6f-.5f)*h,x+px*h*.1066667f,y+(py/6f-.5f)*h,z,Math.Max(.014f,h*.075f),color,shell);
            lastX=px;lastY=py;
        }
    }
}
