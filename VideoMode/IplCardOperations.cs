internal enum IplCardAction { Copy, Move, Delete, Format }
internal interface IIplCardOperations
{
    string SlotCaption(int slot);
    bool CanCopy(IplBrowserItem item);bool CanMove(IplBrowserItem item);
    int Progress {get;}
    void Execute(IplCardAction action,IplBrowserItem? item);
    void Cancel();
}
internal interface IIplBrowserNotifications { int Version {get;} }

internal interface IIplAnimatedArtwork
{
    int ArtworkFrames(IplBrowserItem item);int ArtworkDuration(IplBrowserItem item,int frame);bool ArtworkBounces(IplBrowserItem item);
    IplArtwork? Artwork(IplBrowserItem item,int frame);
}
