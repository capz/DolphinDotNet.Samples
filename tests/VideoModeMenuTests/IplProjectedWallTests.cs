using System;
using System.Reflection;

internal static class IplProjectedWallTests
{
    private static IplPoint Project(IplPoint p)
    {
        float f=IplCamera.Distance/(IplCamera.Distance-p.Z);
        return new IplPoint(322+p.X*118*IplCamera.HorizontalScale*f,202-p.Y*118*f,p.Z);
    }
    internal static void Run()
    {
        var middle=typeof(IplRenderer).GetMethod("ProjectedMiddle",BindingFlags.Static|BindingFlags.NonPublic)!;
        for(int a=-4;a<=4;a++)for(int b=-4;b<=4;b++)
        {
            var left=new IplPoint(-.9f,.45f,a*.25f);
            var right=new IplPoint(.8f,-.55f,b*.25f);
            var expected=Project(new IplPoint((left.X+right.X)*.5f,(left.Y+right.Y)*.5f,(left.Z+right.Z)*.5f));
            var actual=(IplPoint)middle.Invoke(null,new object[]{Project(left),Project(right)})!;
            if(Math.Abs(actual.X-expected.X)>.001f||Math.Abs(actual.Y-expected.Y)>.001f||actual.Z!=expected.Z)
                throw new Exception("Cached wall midpoint must match projecting the world-space midpoint, including unequal depths.");
        }
        Console.WriteLine("Projected wall cache: 81 unequal-depth midpoint projections agree within 0.001 pixels.");
    }
}
