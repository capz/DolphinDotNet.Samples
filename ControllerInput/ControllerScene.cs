using System;
using Dolphin.Diagnostics;
using Dolphin.Graphics;
using Dolphin.Timing;

internal sealed class ControllerScene : IDisposable
{
    private readonly GraphicsDevice device=GraphicsDevice.Default;
    private readonly Renderer renderer=new Renderer();
    private readonly Camera camera=new Camera(35,4f/3,.1f,30),screenCamera=new Camera();
    private readonly ControllerModel model;
    private readonly ControllerHud hud;
    private readonly Mesh background;
    private readonly Transform identity=new Transform();
    private readonly Material screenMaterial=new Material{Cull=CullMode.None,DepthTest=false,DepthWrite=false};
    private int frames;
    private long activeTotal,activeMaximum,frameTotal;
    internal ControllerScene()
    {
        camera.SetOrthographic(7.65f,5.7375f,.1f,30);
        // A modest above-view angle preserves the reference alignment while
        // making the real face-button Z travel visible in the native image.
        camera.LookAt(new Vector3(0,1.4f,8),Vector3.Zero,new Vector3(0,1,0));
        screenCamera.SetOrthographic(640,480,.1f,10);
        screenCamera.LookAt(new Vector3(0,0,2),Vector3.Zero,new Vector3(0,1,0));
        model=new ControllerModel(device);
        model.Root.LocalPosition=new Vector3(0,.35f,0);
        hud=new ControllerHud();
        background=ControllerHud.CreateBackground();
        DebugOverlay.WriteLine("CONTROLLER MODEL triangles "+model.TriangleCount+" parts "+model.PartCount);
        DebugOverlay.WriteLine("CONTROLLER MEMORY model_native "+model.NativeBytes+" device_native "+device.NativeBytes);
    }
    internal void Present(ControllerSession session,float delta,long start)
    {
        hud.Update(session);
        device.BeginFrame(new Color32(12,7,25));
        renderer.Begin3D(screenCamera);renderer.DrawMesh(background,screenMaterial,identity);renderer.End3D();
        renderer.Begin3D(camera);model.Draw(renderer,session.State,delta);renderer.End3D();
        renderer.Begin3D(screenCamera);hud.Draw(renderer);renderer.End3D();
        long active=Clock.Timestamp-start;
        device.EndFrame();
        long duration=Clock.Timestamp-start;
        // Sparse measurements leave ordinary frames allocation-free. Capture runs
        // sample completed presentation boundaries independently of this log.
        if(frames>=30){activeTotal+=active;frameTotal+=duration;if(active>activeMaximum)activeMaximum=active;}
        frames++;
        if(frames==150){
            DebugOverlay.WriteLine("CONTROLLER PROFILE samples 120 active_us "+(activeTotal/120)+" max_us "+activeMaximum);
            DebugOverlay.WriteLine("CONTROLLER FRAME mean_us "+(frameTotal/120)+" native "+device.NativeBytes);
        }
    }
    public void Dispose(){background.Dispose();hud.Dispose();model.Dispose();}
}
