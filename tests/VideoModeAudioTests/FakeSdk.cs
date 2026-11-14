using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

// Resource/control fakes for the SDK surface used by IplUiAudio. These verify
// application ownership and scheduling, not firmware decoding, sound fidelity,
// DSP behavior or physical performance. The managed sample build checks the
// same calls against the actual published SDK.
internal static class AudioFixture
{
    internal static int Initialized,Read,ImagesDisposed,CatalogsDisposed,Created,Disposed,FailAt=-1,RequiredPreparationBytes;
    internal static bool Unsupported;
    internal static ManualResetEventSlim Entered=new(false),Continue=new(true);
    internal static List<Dolphin.Audio.SoundChannel> Voices=new();
    internal static List<string> DisposalOrder=new();
    internal static void Reset()
    {
        Initialized=Read=ImagesDisposed=CatalogsDisposed=Created=Disposed=RequiredPreparationBytes=0;FailAt=-1;Unsupported=false;
        Entered.Dispose();Continue.Dispose();Entered=new(false);Continue=new(true);
        Voices=new();DisposalOrder=new();Dolphin.Timing.Clock.Timestamp=0;
    }
}

namespace Dolphin.Audio
{
    public static class Audio
    {
        public static void Initialize(){AudioFixture.Initialized++;}
    }
    public sealed class Sound : IDisposable
    {
        private bool disposed;
        private readonly int id;
        public int NativeBytes=>120000;
        public float Volume {get;set;}=1;
        internal Sound(int id){this.id=id;AudioFixture.Created++;}
        public SoundChannel Play(bool loop=false)
        {
            if(disposed)throw new ObjectDisposedException(nameof(Sound));
            var voice=new SoundChannel {Volume=Volume};AudioFixture.Voices.Add(voice);return voice;
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;AudioFixture.Disposed++;AudioFixture.DisposalOrder.Add("clip"+id);
        }
    }
    public sealed class SoundChannel : IDisposable
    {
        private float volume=1;
        public bool IsPlaying {get;private set;}=true;
        internal bool FinishOnVolumeWrite;
        public float Volume
        {
            get=>volume;
            set
            {
                if(FinishOnVolumeWrite){IsPlaying=false;throw new InvalidOperationException("Playback has finished.");}
                volume=value;
            }
        }
        public void Dispose()
        {
            if(!IsPlaying)return;
            IsPlaying=false;AudioFixture.DisposalOrder.Add("voice");
        }
    }
}

namespace Dolphin.Diagnostics
{
    public static class DebugOverlay {public static void WriteLine(string text){}}
}
namespace Dolphin.Timing
{
    public static class Clock {public static long Timestamp {get;set;}}
}
namespace Dolphin.System
{
    [Flags]
    public enum FirmwareCatalogOptions {None=0,IncludeMenuAudio=1}
    public enum SystemMenuSound
    {TurnSide,TurnCenter,Select,Back,OptionCancel,OptionConfirm,OptionMove1,OptionMove2,OptionMove3,OptionSelect,PressStart}
    public sealed class SystemMenuSoundInfo {}
    public sealed class FirmwareImage : IDisposable
    {
        public FirmwareCatalog CreateCatalog(FirmwareCatalogOptions options,CancellationToken token)=>new();
        public void Dispose(){AudioFixture.ImagesDisposed++;}
    }
    public sealed class FirmwareCatalog : IDisposable
    {
        public bool TryGetMenuSoundInfo(SystemMenuSound id,out SystemMenuSoundInfo? info)
        {info=AudioFixture.Unsupported?null:new();return info!=null;}
        public void Dispose(){AudioFixture.CatalogsDisposed++;}
    }
    public static class SystemFirmware
    {
        public static FirmwareImage ReadConsole(CancellationToken token=default)
        {
            AudioFixture.Read++;AudioFixture.Entered.Set();AudioFixture.Continue.Wait(token);
            token.ThrowIfCancellationRequested();return new();
        }
    }
    public static class SystemResources
    {
        public static Dolphin.Audio.Sound CreateMenuSound(FirmwareCatalog catalog,SystemMenuSound id,
            CancellationToken token=default,int nativeBudgetBytes=8*1024*1024)
        {
            token.ThrowIfCancellationRequested();
            if(nativeBudgetBytes<AudioFixture.RequiredPreparationBytes)throw new OutOfMemoryException("Effect scratch budget is too small.");
            if((int)id==AudioFixture.FailAt)throw new IOException("Partial preparation failure.");
            return new((int)id);
        }
    }
}
