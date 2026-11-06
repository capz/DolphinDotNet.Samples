using System;

// A whole field rocks in 3D; its block geometry and bevels stay immutable.
// Shared with the CPU proof fallback. The native canvas loads this basis into GX.
internal struct IplSelectorPose
{
    internal const float Distance=900;
    internal IplPoint Right,Down;
    internal float X,Y;
    internal bool Moving;
    internal static IplSelectorPose For(IplShell shell,int part,float width)
    {
        var pose=new IplSelectorPose {Right=new IplPoint(1,0),Down=new IplPoint(0,-1)};
        if(!shell.Editing||shell.Video.Pending||shell.ReducedMotion||part!=shell.Part||shell.SelectorTime<=0)return pose;
        float ease=Math.Min(1,shell.SelectorTime/.18f);ease=ease*ease*(3-2*ease);
        float t=shell.Time;
        float yaw=(float)Math.Sin(t*1.51f)*.28f*ease,pitch=(float)Math.Sin(t*1.17f)*.20f*ease;
        // Wide words keep clear of the arrows; short time/date parts rock more.
        float roll=(float)Math.Sin(t*1.93f)*.13f*Math.Min(1,160/Math.Max(1,width))*ease;
        float cy=(float)Math.Cos(yaw),sy=(float)Math.Sin(yaw),cp=(float)Math.Cos(pitch),sp=(float)Math.Sin(pitch),cr=(float)Math.Cos(roll),sr=(float)Math.Sin(roll);
        pose.Right=new IplPoint(cy*cr,cy*sr,-sy);
        pose.Down=new IplPoint(-sy*sp*cr+cp*sr,-sy*sp*sr-cp*cr,-cy*sp);
        pose.X=(float)Math.Sin(t*1.39f)*3*ease;pose.Y=(float)Math.Sin(t*1.73f)*2.8f*ease;
        pose.Moving=yaw!=0||pitch!=0||roll!=0||pose.X!=0||pose.Y!=0;
        return pose;
    }
    internal IplPoint Project(float x,float y,float centerX,float centerY)
    {
        float scale=Distance/(Distance-Right.Z*x-Down.Z*y);
        return new IplPoint(centerX+(Right.X*x+Down.X*y+X)*scale,centerY-(Right.Y*x+Down.Y*y-Y)*scale);
    }
}
