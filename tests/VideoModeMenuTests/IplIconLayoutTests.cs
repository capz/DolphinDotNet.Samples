using System;

internal static class IplIconLayoutTests
{
    internal static void Run()
    {
        for(int style=0;style<6;style++)
        {
            var face=(ShellFace)style;var bitmap=IplRenderer.IconBitmap(face);
            var layout=IplIconLayout.Create(bitmap);
            float minU=float.MaxValue,maxU=float.MinValue,minV=float.MaxValue,maxV=float.MinValue;
            for(int y=0;y<bitmap.Length;y++)for(int x=0;x<bitmap[y].Length;x++)if(bitmap[y][x]=='1')
            {
                float u=layout.Left+x*layout.Cell,v=layout.Top-y*layout.RowStep;
                float right=u+layout.Cell-layout.Gap,bottom=v-layout.RowStep+layout.Gap;
                minU=Math.Min(minU,u);maxU=Math.Max(maxU,right);minV=Math.Min(minV,bottom);maxV=Math.Max(maxV,v);
                for(int corner=0;corner<4;corner++)
                {
                    var p=IplIconLayout.Point(face,(corner&1)==0?u:right,(corner&2)==0?v:bottom);
                    float qx=Math.Abs(p.X)-IplSealedGlassGeometry.Core,qy=Math.Abs(p.Y)-IplSealedGlassGeometry.Core,qz=Math.Abs(p.Z)-IplSealedGlassGeometry.Core;
                    float ax=Math.Max(qx,0),ay=Math.Max(qy,0),az=Math.Max(qz,0);
                    float distance=(float)Math.Sqrt(ax*ax+ay*ay+az*az)+Math.Min(Math.Max(qx,Math.Max(qy,qz)),0)-
                        (IplSealedGlassGeometry.Radius-IplSealedGlassGeometry.Thickness);
                    if(distance>-.30f)throw new Exception("Icon block or highlight envelope approaches a cavity wall: "+face);
                }
            }
            if(Math.Abs(minU+maxU)>0.00001f||Math.Abs(minV+maxV)>0.00001f||Math.Max(maxU-minU,maxV-minV)>IplIconLayout.MaximumSpan+.00001f)
                throw new Exception("Visible icon bounds must be centered and leave generous margins: "+face);
            var origin=IplIconLayout.Point(face,0,0);
            if(origin.X!=0||origin.Y!=0||origin.Z!=0)throw new Exception("Every icon must be at the volume's 3D center.");
            var a=IplIconLayout.Point(face,1,0);var b=IplIconLayout.Point(face,0,1);
            if(a.X*a.X+a.Y*a.Y+a.Z*a.Z!=1||b.X*b.X+b.Y*b.Y+b.Z*b.Z!=1||a.X*b.X+a.Y*b.Y+a.Z*b.Z!=0)
                throw new Exception("Each centered icon needs an orthonormal face orientation.");
        }
        var topRight=IplIconLayout.Point(ShellFace.Memory,1,0);var topUp=IplIconLayout.Point(ShellFace.Memory,0,1);
        if(topRight.X!=1||topRight.Y!=0||topRight.Z!=0||topUp.X!=0||topUp.Y!=0||topUp.Z!=-1)
            throw new Exception("Storage's centered icon must follow the top face basis.");
        Console.WriteLine("Cube icons: all six visible bounds centered at the 3D origin, with >=0.30 cavity clearance; rigid cube rotation preserves that clearance.");
    }
}
