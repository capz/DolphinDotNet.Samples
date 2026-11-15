using System;
using System.Threading;
using System.Threading.Tasks;
using Dolphin.Audio;
using Dolphin.Diagnostics;
using Dolphin.System;
using Dolphin.Timing;

// Owns the currently auditioned cue/source. Repeating sections belong to the
// SDK; this UI never supplies loop flags, loop endpoints or a repeat scheduler.
internal sealed class IplPlayback
{
    private readonly IplAudioResources resources;
    private readonly CancellationToken cancellation;
    private SystemAudioSession? Session=>resources.Session;
    private SystemAudioPlayback? playback;
    private SoundChannel? voice;
    private SystemAudioSample? sample;
    private Task<SystemAudioSample>? pending;
    private int sampleIndex=-1,pendingIndex=-1;
    private bool autoplay,startup;
    private int motion=-1;
    private long motionStart,warmStart,nextStats;
    internal int PlayingCategory {get;private set;}=-1;
    internal int PlayingIndex {get;private set;}=-1;
    internal bool AmbienceEnabled {get;private set;}
    internal bool Warming=>AmbienceEnabled&&!startup&&Clock.Timestamp-warmStart<5000000;
    internal bool Pending=>pending!=null;
    internal string Error {get;private set;}="";
    internal SystemAudioStatistics? Statistics {get;private set;}
    internal IplPlayback(IplAudioResources resources,CancellationToken cancellation)
    {this.resources=resources;this.cancellation=cancellation;}
    private void ClearAudition()
    {
        playback?.Dispose();playback=null;voice?.Dispose();voice=null;autoplay=false;
        PlayingCategory=-1;PlayingIndex=-1;
        if(motion>=0&&Session!=null&&Session.State==SystemAudioSessionState.Playing)
        {
            Session.SetControl(SystemAudioControl.CubeMotion,0);
            Session.SetControl(SystemAudioControl.SubmenuMotion,0);
        }
        motion=-1;
    }
    internal void Stop()
    {
        ClearAudition();Session?.Stop();startup=false;AmbienceEnabled=false;Error="";
        DebugOverlay.WriteLine("IPL STOP");
    }
    private void StartIdle()
    {
        if(startup){Session!.Stop();startup=false;}
        bool stopped=Session!.State==SystemAudioSessionState.Stopped;
        Session.Start();Session.SetControl(SystemAudioControl.AmbienceEnabled,AmbienceEnabled?1:0);
        if(stopped)warmStart=Clock.Timestamp;
    }
    internal void Play(int category,int index)
    {
        if(pending!=null)return;
        Error="";ClearAudition();
        try
        {
            if(Session!=null)
            {
                if(category==0)
                {
                    Session.PlayStartup((SystemStartupSoundVariant)index);
                    startup=true;AmbienceEnabled=true;
                }
                else if(category==3)
                {
                    AmbienceEnabled=true;StartIdle();
                    if(index==1||index==2){motion=index-1;motionStart=Clock.Timestamp;}
                    else if(index>=3)Session.OutputMode=index==3?SystemAudioOutputMode.Stereo:SystemAudioOutputMode.Mono;
                }
                else
                {
                    StartIdle();
                    playback=category==4?Session.PlaySample(resources.Samples[index]):
                        Session.PlayCue((SystemAudioCue)(category==1?index:index+12));
                }
                PlayingCategory=category;PlayingIndex=index;
            }
            else if(category==4)
            {
                if(sample!=null&&sampleIndex==index)
                {voice=sample.Play();PlayingCategory=4;PlayingIndex=index;}
                else
                {
                    sample?.Dispose();sample=null;sampleIndex=-1;pendingIndex=index;autoplay=true;
                    pending=SystemResources.CreateAudioSampleAsync(resources.Catalog!,resources.Samples[index],cancellation);
                }
            }
            else if(category<2)
            {
                int sound=category==0?index:index+3;
                if(resources.Sounds[sound]!=null)
                {voice=resources.Sounds[sound]!.Play();PlayingCategory=category;PlayingIndex=index;}
            }
            DebugOverlay.WriteLine("IPL PLAY "+category+"/"+index);
        }
        catch(Exception error){Error=error.Message;DebugOverlay.WriteLine("IPL PLAY FAILED "+error.Message);}
    }
    internal void ToggleAmbience()
    {
        if(Session==null)return;
        try
        {
            Error="";AmbienceEnabled=!AmbienceEnabled;
            if(!startup)
            {
                bool stopped=Session.State==SystemAudioSessionState.Stopped;
                Session.SetControl(SystemAudioControl.AmbienceEnabled,AmbienceEnabled?1:0);
                if(stopped||AmbienceEnabled)warmStart=Clock.Timestamp;
            }
            DebugOverlay.WriteLine(AmbienceEnabled?"IPL AMBIENCE ON":"IPL AMBIENCE OFF");
        }
        catch(Exception error){Error=error.Message;DebugOverlay.WriteLine("IPL CONTROL FAILED "+error.Message);}
    }
    internal void Update(bool exiting)
    {
        if(pending!=null&&pending.IsCompleted)
        {
            try
            {
                var prepared=pending.GetAwaiter().GetResult();
                if(exiting)prepared.Dispose();
                else
                {
                    sample=prepared;sampleIndex=pendingIndex;
                    if(autoplay){voice=sample.Play();PlayingCategory=4;PlayingIndex=sampleIndex;}
                }
            }
            catch(OperationCanceledException){}
            catch(Exception error){Error=error.Message;resources.SampleStatus[pendingIndex]="UNAVAILABLE";}
            pending=null;pendingIndex=-1;autoplay=false;
        }
        if(exiting)return;
        if(voice!=null&&!voice.IsPlaying||playback!=null&&!playback.IsPlaying)
        {
            DebugOverlay.WriteLine("IPL ENDED "+PlayingCategory+"/"+PlayingIndex);
            voice?.Dispose();voice=null;playback?.Dispose();playback=null;PlayingCategory=-1;PlayingIndex=-1;
        }
        if(Session==null)return;
        long now=Clock.Timestamp;
        if(Session.State==SystemAudioSessionState.Faulted){Error="Audio fault - restart demo";motion=-1;startup=false;}
        else
        {
            if(startup&&!Session.IsStartupPlaying)
            {
                startup=false;warmStart=now;PlayingCategory=-1;PlayingIndex=-1;
                Session.SetControl(SystemAudioControl.AmbienceEnabled,AmbienceEnabled?1:0);
                DebugOverlay.WriteLine("IPL STARTUP HANDOVER");
            }
            if(motion>=0)
            {
                // Animate the verified original motion control, not audio loops.
                float phase=(float)((now-motionStart)%4000000)/2000000;
                Session.SetControl(motion==0?SystemAudioControl.CubeMotion:SystemAudioControl.SubmenuMotion,phase<=1?phase:2-phase);
            }
        }
        if(now>=nextStats){Statistics=Session.GetStatistics();nextStats=now+250000;}
    }
    internal string Status(int category,int index)
    {
        if(Error.Length!=0)return "PLAYBACK FAILED";
        if(Session!=null)
        {
            if(Session.State==SystemAudioSessionState.Faulted)return "AUDIO FAULT";
            if(category==3&&AmbienceEnabled)return Warming?"STARTING AMBIENCE":"PLAYING";
            return PlayingCategory==category&&PlayingIndex==index?"PLAYING":"READY";
        }
        if(category==4)return pendingIndex==index?"PREPARING":PlayingCategory==4&&PlayingIndex==index?"PLAYING":resources.SampleStatus[index];
        return PlayingCategory==category&&PlayingIndex==index?"PLAYING":resources.Status[category==0?index:index+3];
    }
    internal string Details(int category,int index)
    {
        if(Error.Length!=0)return Error;
        if(category==4)return resources.SampleDetails[index];
        if(Session==null)return resources.Details[category==0?index:index+3];
        if(category==0)return "Original stereo startup score";
        if(category==3)return Session.OutputMode==SystemAudioOutputMode.Mono?"Original ambience / mono":"Original ambience / stereo";
        return "Original sequenced cue";
    }
    internal string Repeat(int category,int index)
    {
        if(category==4)return resources.Samples[index].HasLoop?"LOOPS AUTOMATICALLY":"PLAYS ONCE";
        if(Session==null)return resources.Sources[category==0?index:index+3]??"";
        if(category==0)return "THEN IDLE AMBIENCE";
        if(category==3)return AmbienceEnabled?"CONTINUOUS / Y TO MUTE":"Y TO ENABLE AMBIENCE";
        var cue=(SystemAudioCue)(category==1?index:index+12);
        foreach(var info in resources.Cues)if(info.Cue==cue)return info.RepeatsUntilStopped?"REPEATS UNTIL STOPPED":"PLAYS ONCE";
        return "UNAVAILABLE";
    }
    internal async Task CloseAsync()
    {
        if(pending!=null){try{(await pending).Dispose();}catch(Exception){}pending=null;}
        Stop();sample?.Dispose();sample=null;
    }
}
