using System;

// The native adapter and the visual proof exporter consume the same geometry.
internal interface IIplCanvas
{
    void Triangle(float ax,float ay,float bx,float by,float cx,float cy,uint color);
    void Text(string value,float x,float y,float size,uint color,float angle);
}
internal struct IplPoint
{
    internal float X,Y,Z;
    internal IplPoint(float x,float y,float z=0) { X=x;Y=y;Z=z; }
}
internal sealed class IplRenderer
{
    private readonly IIplCanvas c;
    private float yaw,pitch,cy,scale;
    private float cosYaw,sinYaw,cosPitch,sinPitch;
    private const uint White=0xf3f4faff, Green=0x15f88cff, Block=0x60db18ff;
    private readonly IplPoint[] points=new IplPoint[6*25*25];
    private readonly int[] faceOrder=new int[6];
    private readonly float[] depth=new float[6];
    internal IplRenderer(IIplCanvas canvas)
    {
        c=canvas;
        for(int f=0;f<6;f++) for(int y=0;y<=24;y++) for(int x=0;x<=24;x++)
            points[f*625+y*25+x]=Rounded(Surface(f,Coordinate(x),Coordinate(y)));
    }
    private static float Coordinate(int i) => i<6 ? -1+.09f*(1-(float)Math.Cos(i/6f*1.5707963f)) : i>18 ? -Coordinate(24-i) : -.91f+(i-6)*1.82f/12;
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
    private static IplPoint Rounded(IplPoint p)
    {
        float x=Clamp(p.X,-.91f,.91f), y=Clamp(p.Y,-.91f,.91f), z=Clamp(p.Z,-.91f,.91f);
        float dx=p.X-x,dy=p.Y-y,dz=p.Z-z;
        float s=.09f/(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);
        return new IplPoint(x+dx*s,y+dy*s,z+dz*s);
    }
    private IplPoint Rotate(IplPoint p)
    {
        float x=cosYaw*p.X+sinYaw*p.Z,z=-sinYaw*p.X+cosYaw*p.Z;
        return new IplPoint(x,cosPitch*p.Y-sinPitch*z,sinPitch*p.Y+cosPitch*z);
    }
    private IplPoint Project(IplPoint p)
    {
        p=Rotate(p);float f=3.8f/(3.8f-p.Z);
        return new IplPoint(320+p.X*scale*f,cy-p.Y*scale*f,p.Z);
    }
    private void Tri(IplPoint a,IplPoint b,IplPoint d,uint color)=>c.Triangle(a.X,a.Y,b.X,b.Y,d.X,d.Y,color);
    private void Rect(float x,float y,float w,float h,uint color)
    {c.Triangle(x,y,x+w,y,x,y+h,color);c.Triangle(x+w,y,x+w,y+h,x,y+h,color);}
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
            float a=i*6.2831853f/32,b=(i+1)*6.2831853f/32;
            float ax=x+(float)Math.Cos(a)*r,ay=y+(float)Math.Sin(a)*r;
            float bx=x+(float)Math.Cos(b)*r,by=y+(float)Math.Sin(b)*r;
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
    internal void Present(IplShell shell)
    {
        Rect(0,0,640,480,0x000000ff);
        if(shell.Editing) { Detail(shell); Footer(shell); return; }
        float t=shell.Turn, ease=1-(float)Math.Pow(1-t,3);
        if(t<1)ease+=(float)Math.Sin(t*3.1415926f)*.045f;
        yaw=PoseYaw(shell.PreviousFace)+(PoseYaw(shell.Face)-PoseYaw(shell.PreviousFace))*ease;
        pitch=PosePitch(shell.PreviousFace)+(PosePitch(shell.Face)-PosePitch(shell.PreviousFace))*ease;
        if(!shell.ReducedMotion){yaw+=(float)Math.Sin(shell.Time/5.3f*6.2831853f)*.075f;pitch+=.035f+(float)Math.Sin(shell.Time/7.1f*6.2831853f)*.055f;}
        cosYaw=(float)Math.Cos(yaw);sinYaw=(float)Math.Sin(yaw);cosPitch=(float)Math.Cos(pitch);sinPitch=(float)Math.Sin(pitch);
        cy=211+(shell.ReducedMotion?0:(float)Math.Sin(shell.Time*.7f)*3);scale=109;
        Cube();
        if(t>.85f)
        {
            if(shell.Page==ShellPage.Cube) MenuLabels(shell);
            else FaceMenu(shell);
        }
        Footer(shell);
    }
    private void Cube()
    {
        for(int f=0;f<6;f++){faceOrder[f]=f;depth[f]=Rotate(Surface(f,0,0)).Z;}
        for(int i=1;i<6;i++){int j=i;while(j>0&&depth[faceOrder[j]]<depth[faceOrder[j-1]]){int v=faceOrder[j];faceOrder[j]=faceOrder[j-1];faceOrder[j-1]=v;j--;}}
        for(int n=0;n<6;n++)
        {
            int face=faceOrder[n];
            for(int y=0;y<24;y++)for(int x=0;x<24;x++)
            {
                int at=face*625+y*25+x;
                var a=points[at];var b=points[at+1];var d=points[at+25];var e=points[at+26];
                float mx=(a.X+b.X+d.X+e.X)*.25f,my=(a.Y+b.Y+d.Y+e.Y)*.25f,mz=(a.Z+b.Z+d.Z+e.Z)*.25f;
                float nx=mx-Clamp(mx,-.91f,.91f),ny=my-Clamp(my,-.91f,.91f),nz=mz-Clamp(mz,-.91f,.91f);
                float len=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz);var normal=Rotate(new IplPoint(nx/len,ny/len,nz/len));
                var p=Rotate(new IplPoint(mx,my,mz));
                float lit=Clamp(normal.X*-.35f+normal.Y*.6f+normal.Z*.65f,0,1);
                float spec=Clamp(normal.X*-.25f+normal.Y*.48f+normal.Z*.84f,0,1);
                spec*=spec;spec*=spec;spec*=spec;spec*=spec;spec*=spec;
                // Broad environmental reflection, a tight hot spot and the thin
                // rounded rim give the IPL's dark glass, rather than opaque plastic.
                float edge=1-(float)Math.Abs(normal.Z);
                float streak=(p.Y-.72f+p.X*.14f)*24;streak*=streak;
                streak=.32f/(1+streak*streak);
                int r=(int)(8+lit*13+spec*190+streak*120+edge*55);
                int g=(int)(11+lit*19+spec*198+streak*133+edge*63);
                int blue=(int)(18+lit*22+spec*210+streak*145+edge*87);
                if(p.X>.25f){g+=9;r+=3;} if(p.X<-.5f)blue+=12;
                uint color=Rgba(r,g,blue,normal.Z>0?135:48);
                var aa=Project(a);var bb=Project(b);var dd=Project(d);var ee=Project(e);
                Tri(aa,bb,dd,color);Tri(bb,ee,dd,color);
            }
        }
    }
    private static uint Rgba(int r,int g,int b,int a)=>(uint)((Math.Min(255,r)<<24)|(Math.Min(255,g)<<16)|(Math.Min(255,b)<<8)|a);
    private static int SurfaceIndex(ShellFace face)=>face==ShellFace.Calendar?4:face==ShellFace.Options?2:face==ShellFace.Play?3:(int)face;
    private IplPoint FacePoint(IplShell s,float u,float v)=>Project(Surface(SurfaceIndex(s.Face),u,v));
    private void FaceText(IplShell s,string text,float u,float v,float size,uint color,float extraAngle=0)
    {
        var p=FacePoint(s,u,v);var right=FacePoint(s,u+.1f,v);
        float angle=(float)Math.Atan2(right.Y-p.Y,right.X-p.X)*57.29578f+extraAngle;
        float f=3.8f/(3.8f-p.Z);size*=f/1.357f;
        float width=TextWidth(text,size),rad=angle*.017453293f;
        c.Text(text,p.X-width*.5f*(float)Math.Cos(rad),p.Y-width*.5f*(float)Math.Sin(rad),size,color,angle);
    }
    private void MenuLabels(IplShell s)
    {
        FaceText(s,"Game Play",0,.79f,24,White);
        FaceText(s,"Video",.81f,.05f,24,White,90);
        FaceText(s,"Calendar",0,-.78f,24,White);
        FaceText(s,"Options",-.81f,-.02f,24,White,-90);
    }
    private void FaceMenu(IplShell s)
    {
        FaceText(s,s.Title,0,.77f,26,White);
        if(s.Face==ShellFace.Memory||s.Face==ShellFace.Play)
        {
            FaceText(s,s.Face==ShellFace.Play?"No disc inserted.":"No card mounted.",0,.1f,16,White);
            FaceText(s,"Demonstration",0,-.16f,12,0xb5b9c9ff);return;
        }
        for(int i=0;i<s.Rows;i++)
        {
            float y=.38f-i*.36f;
            uint color=i==s.Row?White:0xb5b9c9ff;
            float size=i==s.Row?20:17;
            FaceText(s,s.Label(i),-.27f,y,size,color);
            string value=s.Value(i); if(s.Face==ShellFace.Video&&i==0)value+=" lines";
            FaceText(s,value,.30f,y-.16f,size-1,color);
        }
    }
    private void Grid()
    {
        for(int x=8;x<640;x+=16)for(int y=0;y<480;y+=16)
        {
            float fade=Clamp(1-(float)Math.Sqrt((x-320)*(x-320)/115600f+(y-240)*(y-240)/78400f),0,1);
            uint color=Rgba(61,65,67,(int)(fade*165));
            Line(x,y,x,y+16,1,color);Line(x,y,x+16,y,1,color);
        }
    }
    private void Detail(IplShell s)
    {
        Grid();
        Center("Choose a system setting.",320,28,20,White);
        if(s.Video.Pending)
        {
            Banner("Keep these video settings?",false);
            Center(((int)Math.Ceiling(s.Video.Remaining)).ToString()+" seconds to confirm",320,357,18,White);
        }
        Center(s.Label(s.Row),320,104,23,Green);
        if(s.Face==ShellFace.Calendar)
        {
            int count=s.Row==0?2:3;
            float total=s.Row==0?214:352,start=320-total/2;
            for(int i=0;i<count;i++)
            {
                string value=s.Segment(i);float w=i==2?126:84;
                float x=start+w/2;
                BlockText(value,x,180,i==s.Part?6:5,i==s.Part?Block:0x246b14ff);
                if(i==s.Part)Arrows(x,151,263);
                Center(s.Row==0?(i==0?"Hour":"Minute"):(i==0?"Day":i==1?"Month":"Year"),x,285,16,White);
                start+=w+28;
            }
            Center("Calendar demonstration. Changes last for this session.",320,351,14,0xc4c9deff);
        }
        else
        {
            string value=s.Value(s.Row).ToUpperInvariant();
            float cell=Math.Min(8,510f/Math.Max(1,value.Length*6-1));
            BlockText(value,320,182,cell,Block);Arrows(320,148,267);
            if(s.Face==ShellFace.Video&&!s.Video.Pending)
            {
                string status=s.Video.Status;
                if(status=="UNSUPPORTED MODE OR CABLE"||status=="NOT ENOUGH MEMORY FOR MODE")Banner(status=="UNSUPPORTED MODE OR CABLE"?"Unsupported mode or cable.":"Not enough memory for this mode.",true);
                else Center("A previews the mode. B discards changes.",320,350,16,White);
            }
            else if(s.Face==ShellFace.Options)Center("Sound demonstration. Changes last for this session.",320,351,14,0xc4c9deff);
        }
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
    private void Footer(IplShell s)
    {
        const float y=437;
        for(int r=15;r>3;r-=3)Circle(91,y,r,Rgba(210,214,217,130),false);
        Circle(91,y,3,0xd7d9dfff,true);
        Text("... "+(s.Page==ShellPage.Cube?"Menu Selection":s.Face==ShellFace.Calendar&&s.Editing?"Part / Value":"Select"),115,y-13,17,White);
        if(s.Page==ShellPage.Cube)return;
        for(int r=22;r>10;r-=3)Circle(339,y,r,Rgba(255,35,107,10),true);
        Circle(339,y,11,0xa42351ff,true);Center("B",339,y-14,20,White);Text("... "+(s.Editing?"Cancel":"Back"),359,y-13,17,White);
        for(int r=23;r>12;r-=3)Circle(510,y,r,Rgba(15,245,196,10),true);
        Circle(510,y,13,0x168a73ff,true);Center("A",510,y-14,20,White);Text("... "+(s.Editing?"Confirm":"Open"),532,y-13,17,White);
    }
}
