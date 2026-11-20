using System;
using System.Collections.Generic;
using Dolphin.Graphics;
using Dolphin.Input;

/// <summary>
/// Rigid controls registered to the approved photograph. Neutral projection is
/// deliberately the source image: each face UV comes from its original screen
/// position. Depth, cap bevels, shafts and pivots remain real 3D geometry.
/// </summary>
internal sealed class ControllerControls : IDisposable
{
    private readonly List<Part> parts=new List<Part>();
    private readonly Material face,pressed;
    private bool disposed;
    internal int TriangleCount {get;private set;}
    internal int VertexCount {get;private set;}
    internal int NativeBytes {get;private set;}
    internal int PartCount=>parts.Count;

    private sealed class Part
    {
        internal readonly Mesh Face,Sides;
        internal readonly Material SideMaterial;
        internal readonly Transform Pose=new Transform();
        internal readonly Vector3 Rest;
        internal readonly ControllerButton Button;
        internal readonly int Kind;
        internal float Press,X,Y;
        internal Part(Mesh face,Mesh sides,Material sideMaterial,Vector3 rest,int kind,ControllerButton button)
        {Face=face;Sides=sides;SideMaterial=sideMaterial;Rest=rest;Kind=kind;Button=button;Pose.LocalPosition=rest;}
    }

    internal ControllerControls(GraphicsDevice device,Texture2D originalMockup)
    {
        if(device==null)throw new ArgumentNullException("device");
        if(originalMockup==null)throw new ArgumentNullException("originalMockup");
        face=new Material{Texture=originalMockup,Lighting=LightingMode.Unlit,Cull=CullMode.None};
        pressed=new Material{Texture=originalMockup,Lighting=LightingMode.Unlit,Cull=CullMode.None,Color=new Color32(218,218,218)};
        Add(0,435.80f,170.39f,4,0,ControllerButton.A,new Color32(15,100,81));
        Add(1,391.60f,190.28f,4,0,ControllerButton.B,new Color32(135,28,34));
        Add(2,477.57f,159.56f,4,0,ControllerButton.X,new Color32(114,114,130));
        Add(3,423.87f,128.84f,4,0,ControllerButton.Y,new Color32(114,114,130));
        Add(4,319.12f,166.63f,3.5f,0,ControllerButton.Start,new Color32(111,112,128));
        Add(5,203.54f,169.72f,-14,1,ControllerButton.None,new Color32(92,94,108));
        Add(6,256.35f,253.04f,3,3,ControllerButton.None,new Color32(95,97,114));
        Add(7,380.99f,253.92f,-14,2,ControllerButton.None,new Color32(158,115,17));
        Add(8,190.94f,92.38f,3,4,ControllerButton.L,new Color32(109,109,125));
        Add(9,447.51f,86.19f,3,5,ControllerButton.R,new Color32(109,109,125));
        Add(10,448.40f,95.25f,6,6,ControllerButton.Z,new Color32(64,34,133));
    }

    // Shared with the foreground depth mask, in native 640 x 480 screen pixels.
    // Rear L/R/Z are intentionally last: the shell covers their moving bases.
    internal static Vector2[][] GetApertures()
    {
        var result=new Vector2[11][];
        for(int i=0;i<result.Length;i++)result[i]=Contour(i);
        result[5]=Ellipse(203.54f,169.72f,29.2f,29.2f,8,.392699082f);
        result[7]=Ellipse(380.99f,253.92f,29.5f,29.5f,8,.392699082f);
        for(int i=0;i<8;i++)if(i!=5&&i!=7)result[i]=Expanded(result[i],1.2f);
        return result;
    }
    private static Vector2[] Expanded(Vector2[] points,float amount)
    {
        float x=0,y=0;for(int i=0;i<points.Length;i++){x+=points[i].X;y+=points[i].Y;}x/=points.Length;y/=points.Length;
        var result=new Vector2[points.Length];
        for(int i=0;i<points.Length;i++){float dx=points[i].X-x,dy=points[i].Y-y,length=(float)Math.Sqrt(dx*dx+dy*dy);result[i]=new Vector2(points[i].X+dx/length*amount,points[i].Y+dy/length*amount);}
        return result;
    }
    private static Vector2[] Ellipse(float x,float y,float rx,float ry,int count=48,float angle=0)
    {
        var result=new Vector2[count];for(int i=0;i<count;i++){float a=angle+i*6.283185307f/count;result[i]=new Vector2(x+(float)Math.Cos(a)*rx,y+(float)Math.Sin(a)*ry);}return result;
    }
    private static Vector2[] Points(params float[] xy)
    {
        var result=new Vector2[xy.Length/2];for(int i=0;i<result.Length;i++)result[i]=new Vector2(xy[i*2],xy[i*2+1]);return result;
    }
    private static Vector2[] Contour(int index)
    {
        if(index==0)return Ellipse(435.80f,170.39f,24.75f,24.98f);
        if(index==1)return Ellipse(391.60f,190.28f,14.58f,14.80f);
        if(index==2)return Rounded(Points(474,137.5f,480,139,486,147,490,159,491.05f,169,488,178,483,181.66f,477,181,472,176,470,167,468,158,465,150,464.09f,145,467,140),.65f);
        if(index==3)return Rounded(Points(404,124,410,118,420,115,431,114.48f,440,117,445,122,445.97f,128,444,134,439,136,430,136.8f,421,140,412,143.20f,406,141,401.77f,136,401.77f,130),.65f);
        if(index==4)return Ellipse(319.12f,166.63f,9.28f,9.28f,32);
        if(index==5)return Ellipse(203.54f,169.72f,28.5f,27.85f,64);
        if(index==6)return Rounded(Points(247.3f,227.62f,265.3f,227.62f,265.3f,245,281.99f,245,281.99f,262.9f,265.3f,262.9f,265.3f,278.45f,247.3f,278.45f,247.3f,262.9f,230.72f,262.9f,230.72f,245,247.3f,245),1.3f);
        if(index==7)return Ellipse(380.99f,253.92f,27.85f,28.06f,8,.392699082f);
        if(index==8)return Rounded(Points(156.46f,113.59f,158,102,162,92,169,82,179,75,188,71.16f,199,72,210,76,219,82,225.41f,88,211,92,191,100,171,109),.65f);
        if(index==9)return Rounded(Points(418.12f,82.5f,427,76,438,72.49f,449,73,459,78,467,85,473,94,476.91f,99.89f,460,94,443,88,428,84),.65f);
        return Rounded(Points(415.91f,83.4f,424,81.33f,436,82.7f,450,87,465,92.8f,476.6f,99.8f,480.88f,104,480,108,476,109.17f,465,103.7f,450,97,435,91.8f,422,87.9f),.65f);
    }
    // Round individual corners without Catmull-Rom overshoot beyond the measured
    // photographic silhouette. Concave D-pad corners remain concave.
    private static Vector2[] Rounded(Vector2[] source,float radius)
    {
        var result=new Vector2[source.Length*4];
        for(int i=0;i<source.Length;i++){
            var a=source[(i+source.Length-1)%source.Length];var b=source[i];var c=source[(i+1)%source.Length];
            float ax=a.X-b.X,ay=a.Y-b.Y,cx=c.X-b.X,cy=c.Y-b.Y;
            float al=(float)Math.Sqrt(ax*ax+ay*ay),cl=(float)Math.Sqrt(cx*cx+cy*cy),r=Math.Min(radius,Math.Min(al,cl)*.3f);
            var p=new Vector2(b.X+ax/al*r,b.Y+ay/al*r);var q=new Vector2(b.X+cx/cl*r,b.Y+cy/cl*r);
            for(int j=0;j<4;j++){float t=j/3f,s=1-t;result[i*4+j]=new Vector2(p.X*s*s+b.X*2*s*t+q.X*t*t,p.Y*s*s+b.Y*2*s*t+q.Y*t*t);}
        }
        return result;
    }

    private void Add(int index,float cx,float cy,float restZ,int kind,ControllerButton button,Color32 sideColor)
    {
        var outline=Contour(index);EnsureCounterClockwise(outline);
        var cap=new Builder();var sides=new Builder();
        bool stick=kind==1||kind==2;
        if(stick){
            // The boot, bevel and upper thumb disk have separate depth rings;
            // their UVs are the unchanged neutral projection of the photograph.
            var inner=Scale(outline,cx,cy,kind==1?.83f:.58f);
            Ring(cap,outline,inner,cx,cy,kind==1?15:15,kind==1?29:27);
            Face(cap,inner,cx,cy,kind==1?29:27,0);
            Walls(sides,outline,cx,cy,0,15,sideColor);
            Shaft(sides,cx,cy,kind==1?8:7,-5,17,sideColor);
        }else{
            var inner=Scale(outline,cx,cy,.96f);Ring(cap,outline,inner,cx,cy,0,1.1f);
            Face(cap,inner,cx,cy,1.1f,0);
            Walls(sides,outline,cx,cy,-7,0,sideColor);
        }
        Mesh capMesh=Upload(cap),sideMesh=Upload(sides);
        var sideMaterial=new Material{Lighting=LightingMode.Unlit,Cull=CullMode.None};
        parts.Add(new Part(capMesh,sideMesh,sideMaterial,new Vector3(cx-320,240-cy,restZ),kind,button));
    }
    private Mesh Upload(Builder b){TriangleCount+=b.Indices.Count/3;VertexCount+=b.Vertices.Count;var mesh=Mesh.Create(b.Vertices.ToArray(),b.Indices.ToArray());NativeBytes+=mesh.NativeBytes;return mesh;}
    private static Vector2[] Scale(Vector2[] points,float cx,float cy,float factor)
    {var result=new Vector2[points.Length];for(int i=0;i<points.Length;i++)result[i]=new Vector2(cx+(points[i].X-cx)*factor,cy+(points[i].Y-cy)*factor);return result;}
    private static Vector2[] Inset(Vector2[] points,float amount)
    {
        var result=new Vector2[points.Length];
        for(int i=0;i<points.Length;i++){var before=points[(i+points.Length-1)%points.Length];var after=points[(i+1)%points.Length];float dx=after.X-before.X,dy=after.Y-before.Y,length=(float)Math.Sqrt(dx*dx+dy*dy);result[i]=new Vector2(points[i].X-dy/length*amount,points[i].Y+dx/length*amount);}return result;
    }
    private static void Ring(Builder b,Vector2[] outer,Vector2[] inner,float cx,float cy,float outerZ,float innerZ)
    {
        int start=b.Vertices.Count;for(int i=0;i<outer.Length;i++){b.Photo(outer[i],cx,cy,outerZ);b.Photo(inner[i],cx,cy,innerZ);}
        for(int i=0;i<outer.Length;i++){int next=(i+1)%outer.Length;b.Quad(start+i*2,start+next*2,start+next*2+1,start+i*2+1);}
    }
    private static void Face(Builder b,Vector2[] outline,float cx,float cy,float z,float slope)
    {
        int start=b.Vertices.Count;for(int i=0;i<outline.Length;i++)b.Photo(outline[i],cx,cy,z+(cy-outline[i].Y)*slope);
        var indices=Triangulate(outline);for(int i=0;i<indices.Length;i+=3)b.Triangle(start+indices[i],start+indices[i+1],start+indices[i+2]);
    }
    private static void Walls(Builder b,Vector2[] outline,float cx,float cy,float low,float high,Color32 color)
    {
        int start=b.Vertices.Count;for(int i=0;i<outline.Length;i++){b.Solid(outline[i],cx,cy,low,color);b.Solid(outline[i],cx,cy,high,color);}
        for(int i=0;i<outline.Length;i++){int next=(i+1)%outline.Length;b.Quad(start+i*2,start+next*2,start+next*2+1,start+i*2+1);}
    }
    private static void Shaft(Builder b,float cx,float cy,float radius,float low,float high,Color32 color)=>Walls(b,Ellipse(cx,cy,radius,radius,20),cx,cy,low,high,color);
    private sealed class Builder
    {
        internal readonly List<VertexPositionNormalColorTexture> Vertices=new List<VertexPositionNormalColorTexture>();
        internal readonly List<ushort> Indices=new List<ushort>();
        internal void Photo(Vector2 p,float cx,float cy,float z)=>Vertices.Add(new VertexPositionNormalColorTexture(new Vector3(p.X-cx,cy-p.Y,z),new Vector3(0,0,1),new Color32(255,255,255),new Vector2(p.X/640,p.Y/480)));
        internal void Solid(Vector2 p,float cx,float cy,float z,Color32 color)=>Vertices.Add(new VertexPositionNormalColorTexture(new Vector3(p.X-cx,cy-p.Y,z),new Vector3(0,0,1),color,Vector2.Zero));
        internal void Triangle(int a,int b,int c){Indices.Add((ushort)a);Indices.Add((ushort)b);Indices.Add((ushort)c);}
        internal void Quad(int a,int b,int c,int d){Triangle(a,b,c);Triangle(a,c,d);}
    }
    private static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
    private static void EnsureCounterClockwise(Vector2[] p)
    {float area=0;for(int i=0;i<p.Length;i++){var a=p[i];var b=p[(i+1)%p.Length];area+=a.X*b.Y-b.X*a.Y;}if(area<0)Array.Reverse(p);}
    private static int[] Triangulate(Vector2[] p)
    {
        var remaining=new List<int>(p.Length);for(int i=0;i<p.Length;i++)remaining.Add(i);var result=new List<int>((p.Length-2)*3);
        while(remaining.Count>3){bool found=false;
            for(int i=0;i<remaining.Count;i++){
                int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];if(Cross(p[a],p[b],p[c])<=.00001f)continue;
                bool occupied=false;for(int j=0;j<remaining.Count;j++){int q=remaining[j];if(q==a||q==b||q==c)continue;if(Cross(p[a],p[b],p[q])>-.00001f&&Cross(p[b],p[c],p[q])>-.00001f&&Cross(p[c],p[a],p[q])>-.00001f){occupied=true;break;}}
                if(occupied)continue;result.Add(a);result.Add(b);result.Add(c);remaining.RemoveAt(i);found=true;break;
            }
            if(!found)throw new InvalidOperationException("Controller control silhouette cannot be triangulated.");
        }
        result.Add(remaining[0]);result.Add(remaining[1]);result.Add(remaining[2]);return result.ToArray();
    }
    private static Quaternion Rotation(float x,float y,float z=0)
    {
        float sx=(float)Math.Sin(x*.5f),cx=(float)Math.Cos(x*.5f),sy=(float)Math.Sin(y*.5f),cy=(float)Math.Cos(y*.5f),sz=(float)Math.Sin(z*.5f),cz=(float)Math.Cos(z*.5f);
        return new Quaternion(sx*cy*cz-cx*sy*sz,cx*sy*cz+sx*cy*sz,cx*cy*sz-sx*sy*cz,cx*cy*cz+sx*sy*sz);
    }
    internal void Draw(Renderer renderer,ControllerState state,float deltaSeconds)
    {
        if(disposed)throw new ObjectDisposedException("ControllerControls");
        float blend=Math.Min(1,Math.Max(0,deltaSeconds)*32);
        for(int i=0;i<parts.Count;i++){
            var p=parts[i];float press=0,x=0,y=0;
            if(p.Kind==1||p.Kind==2){var stick=p.Kind==1?state.Stick:state.CStick;x=-stick.Y*.36f;y=stick.X*.36f;}
            else if(p.Kind==3){x=((state.DPadDown.IsHeld?1:0)-(state.DPadUp.IsHeld?1:0))*.13f;y=((state.DPadRight.IsHeld?1:0)-(state.DPadLeft.IsHeld?1:0))*.13f;}
            else if(p.Kind==4||p.Kind==5)press=state[p.Button].IsHeld?1:(p.Kind==4?state.LeftTrigger:state.RightTrigger)*.9f;
            else press=state[p.Button].IsHeld?1:0;
            p.Press+=(press-p.Press)*blend;p.X+=(x-p.X)*blend;p.Y+=(y-p.Y)*blend;
            if(Math.Abs(p.Press)<.0001f)p.Press=0;if(Math.Abs(p.X)<.0001f)p.X=0;if(Math.Abs(p.Y)<.0001f)p.Y=0;
            bool trigger=p.Kind==4||p.Kind==5;
            float travel=trigger?8:p.Kind==6?8:6;
            p.Pose.LocalPosition=new Vector3(p.Rest.X,p.Rest.Y-p.Press*(trigger?5:1.3f),p.Rest.Z-p.Press*travel);
            p.Pose.LocalRotation=Rotation(p.X+(trigger?-p.Press*.10f:p.Kind==6?-p.Press*.08f:0),p.Y);
            bool darken=trigger?p.Press>.95f:p.Kind==0||p.Kind==6?p.Press>.10f:false;
            renderer.DrawMesh(p.Sides,p.SideMaterial,p.Pose);
            renderer.DrawMesh(p.Face,darken?pressed:face,p.Pose);
        }
    }
    public void Dispose(){if(disposed)return;disposed=true;for(int i=0;i<parts.Count;i++){parts[i].Face.Dispose();parts[i].Sides.Dispose();}}
}
