using Dolphin.Graphics;
using Dolphin.Input;
using Dolphin.Timing;

internal sealed class VideoModeOutput : IVideoModeOutput
{
    private readonly GraphicsDevice device;
    internal VideoModeOutput(GraphicsDevice device) { this.device=device; }

    public VideoModeSettings Read()
    {
        var display=device.Display;
        int resolution=display.Resolution==GraphicsResolution.R320x240?0:
                       display.Resolution==GraphicsResolution.R640x576?2:1;
        return new VideoModeSettings(resolution,(int)display.Scan,display.Widescreen);
    }

    public void Configure(VideoModeSettings settings)
    {
        var current=Read();
        bool renderingOnly=settings.Resolution==current.Resolution && settings.Scan==current.Scan;
        device.ConfigureDisplay(new DisplayOptions {
            Resolution=renderingOnly?GraphicsResolution.Auto:
                       settings.Resolution==0?GraphicsResolution.R320x240:
                       settings.Resolution==2?GraphicsResolution.R640x576:GraphicsResolution.R640x480,
            Scan=renderingOnly?ScanMode.Auto:(ScanMode)settings.Scan,
            Widescreen=settings.Widescreen
        });
    }
}

internal static class Program
{
    private static int Main()
    {
        var device=GraphicsDevice.Default;
        // Initialize and retain the boot mode. All subsequent changes precede BeginFrame.
        var menu=new VideoModeMenu(new VideoModeOutput(device));
        using var draw=new Draw2D(device); // Display-correct logical coordinates, including 240p.
        using var text=new SampleText();
        var clock=new FrameClock { MaximumDeltaSeconds=0.25f };
        while(true)
        {
            Controller.Update();
            var pad=Controller.GetState(0);
            menu.Tick(clock.Tick());
            if(pad.Start.WasPressed)
            {
                menu.Revert();
                if(!menu.Pending) return 0;
            }
            if(pad.X.WasPressed) menu.SafeMode();
            else if(menu.Pending)
            {
                if(pad.B.WasPressed) menu.Revert();
                else if(pad.A.WasPressed) menu.Keep();
            }
            else
            {
                if(pad.DPadUp.WasPressed) menu.Move(-1);
                if(pad.DPadDown.WasPressed) menu.Move(1);
                if(pad.DPadLeft.WasPressed) menu.Edit(-1);
                if(pad.DPadRight.WasPressed) menu.Edit(1);
                if(pad.A.WasPressed) menu.Apply();
            }
            Present(device,draw,text,menu);
        }
    }

    private static void Circle(Draw2D draw,Vector2 center,float radius,Color32 color,float thickness)
    {
        var previous=new Vector2(center.X+radius,center.Y);
        for(int i=1;i<=32;i++)
        {
            float angle=i*6.28318530718f/32;
            var next=new Vector2(center.X+(float)System.Math.Cos(angle)*radius,
                                 center.Y+(float)System.Math.Sin(angle)*radius);
            draw.DrawLine(previous,next,color,thickness); previous=next;
        }
    }

    private static string Resolution(int value)
    { return value==0?"320X240":value==2?"640X576":"640X480"; }
    private static string Scan(int value)
    { return value==0?"AUTO":value==1?"INTERLACED":"PROGRESSIVE"; }

    private static void Present(GraphicsDevice device,Draw2D draw,SampleText text,VideoModeMenu menu)
    {
        device.BeginFrame(new Color32(14,20,32));
        draw.Begin();
        var size=draw.LogicalSize;
        // Logical height follows the render buffer. Scale the entire small UI for 240p.
        float unit=size.Y/240f;
        float left=12*unit, top=10*unit;
        var white=new Color32(226,235,245);
        var accent=new Color32(96,226,192);
        var dim=new Color32(142,160,180);
        text.Draw(draw,"VIDEO MODE",left,top,2*unit,white);
        var selected=menu.Selected;
        string[] rows={
            "RESOLUTION  "+Resolution(selected.Resolution),
            "SCAN        "+Scan(selected.Scan),
            "WIDESCREEN  "+(selected.Widescreen?"ON - 16:9":"OFF - 4:3")
        };
        for(int i=0;i<3;i++)
        {
            float y=(40+i*17)*unit;
            if(menu.Row==i) draw.FillRectangle(new RectF(left-3*unit,y-3*unit,220*unit,12*unit),new Color32(32,54,67));
            text.Draw(draw,rows[i],left,y,unit,menu.Row==i?accent:white);
        }

        var display=device.Display;
        var output=display.OutputSize;
        var render=display.RenderSize;
        text.Draw(draw,"ACTIVE "+((int)output.X).ToString()+"X"+((int)output.Y).ToString()+" "+
            (menu.Applied.Scan==1?"I":"P")+" "+(display.Widescreen?"16:9":"4:3"),left,98*unit,unit,white);
        text.Draw(draw,"OUTPUT "+((int)output.X).ToString()+"X"+((int)output.Y).ToString()+
            "  RENDER "+((int)render.X).ToString()+"X"+((int)render.Y).ToString(),left,110*unit,unit,dim);
        text.Draw(draw,"WIDESCREEN CHANGES RENDERING ONLY",left,122*unit,unit,dim);

        // Both shapes use identical horizontal/vertical units. On a matching TV
        // setting they remain square/round while the canvas expands for 16:9.
        float x=left, yPreview=144*unit, side=32*unit;
        draw.DrawRectangle(new RectF(x,yPreview,side,side),accent,unit);
        Circle(draw,new Vector2(x+55*unit,yPreview+16*unit),16*unit,accent,unit);
        text.Draw(draw,"SQUARE AND CIRCLE",left+82*unit,150*unit,unit,white);
        text.Draw(draw,"MATCH TV TO 4:3 OR 16:9",left+82*unit,161*unit,unit,dim);

        // This edge moves automatically with Draw2D.LogicalSize in widescreen.
        draw.FillRectangle(new RectF(size.X-6*unit,6*unit,unit,size.Y-12*unit),accent);
        if(menu.Pending) text.Draw(draw,menu.Status,left,178*unit,unit,accent);
        string status=menu.Pending?"KEEP MODE? "+((int)System.Math.Ceiling(menu.Remaining)).ToString()+" SECONDS":menu.Status;
        text.Draw(draw,status,left,190*unit,unit,accent);
        text.Draw(draw,menu.Pending?"A KEEP  B REVERT  X SAFE":"DPAD SELECT AND EDIT  A APPLY",left,204*unit,unit,white);
        text.Draw(draw,"X 640X480I 4:3  START EXIT  Z STATS",left,218*unit,unit,dim);
        draw.End();
        device.EndFrame();
    }
}
