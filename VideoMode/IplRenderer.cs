using System;

// The native adapter and the visual proof exporter consume the same geometry.
internal interface IIplCanvas
{
    void Section(IplRenderPhase phase);
    bool BeginSelectorCache(int part,string value,float cell,uint color);
    void EndSelectorCache();
    void PrepareSelector(int part,IplSelectorPose pose,float centerX,float centerY);
    bool DrawSelectorCache(int part);
    bool BeginGridCache();
    void EndGridCache();
    bool DrawGridCache();
    void ResetFooterTextCache();
    bool BeginFooterTextCache();
    void EndFooterTextCache();
    bool DrawFooterTextCache();
    void Triangle(float ax,float ay,float bx,float by,float cx,float cy,uint color);
    void Rectangle(float x,float y,float width,float height,uint color);
    void Text(string value,float x,float y,float size,uint color,float angle,float stretch=1);
    void BeginGlass();
    void GlassVertex(int index,IplPoint point,uint color,float u,float v,float reflection);
    void EndGlassFace();
    void EndGlass();
    void DrawGlass();
    void DrawReflections();
    void BeginRims();
    void RimQuad(IplPoint a,IplPoint b,IplPoint c,IplPoint d,uint color);
    void EndRims();
    void DrawRims();
    bool BeginFooterCache(int style);
    void EndFooterCache();
    bool DrawFooterCache(int style);
    bool BeginIconCache(int style);
    void EndIconCache();
    void PrepareIcon(IplPoint origin,IplPoint x,IplPoint y,float scale,float centerY);
    bool DrawIconCache(int style);
    void BeginPanels();
    void PanelTriangle(IplPoint a,IplPoint b,IplPoint d);
    void EndPanels();
    void PreparePanelTint(int selected);
    bool DrawPanelTint();
    void CaptureBackdrop();
    void DrawFrostedPanels();
}
internal struct IplPoint
{
    internal float X,Y,Z;
    internal IplPoint(float x,float y,float z=0) { X=x;Y=y;Z=z; }
}
internal sealed partial class IplRenderer
{
    private readonly IIplCanvas c;
    private float yaw,pitch,cy,scale,rimVisibility;
    private float cosYaw,sinYaw,cosPitch,sinPitch;
    private IplSelectorPose selectorPose;
    private readonly string[] selectorValues=new string[3];
    private float selectorX,selectorY;
    private bool transformingSelector;
    private bool footerReady,iconReady,iconBuilding;
#if IPL_CACHED_GRID
    private bool gridReady;
#endif
    internal const uint SelectedPanelTint=0xc5c8d436u,IdlePanelTint=0xb9bdbd32u;
    private const uint White=0xf3f4faff, Green=0x15f88cff, Block=0x60db18ff;
    // Cube faces are signed permutations of one grid. Cache its invariant unit
    // normals and curvature, rather than renormalizing every moving vertex.
    // Together these use 17,500 data bytes, versus 45,000 for six position grids.
    private readonly IplPoint[] points=new IplPoint[25*25],normals=new IplPoint[25*25];
    private readonly float[] curvatures=new float[25*25];
    private readonly int[] faceOrder=new int[6];
    private readonly float[] depth=new float[6];
    private readonly uint[] wallTints=new uint[OuterRimBands];
    private readonly IplPoint[] wallDirections=new IplPoint[4*13];
    // Adjacent bands share a boundary. Retain its rotated endpoints in a small
    // scratch ring rather than rebuilding it and rotating both ends per segment.
    private readonly IplPoint[] wallBoundary=new IplPoint[4*13];
    private readonly IplPoint[] circleDirections=new IplPoint[33],panelDirections=new IplPoint[4*9];
    // A broad corner arc must remain visible at native resolution. The optical
    // wall uses the same radius family as the underlying rounded solid.
    private const float Round=.24f,Core=1-Round,WallCorner=.28f;
    private const int WallBandDivisions=16,OuterRimBands=3;
    internal IplRenderer(IIplCanvas canvas)
    {
        c=canvas;
        for(int y=0;y<=24;y++)for(int x=0;x<=24;x++)
        {
            int at=y*25+x;var point=Rounded(Surface(0,Coordinate(x),Coordinate(y)));
            points[at]=point;
            var local=new IplPoint(point.X-Clamp(point.X,-Core,Core),point.Y-Clamp(point.Y,-Core,Core),point.Z-Clamp(point.Z,-Core,Core));
            float length=(float)Math.Sqrt(local.X*local.X+local.Y*local.Y+local.Z*local.Z);
            normals[at]=new IplPoint(local.X/length,local.Y/length,local.Z/length);
            curvatures[at]=Clamp((1-Math.Max(Math.Max(Math.Abs(local.X),Math.Abs(local.Y)),Math.Abs(local.Z))/length)*3,0,1);
        }
        for(int corner=0;corner<4;corner++)for(int j=0;j<=12;j++)
        {
            float a=(180-corner*90-j*90f/12)*.017453293f;
            wallDirections[corner*13+j]=new IplPoint((float)Math.Cos(a),(float)Math.Sin(a));
        }
        for(int i=0;i<=32;i++)
        {
            float a=i*6.2831853f/32;
            circleDirections[i]=new IplPoint((float)Math.Cos(a),(float)Math.Sin(a));
        }
        for(int corner=0;corner<4;corner++)for(int j=0;j<=8;j++)
        {
            float a=(180-corner*90-j*90f/8)*.017453293f;
            panelDirections[corner*9+j]=new IplPoint((float)Math.Cos(a),(float)Math.Sin(a));
        }
        // Retain only the outer polish. The former middle and inset returns,
        // including their ambient wall tint, made separate interior contours.
        for(int band=0;band<OuterRimBands;band++)
        {
            float t=(band+.5f)/WallBandDivisions;
            float polish=(float)Math.Exp(-Math.Pow((t-.10f)/.035f,2));
            float spectrum=(float)Math.Sin(t*12.56637f),envelope=(float)Math.Sin(t*3.1415926f);
            int alpha=(int)(polish*205*Math.Sqrt(envelope));
            wallTints[band]=Rgba((int)(35+polish*150+spectrum*20),(int)(52+polish*160),(int)(72+polish*170-spectrum*20),alpha);
        }
    }
    private static float Coordinate(int i) => i<6 ? -1+Round*(1-(float)Math.Cos(i/6f*1.5707963f)) : i>18 ? -Coordinate(24-i) : -Core+(i-6)*2*Core/12;
    private static float Clamp(float a,float lo,float hi)=>Math.Max(lo,Math.Min(hi,a));
    private static IplPoint Surface(int f,float u,float v)
    {
        if(f==0)return new IplPoint(u,v,1);
        if(f==1)return new IplPoint(1,v,-u);
        if(f==2)return new IplPoint(-1,v,u);
        if(f==3)return new IplPoint(u,1,-v);
        if(f==4)return new IplPoint(u,-1,v);
        return new IplPoint(-u,v,-1);
    }
    private static IplPoint Orient(int face,IplPoint p)
    {
        if(face==0)return p;
        if(face==1)return new IplPoint(p.Z,p.Y,-p.X);
        if(face==2)return new IplPoint(-p.Z,p.Y,p.X);
        if(face==3)return new IplPoint(p.X,p.Z,-p.Y);
        if(face==4)return new IplPoint(p.X,-p.Z,p.Y);
        return new IplPoint(-p.X,p.Y,-p.Z);
    }
    private static IplPoint Rounded(IplPoint p)
    {
        float x=Clamp(p.X,-Core,Core), y=Clamp(p.Y,-Core,Core), z=Clamp(p.Z,-Core,Core);
        float dx=p.X-x,dy=p.Y-y,dz=p.Z-z;
        float s=Round/(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);
        return new IplPoint(x+dx*s,y+dy*s,z+dz*s);
    }
    private IplPoint Rotate(IplPoint p)
    {
        float x=cosYaw*p.X+sinYaw*p.Z,z=-sinYaw*p.X+cosYaw*p.Z;
        return new IplPoint(x,cosPitch*p.Y-sinPitch*z,sinPitch*p.Y+cosPitch*z);
    }
    private IplPoint Project(IplPoint p)
    {
        return ProjectView(Rotate(p));
    }
    private IplPoint ProjectView(IplPoint p)
    {
        float f=3.8f/(3.8f-p.Z);
        return new IplPoint(322+p.X*scale*.975f*f,cy-p.Y*scale*f,p.Z);
    }
    private void Tri(IplPoint a,IplPoint b,IplPoint d,uint color)
    {
        // Quantized optical lighting often produces exactly zero alpha. Skip
        // those triangles before virtual dispatch and native canvas staging.
        if((color&255)!=0)c.Triangle(a.X,a.Y,b.X,b.Y,d.X,d.Y,color);
    }
    private void Rect(float x,float y,float w,float h,uint color)
    {
        if((color&255)==0)return;
        if(!transformingSelector){c.Rectangle(x,y,w,h,color);return;}
        var a=selectorPose.Project(x,y,selectorX,selectorY);var b=selectorPose.Project(x+w,y,selectorX,selectorY);
        var d=selectorPose.Project(x,y+h,selectorX,selectorY);var e=selectorPose.Project(x+w,y+h,selectorX,selectorY);
        Tri(a,b,d,color);Tri(b,e,d,color);
    }
    private void Line(float x,float y,float xx,float yy,float width,uint color)
    {
        float dx=xx-x,dy=yy-y, d=(float)Math.Sqrt(dx*dx+dy*dy);if(d<.001f)return;
        float nx=-dy/d*width*.5f,ny=dx/d*width*.5f;
        c.Triangle(x+nx,y+ny,xx+nx,yy+ny,x-nx,y-ny,color);
        c.Triangle(xx+nx,yy+ny,xx-nx,yy-ny,x-nx,y-ny,color);
    }
    private void Circle(float x,float y,float r,uint color,bool fill)
    {
        for(int i=0;i<32;i++)
        {
            var a=circleDirections[i];var b=circleDirections[i+1];
            float ax=x+a.X*r,ay=y+a.Y*r;
            float bx=x+b.X*r,by=y+b.Y*r;
            if(fill)c.Triangle(x,y,ax,ay,bx,by,color);else Line(ax,ay,bx,by,1,color);
        }
    }
    internal static float TextWidth(string value,float size)
    {
        float w=0;for(int i=0;i<value.Length;i++){int at=value[i]-32;if(at>=0&&at<96)w+=IplFontData.Widths[at]*size/28;}
        return w;
    }
    private void Text(string text,float x,float y,float size,uint color)=>c.Text(text,x,y,size,color,0);
    private void Center(string text,float x,float y,float size,uint color)=>Text(text,x-TextWidth(text,size)/2,y,size,color);
    private static float PoseYaw(ShellFace face)=>face==ShellFace.Video?-1.5707963f:face==ShellFace.Options?1.5707963f:face==ShellFace.Memory?3.1415926f:0;
    private static float PosePitch(ShellFace face)=>face==ShellFace.Play?1.5707963f:face==ShellFace.Calendar?-1.5707963f:0;
    // Geometry uploads and capture allocation happen between frames, as required
    // by the SDK. No dynamic mesh is modified during a GX pass.
    internal void Prepare(IplShell shell)
    {
#if IPL_CACHED_FOOTER_TEXT
        c.ResetFooterTextCache();
#endif
        if(!footerReady)
        {
            for(int style=0;style<3;style++)if(c.BeginFooterCache(style)){FooterShapes(style);c.EndFooterCache();}
            footerReady=true;
        }
#if IPL_CACHED_ICONS
        if(!iconReady){
            iconBuilding=true;
            for(int i=0;i<6;i++)if(c.BeginIconCache(i)){FaceIcon((ShellFace)i,shell);c.EndIconCache();}
            iconBuilding=false;iconReady=true;
        }
#endif
        if(shell.Page==ShellPage.Browser){PrepareBrowser(shell);return;}
        browserMaskReady=false;
        if(shell.Editing){
#if IPL_CACHED_GRID
            if(!gridReady&&c.BeginGridCache()){Grid();c.EndGridCache();gridReady=true;}
#endif
            Selectors(shell,true);
            return;
        }
        c.Section(IplRenderPhase.GlassGeometry);
        float t=shell.Turn, ease=1-(float)Math.Pow(1-t,3);
        if(t<1)ease+=(float)Math.Sin(t*3.1415926f)*.045f;
        yaw=PoseYaw(shell.PreviousFace)+(PoseYaw(shell.Face)-PoseYaw(shell.PreviousFace))*ease;
        pitch=PosePitch(shell.PreviousFace)+(PosePitch(shell.Face)-PosePitch(shell.PreviousFace))*ease;
        if(!shell.ReducedMotion){yaw+=(float)Math.Sin(shell.Time/5.3f*6.2831853f)*.075f;pitch+=.035f+(float)Math.Sin(shell.Time/7.1f*6.2831853f)*.055f;}
#if IPL_VISUAL_REVIEW_ANGLE
        // Reproducible oblique pose exposes walls and face seams for native review.
        yaw+=.30f;pitch-=.16f;
#endif
        cosYaw=(float)Math.Cos(yaw);sinYaw=(float)Math.Sin(yaw);cosPitch=(float)Math.Cos(pitch);sinPitch=(float)Math.Sin(pitch);
        cy=202+(shell.ReducedMotion?0:(float)Math.Sin(shell.Time*.7f)*3);scale=106;
        Cube();
        c.Section(IplRenderPhase.FrostGeometry);
        c.BeginPanels();
        if(shell.Page==ShellPage.Face&&shell.Face!=ShellFace.Memory)
            for(int i=0;i<shell.Rows;i++)FacePanel(shell,i);
        c.EndPanels();
#if IPL_RETAINED_TINT
        c.PreparePanelTint(shell.Row);
#endif
        c.Section(IplRenderPhase.RimGeometry);
        c.BeginRims();GlassRims();c.EndRims();
#if IPL_CACHED_ICONS
        int iconFace=SurfaceIndex(shell.Face);
        c.PrepareIcon(Rotate(Surface(iconFace,0,0)),Rotate(Surface(iconFace,1,0)),Rotate(Surface(iconFace,0,1)),scale,cy);
#endif
        c.Section(IplRenderPhase.None);
#if IPL_CACHED_FOOTER_TEXT
        if(c.BeginFooterTextCache()){
            Footer(shell);c.EndFooterTextCache();
        }
#endif
    }
    internal void Present(IplShell shell)
    {
        Rect(0,0,640,480,0x000000ff);
        if(shell.Page==ShellPage.Browser){PresentBrowser(shell);return;}
        if(shell.Editing)
        {
            c.Section(IplRenderPhase.Editor);Detail(shell);
            c.Section(IplRenderPhase.Footer);Footer(shell);c.Section(IplRenderPhase.None);
            if(shell.SystemStatus.Length>0)Center(shell.SystemStatus,320,463,11,0xaabbd0ddu);
            return;
        }
        c.Section(IplRenderPhase.Transmission);
        c.DrawGlass();
        c.Section(IplRenderPhase.Rims);c.DrawRims();
        c.Section(IplRenderPhase.Reflections);c.DrawReflections();
        if(shell.Turn>.85f)
        {
            c.Section(IplRenderPhase.Icons);FaceIcon(shell);
            if(shell.Page==ShellPage.Face&&shell.Face!=ShellFace.Memory)
            {
                c.Section(IplRenderPhase.Capture);c.CaptureBackdrop();
                c.Section(IplRenderPhase.Frost);c.DrawFrostedPanels();
                c.Section(IplRenderPhase.PanelTint);
#if IPL_RETAINED_TINT
                if(!c.DrawPanelTint())
#endif
                    for(int i=0;i<shell.Rows;i++)PanelTint(shell,i);
            }
            c.Section(IplRenderPhase.Labels);
            if(shell.Page==ShellPage.Cube) MenuLabels(shell);
            else FaceMenu(shell);
        }
        c.Section(IplRenderPhase.Footer);Footer(shell);c.Section(IplRenderPhase.None);
        if(shell.SystemStatus.Length>0)Center(shell.SystemStatus,320,463,11,0xaabbd0ddu);
    }
    private void Cube()
    {
        c.BeginGlass();
        for(int f=0;f<6;f++){faceOrder[f]=f;depth[f]=Rotate(Surface(f,0,0)).Z;}
        for(int i=1;i<6;i++){int j=i;while(j>0&&depth[faceOrder[j]]<depth[faceOrder[j-1]]){int v=faceOrder[j];faceOrder[j]=faceOrder[j-1];faceOrder[j-1]=v;j--;}}
        for(int n=0;n<6;n++)
        {
            int face=faceOrder[n];
            // The old path uploaded and drew all six faces, even when their
            // complete transmission and reflection contribution was zero.
            float facing=Clamp((depth[face]-.08f)/.20f,0,1);
            if(facing<=0)continue;
            facing=facing*facing*(3-2*facing);
            for(int i=0;i<625;i++)
            {
                var p=Rotate(Orient(face,points[i]));var normal=Rotate(Orient(face,normals[i]));
                var uv=ReflectionUv(p,normal,curvatures[i]);
                uint color=GlassColor(p,normal);
                color=(color&0xffffff00)|(uint)((color&255)*facing);
                c.GlassVertex(i,ProjectView(p),color,uv.X,uv.Y,uv.Z);
            }
            c.EndGlassFace();
        }
        c.EndGlass();
    }
    private static IplPoint ReflectionUv(IplPoint p,IplPoint n,float curvature)
    {
        var eye=new IplPoint(-p.X,-p.Y,3.8f-p.Z);
        float length=(float)Math.Sqrt(eye.X*eye.X+eye.Y*eye.Y+eye.Z*eye.Z);
        eye=new IplPoint(eye.X/length,eye.Y/length,eye.Z/length);
        float dot=n.X*eye.X+n.Y*eye.Y+n.Z*eye.Z;
        float grazing=Clamp(1-dot,0,1);
        return new IplPoint(Clamp(.5f+(2*dot*n.X-eye.X)*.5f,.002f,.998f),Clamp(.5f+(2*dot*n.Y-eye.Y)*.5f,.002f,.998f),Clamp(.04f+1.8f*grazing,0,1)*(.12f+.88f*curvature));
    }
    private static uint GlassColor(IplPoint p,IplPoint normal)
    {
        if(normal.Z<=0)return 0;
        // Soft environment lobes, anchored in world space, move across the glass
        // during the idle float. These are light, not an opaque blue coating.
        float teal=Light(p.X-.70f,p.Y-.65f,.68f);
        float amber=Light(p.X+.86f,p.Y+.22f,.70f);
        float violet=Light(p.X-.05f,p.Y+.92f,.55f);
        float fresnel=1-normal.Z;
        // Transmission carries colored light; the separate low-roughness
        // reflection pass supplies white polish without a diffuse bevel haze.
        int r=(int)(8+teal*82+amber*127+violet*51+fresnel*12);
        int g=(int)(15+teal*155+amber*63+violet*24+fresnel*18);
        int blue=(int)(18+teal*140+amber*9+violet*133+fresnel*27);
        return Rgba(r,g,blue,(int)(205-normal.Z*22));
    }
    private static float Light(float x,float y,float spread)
    {
        // Match the lookup's exact cutoff before entering its managed-array
        // path. Most narrow rim lights are out of range at a given segment.
        float distance=(x*x+y*y)/(spread*spread);
        return distance>=16?0:IplLighting.ExpNegative(distance);
    }

    // A shaded optical cross-section follows the rotating wall without strokes.
    private void GlassRims()
    {
        for(int face=0;face<6;face++)
        {
            float facing=Rotate(Surface(face,0,0)).Z;
            if(facing<=0)continue;
            rimVisibility=Clamp((facing-.08f)/.20f,0,1);
            if(rimVisibility<=0)continue;
            rimVisibility=rimVisibility*rimVisibility*(3-2*rimVisibility);
            // Keep the original outer-bevel width and rounded silhouette.
            // No bands extend to the former middle or inset inner boundary.
            for(int band=0;band<OuterRimBands;band++)
            {
                float outside=band/(float)WallBandDivisions,inside=(band+1)/(float)WallBandDivisions;
                GlassWall(face,band,.997f-outside*.147f,.997f-inside*.147f,1-outside*.050f,1-inside*.050f,wallTints[band]);
            }
        }
    }
    // Translucent ribbons give the optical wall a body between its polished
    // boundaries. All points share the rotating cube projection.
    private void GlassWall(int face,int band,float outer,float inner,float front,float back,uint color)
    {
        IplPoint previousOuter=default,previousInner=default,firstOuter=default,firstInner=default;
        for(int corner=0;corner<4;corner++)for(int j=0;j<=12;j++)
        {
            int at=corner*13+j;var direction=wallDirections[at];
            var o=band==0?Rotate(WallPoint(face,corner,direction,outer,WallCorner+(outer-.997f)/.147f*.015f,front)):wallBoundary[at];
            var n=Rotate(WallPoint(face,corner,direction,inner,WallCorner+(inner-.997f)/.147f*.015f,back));
            wallBoundary[at]=n;
            if(corner==0&&j==0){firstOuter=o;firstInner=n;}
            else WallSegment(previousOuter,o,previousInner,n,color);
            previousOuter=o;previousInner=n;
        }
        WallSegment(previousOuter,firstOuter,previousInner,firstInner,color);
    }
    private static IplPoint WallPoint(int face,int corner,IplPoint direction,float extent,float radius,float depth)
    {
        float x=corner==0||corner==3?-extent+radius:extent-radius;
        float y=corner<2?extent-radius:-extent+radius;
        // Shift samples through the outer bevel to retain its polished depth.
        float refraction=(1-depth)/.050f*.105f;
        var p=Surface(face,x+direction.X*radius+refraction,y+direction.Y*radius);
        return new IplPoint(p.X*depth,p.Y*depth,p.Z*depth);
    }
    private static IplPoint Lerp(IplPoint a,IplPoint b,float t)=>new IplPoint(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);
    private void WallSegment(IplPoint a,IplPoint b,IplPoint innerA,IplPoint innerB,uint color)
    {
        // Endpoints are already in view space, shared by neighboring segments
        // and bands. Projection and lighting retain the original interpolation.
        var start=ProjectView(a);var end=ProjectView(b);
        float dx=end.X-start.X,dy=end.Y-start.Y;
        int steps=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(dx*dx+dy*dy)/5));
        var previousOuter=start;IplPoint previousInner=default;bool previousVisible=false;
        for(int i=1;i<=steps;i++)
        {
            var p=Lerp(a,b,i/(float)steps);
            uint shade=RimColor(p,color);
            if((shade&255)==0){previousVisible=false;continue;}
            // Invisible samples still determine the same lighting and adaptive
            // tessellation. Project their boundary only when a visible neighbor
            // actually needs it; preserve cached endpoints across visible runs.
            if(!previousVisible)
            {
                float t=(i-1)/(float)steps;
                if(i>1)previousOuter=ProjectView(Lerp(a,b,t));
                previousInner=i==1?ProjectView(innerA):ProjectView(Lerp(innerA,innerB,t));
            }
            var outer=ProjectView(p);var inner=ProjectView(Lerp(innerA,innerB,i/(float)steps));
            c.RimQuad(previousOuter,outer,previousInner,inner,shade);
            previousOuter=outer;previousInner=inner;previousVisible=true;
        }
    }
    private uint RimColor(IplPoint p,uint color)
    {
        float top=Light(p.X-.55f,(p.Y-1)*2,.28f);
        float corner=Light(p.X+.91f,(p.Y-.92f)*1.4f,.17f);
        // Localized reflected lights reveal each curved return. Avoid a uniform
        // contour, which would make the optical wall look like a stroked frame.
        float rightCorner=Light(p.X-.92f,(p.Y-.92f)*1.25f,.18f);
        float bottomCorners=Light(p.X+.92f,(p.Y+.92f)*1.25f,.20f)
                           +Light(p.X-.92f,(p.Y+.92f)*1.25f,.20f);
        float lower=Light(p.X-.08f,(p.Y+1)*2,.42f);
        lower+=bottomCorners*.35f;
        float warm=Light(p.X+1,p.Y+.50f,.45f);
        float glint=top+corner*.85f+rightCorner*.70f+bottomCorners*.30f;
        int alpha=(int)(Math.Min(255,(color&255)*(.06f+glint*4+warm*.6f+lower*1.4f))*rimVisibility);
        // Transparent segments are discarded by the caller. Avoid evaluating
        // their RGB channels and packing a color that can never reach GX.
        if(alpha==0)return 0;
        int r=(int)((color>>24)*.55f+glint*190+warm*100+lower*40);
        int g=(int)(((color>>16)&255)*.65f+glint*185+warm*58+lower*28);
        int b=(int)(((color>>8)&255)*.80f+glint*170+lower*120);
        return Rgba(r,g,b,alpha);
    }
    private static uint Rgba(int r,int g,int b,int a)=>(uint)((Math.Min(255,r)<<24)|(Math.Min(255,g)<<16)|(Math.Min(255,b)<<8)|a);
    private static int SurfaceIndex(ShellFace face)=>face==ShellFace.Calendar?4:face==ShellFace.Options?2:face==ShellFace.Play?3:(int)face;
    private IplPoint FacePoint(IplShell s,float u,float v)=>Project(Surface(SurfaceIndex(s.Face),u,v));
    private void FaceText(IplShell s,string text,float u,float v,float size,uint color,float extraAngle=0,float stretch=1)
    {
        var p=FacePoint(s,u,v);var right=FacePoint(s,u+.1f,v);
        float angle=(float)Math.Atan2(right.Y-p.Y,right.X-p.X)*57.29578f+extraAngle;
        float f=3.8f/(3.8f-p.Z);size*=f/1.357f;
        float width=TextWidth(text,size)*stretch,rad=angle*.017453293f;
        c.Text(text,p.X-width*.5f*(float)Math.Cos(rad),p.Y-width*.5f*(float)Math.Sin(rad),size,color,angle,stretch);
    }
    private void MenuLabels(IplShell s)
    {
        FaceText(s,"Game Play",0,.72f,24,White);
        FaceText(s,"Video",.64f,.05f,24,White,90);
        FaceText(s,"Calendar",0,-.60f,24,White);
        FaceText(s,"Options",-.67f,.05f,24,White,-90);
    }
    private void FaceAlignedText(IplShell s,string text,float u,float v,float size,uint color,bool rightAligned)
    {
        var p=FacePoint(s,u,v);var next=FacePoint(s,u+.1f,v);
        float angle=(float)Math.Atan2(next.Y-p.Y,next.X-p.X)*57.29578f;
        size*=3.8f/(3.8f-p.Z)/1.357f;
        float width=rightAligned?TextWidth(text,size):0,rad=angle*.017453293f;
        float x=p.X-width*(float)Math.Cos(rad),y=p.Y-width*(float)Math.Sin(rad);
        c.Text(text,x+.5f,y+1.6f,size,0x00000078,angle);
        c.Text(text,x,y,size,color,angle);
    }
    private void FacePanel(IplShell s,int row)=>PanelShape(s,row,0);
    private void PanelTint(IplShell s,int row)=>PanelShape(s,row,row==s.Row?SelectedPanelTint:IdlePanelTint);
    private void PanelShape(IplShell s,int row,uint tint)
    {
        const float left=-.78f,right=.78f,height=.33f,radius=.075f;
        float top=.46f-row*.43f,bottom=top-height;
        var center=FacePoint(s,0,top-height/2);IplPoint first=default,previous=default;
        for(int corner=0;corner<4;corner++)for(int j=0;j<=8;j++)
        {
            float x=corner==0||corner==3?left+radius:right-radius;
            float y=corner<2?top-radius:bottom+radius;
            var direction=panelDirections[corner*9+j];
            var point=FacePoint(s,x+direction.X*radius,y+direction.Y*radius);
            if(corner==0&&j==0)first=point;
            else if(tint==0)c.PanelTriangle(center,previous,point);else Tri(center,previous,point,tint);
            previous=point;
        }
        if(tint==0)c.PanelTriangle(center,previous,first);else Tri(center,previous,first,tint);
    }
    private static readonly string[] monitor={"11111111111","10000000001","10110000001","10100000001","10000000001","11111111111","00000100000","00000100000","00011111000"};
    private static readonly string[] calendar={"00100000100","00100000100","11111111111","10000000001","11111111111","10101010101","10000000001","10101010101","10000000001","10101010101","11111111111"};
    private static readonly string[] options={"000111000","010111010","111111111","111000111","111000111","111000111","111111111","010111010","000111000"};
    private static readonly string[] play={"110000000","111100000","111111000","111111110","111111111","111111110","111111000","111100000","110000000"};
    private static readonly string[] memory={"011111110","111111111","110111011","110111011","110111011","110111011","110000011","111111111","111111111","111111111","111111111"};
    private static readonly string[] home={"00011110000","01110011100","11100001110","11111011110","11111111110","11101110110","11100100110","01111111100","00011110000"};
    private void FaceIcon(IplShell s)=>FaceIcon(s.Face,s);
    private IplPoint IconPoint(IplShell s,float u,float v)=>iconBuilding?new IplPoint(u,v):FacePoint(s,u,v);
    private void FaceIcon(ShellFace face,IplShell s)
    {
#if IPL_CACHED_ICONS
        if(!iconBuilding&&c.DrawIconCache((int)face))return;
#endif
        var bitmap=face==ShellFace.Video?monitor:face==ShellFace.Calendar?calendar:face==ShellFace.Options?options:face==ShellFace.Play?play:face==ShellFace.Memory?memory:home;
        uint color=face==ShellFace.Video?0x3f6ed54bu:face==ShellFace.Calendar?0x39a88148u:face==ShellFace.Options?0xd878dc55u:face==ShellFace.Play?0xe0a34d48u:face==ShellFace.Memory?0x6788d54bu:0x708fc535u;
        const float cell=.122f,rowStep=.114f;float left=-bitmap[0].Length*cell/2,top=bitmap.Length*rowStep/2+.075f;
        for(int y=0;y<bitmap.Length;y++)for(int x=0;x<bitmap[y].Length;x++)if(bitmap[y][x]=='1')
        {
            float u=left+x*cell,v=top-y*rowStep;
            FaceBlock(s,u,v,cell-.012f,rowStep-.012f,color);
            FaceQuad(s,u+.006f,v-.006f,cell-.024f,.010f,0xc2d4ff28);
            FaceQuad(s,u+cell-.022f,v-.017f,.010f,rowStep-.032f,0x080d1a40);
        }
    }
    private void FaceBlock(IplShell s,float u,float v,float width,float height,uint color)
    {
        const float radius=.012f;
        var center=IconPoint(s,u+width/2,v-height/2);IplPoint first=default,previous=default;
        for(int corner=0;corner<4;corner++)for(int j=0;j<=3;j++)
        {
            float x=corner==0||corner==3?u+radius:u+width-radius;
            float y=corner<2?v-radius:v-height+radius;
            var direction=wallDirections[corner*13+j*4];
            var p=IconPoint(s,x+direction.X*radius,y+direction.Y*radius);
            if(corner==0&&j==0)first=p;else Tri(center,previous,p,color);
            previous=p;
        }
        Tri(center,previous,first,color);
    }
    private void FaceQuad(IplShell s,float u,float v,float w,float h,uint color)
    {
        var a=IconPoint(s,u,v);var b=IconPoint(s,u+w,v);var d=IconPoint(s,u,v-h);var e=IconPoint(s,u+w,v-h);
        Tri(a,b,d,color);Tri(b,e,d,color);
    }
    private void FaceMenu(IplShell s)
    {
        FaceText(s,s.Title,.045f,.79f,26,White,0,.95f);
        if(s.Face==ShellFace.Memory)
        {
            FaceText(s,s.Face==ShellFace.Play?"No disc selected.":"No card selected.",0,.1f,16,White);
            FaceText(s,"Preview only.",0,-.16f,12,0xb5b9c9ff);return;
        }
        for(int i=0;i<s.Rows;i++)
        {
            float y=.35f-i*.43f;
            uint color=i==s.Row?White:0xd5d6dcff;
            float focus=s.Focus(i);focus=focus*focus*(3-2*focus);
            float size=16.5f*(1+.30f*focus);
            string value=s.Value(i);
            // Keep both edge anchors fixed as the type grows/shrinks.
            float maximum=TextWidth(s.Label(i),size)+TextWidth(value,size);
            if(maximum>193)size*=193/maximum;
            FaceAlignedText(s,s.Label(i),-.695f,y,size,color,false);
            FaceAlignedText(s,value,.735f,y,size,color,true);
        }
    }
    private void Grid()
    {
        for(int x=8;x<640;x+=16)for(int y=0;y<480;y+=16)
        {
            float fade=Clamp(1-(float)Math.Sqrt((x-320)*(x-320)/115600f+(y-240)*(y-240)/78400f),0,1);
            uint color=Rgba(61,65,67,(int)(fade*165));
            Rect(x-.5f,y,1,16,color);Rect(x,y-.5f,16,1,color);
        }
    }
    private void Detail(IplShell s)
    {
#if IPL_CACHED_GRID
        if(!c.DrawGridCache())
#endif
            Grid();
        Center("Choose a system setting.",320,28,20,White);
        if(s.Video.Pending)
        {
            Banner(s.Video.Reverting?"Restoring previous settings.":"Keep these video settings?",s.Video.Reverting);
            Center(s.Video.Reverting?"Unable to restore. Retrying.":((int)Math.Ceiling(s.Video.Remaining)).ToString()+" seconds to confirm",320,357,18,White);
        }
        Center(s.Label(s.Row),320,104,23,Green);
        Selectors(s,false);
        if(s.Face==ShellFace.Calendar)
            Center(s.RealCalendar?"A sets the GameCube clock. B discards changes.":"Calendar demonstration. Changes last for this session.",320,351,14,0xc4c9deff);
        else if(s.Face==ShellFace.Video&&!s.Video.Pending)
        {
            string status=s.Video.Status;
            if(status=="UNSUPPORTED MODE OR CABLE"||status=="NOT ENOUGH MEMORY FOR MODE")Banner(status=="UNSUPPORTED MODE OR CABLE"?"Unsupported mode or cable.":"Not enough memory for this mode.",true);
            else Center("A previews the mode. B discards changes.",320,350,16,White);
        }
        else if(s.Face==ShellFace.Options)Center("A confirms. B discards changes.",320,351,14,0xc4c9deff);
    }
    private void Selectors(IplShell s,bool preparing)
    {
        if(s.Face==ShellFace.Calendar)
        {
            int count=s.Row==0?2:3;
            float total=s.Row==0?214:352,start=320-total/2;
            for(int i=0;i<count;i++)
            {
                string value=preparing?s.Segment(i):selectorValues[i];if(preparing)selectorValues[i]=value;
                float w=i==2?126:84;
                float x=start+w/2;
                Selector(s,i,value,x,180,i==s.Part?6:5,i==s.Part?Block:0x246b14ff,preparing);
                if(!preparing){
                    if(i==s.Part)Arrows(x,151,263);
                    Center(s.Row==0?(i==0?"Hour":"Minute"):(i==0?"Day":i==1?"Month":"Year"),x,285,16,White);
                }
                start+=w+28;
            }
        }
        else
        {
            string value=preparing?s.Value(s.Row).ToUpperInvariant():selectorValues[0];if(preparing)selectorValues[0]=value;
            float cell=Math.Min(8,510f/Math.Max(1,value.Length*6-1));
            Selector(s,0,value,320,182,cell,Block,preparing);
            if(!preparing)Arrows(320,148,267);
        }
    }
    private void Selector(IplShell s,int part,string value,float x,float y,float cell,uint color,bool preparing)
    {
        float centerY=y+3.5f*cell;
        if(preparing){
            if(c.BeginSelectorCache(part,value,cell,color)){BlockText(value,0,-3.5f*cell,cell,color);c.EndSelectorCache();}
            c.PrepareSelector(part,IplSelectorPose.For(s,part,(value.Length*6-1)*cell),x,centerY);
            return;
        }
        if(c.DrawSelectorCache(part))return;
        selectorPose=IplSelectorPose.For(s,part,(value.Length*6-1)*cell);
        if(!selectorPose.Moving){BlockText(value,x,y,cell,color);return;}
        selectorX=x;selectorY=centerY;transformingSelector=true;
        BlockText(value,0,-3.5f*cell,cell,color);transformingSelector=false;
    }
    private void Banner(string value,bool alert)
    {
        Rect(70,312,500,47,0xc3c5d5ff);
        for(int y=0;y<43;y++)Rect(72,314+y,496,1,alert?Rgba(138-y,12,58+y,240):Rgba(30+y/2,26,145-y,240));
        Center(value,320,324,18,White);
    }
    private void Arrows(float x,float top,float bottom)
    {c.Triangle(x,top-8,x-9,top+5,x+9,top+5,Green);c.Triangle(x,bottom+8,x-9,bottom-5,x+9,bottom-5,Green);}
    private static readonly string[] letters={
        "01110100011000111111100011000110001","11110100011000111110100011000111110","01111100001000010000100001000001111","11110100011000110001100011000111110","11111100001000011110100001000011111","11111100001000011110100001000010000","01111100001000010111100011000101111","10001100011000111111100011000110001","11111001000010000100001000010011111","00111000100001000010100101001001100","10001100101010011000101001001010001","10000100001000010000100001000011111","10001110111010110101100011000110001","10001110011010110011100011000110001","01110100011000110001100011000101110","11110100011000111110100001000010000","01110100011000110001101011001001101","11110100011000111110101001001010001","01111100001000001110000010000111110","11111001000010000100001000010000100","10001100011000110001100011000101110","10001100011000110001100010101000100","10001100011000110101101011010101010","10001100010101000100010101000110001","10001100010101000100001000010000100","11111000010001000100010001000011111",
        "01110100011001110101110011000101110","00100011000010000100001000010001110","01110100010000100110010001000011111","11110000010000101110000010000111110","00010001100101010010111110001000010","11111100001000011110000010000111110","01110100001000011110100011000101110","11111000010001000100010000100001000","01110100011000101110100011000101110","01110100011000101111000010000101110"};
    private void BlockText(string value,float center,float y,float cell,uint color)
    {
        float x=center-(value.Length*6-1)*cell/2;
        for(int i=0;i<value.Length;i++)
        {
            int ch=value[i],at=ch>=65&&ch<=90?ch-65:ch>=48&&ch<=57?26+ch-48:-1;
            for(int row=0;row<7;row++)for(int col=0;col<5;col++)
            {
                bool on=at>=0?letters[at][row*5+col]=='1':ch==58?col==2&&(row==2||row==4):ch==47?col==4-row*4/6:ch==45?row==3:false;
                if(!on)continue;float xx=x+(i*6+col)*cell,yy=y+row*cell;
                Rect(xx,yy,cell-.8f,cell-.8f,color);
                Rect(xx+.5f,yy+.5f,cell-1.8f,.7f,0xb6ff8560);
                Rect(xx+cell-1.5f,yy+1,.6f,cell-1.7f,0x10250790);
            }
        }
    }
    private void FooterShapes(int style)
    {
        const float y=428;
        float stick=style==0?214:111;
        for(int r=15;r>3;r-=3)Circle(stick,y,r,Rgba(210,214,217,130),false);
        Circle(stick,y,3,0xd7d9dfff,true);
        if(style==0)return;
        for(int r=22;r>10;r-=3)Circle(320,y,r,Rgba(255,35,107,10),true);
        Circle(320,y,11,0xa42351ff,true);
        if(style==2)return;
        for(int r=23;r>12;r-=3)Circle(470,y,r,Rgba(15,245,196,10),true);
        Circle(470,y,13,0x168a73ff,true);
    }
    private void Footer(IplShell s)
    {
        const float y=428;
        int style=s.Page==ShellPage.Cube?0:s.Video.Reverting?2:1;
        if(!c.DrawFooterCache(style))FooterShapes(style);
#if IPL_CACHED_FOOTER_TEXT
        if(c.DrawFooterTextCache())return;
#endif
        float stick=style==0?214:111;
        Text(s.Page==ShellPage.Cube?"... Menu Selection":s.Face==ShellFace.Calendar&&s.Editing?"... Part / Value":"... Select",stick+24,y-13,s.Page==ShellPage.Cube?20:17,White);
        if(s.Page==ShellPage.Cube)return;
        Center("B",320,y-14,20,White);Text(s.Editing?"... Cancel":"... Back",340,y-13,17,White);
        if(s.Video.Reverting)return;
        Center("A",470,y-14,20,White);Text(s.Editing?"... Confirm":"... Open",492,y-13,17,White);
    }
}
