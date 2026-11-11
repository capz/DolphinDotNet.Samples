using System;
using System.Reflection;

// A geometric framing contract; native material fidelity still needs screenshots.
internal static class IplCameraTests
{
    private static readonly MethodInfo Project=typeof(IplRenderer).GetMethod("Project",BindingFlags.Instance|BindingFlags.NonPublic)!;
    private static readonly MethodInfo View=typeof(IplRenderer).GetMethod("ProjectView",BindingFlags.Instance|BindingFlags.NonPublic)!;
    private static readonly MethodInfo Face=typeof(IplRenderer).GetMethod("FacePoint",BindingFlags.Instance|BindingFlags.NonPublic)!;
    private static IplPoint ProjectPoint(IplRenderer renderer,IplPoint point)=>(IplPoint)Project.Invoke(renderer,new object[]{point})!;
    private static IplPoint FacePoint(IplRenderer renderer,IplShell shell,float u,float v)=>(IplPoint)Face.Invoke(renderer,new object[]{shell,u,v})!;
    private static void Close(float actual,float expected,float tolerance,string detail)
    {if(Math.Abs(actual-expected)>tolerance)throw new Exception(detail+": "+actual+" versus "+expected);}

    internal static void Run()
    {
        for(int style=0;style<6;style++)
        {
            var face=(ShellFace)style;var shell=new IplShell(new FakeOutput()){ReducedMotion=true};
            shell.TurnTo(face);shell.Tick(.7f);shell.Time=0;
            var renderer=new IplRenderer(new IplProof());renderer.Prepare(shell);
            float left=float.MaxValue,right=float.MinValue,top=float.MaxValue,bottom=float.MinValue;
            for(int i=0;i<IplSealedGlassGeometry.SurfaceVertices;i++)
            {
                IplSealedGlassGeometry.Vertex(i,out var local,out var normal);var point=ProjectPoint(renderer,local);
                left=Math.Min(left,point.X);right=Math.Max(right,point.X);top=Math.Min(top,point.Y);bottom=Math.Max(bottom,point.Y);
            }
            // Measurements of the supplied 1448x1086 reference, normalized to 640x480.
            // Allow three pixels for raster-boundary/reflection ambiguity, not an exact-match claim.
            Close(left,181.7f,3,"Reference left silhouette "+face);Close(right,461.4f,3,"Reference right silhouette "+face);
            Close(top,61.9f,3,"Reference top silhouette "+face);Close(bottom,348.3f,3,"Reference bottom silhouette "+face);

            // Check projected winding independently of the manifold's signed
            // volume. Otherwise GX_CULL_BACK can silently select the rear wall.
            for(int boundary=0;boundary<2;boundary++)for(int surface=0;surface<6;surface++)for(int triangle=0;triangle<2;triangle++)
            {
                var points=new IplPoint[3];
                for(int slot=0;slot<3;slot++){
                    int index=IplSealedGlassGeometry.NativeTriangleIndex(surface,12,12,triangle*3+slot,boundary==1);
                    IplSealedGlassGeometry.Vertex(index,out var local,out _);points[slot]=ProjectPoint(renderer,local);
                }
                if(Math.Abs(points[0].Z)<.8f)continue;
                float area=(points[1].X-points[0].X)*(points[2].Y-points[0].Y)-(points[1].Y-points[0].Y)*(points[2].X-points[0].X);
                bool frontFacing=boundary==0?points[0].Z>0:points[0].Z<0;
                if((area>0)!=frontFacing)throw new Exception("Native culling selects the wrong glass interface: "+face);
            }

            var a=FacePoint(renderer,shell,-.5f,.2f);var b=FacePoint(renderer,shell,.5f,.2f);
            var c=FacePoint(renderer,shell,.1f,.5f);var d=FacePoint(renderer,shell,.1f,-.5f);
            Close(a.Y,b.Y,.001f,"Face rows must have no permanent roll "+face);
            Close(c.X,d.X,.001f,"Face columns must be upright "+face);
            if(a.X>=b.X||c.Y>=d.Y)throw new Exception("Face projection is mirrored or inverted: "+face);
            var icon=ProjectPoint(renderer,IplIconLayout.Point(face,0,0));
            Close(icon.X,321.7f,1,"Centered icon horizontal projection "+face);Close(icon.Y,205.1f,1,"Centered icon vertical projection "+face);
        }
        var neutral=new IplShell(new FakeOutput()){ReducedMotion=true};var camera=new IplRenderer(new IplProof());camera.Prepare(neutral);
        var front=(IplPoint)View.Invoke(camera,new object[]{new IplPoint(.5f,0,1)})!;
        var rear=(IplPoint)View.Invoke(camera,new object[]{new IplPoint(.5f,0,-1)})!;
        var center=(IplPoint)View.Invoke(camera,new object[]{new IplPoint(0,0,0)})!;
        float ratio=(rear.X-center.X)/(front.X-center.X),oldRatio=(3.8f-1)/(3.8f+1);
        if(ratio<=oldRatio+.15f||ratio>=1)throw new Exception("Rear walls need weaker perspective contraction than the previous close camera.");
        Console.WriteLine("Camera framing: actual rounded silhouette near reference bounds, six upright face poses, centered icon and weaker rear-wall perspective passed; material matching requires native review.");
    }
}
