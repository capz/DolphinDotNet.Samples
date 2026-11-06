using System;
using Dolphin.Storage;
using Dolphin.Timing;
internal sealed class IplNativeCards : IIplBrowserSource,IIplCardOperations,IIplBrowserNotifications,IIplAnimatedArtwork
{
#if IPL_MEMORY_CARD_SERVICES
    private readonly MemoryCardManager?[] cards=new MemoryCardManager?[2];
    private readonly int[] counts=new int[2],free=new int[2];
    private readonly bool[] ready=new bool[2],format=new bool[2],needsRefresh=new bool[2];
    private readonly string[] captions={"Slot A: checking...","Slot B: checking..."};
    private MemoryCardTransfer? transfer;private long nextProbe;
#endif
    private int slot;
    public int Version {get;private set;}
    public int Total {get;private set;}
    public bool Busy {get;private set;}
    public int Progress {get;private set;}
    public bool LaunchRequested=>false;
    public string Status {get;private set;}="Insert a memory card.";
    public string SlotCaption(int index){
#if IPL_MEMORY_CARD_SERVICES
        return captions[index];
#else
        return index==0?"Slot A":"Slot B";
#endif
    }
    public void Begin(IplBrowserKind kind,string path,int slot,bool rescan)
    {
        Dispose();this.slot=slot;Version++;
#if IPL_MEMORY_CARD_SERVICES
        Probe(0);Probe(1);UpdateTotal();if(ready[slot])Status="Select a saved file. L / R switches memory-card slots.";else if(!format[slot])Status="Insert a memory card in the selected slot.";nextProbe=Clock.Timestamp+500000;
#else
        throw new InvalidOperationException("Memory-card management needs the source SDK card-services build.");
#endif
    }
#if IPL_MEMORY_CARD_SERVICES
    private void UpdateTotal(){Total=ready[slot]?counts[slot]:format[slot]?1:0;}
    private void Probe(int index)
    {
        string label=index==0?"Slot A":"Slot B";bool before=ready[index],oldFormat=format[index];int oldCount=counts[index],oldFree=free[index];string oldCaption=captions[index];
        try{
            if(cards[index]==null)cards[index]=MemoryCardManager.Open(index==0?StorageDevice.MemoryCardSlotA:StorageDevice.MemoryCardSlotB);
            var card=cards[index]!;if(needsRefresh[index]){card.Refresh();needsRefresh[index]=false;}
            ready[index]=card.IsReady;format[index]=!ready[index];
            if(ready[index]){counts[index]=card.Count;free[index]=card.FreeBlocks;captions[index]=label+": "+free[index].ToString()+" blocks free";}
            else{counts[index]=free[index]=0;captions[index]=label+": needs attention";if(index==slot)Status=card.MountResult==-13?"Different card encoding. Formatting erases every save.":"Card needs formatting. No data is changed automatically.";}
        }catch(Exception){
            ready[index]=format[index]=false;counts[index]=free[index]=0;captions[index]=label+": unavailable";
            cards[index]?.Dispose();cards[index]=null;needsRefresh[index]=false;
        }
        if(before!=ready[index]||oldFormat!=format[index]||oldCount!=counts[index]||oldFree!=free[index]||oldCaption!=captions[index])Version++;
    }
    private void RefreshCards(){for(int i=0;i<2;i++)if(cards[i]!=null)try{cards[i]!.Refresh();}catch(Exception){needsRefresh[i]=true;}Probe(0);Probe(1);UpdateTotal();Version++;}
    private static string Display(string text,string fallback){if(text.Length==0)return fallback;var chars=new char[text.Length];bool ascii=false;for(int i=0;i<chars.Length;i++){bool valid=text[i]>=32&&text[i]<127;chars[i]=valid?text[i]:'?';ascii|=valid&&text[i]!=' ';}return ascii?new string(chars).Trim():fallback;}
#endif
    public void Tick()
    {
#if IPL_MEMORY_CARD_SERVICES
        if(transfer!=null){try{transfer.Step();Progress=transfer.Progress;Status="Transfer: "+Progress.ToString()+"%";if(transfer.Complete){transfer.Dispose();transfer=null;Busy=false;Status="Transfer verified and complete.";RefreshCards();}}
            catch(Exception e){bool completed=transfer?.Complete==true;try{transfer?.Dispose();}catch(Exception){}transfer=null;Busy=false;if(completed)Progress=100;Status=completed?"Transfer completed. Refresh the slots before another action.":e.Message??"Card transfer failed. Check both slots.";RefreshCards();}return;}
        if(Clock.Timestamp>=nextProbe){nextProbe=Clock.Timestamp+500000;Probe(0);Probe(1);UpdateTotal();}
#endif
    }
    public int ReadPage(int start,IplBrowserItem?[] output)
    {
        Array.Clear(output,0,output.Length);
#if IPL_MEMORY_CARD_SERVICES
        if(format[slot]){if(start==0){output[0]=new IplBrowserItem("@format","Format memory card","Erases all saved data",IplItemKind.CardFormat);return 1;}return 0;}
        if(!ready[slot]||cards[slot]==null)return 0;
        var page=cards[slot]!.ReadPage(start,6);for(int i=0;i<page.Length;i++){var save=page[i];string fallback=Display(save.Filename,save.GameCode+" save");string title=Display(save.Title,fallback);var item=new IplBrowserItem(save.Filename,title,save.Blocks.ToString()+" blocks"+(!save.CanCopy||!save.CanMove?" / protected":""),IplItemKind.Save,save.Length,save.GameCode);item.Handle=save;output[i]=item;}return page.Length;
#else
        return 0;
#endif
    }
    public bool CanCopy(IplBrowserItem item){
#if IPL_MEMORY_CARD_SERVICES
        return item.Handle is ManagedCardSave save&&save.CanCopy;
#else
        return false;
#endif
    }
    public bool CanMove(IplBrowserItem item){
#if IPL_MEMORY_CARD_SERVICES
        return item.Handle is ManagedCardSave save&&save.CanMove;
#else
        return false;
#endif
    }
    public int ArtworkFrames(IplBrowserItem item){
#if IPL_MEMORY_CARD_SERVICES
        return item.Handle is ManagedCardSave save?save.IconFrameCount:0;
#else
        return 0;
#endif
    }
    public int ArtworkDuration(IplBrowserItem item,int frame){
#if IPL_MEMORY_CARD_SERVICES
        return item.Handle is ManagedCardSave save?save.IconFrameDuration(frame):4;
#else
        return 4;
#endif
    }
    public bool ArtworkBounces(IplBrowserItem item){
#if IPL_MEMORY_CARD_SERVICES
        return item.Handle is ManagedCardSave save&&save.IconAnimationBounces;
#else
        return false;
#endif
    }
    public IplArtwork? Artwork(IplBrowserItem item)=>Artwork(item,0);
    public IplArtwork? Artwork(IplBrowserItem item,int frame){
#if IPL_MEMORY_CARD_SERVICES
        if(item.Handle is ManagedCardSave save&&save.IconFrameCount>frame&&cards[slot]!=null)return new IplArtwork(32,32,cards[slot]!.ReadIcon(save,frame));
#endif
        return null;
    }
    public string Describe(IplBrowserItem item){
#if IPL_MEMORY_CARD_SERVICES
        if(item.Handle is ManagedCardSave save)return save.Blocks.ToString()+" blocks / "+save.GameCode+" / "+save.CompanyCode+"\n"+Display(save.Comment,"")+"\n"+(save.CanCopy?"Copy allowed":"Copy protected")+" / "+(save.CanMove?"Move allowed":"Move protected");
#endif
        return "";
    }
    public void Open(IplBrowserItem item,VideoModeSettings video){}
    public void Execute(IplCardAction action,IplBrowserItem? item)
    {
#if IPL_MEMORY_CARD_SERVICES
        var card=cards[slot];if(card==null)throw new InvalidOperationException("Reinsert the card and select again.");
        if(action==IplCardAction.Format){if(!format[slot])throw new InvalidOperationException("Card status changed. Select it again.");try{card.Format();Status="Memory card formatted.";}catch(MemoryCardCommittedException e){Status=e.Message??"Operation completed. Refresh the slot before another action.";}RefreshCards();return;}
        if(!(item?.Handle is ManagedCardSave save))throw new InvalidOperationException("Select a saved file again.");
        if(action==IplCardAction.Delete){try{card.Delete(save);Status="Saved file deleted.";}catch(MemoryCardCommittedException e){Status=e.Message??"Operation completed. Refresh the slot before another action.";}RefreshCards();return;}
        Probe(1-slot);var destination=cards[1-slot];if(destination==null||!ready[1-slot])throw new InvalidOperationException("The other slot needs a ready memory card.");
        transfer=card.BeginCopy(save,destination,action==IplCardAction.Move);Busy=true;Progress=0;Status="Copying and verifying the saved file...";
#else
        throw new InvalidOperationException("Card services are unavailable in this build.");
#endif
    }
    public void Cancel(){
#if IPL_MEMORY_CARD_SERVICES
        if(transfer!=null){try{transfer.Cancel();Status="Transfer cancelled. Source kept.";}finally{transfer=null;Busy=false;RefreshCards();}}
#endif
    }
    public void Dispose(){
#if IPL_MEMORY_CARD_SERVICES
        try{transfer?.Dispose();}catch(Exception){}transfer=null;for(int i=0;i<2;i++){cards[i]?.Dispose();cards[i]=null;ready[i]=format[i]=false;counts[i]=free[i]=0;}
#endif
        Busy=false;Total=0;
    }
}
