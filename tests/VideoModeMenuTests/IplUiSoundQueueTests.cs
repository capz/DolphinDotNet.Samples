using System;

internal static class IplUiSoundQueueTests
{
    private static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    internal static void Run()
    {
        var sounds=new IplUiSoundQueue();
        Check(sounds.Consume()==IplUiSound.None,"No sound without an accepted action");
        sounds.Emit(IplUiSound.EnterMenu);sounds.Emit(IplUiSound.ChangeRow);
        Check(sounds.Consume()==IplUiSound.EnterMenu,"Nested initial selection does not duplicate an opening cue");
        Check(sounds.Consume()==IplUiSound.None,"A semantic cue is consumed exactly once");
        sounds.Emit(IplUiSound.ConfirmEdit);sounds.Emit(IplUiSound.CancelEdit);sounds.Emit(IplUiSound.EnterMenu);
        Check(sounds.Consume()==IplUiSound.CancelEdit,"Failure overrides confirmation and nested opening");
        sounds.Emit(IplUiSound.ChangeValue);sounds.Emit(IplUiSound.ChangeField);
        Check(sounds.Consume()==IplUiSound.ChangeField,"The final accepted navigation wins within a cycle");
        Console.WriteLine("UI sound queue: one-shot dispatch, nested-action deduplication and failure priority passed.");
    }
}
