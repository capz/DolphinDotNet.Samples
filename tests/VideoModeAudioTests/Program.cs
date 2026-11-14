using System;
using System.Threading.Tasks;

internal static class Program
{
    private static void Check(bool pass,string why)
    {if(!pass)throw new Exception("UI audio lifecycle: "+why);}
    private static async Task WaitForPreparation(IplUiAudio audio)
    {
        for(int i=0;i<2000&&audio.State==IplUiAudioState.Loading;i++)
        {audio.Tick(2);await Task.Delay(1);}
        Check(audio.State!=IplUiAudioState.Loading,"background preparation must complete");
    }
    private static async Task ResidentPlayback()
    {
        AudioFixture.Reset();var audio=new IplUiAudio();
        audio.Tick(0);audio.Tick(0);
        Check(AudioFixture.Initialized==0&&AudioFixture.Read==0,"saved Off skips SDK initialization and ROM reads");
        audio.Play(IplUiSound.EnterMenu);audio.Tick(2);await WaitForPreparation(audio);
        Check(audio.State==IplUiAudioState.Ready&&audio.LoadedCount==10&&AudioFixture.Created==10,"supported clips load once");
        Check(AudioFixture.ImagesDisposed==1&&AudioFixture.CatalogsDisposed==1&&AudioFixture.Disposed==0,
            "resident clips outlive the temporary image and catalog");
        Check(audio.PlayedCount==0,"inputs before readiness are discarded");
        for(int i=0;i<8;i++)audio.Play(IplUiSound.ConfirmEdit);
        Check(audio.PlayedCount==8&&audio.StolenCount==2,"overlap is bounded to six voices");
        Check(!AudioFixture.Voices[0].IsPlaying&&!AudioFixture.Voices[1].IsPlaying,"oldest voices are stolen first");
        for(int i=2;i<8;i++)Check(AudioFixture.Voices[i].IsPlaying,"newer effect tails remain active");
        audio.Tick(1);
        foreach(var voice in AudioFixture.Voices)if(voice.IsPlaying)Check(voice.Volume==.25f,"volume updates active tails");
        AudioFixture.Voices[7].FinishOnVolumeWrite=true;audio.Tick(2);
        Check(!AudioFixture.Voices[7].IsPlaying,"DSP completion during a volume update is harmless");
        audio.Tick(0);
        foreach(var voice in AudioFixture.Voices)Check(!voice.IsPlaying,"Off immediately stops every tail");
        audio.Tick(2);Check(AudioFixture.Created==10&&AudioFixture.Read==1,"unmuting reuses resident clips");
        Dolphin.Timing.Clock.Timestamp=1000;audio.Play(IplUiSound.ChangeRow);audio.Play(IplUiSound.ChangeValue);
        Check(audio.PlayedCount==9&&audio.ThrottledCount==1,"rapid movement uses the monotonic rate limiter");
        Dolphin.Timing.Clock.Timestamp=41000;audio.Play(IplUiSound.ChangeField);
        Check(audio.PlayedCount==10,"movement resumes at forty milliseconds");
        var close=audio.Close();Check(ReferenceEquals(close,audio.Close()),"Close returns the same completion task");await close;
        Check(AudioFixture.Disposed==10&&audio.State==IplUiAudioState.Closed,"shutdown releases all clips");
        int lastVoice=AudioFixture.DisposalOrder.LastIndexOf("voice");
        int firstClip=AudioFixture.DisposalOrder.FindIndex(s=>s.StartsWith("clip"));
        Check(lastVoice<firstClip,"voices are stopped before their clips are disposed");
    }
    private static async Task PreparationFailures()
    {
        AudioFixture.Reset();AudioFixture.FailAt=3;var audio=new IplUiAudio();
        audio.Tick(2);await WaitForPreparation(audio);
        Check(audio.State==IplUiAudioState.Unavailable&&AudioFixture.Created==3&&AudioFixture.Disposed==3,
            "partial preparation releases every completed clip");
        audio.Tick(0);audio.Tick(2);Check(AudioFixture.Read==1,"failed preparation is not retried each frame");
        await audio.Close();

        AudioFixture.Reset();AudioFixture.Unsupported=true;audio=new();audio.Tick(2);await WaitForPreparation(audio);
        Check(audio.State==IplUiAudioState.Unavailable&&AudioFixture.Created==0&&
            AudioFixture.ImagesDisposed==1&&AudioFixture.CatalogsDisposed==1,"unknown profiles stay silent and release temporary resources");
        await audio.Close();
    }
    private static async Task CancelPreparation()
    {
        AudioFixture.Reset();AudioFixture.Continue.Reset();var audio=new IplUiAudio();audio.Tick(2);
        Check(AudioFixture.Entered.Wait(2000),"background preparation reached its cancellation point");
        audio.Play(IplUiSound.EnterMenu);await audio.Close();
        Check(audio.State==IplUiAudioState.Closed&&AudioFixture.Created==0,
            "closing cancels blocked preparation and waits for cleanup");
    }
    private static async Task Main()
    {
        await ResidentPlayback();await PreparationFailures();await CancelPreparation();
        Console.WriteLine("UI audio lifecycle: saved Off, one-time loading, native ownership, bounded voices, live volume/mute, partial failures, unsupported profiles and cancellation passed.");
    }
}
