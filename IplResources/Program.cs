using System;
using System.Threading;
using System.Threading.Tasks;
using Dolphin.Audio;
using Dolphin.Diagnostics;
using Dolphin.Graphics;
using Dolphin.Input;
using Dolphin.System;

internal static class Program
{
    private static readonly Color32 Background=new Color32(18,25,40),Muted=new Color32(155,174,203),Accent=new Color32(174,145,245);
    private static readonly float[] TabX={34,99,144,194,261},TabWidth={62,42,47,64,49};
    private static string Short(string value,int limit)=>value.Length>limit?value.Substring(0,limit):value;
    private static async Task<int> Main()
    {
        Audio.Initialize();DebugOverlay.TextVisible=false;
        using var draw=new Draw2D(GraphicsDevice.Default,new Draw2DOptions{Width=640,Height=480});
        using var text=new IplText();using var cancellation=new CancellationTokenSource();
        string buildLabel="SDK "+IplBuildInfo.SdkVersion+(text.Original?" / IPL font":" / sample font");
        var selection=new IplSelection();var textures=new Texture2D?[IplSelection.Textures.Length];
        Task<IplAudioResources>? loading=Task.Run(()=>IplAudioResources.Prepare(cancellation.Token));
        IplAudioResources? resources=null;IplPlayback? player=null;
        string state="LOADING IPL",detail="Reading console resources",failureDetail="";
        bool exit=false,stickUp=false,stickDown=false,showStats=false;Exception? failure=null;
        SystemAudioStatistics? lastStats=null;string statsLine="",healthLine="";
        try
        {
            while(true)
            {
                Controller.Update();var pad=Controller.GetState();
                if(pad.Start.WasPressed){exit=true;cancellation.Cancel();}
                if(loading!=null&&loading.IsCompleted)
                {
                    try
                    {
                        resources=loading.GetAwaiter().GetResult();
                        selection.SetSampleCount(resources.Samples.Length);
                        selection.SetCompleteAudio(resources.Session!=null);
                        if(!exit)
                        {
                            // Publish graphics between frames on the app executor.
                            for(int i=0;i<textures.Length;i++)
                            {
                                try{textures[i]=SystemResources.CreateTexture(resources.Catalog!,IplSelection.Textures[i]);}
                                catch(NotSupportedException){}catch(System.IO.InvalidDataException){}
                            }
                            player=new IplPlayback(resources,cancellation.Token);
                            if(resources.Session!=null){resources.Catalog!.Dispose();resources.Catalog=null;}
                            state="READY";detail="";
                            DebugOverlay.WriteLine("IPL DEMO READY "+(resources.Session!=null?"COMPLETE":"PARTIAL"));
                        }
                    }
                    catch(OperationCanceledException){state="CANCELLED";}
                    catch(Exception error)
                    {
                        state="IPL UNAVAILABLE";detail=Short(error.Message,29);
                        failureDetail=error.Message.Length>29?Short(error.Message.Substring(29),29):"";
                        DebugOverlay.WriteLine("IPL LOAD FAILED "+error.Message);
                    }
                    loading=null;
                }
                player?.Update(exit);
                if(exit&&loading==null&&(player==null||!player.Pending))break;
                if(!exit)
                {
                    bool up=pad.Stick.Y>.55f,down=pad.Stick.Y<-.55f;
                    if(pad.DPadUp.WasPressed||up&&!stickUp)selection.Move(-1);
                    if(pad.DPadDown.WasPressed||down&&!stickDown)selection.Move(1);
                    stickUp=up;stickDown=down;
                    if(pad.L.WasPressed)selection.NextCategory(-1);
                    if(pad.R.WasPressed)selection.NextCategory(1);
                    if(pad.X.WasPressed){selection.NextTexture();DebugOverlay.WriteLine("IPL TEXTURE "+selection.Texture);}
                    if(pad.Z.WasPressed)showStats=!showStats;
                    if(pad.B.WasPressed)player?.Stop();
                    if(pad.Y.WasPressed)player?.ToggleAmbience();
                    if(pad.A.WasPressed&&selection.Count>0)player?.Play(selection.Category,selection.Selected);
                }
                if(player?.Statistics!=null&&!ReferenceEquals(lastStats,player.Statistics))
                {
                    lastStats=player.Statistics;
                    statsLine=(lastStats.NativeBytes/1024)+" KB / "+lastStats.ActiveVoices+" voices";
                    healthLine="Queue "+lastStats.BufferedFrames+" / drops "+lastStats.Underruns+" / fault "+lastStats.FaultCode;
                }
                int selected=selection.Selected;var texture=textures[selection.Texture];
                var device=GraphicsDevice.Default;device.BeginFrame(Background);draw.Begin();
                text.Draw(draw,"IPL RESOURCES B05",24,24,25,Color32.White);
                text.Draw(draw,buildLabel,24,61,12,Muted);
                draw.DrawRectangle(new RectF(465,22,151,29),Muted);
                text.Draw(draw,resources?.Revision??"CONSOLE IPL",475,30,12,Muted);
                draw.DrawRectangle(new RectF(24,98,290,31),Muted);
                for(int i=0;i<IplSelection.Categories.Length;i++)
                {
                    if(selection.Category==i)draw.FillRectangle(new RectF(TabX[i]-4,102,TabWidth[i],23),Accent);
                    text.Draw(draw,IplSelection.Categories[i],TabX[i],109,10,selection.Category==i?Background:Color32.White);
                }
                draw.DrawRectangle(new RectF(24,138,290,246),Muted);
                for(int row=0;row<IplSelection.VisibleRows&&selection.FirstVisible+row<selection.Count;row++)
                {
                    int i=selection.FirstVisible+row;float y=153+row*32;
                    if(selected==i)draw.FillRectangle(new RectF(29,y-3,280,27),Accent);
                    string name=selection.Category==4?resources!.SampleNames[i]:IplSelection.GetName(selection.Category,i);
                    text.Draw(draw,name,40,y,17,selected==i?Background:Color32.White);
                }
                if(selection.Count==0)text.Draw(draw,loading!=null?"LOADING":"NO RESOURCES AVAILABLE",38,180,11,Muted);
                else text.Draw(draw,(selected+1)+" / "+selection.Count,247,362,10,Muted);
                draw.DrawRectangle(new RectF(332,98,284,146),Muted);
                draw.DrawRectangle(new RectF(345,112,110,110),Muted);
                if(texture!=null)
                {
                    float scale=Math.Min(98f/texture.Width,98f/texture.Height),w=texture.Width*scale,h=texture.Height*scale;
                    draw.DrawTexture(texture,new RectF(400-w/2,167-h/2,w,h),Color32.White);
                }
                else text.Draw(draw,loading!=null?"LOADING":"UNAVAILABLE",350,162,9,Muted);
                text.Draw(draw,"TEXTURE",469,118,16,Color32.White);
                text.Draw(draw,IplSelection.TextureNames[selection.Texture],469,151,11,Muted);
                text.Draw(draw,"X NEXT TEXTURE",469,203,9,Color32.White);
                draw.DrawRectangle(new RectF(332,256,284,128),Muted);
                bool available=selection.Count>0;
                string title=selection.Category==4?"SOURCE SAMPLE":available?IplSelection.GetName(selection.Category,selected).ToUpperInvariant():IplSelection.Categories[selection.Category];
                text.Draw(draw,Short(title,24),348,269,19,Color32.White);
                string ready=player==null?state:!available?"UNAVAILABLE":player.Status(selection.Category,selected);
                draw.FillRectangle(new RectF(349,305,7,7),ready=="READY"||ready=="PLAYING"?new Color32(104,210,110):Muted);
                text.Draw(draw,ready,365,301,12,Muted);
                string info=player==null?detail:!available?"":player.Details(selection.Category,selected);
                string repeat=player==null?failureDetail:!available?"":player.Repeat(selection.Category,selected);
                text.Draw(draw,Short(showStats&&lastStats!=null?statsLine:info,31),348,328,12,Muted);
                text.Draw(draw,Short(showStats&&lastStats!=null?healthLine:repeat,35),348,347,10,Color32.White);
                text.Draw(draw,"A PLAY / RESTART    B STOP",348,367,10,Color32.White);
                draw.DrawLine(new Vector2(24,414),new Vector2(616,414),Muted);
                text.Draw(draw,"UP DOWN SELECT   L R CATEGORY   A PLAY   B STOP",24,429,10,Color32.White);
                text.Draw(draw,"X TEXTURE   Y AMBIENCE   Z STATS   START EXIT",24,450,10,Color32.White);
                draw.End();device.EndFrame();await Task.Delay(1);
            }
        }
        catch(Exception error){failure=error;}
        // Await cancellation outside finally: the target AOT subset does not
        // implement ExceptionDispatchInfo. Dispose playback before its session.
        cancellation.Cancel();
        if(loading!=null){try{(await loading).Dispose();}catch(Exception){}}
        if(player!=null)await player.CloseAsync();
        resources?.Dispose();foreach(var texture in textures)texture?.Dispose();
        DebugOverlay.WriteLine("IPL DEMO DISPOSED");
        if(failure!=null)throw failure;
        return 0;
    }
}
