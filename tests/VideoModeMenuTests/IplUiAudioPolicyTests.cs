using System;

internal static class IplUiAudioPolicyTests
{
    private static void Check(bool value,string message)
    {if(!value)throw new Exception("UI audio policy: "+message);}
    internal static void Run()
    {
        Check(IplUiAudioPolicy.VolumeForLevel(0)==0&&IplUiAudioPolicy.VolumeForLevel(1)==.25f&&
            IplUiAudioPolicy.VolumeForLevel(2)==.5f&&IplUiAudioPolicy.VolumeForLevel(3)==1,"saved volume levels");
        var policy=new IplUiAudioPolicy();
        Check(!policy.Accept(IplUiSound.None,0)&&!policy.Accept((IplUiSound)99,0),"unknown actions stay silent");
        Check(policy.Accept(IplUiSound.ChangeRow,1000),"first movement plays immediately");
        Check(!policy.Accept(IplUiSound.ChangeValue,40999),"movement types share a 40 ms limit");
        Check(policy.Accept(IplUiSound.ConfirmEdit,2000)&&policy.Accept(IplUiSound.Back,3000)&&
            policy.Accept(IplUiSound.CancelEdit,4000),"confirmation and back bypass movement throttling");
        Check(policy.Accept(IplUiSound.ChangeField,41000),"movement accepted at the exact boundary");
        Check(!policy.Accept(IplUiSound.TurnSide,41000),"simultaneous movement is suppressed");
        Check(policy.Accept(IplUiSound.TurnCenter,81000),"cube return uses the same bounded movement policy");
        Check(policy.Accept(IplUiSound.ChangeRow,1),"a reset test clock does not mute navigation permanently");
        for(int i=0;i<IplUiAudioPolicy.VoiceLimit;i++)
        {Check(policy.NextVoiceSlot()==i,"available slots selected before stealing");policy.Started(i);}
        Check(policy.NextVoiceSlot()==0,"oldest voice selected when full");policy.Started(0);
        Check(policy.NextVoiceSlot()==1,"newest voice tail retained");
        policy.Released(4);Check(policy.NextVoiceSlot()==4,"completed voice reused before stealing");policy.Started(4);
        Check(policy.NextVoiceSlot()==1,"freed slot reuse preserves oldest ordering");
        for(int i=0;i<IplUiAudioPolicy.VoiceLimit;i++)policy.Released(i);
        Check(policy.NextVoiceSlot()==0,"shutdown releases the full bounded pool");
        var expected=new[]{IplUiSound.TurnSide,IplUiSound.TurnCenter,IplUiSound.EnterMenu,IplUiSound.Back,
            IplUiSound.CancelEdit,IplUiSound.ConfirmEdit,IplUiSound.ChangeValue,IplUiSound.ChangeField,
            IplUiSound.ChangeRow,IplUiSound.OpenEditor};
        for(int i=0;i<expected.Length;i++)Check(IplUiAudioPolicy.EffectIndex(expected[i])==i,"stable semantic mapping");
        Console.WriteLine("UI audio policy: semantic mapping, volume, movement rate limit and bounded oldest-voice replacement passed.");
    }
}
