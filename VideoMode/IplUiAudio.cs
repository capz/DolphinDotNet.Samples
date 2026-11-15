#if IPL_SYSTEM_AUDIO
using System;
using System.Threading;
using System.Threading.Tasks;
using Dolphin.Audio;
using Dolphin.Diagnostics;
using Dolphin.System;
using Dolphin.Timing;

internal enum IplUiAudioState { NotStarted,Loading,Ready,Unavailable,Closed }

// Prepare the console's qualified audio once, off the app executor. Prefer the
// IPL explorer's sequenced session; older profiles use resident finite clips.
// No firmware bytes or prepared audio are written to storage.
internal sealed class IplUiAudio
{
    // The SDK's maximum accepted recipe, decoded sources, PCM and delay lines
    // require less than 2 MiB of native scratch. This is separate from the
    // retained clip-bank limit and the managed firmware-extraction heap.
    private const int EffectCount=10,PreparationBudget=2*1024*1024,BankBudget=6*1024*1024;
    private readonly CancellationTokenSource cancellation=new CancellationTokenSource();
    private readonly IplUiAudioPolicy policy=new IplUiAudioPolicy();
    private readonly SoundChannel?[] voices=new SoundChannel?[IplUiAudioPolicy.VoiceLimit];
    private readonly SystemAudioPlayback?[] cues=new SystemAudioPlayback?[IplUiAudioPolicy.VoiceLimit];
    private SystemAudioSession? session;
    private Task<Prepared>? loading;
    private Task? closing;
    private Sound?[]? clips;
    private bool started,stopping;
    private int volumeLevel=2;
    internal IplUiAudioState State {get;private set;}
    internal string Status {get;private set;}="System sounds not initialized.";
    internal int LoadedCount {get;private set;}
    internal int NativeBytes {get;private set;}
    internal int PlayedCount {get;private set;}
    internal int ThrottledCount {get;private set;}
    internal int StolenCount {get;private set;}
    internal int DroppedCount {get;private set;}

    private sealed class Prepared
    {
        internal readonly Sound?[]? Clips;
        internal readonly SystemAudioSession? Session;
        internal readonly int Count,Bytes;
        internal readonly string Failure;
        internal Prepared(Sound?[]? clips,int count,int bytes,string failure)
        {Clips=clips;Count=count;Bytes=bytes;Failure=failure;}
        internal Prepared(SystemAudioSession session){Session=session;Count=EffectCount;Failure="";}
    }
    internal void Start()
    {
        if(started||stopping)return;
        started=true;State=IplUiAudioState.Loading;Status="Loading system sounds...";
        try {Audio.Initialize();loading=Task.Run(Prepare);}
        catch(Exception error){Unavailable(error.Message);}
    }
    private Prepared Prepare()
    {
        var sounds=new Sound?[EffectCount];bool transferred=false;
        try
        {
            var token=cancellation.Token;int count=0,bytes=0;
            using(var image=SystemFirmware.ReadConsole(token))
            using(var catalog=image.CreateCatalog(FirmwareCatalogOptions.IncludeSystemAudio|FirmwareCatalogOptions.IncludeMenuAudio,token))
            {
                // Use the same owned, sequenced session as the working IPL
                // explorer. Do not render and retain ten separate PCM copies.
                if(catalog.SupportsAudioSession)
                {
                    var prepared=SystemResources.CreateAudioSession(catalog,token,BankBudget);
                    try{token.ThrowIfCancellationRequested();return new Prepared(prepared);}
                    catch{prepared.Dispose();throw;}
                }
                for(int i=0;i<EffectCount;i++)
                {
                    token.ThrowIfCancellationRequested();
                    var id=(SystemMenuSound)i;
                    if(!catalog.TryGetMenuSoundInfo(id,out var info)||info==null)continue;
                    sounds[i]=SystemResources.CreateMenuSound(catalog,id,token,PreparationBudget);
                    int retained=sounds[i]!.NativeBytes;
                    if(retained<0||retained>BankBudget-bytes)
                        throw new InvalidOperationException("System menu sound bank exceeds its retained memory limit.");
                    bytes+=retained;count++;
                }
            }
            cancellation.Token.ThrowIfCancellationRequested();
            if(count==0)return new Prepared(null,0,0,"No qualified system menu sound profile.");
            var result=new Prepared(sounds,count,bytes,"");transferred=true;return result;
        }
        catch(OperationCanceledException){return new Prepared(null,0,0,"System sound preparation canceled.");}
        catch(Exception error){return new Prepared(null,0,0,error.Message);}
        finally {if(!transferred)DisposeClips(sounds);}
    }
    internal void Tick(int level)
    {
        float volume=IplUiAudioPolicy.VolumeForLevel(level);
        if(stopping)return;
        if(State==IplUiAudioState.NotStarted)
        {
            volumeLevel=level;
            if(level==0)return;
            Start();
        }
        if(State==IplUiAudioState.Unavailable){volumeLevel=level;return;}
        if(session!=null&&session.State==SystemAudioSessionState.Faulted)
        {Unavailable("IPL sequencer faulted.");volumeLevel=level;return;}
        if(loading!=null&&loading.IsCompleted)
        {
            var pending=loading;loading=null;
            try
            {
                var prepared=pending.Result;
                clips=prepared.Clips;session=prepared.Session;LoadedCount=prepared.Count;NativeBytes=prepared.Bytes;
                if(clips==null&&session==null)Unavailable(prepared.Failure);
                else
                {
                    if(session!=null)NativeBytes=session.GetStatistics().NativeBytes;
                    State=IplUiAudioState.Ready;Status="System sounds ready.";
                    SetVolume(volume);DebugOverlay.WriteLine("UI AUDIO READY clips "+LoadedCount+" native bytes "+NativeBytes);
                }
            }
            catch(Exception error){Unavailable(error.Message);}
        }
        ReapVoices();
        if(volumeLevel!=level)SetVolume(volume);
        volumeLevel=level;
    }
    private void Unavailable(string detail)
    {
        for(int i=0;i<voices.Length;i++)ReleaseVoice(i);
        session?.Dispose();session=null;DisposeClips(clips);clips=null;
        State=IplUiAudioState.Unavailable;Status="System sounds unavailable.";
        DebugOverlay.WriteLine("UI AUDIO UNAVAILABLE "+detail);
    }
    private void ReapVoices()
    {
        for(int i=0;i<voices.Length;i++)
            if(voices[i]!=null&&!voices[i]!.IsPlaying||cues[i]!=null&&!cues[i]!.IsPlaying)ReleaseVoice(i);
    }
    private void ReleaseVoice(int slot)
    {
        voices[slot]?.Dispose();voices[slot]=null;policy.Released(slot);
        cues[slot]?.Dispose();cues[slot]=null;
    }
    private void SetVolume(float volume)
    {
        if(session!=null)session.Volume=volume;
        if(clips!=null)for(int i=0;i<clips.Length;i++)if(clips[i]!=null)clips[i]!.Volume=volume;
        for(int i=0;i<voices.Length;i++)
        {
            if(volume==0){ReleaseVoice(i);continue;}
            var voice=voices[i];if(voice==null)continue;
            // The DSP can finish between reaping and this control operation.
            try {voice.Volume=volume;}
            catch(InvalidOperationException){ReleaseVoice(i);}
        }
        if(session!=null&&volume==0)session.Stop();
    }
    internal void Play(IplUiSound sound)
    {
        int effect=IplUiAudioPolicy.EffectIndex(sound);
        if(effect<0)return;
        if(stopping||State!=IplUiAudioState.Ready||volumeLevel==0||session==null&&(clips==null||clips[effect]==null))
        {DroppedCount++;return;}
        // Inputs received while preparing or muted are discarded, never replayed.
        if(!policy.Accept(sound,IplUiAudioPolicy.IsNavigation(sound)?Clock.Timestamp:0))
        {ThrottledCount++;return;}
        ReapVoices();int slot=policy.NextVoiceSlot();
        if(voices[slot]!=null||cues[slot]!=null){StolenCount++;ReleaseVoice(slot);}
        try
        {
            if(session!=null)
            {
                // SetControl also starts a stopped session. Suppress the idle
                // score before queuing a cue, including after mute/unmute.
                session.SetControl(SystemAudioControl.AmbienceEnabled,0);
                cues[slot]=session.PlayCue((SystemMenuSound)effect);
            }
            else voices[slot]=clips![effect]!.Play();
            policy.Started(slot);PlayedCount++;
        }
        catch(InvalidOperationException error){DroppedCount++;DebugOverlay.WriteLine("UI AUDIO PLAY FAILED "+error.Message);}
    }
    internal Task Close()
    {
        if(closing!=null)return closing;
        stopping=true;cancellation.Cancel();closing=CloseCore();return closing;
    }
    private async Task CloseCore()
    {
        for(int i=0;i<voices.Length;i++)ReleaseVoice(i);
        var pending=loading;loading=null;
        if(pending!=null)
        {
            try {var prepared=await pending;DisposeClips(prepared.Clips);prepared.Session?.Dispose();}
            catch(Exception error){DebugOverlay.WriteLine("UI AUDIO CLOSE "+error.Message);}
        }
        DisposeClips(clips);clips=null;session?.Dispose();session=null;cancellation.Dispose();
        State=IplUiAudioState.Closed;Status="System sounds stopped.";
    }
    private static void DisposeClips(Sound?[]? sounds)
    {
        if(sounds==null)return;
        for(int i=0;i<sounds.Length;i++){sounds[i]?.Dispose();sounds[i]=null;}
    }
}
#endif
