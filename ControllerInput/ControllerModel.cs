using System;
using System.Collections.Generic;
using Dolphin.Graphics;
using Dolphin.Input;

/// <summary>
/// Original DOL-003 proportions, authored as one rounded silhouette rather than
/// overlapping primitive lobes. Front coordinates are measured from the user's
/// reference image; depth, bevels and rigid control travel are explicit meshes.
/// </summary>
internal sealed class ControllerModel : IDisposable
{
    private const float Width=4.8f,Height=3.367f;
    private static readonly Color32 Indigo=new Color32(101,91,170),Gray=new Color32(193,196,208),Dark=new Color32(31,29,49);
    private static readonly Color32 Edge=new Color32(118,106,170),Legend=new Color32(53,52,64);
    private readonly Material solid=new Material{Lighting=LightingMode.Unlit,Cull=CullMode.None};
    private readonly Material pressed=new Material{Lighting=LightingMode.Unlit,Cull=CullMode.None,Color=new Color32(216,216,216)};
    private readonly Material detail=new Material{Lighting=LightingMode.Unlit,Cull=CullMode.None};
    private readonly List<Part> parts=new List<Part>();
    private readonly Mesh body,details;
    private bool disposed;
    internal Transform Root {get;}=new Transform();
    internal int TriangleCount {get;private set;}
    internal int VertexCount {get;private set;}
    internal int NativeBytes {get;private set;}
    internal int PartCount=>parts.Count+2;

    private sealed class Part
    {
        internal readonly Mesh Mesh;
        internal readonly Transform Pose=new Transform();
        internal readonly Vector3 Rest;
        internal readonly int Kind;
        internal readonly ControllerButton Button;
        internal float Press,X,Y;
        internal Part(Mesh mesh,Transform parent,Vector3 rest,int kind,ControllerButton button)
        {Mesh=mesh;Rest=rest;Kind=kind;Button=button;Pose.Parent=parent;Pose.LocalPosition=rest;}
    }
    internal ControllerModel(GraphicsDevice device)
    {
        if(device==null)throw new ArgumentNullException("device");
        var shell=new GeometryBuilder(true,true);BuildShell(shell);body=Upload(shell);
        var ink=new GeometryBuilder(false);BuildDetails(ink);
        Button(ink,.794f,.354f,.2745f,new Color32(21,174,137),ControllerButton.A,'A');
        Button(ink,.686f,.431f,.1607f,new Color32(219,47,53),ControllerButton.B,'B');
        Button(ink,.501f,.352f,.1004f,Gray,ControllerButton.Start,' ');
        Kidney(ink,.897f,.320f,-1.74532925f,.52f,.29f,ControllerButton.X,'X');
        Kidney(ink,.766f,.211f,.2617994f,.51f,.29f,ControllerButton.Y,'Y');
        Stick(ink,.209f,.354f,false);Stick(ink,.661f,.660f,true);
        DPad(ink);Trigger(-1);Trigger(1);ZButton(ink);
        details=Upload(ink);
    }
    private Mesh Upload(GeometryBuilder builder)
    {
        TriangleCount+=builder.TriangleCount;VertexCount+=builder.VertexCount;
        var mesh=builder.Upload();NativeBytes+=mesh.NativeBytes;return mesh;
    }
    private void Add(GeometryBuilder builder,Vector3 rest,int kind,ControllerButton button)
        =>parts.Add(new Part(Upload(builder),Root,rest,kind,button));
    private static Vector2 Point(float u,float v)=>new Vector2((u-.5f)*Width,(.5f-v)*Height);
    private static Vector2 Pixel(float x,float y)=>Point((x-281)/717,(y-109)/503);
    private static float Dome(float x,float y,float cx,float cy,float r)
    {
        float dx=x-cx,dy=y-cy,q=1-(dx*dx+dy*dy)/(r*r);return q>0?q*q:0;
    }
    internal static float ShellHeight(float x,float y)
        =>.075f+.165f*Dome(x,y,-1.397f,.491f,.94f)+.155f*Dome(x,y,1.34f,.47f,1.05f)
            +.05f*Dome(x,y,-.75f,-.54f,.63f)+.05f*Dome(x,y,.77f,-.54f,.63f)
            +.04f*Dome(x,y,0,1.3f,1.55f);
    internal static Vector3 ShellNormal(float x,float y)
        =>GeometryBuilder.Normalize(new Vector3((ShellHeight(x-.008f,y)-ShellHeight(x+.008f,y))/.016f,
            (ShellHeight(x,y-.008f)-ShellHeight(x,y+.008f))/.016f,1));
    private static Vector2[] Silhouette()
    {
        // Clockwise from the crest in the source photograph. The lower path
        // includes the real arch and thumb-pad lobes, not a filled convex hull.
        var control=new[]{Pixel(640,109),Pixel(735,116),Pixel(810,134),Pixel(869,158),Pixel(916,193),Pixel(950,237),
            Pixel(972,289),Pixel(985,351),Pixel(995,425),Pixel(998,492),Pixel(992,553),Pixel(978,592),Pixel(958,610),
            Pixel(940,607),Pixel(918,578),Pixel(899,532),Pixel(878,477),Pixel(854,431),
            Pixel(840,473),Pixel(815,505),Pixel(783,523),Pixel(751,525),Pixel(718,514),Pixel(691,489),
            Pixel(673,459),Pixel(665,430),Pixel(669,397),Pixel(687,363),Pixel(717,335),
            Pixel(684,329),Pixel(640,327),Pixel(598,329),Pixel(562,335),
            Pixel(584,365),Pixel(604,397),Pixel(615,430),Pixel(612,459),Pixel(594,490),Pixel(568,513),
            Pixel(535,525),Pixel(504,522),Pixel(474,506),Pixel(449,478),Pixel(428,442),
            Pixel(408,479),Pixel(388,535),Pixel(365,583),Pixel(346,607),Pixel(326,612),Pixel(307,597),
            Pixel(293,567),Pixel(284,522),Pixel(282,476),Pixel(286,426),Pixel(296,355),Pixel(307,288),
            Pixel(325,233),Pixel(353,192),Pixel(395,158),Pixel(453,133),Pixel(532,116)};
        return GeometryBuilder.Spline(control,128);
    }
    private static void BuildShell(GeometryBuilder b)
    {
        var outline=Silhouette();var front=GeometryBuilder.Inset(outline,.072f);int frontStart=b.VertexCount;
        for(int i=0;i<front.Length;i++)b.Vertex(new Vector3(front[i].X,front[i].Y,ShellHeight(front[i].X,front[i].Y)),ShellNormal(front[i].X,front[i].Y),Indigo);
        var interior=GeometrySurfaceData.Interior;
        for(int i=0;i<interior.Length;i+=2){float x=interior[i],y=interior[i+1];b.Vertex(new Vector3(x,y,ShellHeight(x,y)),ShellNormal(x,y),Indigo);}
        var frontTriangles=GeometrySurfaceData.Triangles;
        for(int i=0;i<frontTriangles.Length;i+=3)b.Triangle(frontStart+frontTriangles[i],frontStart+frontTriangles[i+1],frontStart+frontTriangles[i+2]);
        int count=outline.Length;
        // Six profile rings form one continuous rolled shell boundary.
        var offset=new[]{.072f,.025f,0f,0f,.023f,.075f};int start=b.VertexCount;
        for(int ring=0;ring<6;ring++)for(int i=0;i<count;i++){
            var p=outline[i];var n=GeometryBuilder.Outward(outline,i);float x=p.X-n.X*offset[ring],y=p.Y-n.Y*offset[ring];
            float z=ring==0?ShellHeight(x,y):ring==1?ShellHeight(x,y)-.022f:ring==2?ShellHeight(x,y)-.075f:ring==3?-.12f:ring==4?-.255f:-.30f;
            var normal=ring==0?ShellNormal(x,y):ring==1?new Vector3(n.X*.38f,n.Y*.38f,.92f):ring==2?new Vector3(n.X*.8f,n.Y*.8f,.6f):ring==3?new Vector3(n.X,n.Y,0):ring==4?new Vector3(n.X*.7f,n.Y*.7f,-.7f):new Vector3(0,0,-1);
            var color=ring>3?new Color32(63,56,111):Indigo;b.Vertex(new Vector3(x,y,z),normal,color);
        }
        for(int ring=0;ring<5;ring++)for(int i=0;i<count;i++){int next=(i+1)%count;b.Quad(start+ring*count+i,start+ring*count+next,start+(ring+1)*count+next,start+(ring+1)*count+i);}
        var back=GeometryBuilder.Inset(outline,.075f);var backTriangles=GeometryBuilder.Triangulate(back);int backStart=b.VertexCount;
        for(int i=0;i<back.Length;i++)b.Vertex(new Vector3(back[i].X,back[i].Y,-.30f),new Vector3(0,0,-1),new Color32(59,53,104));
        for(int i=0;i<backTriangles.Length;i+=3)b.Triangle(backStart+backTriangles[i+2],backStart+backTriangles[i+1],backStart+backTriangles[i]);
    }
    private static Vector2[] Placed(Vector2[] points,Vector2 center,float scale=1)
    {
        var result=new Vector2[points.Length];for(int i=0;i<points.Length;i++)result[i]=new Vector2(center.X+points[i].X*scale,center.Y+points[i].Y*scale);return result;
    }
    private static void BuildDetails(GeometryBuilder b)
    {
        var outline=GeometryBuilder.Inset(Silhouette(),.025f);b.Outline(outline,.0025f,.008f,Edge,true);
        var left=GeometryBuilder.Spline(new[]{Pixel(432,165),Pixel(486,174),Pixel(532,211),Pixel(553,259),Pixel(558,315),Pixel(570,352),Pixel(597,392),Pixel(612,434),Pixel(608,472),Pixel(586,503),Pixel(554,521),Pixel(520,524),Pixel(486,512),Pixel(459,487),Pixel(442,453),Pixel(430,425),Pixel(398,403),Pixel(366,373),Pixel(342,332),Pixel(327,287),Pixel(325,243),Pixel(343,200),Pixel(384,173)},96);
        var right=GeometryBuilder.Spline(new[]{Pixel(845,167),Pixel(896,173),Pixel(938,201),Pixel(962,245),Pixel(972,290),Pixel(965,333),Pixel(943,367),Pixel(906,392),Pixel(868,417),Pixel(846,450),Pixel(830,487),Pixel(804,513),Pixel(769,525),Pixel(736,521),Pixel(707,505),Pixel(684,478),Pixel(670,447),Pixel(668,415),Pixel(682,378),Pixel(706,345),Pixel(723,312),Pixel(727,270),Pixel(746,226),Pixel(785,190)},96);
        b.Outline(left,.004f,.008f,new Color32(104,96,156),true);b.Outline(right,.004f,.008f,new Color32(104,96,156),true);
        b.Text("NINTENDO",0,1.32f,.104f,.004f,new Color32(215,217,229),true,.83f);
        b.Text("GAMECUBE",0,1.17f,.145f,.004f,new Color32(225,227,238),true,.40f);
        b.Text("START/PAUSE",0,.735f,.090f,.006f,new Color32(38,34,60),true,.05f);
    }
    private void Button(GeometryBuilder ink,float u,float v,float radius,Color32 color,ControllerButton button,char legend)
    {
        var center=Point(u,v);float z=ShellHeight(center.X,center.Y)+.013f;
        ink.FlatFace(Placed(GeometryBuilder.Circle(radius+.022f,48),center),z,Dark);
        var b=new GeometryBuilder();b.Cap(GeometryBuilder.Circle(radius,48),.105f,color,.022f);
        if(legend!=' ')b.Text(legend.ToString(),0,0,radius*.79f,.108f,button==ControllerButton.A?new Color32(16,94,74):new Color32(135,24,32));
        Add(b,new Vector3(center.X,center.Y,z),0,button);
    }
    private static Vector2[] Bean(float width,float height,float angle)
    {
        var p=new[]{new Vector2(-.50f,-.04f),new Vector2(-.43f,.32f),new Vector2(-.20f,.49f),new Vector2(.17f,.48f),
            new Vector2(.43f,.30f),new Vector2(.50f,.02f),new Vector2(.39f,-.31f),new Vector2(.16f,-.22f),new Vector2(-.05f,-.16f),new Vector2(-.32f,-.32f)};
        for(int i=0;i<p.Length;i++)p[i]=new Vector2(p[i].X*width,p[i].Y*height);
        return GeometryBuilder.Rotate(GeometryBuilder.Spline(p,48),angle);
    }
    private void Kidney(GeometryBuilder ink,float u,float v,float angle,float width,float height,ControllerButton button,char legend)
    {
        var center=Point(u,v);var shape=Bean(width,height,angle);float z=ShellHeight(center.X,center.Y)+.013f;
        ink.FlatFace(Placed(shape,center,1.10f),z,Dark);var b=new GeometryBuilder();b.Cap(shape,.105f,Gray,.018f);
        b.Text(legend.ToString(),0,.012f,.165f,.108f,Legend);Add(b,new Vector3(center.X,center.Y,z),0,button);
    }
    private static void Socket(GeometryBuilder b,Vector2 center,float radius,float z)
    {
        var outer=GeometryBuilder.Circle(radius,8);var inner=GeometryBuilder.Circle(radius-.035f,8);int start=b.VertexCount;
        for(int i=0;i<8;i++){
            b.Vertex(new Vector3(center.X+outer[i].X,center.Y+outer[i].Y,z+.018f),new Vector3(0,0,1),new Color32(111,99,169));
            b.Vertex(new Vector3(center.X+inner[i].X,center.Y+inner[i].Y,z+.003f),new Vector3(0,0,1),Dark);
        }
        for(int i=0;i<8;i++){int next=(i+1)%8;b.Quad(start+i*2,start+next*2,start+next*2+1,start+i*2+1);}
        b.FlatFace(Placed(inner,center),z+.002f,Dark);
    }
    private void Stick(GeometryBuilder ink,float u,float v,bool cStick)
    {
        var p=Point(u,v);float z=ShellHeight(p.X,p.Y)+.016f;Socket(ink,p,cStick?.355f:.385f,z);
        var b=new GeometryBuilder();
        if(cStick){
            var yellow=new Color32(234,183,39);b.Lathe(new[]{.29f,.295f,.21f,.21f,.19f,0f},new[]{0f,.035f,.075f,.12f,.145f,.145f},48,yellow);
            b.Text("C",0,0,.17f,.149f,new Color32(155,111,22));
        }else{
            b.Lathe(new[]{.245f,.26f,.16f,.14f,.265f,.2845f,.277f,.245f,0f},new[]{0f,.055f,.115f,.18f,.185f,.218f,.248f,.265f,.265f},48,Gray);
            var groove=new Color32(128,134,154);b.Ring(0,0,.095f,.269f,.011f,groove);b.Ring(0,0,.15f,.269f,.011f,groove);b.Ring(0,0,.207f,.269f,.011f,groove);
        }
        Add(b,new Vector3(p.X,p.Y,z+.005f),cStick?2:1,ControllerButton.None);
    }
    private void DPad(GeometryBuilder ink)
    {
        var center=Point(.343f,.658f);float z=ShellHeight(center.X,center.Y)+.015f;float r=.315f,w=.107f;
        var shape=new[]{new Vector2(-w,r),new Vector2(-w,w),new Vector2(-r,w),new Vector2(-r,-w),new Vector2(-w,-w),new Vector2(-w,-r),new Vector2(w,-r),new Vector2(w,-w),new Vector2(r,-w),new Vector2(r,w),new Vector2(w,w),new Vector2(w,r)};
        GeometryBuilder.EnsureCounterClockwise(shape);ink.FlatFace(Placed(shape,center,1.13f),z,Dark);
        var b=new GeometryBuilder();b.Cap(shape,.095f,new Color32(160,164,181),.022f);
        // Four small molded arrows rotate with the complete physical rocker.
        for(int i=0;i<4;i++){
            float a=i*1.570796327f,c=(float)Math.Cos(a),s=(float)Math.Sin(a);var tip=new Vector2(-s*.253f,c*.253f);
            var l=new Vector2(-.045f*c-s*.175f,-.045f*s+c*.175f);var rgt=new Vector2(.045f*c-s*.175f,.045f*s+c*.175f);
            b.Stroke(l.X,l.Y,tip.X,tip.Y,.099f,.008f,Legend);b.Stroke(tip.X,tip.Y,rgt.X,rgt.Y,.099f,.008f,Legend);b.Stroke(rgt.X,rgt.Y,l.X,l.Y,.099f,.008f,Legend);
        }
        Add(b,new Vector3(center.X,center.Y,z),3,ControllerButton.None);
    }
    private void Trigger(int side)
    {
        // Caps wrap around the REAR and are foreshortened in the front view.
        // Their highest points are below the shell's central crest.
        var b=new GeometryBuilder();var shape=GeometryBuilder.Spline(new[]{new Vector2(-.37f,-.10f),new Vector2(-.34f,.16f),new Vector2(-.20f,.25f),
            new Vector2(.13f,.25f),new Vector2(.34f,.14f),new Vector2(.37f,-.11f),new Vector2(.15f,-.19f),new Vector2(-.13f,-.19f)},40);
        b.Cap(shape,.13f,Gray,.025f);b.Text(side<0?"L":"R",0,.025f,.14f,.134f,Legend);
        Add(b,new Vector3(side*1.51f,1.39f,-.095f),side<0?4:5,side<0?ControllerButton.L:ControllerButton.R);
    }
    private void ZButton(GeometryBuilder ink)
    {
        var center=new Vector2(1.55f,1.30f);float z=ShellHeight(center.X,center.Y)+.012f;
        var shape=GeometryBuilder.Rotate(GeometryBuilder.Spline(new[]{new Vector2(-.34f,0),new Vector2(-.27f,.10f),new Vector2(.15f,.075f),
            new Vector2(.33f,-.015f),new Vector2(.27f,-.10f),new Vector2(-.18f,-.08f)},32),-.15f);
        ink.FlatFace(Placed(shape,center,1.06f),z,Dark);var b=new GeometryBuilder();b.Cap(shape,.065f,new Color32(104,61,204),.016f);
        b.Text("Z",0,0,.115f,.069f,new Color32(46,25,99));Add(b,new Vector3(center.X,center.Y,z),6,ControllerButton.Z);
    }
    private static Quaternion Rotation(float x,float y,float z=0)
    {
        float sx=(float)Math.Sin(x*.5f),cx=(float)Math.Cos(x*.5f),sy=(float)Math.Sin(y*.5f),cy=(float)Math.Cos(y*.5f),sz=(float)Math.Sin(z*.5f),cz=(float)Math.Cos(z*.5f);
        return new Quaternion(sx*cy*cz-cx*sy*sz,cx*sy*cz+sx*cy*sz,cx*cy*sz-sx*sy*cz,cx*cy*cz+sx*sy*sz);
    }
    internal void Draw(Renderer renderer,ControllerState state,float deltaSeconds)
    {
        if(disposed)throw new ObjectDisposedException("ControllerModel");
        float blend=Math.Min(1,Math.Max(0,deltaSeconds)*32);
        renderer.DrawMesh(body,solid,Root);renderer.DrawMesh(details,detail,Root);
        for(int i=0;i<parts.Count;i++){
            var p=parts[i];float press=0,x=0,y=0;
            if(p.Kind==1||p.Kind==2){var stick=p.Kind==1?state.Stick:state.CStick;x=-stick.Y*.36f;y=stick.X*.36f;}
            else if(p.Kind==3){x=((state.DPadDown.IsHeld?1:0)-(state.DPadUp.IsHeld?1:0))*.115f;y=((state.DPadRight.IsHeld?1:0)-(state.DPadLeft.IsHeld?1:0))*.115f;}
            // Reserve the final detent for the independent digital L/R click.
            // A calibrated analog value of 1 alone must still differ from it.
            else if(p.Kind==4||p.Kind==5)press=state[p.Button].IsHeld?1:(p.Kind==4?state.LeftTrigger:state.RightTrigger)*.9f;
            else press=state[p.Button].IsHeld?1:0;
            p.Press+=(press-p.Press)*blend;p.X+=(x-p.X)*blend;p.Y+=(y-p.Y)*blend;
            if(p.Kind==4||p.Kind==5){
                p.Pose.LocalPosition=new Vector3(p.Rest.X,p.Rest.Y-p.Press*.10f,p.Rest.Z-p.Press*.09f);
                p.Pose.LocalRotation=Rotation(-.70f,0);
            }else{
                p.Pose.LocalPosition=new Vector3(p.Rest.X,p.Rest.Y,p.Rest.Z-p.Press*(p.Kind==6?.038f:.060f));
                p.Pose.LocalRotation=Rotation(p.X+(p.Kind==6?-p.Press*.08f:0),p.Y);
            }
            bool held=p.Kind==3?Math.Abs(p.X)+Math.Abs(p.Y)>.025f:
                p.Kind==0||p.Kind>=4?p.Press>.10f:false;
            renderer.DrawMesh(p.Mesh,held?pressed:solid,p.Pose);
        }
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;for(int i=0;i<parts.Count;i++)parts[i].Mesh.Dispose();details.Dispose();body.Dispose();
    }
}
