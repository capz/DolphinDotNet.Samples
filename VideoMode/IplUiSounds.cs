// Pure semantic feedback shared by the shell and storage browser. The native
// audio backend consumes at most one cue after a logical input/update cycle.
internal enum IplUiSound
{
    None,TurnSide,TurnCenter,EnterMenu,Back,CancelEdit,ConfirmEdit,
    ChangeValue,ChangeField,ChangeRow,OpenEditor
}

internal interface IIplUiSoundSink
{
    void Emit(IplUiSound cue);
}

internal sealed class IplUiSoundQueue : IIplUiSoundSink
{
    private IplUiSound pending;
    public void Emit(IplUiSound cue)
    {
        // A failed nested operation must not be masked by its opening or
        // confirmation cue. Equal-priority events keep the latest transition.
        if(Priority(cue)>=Priority(pending))pending=cue;
    }
    internal IplUiSound Consume(){var cue=pending;pending=IplUiSound.None;return cue;}
    private static int Priority(IplUiSound cue)=>cue==IplUiSound.None?0:
        cue==IplUiSound.CancelEdit?6:cue==IplUiSound.ConfirmEdit?5:
        cue==IplUiSound.Back?4:cue==IplUiSound.OpenEditor||cue==IplUiSound.EnterMenu?3:
        cue==IplUiSound.TurnSide||cue==IplUiSound.TurnCenter?2:1;
}
