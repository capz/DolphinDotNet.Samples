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
    internal readonly Sound?[] Sounds=new Sound?[13];
    internal readonly string[] Status=new string[13],Details=new string[13];
    internal readonly string[] Sources=new string[13];
    internal FirmwareAudioSampleInfo[] Samples=new FirmwareAudioSampleInfo[0];
    internal string[] SampleNames=new string[0],SampleDetails=new string[0],SampleStatus=new string[0];
    internal string Revision="CONSOLE IPL";
    internal static IplAudioResources Prepare(CancellationToken cancellation)
    {
        var result=new IplAudioResources();
        try
        {
            // Reads the configured console/emulator ROM through EXI. No file
            // probes, embedded Nintendo assets, synthetic sound or WAV fallback.
            using var image=SystemFirmware.ReadConsole(cancellation);
            result.Catalog=image.CreateCatalog(FirmwareCatalogOptions.IncludeSystemAudio,cancellation);
            result.Samples=result.Catalog.GetAudioSamples();
            result.SampleNames=new string[result.Samples.Length];result.SampleDetails=new string[result.Samples.Length];result.SampleStatus=new string[result.Samples.Length];
            for(int i=0;i<result.Samples.Length;i++)
            {
                var info=result.Samples[i];
                result.SampleNames[i]=info.WaveId<0?"Sample "+(i+1):"Wave "+info.WaveId;
                result.SampleDetails[i]="Mono / "+(info.SampleRate/1000)+" kHz";result.SampleStatus[i]="READY";
            }
            if(image.Revision.Contains("Revision 1.2"))result.Revision=image.Revision.Contains("PAL")?"PAL 1.2":"NTSC 1.2";
            else result.Revision="EARLY IPL";
            for(int i=0;i<result.Sounds.Length;i++)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    if(i<3)
                    {
                        var variant=(SystemStartupSoundVariant)i;
                        if(result.Catalog.TryGetStartupSoundInfo(variant,out var info)&&info!=null)
                        {
                            result.Sounds[i]=SystemResources.CreateStartupVariantSound(result.Catalog,variant,cancellation);
                            result.Sources[i]="Wave IDs "+info.LeftSourceWaveId+" / "+info.RightSourceWaveId;
                        }
                        else if(i==0)
                        {
                            result.Sounds[i]=SystemResources.CreateStartupSound(result.Catalog,cancellation);
                            result.Sources[i]="Stereo startup archive";
                        }
                        else {result.Status[i]="UNAVAILABLE";result.Details[i]="Startup unsupported";continue;}
                    }
                    else
                    {
                        var identity=IplSelection.Effects[i-3];
                        if(!result.Catalog.TryGetMenuSoundInfo(identity,out var info)||info==null)
                        {result.Status[i]="UNAVAILABLE";result.Details[i]="Menu profile unsupported";continue;}
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
                catch(NotSupportedException){result.Status[i]="UNAVAILABLE";result.Details[i]=i<3?"Startup unsupported":"Menu profile unsupported";}
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
        IplAudioResources? resources=null;SoundChannel? voice=null;string state="LOADING IPL",detail="Reading console resources",failureDetail="";
        bool exit=false;int playing=-1,playingCategory=-1;bool stickUp=false,stickDown=false;Exception? failure=null;
        SystemAudioSample? sourceSample=null;int sourceIndex=-1,loadingSource=-1;
        Task<SystemAudioSample>? sourceLoading=null;bool playPreparedSource=false;
        var titles=new string[IplSelection.Names.Length];
        for(int i=0;i<titles.Length;i++)titles[i]=IplSelection.Names[i].ToUpperInvariant();
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
                        finally {DebugOverlay.WriteLine("IPL IMAGE DISPOSED; OWNED CATALOG READY");}
                    }
                    catch(OperationCanceledException){state="CANCELLED";}
                    catch(Exception error)
                    {
                        state="IPL UNAVAILABLE";detail=Short(error.Message,27);
                        failureDetail=error.Message.Length>27?Short(error.Message.Substring(27),27):"";
                        DebugOverlay.WriteLine("IPL LOAD FAILED "+error.Message);
                    }
                    loading=null;
                }
                if(sourceLoading!=null&&sourceLoading.IsCompleted)
                {
                    try
                    {
                        var prepared=sourceLoading.GetAwaiter().GetResult();
                        if(exit)prepared.Dispose();
                        else
                        {
                            sourceSample?.Dispose();sourceSample=prepared;sourceIndex=loadingSource;
                            if(playPreparedSource){voice=sourceSample.Play();playing=sourceIndex;playingCategory=4;}
                        }
                    }
                    catch(OperationCanceledException){}
                    catch(Exception error)
                    {if(resources!=null){resources.SampleStatus[loadingSource]="UNAVAILABLE";resources.SampleDetails[loadingSource]=Short(error.Message,27);}}
                    sourceLoading=null;loadingSource=-1;playPreparedSource=false;
                }
                if(exit&&loading==null&&sourceLoading==null)break;
                if(!exit)
                {
                    bool up=pad.Stick.Y>.55f,down=pad.Stick.Y<-.55f;
                    if(pad.DPadUp.WasPressed||up&&!stickUp)selection.Move(-1);
                    if(pad.DPadDown.WasPressed||down&&!stickDown)selection.Move(1);
                    stickUp=up;stickDown=down;
                    if(pad.L.WasPressed)selection.NextCategory(-1);
                    if(pad.R.WasPressed)selection.NextCategory(1);
                    if(pad.X.WasPressed)selection.NextTexture();
                    if(voice!=null&&!voice.IsPlaying){voice.Dispose();voice=null;playing=-1;playingCategory=-1;}
                    if(pad.B.WasPressed){voice?.Dispose();voice=null;playing=-1;playingCategory=-1;playPreparedSource=false;}
                    if(pad.A.WasPressed&&resources!=null&&selection.Count>0&&sourceLoading==null)
                    {
                        int index=selection.Category==0?selection.Selected:selection.Selected+3;
                        voice?.Dispose();voice=null;playing=-1;playingCategory=-1;
                        try
                        {
                            if(selection.Category==4)
                            {
                                if(sourceIndex==selection.Selected&&sourceSample!=null)
                                {voice=sourceSample.Play();playing=selection.Selected;playingCategory=4;}
                                else
                                {
                                    sourceSample?.Dispose();sourceSample=null;sourceIndex=-1;
                                    loadingSource=selection.Selected;playPreparedSource=true;
                                    sourceLoading=SystemResources.CreateAudioSampleAsync(resources.Catalog!,resources.Samples[loadingSource],cancellation.Token);
                                }
                            }
                            else if(index<resources.Sounds.Length&&resources.Sounds[index]!=null)
                            {voice=resources.Sounds[index]!.Play();playing=selection.Selected;playingCategory=selection.Category;}
                        }
                        catch(Exception error)
                        {
                            if(selection.Category==4){loadingSource=-1;playPreparedSource=false;resources.SampleStatus[selection.Selected]="PLAYBACK FAILED";resources.SampleDetails[selection.Selected]=Short(error.Message,27);}
                            else {resources.Status[index]="PLAYBACK FAILED";resources.Details[index]=Short(error.Message,27);}
                        }
                    }
                }
                int selected=selection.Selected;var texture=textures[selection.Texture];
                var device=GraphicsDevice.Default;device.BeginFrame(Background);draw.Begin();
                text.Draw(draw,"IPL RESOURCES B04",24,24,25,Color32.White);
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
                    string name=selection.Category==4?resources!.SampleNames[i]:IplSelection.Names[selection.Category==0?i:i+3];
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
                int soundIndex=selection.Category==0?selected:selected+3;
                bool sampleSelected=selection.Category==4,available=selection.Count>0;
                string title=sampleSelected?"SOURCE SAMPLE":available?titles[soundIndex]:IplSelection.Categories[selection.Category];
                text.Draw(draw,title,348,269,19,Color32.White);
                string ready=resources==null?state:!available?"UNAVAILABLE":playing==selected&&playingCategory==selection.Category?"PLAYING":sampleSelected&&loadingSource==selected?"PREPARING":sampleSelected?resources.SampleStatus[selected]:resources.Status[soundIndex];
                draw.FillRectangle(new RectF(349,305,7,7),ready=="READY"||ready=="PLAYING"?new Color32(104,210,110):Muted);
                text.Draw(draw,ready,365,301,12,Muted);
                text.Draw(draw,resources==null?detail:!available?"":sampleSelected?resources.SampleDetails[selected]:resources.Details[soundIndex],348,328,12,Muted);
                if(resources!=null&&available&&sampleSelected)
                    text.Draw(draw,resources.Samples[selected].HasLoop?"LOOPS AUTOMATICALLY":"PLAYS ONCE",348,347,10,Color32.White);
                else if(resources!=null&&available&&resources.Sources[soundIndex]!=null)
                    text.Draw(draw,resources.Sources[soundIndex],348,347,10,Muted);
                else if(resources==null&&failureDetail.Length!=0)
                    text.Draw(draw,failureDetail,348,347,12,Muted);
                text.Draw(draw,"A PLAY    B STOP",348,367,11,Color32.White);
                draw.DrawLine(new Vector2(24,414),new Vector2(616,414),Muted);
                text.Draw(draw,"UP DOWN SELECT   L R CATEGORY   A PLAY   B STOP",24,429,10,Color32.White);
                text.Draw(draw,"X TEXTURE   Y AMBIENCE   Z STATS   START EXIT",24,450,10,Color32.White);
                draw.End();device.EndFrame();await Task.Delay(1);
            }
        }
        catch(Exception error){failure=error;}
        // Keep awaited cancellation outside finally, as in AudioDemo: the
        // GameCube AOT subset does not implement ExceptionDispatchInfo.
        cancellation.Cancel();
        if(loading!=null){try{(await loading).Dispose();}catch(Exception){}}
        if(sourceLoading!=null){try{(await sourceLoading).Dispose();}catch(Exception){}}
        voice?.Dispose();sourceSample?.Dispose();resources?.Dispose();foreach(var texture in textures)texture?.Dispose();
        if(failure!=null)throw failure;
        return 0;
    }
}
