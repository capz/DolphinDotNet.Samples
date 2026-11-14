using System;

// Bounded playback policy shared with host tests; no SDK resource or clock calls.
internal sealed class IplUiAudioPolicy
{
    internal const int VoiceLimit=6;
    internal const long NavigationIntervalMicroseconds=40000;
    private readonly long[] voiceOrder=new long[VoiceLimit];
    private long serial,lastNavigation;
    private bool navigationPlayed;

    internal static float VolumeForLevel(int level)
    {
        if(level<0||level>3)throw new ArgumentOutOfRangeException(nameof(level));
        return level==0?0:level==1?.25f:level==2?.5f:1;
    }
    internal static int EffectIndex(IplUiSound sound)
    {
        return sound switch
        {
            IplUiSound.TurnSide=>0,IplUiSound.TurnCenter=>1,
            IplUiSound.EnterMenu=>2,IplUiSound.Back=>3,
            IplUiSound.CancelEdit=>4,IplUiSound.ConfirmEdit=>5,
            IplUiSound.ChangeValue=>6,IplUiSound.ChangeField=>7,
            IplUiSound.ChangeRow=>8,IplUiSound.OpenEditor=>9,_=>-1
        };
    }
    internal static bool IsNavigation(IplUiSound sound)
        =>sound==IplUiSound.TurnSide||sound==IplUiSound.TurnCenter||
          sound==IplUiSound.ChangeValue||sound==IplUiSound.ChangeField||sound==IplUiSound.ChangeRow;
    internal bool Accept(IplUiSound sound,long microseconds)
    {
        if(EffectIndex(sound)<0)return false;
        if(!IsNavigation(sound))return true;
        if(navigationPlayed&&microseconds>=lastNavigation&&
           microseconds-lastNavigation<NavigationIntervalMicroseconds)return false;
        navigationPlayed=true;lastNavigation=microseconds;return true;
    }
    internal int NextVoiceSlot()
    {
        int oldest=0;
        for(int i=0;i<VoiceLimit;i++)
        {
            if(voiceOrder[i]==0)return i;
            if(voiceOrder[i]<voiceOrder[oldest])oldest=i;
        }
        return oldest;
    }
    internal void Started(int slot){voiceOrder[slot]=++serial;}
    internal void Released(int slot){voiceOrder[slot]=0;}
}
