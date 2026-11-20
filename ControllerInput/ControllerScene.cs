using System;
using Dolphin.Diagnostics;
using Dolphin.Graphics;
using Dolphin.Timing;

internal sealed class ControllerScene : IDisposable
{
    private readonly GraphicsDevice device=GraphicsDevice.Default;
    private readonly Renderer renderer=new Renderer();
    private readonly Camera camera=new Camera(),screenCamera=new Camera();
    private readonly ControllerControls controls;
    private readonly ControllerOcclusion occlusion;
    private readonly ControllerHud hud;
    private readonly Texture2D original,plate;
    private readonly Mesh background;
    private readonly Transform identity=new Transform();
    private readonly Material screenMaterial;
    private int frames;
    private long activeTotal,activeMaximum,frameTotal;
    internal ControllerScene()
    {
        // The neutral projections and photographic UVs share native pixels.
        // Only the controls occupy 3D space; the approved shell stays exact.
        camera.SetOrthographic(640,480,.1f,128);
        camera.LookAt(new Vector3(0,0,64),Vector3.Zero,new Vector3(0,1,0));
        screenCamera.SetOrthographic(640,480,.1f,10);
        screenCamera.LookAt(new Vector3(0,0,2),Vector3.Zero,new Vector3(0,1,0));
        original=HybridAssets.LoadOriginal();
        plate=HybridAssets.LoadPlate();
        screenMaterial=new Material{Texture=plate,Cull=CullMode.None,DepthTest=false,DepthWrite=false};
        background=Mesh.Create(new[]{
            new VertexPositionColorTexture(new Vector3(-320,240,0),Color32.White,new Vector2(0,0)),
            new VertexPositionColorTexture(new Vector3(320,240,0),Color32.White,new Vector2(1,0)),
            new VertexPositionColorTexture(new Vector3(320,-240,0),Color32.White,new Vector2(1,1)),
            new VertexPositionColorTexture(new Vector3(-320,-240,0),Color32.White,new Vector2(0,1))
        },new ushort[]{0,1,2,0,2,3});
        controls=new ControllerControls(device,original);
        occlusion=new ControllerOcclusion(device);
        hud=new ControllerHud();
        DebugOverlay.WriteLine("CONTROLLER MODEL triangles "+(controls.TriangleCount+occlusion.TriangleCount)+" parts "+controls.PartCount);
        DebugOverlay.WriteLine("CONTROLLER MEMORY model_native "+controls.NativeBytes+" device_native "+device.NativeBytes);
    }
    internal void Present(ControllerSession session,float delta,long start)
    {
        hud.Update(session);
        device.BeginFrame(new Color32(12,7,25));
        renderer.Begin3D(screenCamera);renderer.DrawMesh(background,screenMaterial,identity);renderer.End3D();
        renderer.Begin3D(camera);occlusion.Draw(renderer);controls.Draw(renderer,session.State,delta);renderer.End3D();
        renderer.Begin3D(screenCamera);hud.Draw(renderer);renderer.End3D();
        long active=Clock.Timestamp-start;
        device.EndFrame();
        long duration=Clock.Timestamp-start;
        if(frames>=30){activeTotal+=active;frameTotal+=duration;if(active>activeMaximum)activeMaximum=active;}
        frames++;
        if(frames==150){
            DebugOverlay.WriteLine("CONTROLLER PROFILE samples 120 active_us "+(activeTotal/120)+" max_us "+activeMaximum);
            DebugOverlay.WriteLine("CONTROLLER FRAME mean_us "+(frameTotal/120)+" native "+device.NativeBytes);
        }
    }
    public void Dispose(){hud.Dispose();occlusion.Dispose();controls.Dispose();background.Dispose();plate.Dispose();original.Dispose();}
}
