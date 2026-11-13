using System;
using System.Threading;
using System.Threading.Tasks;
using Dolphin.Audio;
using Dolphin.Diagnostics;
using Dolphin.Graphics;
using Dolphin.Input;
using Dolphin.System;

internal sealed class IplAudioResources : IDisposable
{
    internal FirmwareCatalog? Catalog;
    internal readonly Sound?[] Sounds=new Sound?[11];
    internal readonly string[] Status=new string[11],Details=new string[11];
    internal readonly string[] Sources=new string[11];
    internal string Revision="CONSOLE IPL";
    internal static IplAudioResources Prepare(CancellationToken cancellation)
    {
        var result=new IplAudioResources();
        try
        {
            // Reads the configured console/emulator ROM through EXI. No file
            // probes, embedded Nintendo assets, synthetic sound or WAV fallback.
            using var image=SystemFirmware.ReadConsole(cancellation);
            result.Catalog=image.CreateCatalog(FirmwareCatalogOptions.IncludeMenuAudio,cancellation);
            if(image.Revision.Contains("Revision 1.2"))result.Revision=image.Revision.Contains("PAL")?"PAL 1.2":"NTSC 1.2";
            for(int i=0;i<result.Sounds.Length;i++)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    if(i==0)
                    {
                        result.Sounds[i]=SystemResources.CreateStartupSound(result.Catalog,cancellation);
                        result.Sources[i]="Stereo startup archive";
                    }
                    else
                    {
                        var identity=IplSelection.Effects[i-1];
                        if(!result.Catalog.TryGetMenuSoundInfo(identity,out var info)||info==null)
                        {result.Status[i]="UNAVAILABLE";result.Details[i]="IPL profile unsupported";continue;}
                        // New metadata exposes original instrument IDs, distinct
                        // from the raw WSYS enumeration order.
                        string ids=info.SourceWaveCount==1?"Wave ID ":"Wave IDs ";
                        for(int source=0;source<info.SourceWaveCount;source++)
                            ids+=(source==0?"":" / ")+info.GetSourceWaveId(source);
                        result.Sources[i]=ids;
                        DebugOverlay.WriteLine(IplSelection.Names[i]+" "+info.ProfileId+" "+ids);
                        result.Sounds[i]=SystemResources.CreateMenuSound(result.Catalog,identity,cancellation);
                    }
                    var sound=result.Sounds[i]!;result.Status[i]="READY";
                    result.Details[i]=(sound.Channels==2?"Stereo":"Mono")+" / "+(sound.SampleRate/1000)+" kHz";
                }
                catch(NotSupportedException){result.Status[i]="UNAVAILABLE";result.Details[i]="IPL profile unsupported";}
            }
            DebugOverlay.WriteLine("IPL AUDIO PREPARED "+result.Catalog.MenuAudioRetainedBytes);
            return result;
        }
        catch {result.Dispose();throw;}
    }
    public void Dispose()
    {Catalog?.Dispose();Catalog=null;foreach(var sound in Sounds)sound?.Dispose();}
}

internal sealed class IplText : IDisposable
{
    private readonly SampleText fallback=new SampleText();
    private readonly SystemFont? font;
    internal bool Original=>font!=null;
    internal IplText()
    {try{font=SystemFont.Load();}catch(NotSupportedException){}catch(InvalidOperationException){}}
    internal void Draw(Draw2D draw,string value,float x,float y,float height,Color32 color)
    {
        if(font!=null)font.Draw(draw,value,new Vector2(x,y),color,height/font.CellHeight);
        else fallback.Draw(draw,value,x,y,height/5,color);
    }
    public void Dispose(){font?.Dispose();fallback.Dispose();}
}

internal static class Program
{
    private static readonly Color32 Background=new Color32(18,25,40),Muted=new Color32(155,174,203),Accent=new Color32(174,145,245);
    private static string Short(string value,int limit)=>value.Length>limit?value.Substring(0,limit):value;
    private static async Task<int> Main()
    {
        Audio.Initialize();DebugOverlay.TextVisible=false;
        using var draw=new Draw2D(GraphicsDevice.Default,new Draw2DOptions{Width=640,Height=480,SpriteCapacity=2048});
        using var text=new IplText();using var cancellation=new CancellationTokenSource();
        var selection=new IplSelection();var textures=new Texture2D?[IplSelection.Textures.Length];
        Task<IplAudioResources>? loading=Task.Run(()=>IplAudioResources.Prepare(cancellation.Token));
        IplAudioResources? resources=null;SoundChannel? voice=null;string state="LOADING IPL",detail="Reading console resources";
        bool exit=false;int playing=-1;bool stickUp=false,stickDown=false;Exception? failure=null;
        var rows=new string[IplSelection.Names.Length];var titles=new string[rows.Length];
        for(int i=0;i<rows.Length;i++)
        {rows[i]=(i+1).ToString("D2")+". "+IplSelection.Names[i];titles[i]=IplSelection.Names[i].ToUpperInvariant();}
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
                        try
                        {
                            if(!exit)
                            {
                                // Graphics publication is serialized on this
                                // executor, between frames, after background audio.
                                for(int i=0;i<textures.Length;i++)
                                {
                                    try{textures[i]=SystemResources.CreateTexture(resources.Catalog!,IplSelection.Textures[i]);}
                                    catch(NotSupportedException){}catch(System.IO.InvalidDataException){}
                                }
                                state="READY";detail="";
                            }
                        }
                        finally
                        {
                            resources.Catalog?.Dispose();resources.Catalog=null;
                            DebugOverlay.WriteLine("IPL IMAGE AND CATALOG DISPOSED");
                        }
                    }
                    catch(OperationCanceledException){state="CANCELLED";}
                    catch(Exception error){state="IPL UNAVAILABLE";detail=Short(error.Message,27);}
                    loading=null;
                }
                if(exit&&loading==null)break;
                if(!exit)
                {
                    bool up=pad.Stick.Y>.55f,down=pad.Stick.Y<-.55f;
                    if(pad.DPadUp.WasPressed||up&&!stickUp)selection.Move(-1);
                    if(pad.DPadDown.WasPressed||down&&!stickDown)selection.Move(1);
                    stickUp=up;stickDown=down;
                    if(pad.X.WasPressed)selection.NextTexture();
                    if(voice!=null&&!voice.IsPlaying){voice.Dispose();voice=null;playing=-1;}
                    if(pad.B.WasPressed){voice?.Dispose();voice=null;playing=-1;}
                    if(pad.A.WasPressed&&resources!=null&&resources.Sounds[selection.Selected]!=null)
                    {
                        voice?.Dispose();voice=null;playing=-1;
                        try{voice=resources.Sounds[selection.Selected]!.Play();playing=selection.Selected;}
                        catch(Exception error){resources.Status[selection.Selected]="PLAYBACK FAILED";resources.Details[selection.Selected]=Short(error.Message,27);}
                    }
                }
                int selected=selection.Selected;var texture=textures[selection.Texture];
                var device=GraphicsDevice.Default;device.BeginFrame(Background);draw.Begin();
                text.Draw(draw,"IPL RESOURCES",24,24,25,Color32.White);
                text.Draw(draw,text.Original?"Original system font, textures and audio":"System textures and audio - sample font",24,61,12,Muted);
                draw.DrawRectangle(new RectF(465,22,151,29),Muted);
                text.Draw(draw,resources?.Revision??"CONSOLE IPL",475,30,12,Muted);
                draw.DrawRectangle(new RectF(24,98,290,286),Muted);
                for(int i=0;i<rows.Length;i++)
                {
                    float y=108+i*24;
                    if(selected==i)draw.FillRectangle(new RectF(29,y-2,280,23),Accent);
                    text.Draw(draw,rows[i],38,y,15,selected==i?Background:Color32.White);
                }
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
                text.Draw(draw,titles[selected],348,269,19,Color32.White);
                string ready=resources==null?state:playing==selected?"PLAYING":resources.Status[selected];
                draw.FillRectangle(new RectF(349,305,7,7),ready=="READY"||ready=="PLAYING"?new Color32(104,210,110):Muted);
                text.Draw(draw,ready,365,301,12,Muted);
                text.Draw(draw,resources==null?detail:resources.Details[selected],348,328,12,Muted);
                if(resources!=null&&resources.Sources[selected]!=null)
                    text.Draw(draw,resources.Sources[selected],348,347,10,Muted);
                text.Draw(draw,"A PLAY",348,367,11,Color32.White);
                draw.DrawLine(new Vector2(24,414),new Vector2(616,414),Muted);
                text.Draw(draw,"UP DOWN SELECT   A PLAY   B STOP   X TEXTURE   START EXIT",24,436,10,Color32.White);
                draw.End();device.EndFrame();await Task.Delay(1);
            }
        }
        catch(Exception error){failure=error;}
        // Keep awaited cancellation outside finally, as in AudioDemo: the
        // GameCube AOT subset does not implement ExceptionDispatchInfo.
        cancellation.Cancel();
        if(loading!=null){try{(await loading).Dispose();}catch(Exception){}}
        voice?.Dispose();resources?.Dispose();foreach(var texture in textures)texture?.Dispose();
        if(failure!=null)throw failure;
        return 0;
    }
}
