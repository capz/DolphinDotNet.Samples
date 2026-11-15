using System;
using System.Threading;
using Dolphin.Audio;
using Dolphin.Diagnostics;
using Dolphin.System;

internal sealed class IplAudioResources : IDisposable
{
    internal FirmwareCatalog? Catalog;
    internal SystemAudioSession? Session;
    internal SystemAudioCueInfo[] Cues=new SystemAudioCueInfo[0];
    internal string AudioFailure="";
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
            if(result.Catalog.SupportsAudioSession)
            {
                try {result.Session=SystemResources.CreateAudioSession(result.Catalog,cancellation);result.Cues=result.Catalog.GetAudioCues();}
                catch(OperationCanceledException){throw;}
                catch(Exception error){result.AudioFailure=error.Message;DebugOverlay.WriteLine("IPL SESSION FAILED "+error.Message);}
            }
            if(result.Session!=null)
            {
                result.Samples=new FirmwareAudioSampleInfo[result.Session.SampleCount];
                // PlaySample accepts descriptors owned by this prepared session.
                for(int i=0;i<result.Samples.Length;i++)result.Samples[i]=result.Session.GetSample(i);
            }
            else result.Samples=result.Catalog.GetAudioSamples();
            result.SampleNames=new string[result.Samples.Length];result.SampleDetails=new string[result.Samples.Length];result.SampleStatus=new string[result.Samples.Length];
            for(int i=0;i<result.Samples.Length;i++)
            {
                var info=result.Samples[i];
                result.SampleNames[i]=info.WaveId<0?"Sample "+(i+1):"Wave "+info.WaveId;
                result.SampleDetails[i]="Mono "+(info.Codec<2?"ADPCM":info.Codec==2?"PCM8":"PCM16")+" / "+(info.SampleRate/1000)+" kHz";result.SampleStatus[i]="READY";
            }
            if(image.Revision.Contains("Revision 1.2"))result.Revision=image.Revision.Contains("PAL")?"PAL 1.2":"NTSC 1.2";
            else result.Revision="EARLY IPL";
            if(result.Session!=null)
            {
                DebugOverlay.WriteLine("IPL SESSION READY "+result.Session.ProfileId+" SOURCES "+result.Samples.Length);
                return result;
            }
            for(int i=0;i<result.Sounds.Length;i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if(result.AudioFailure.Length!=0){result.Status[i]="AUDIO UNAVAILABLE";result.Details[i]=result.AudioFailure;continue;}
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
    {Session?.Dispose();Session=null;Catalog?.Dispose();Catalog=null;foreach(var sound in Sounds)sound?.Dispose();}
}

