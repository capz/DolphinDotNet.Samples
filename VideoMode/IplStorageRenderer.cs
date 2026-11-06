using System;

internal sealed partial class IplRenderer
{
    private int storageTextRevision=-1,storageTextSelection=-1;
    private readonly string[] storageNames=new string[IplStorageLayout.ListRows],storageGames=new string[IplStorageLayout.ListRows];
    private string storageName="",storageGame="",storageSize="";
    private void StorageSurface(IIplBrowserCanvas? mask,bool body)
    {
        const float x=38,y=93,width=564,height=263,radius=9;
        var center=new IplPoint(x+width/2,y+height/2);IplPoint first=default,previous=default;
        for(int corner=0;corner<4;corner++)for(int j=0;j<=8;j++){
            float cx=corner==0||corner==3?x+radius:x+width-radius,cy=corner<2?y+height-radius:y+radius;
            var direction=panelDirections[corner*9+j];var point=new IplPoint(cx+direction.X*radius,cy+direction.Y*radius);
            if(corner==0&&j==0)first=point;
            else if(body)FlatRowTriangle(center,previous,point,x,y,width,height);else mask!.BrowserMaskTriangle(center,previous,point);
            previous=point;
        }
        if(body)FlatRowTriangle(center,previous,first,x,y,width,height);else mask!.BrowserMaskTriangle(center,previous,first);
    }
    private void PrepareStorageBrowser(IplShell shell,IplBrowser browser)
    {
#if IPL_CACHED_GRID
        if(!gridReady&&c.BeginGridCache()){Grid();c.EndGridCache();gridReady=true;}
#endif
        if(c is IIplBrowserCanvas cards){
            if(!browserMaskReady){cards.BeginBrowserMask();StorageSurface(cards,false);cards.EndBrowserMask(108);browserMaskReady=true;}
            if(cards.BeginBrowserCache(5)){StorageSurface(null,true);cards.EndBrowserCache();}
        }
        int kind=(int)browser.Kind+(browser.ListView?3:0);bool parent=browser.Kind==IplBrowserKind.Sd&&browser.Path.Length>0;
        if(counterStart!=browser.PageStart||counterCount!=browser.Count||counterTotal!=browser.Total||counterBusy!=browser.Busy||counterParent!=parent||counterKind!=kind){
            counterBusy=browser.Busy;counterParent=parent;counterKind=kind;counterStart=browser.PageStart;counterCount=browser.Count;counterTotal=browser.Total;
            browserCounter=(parent?"L Parent  |  ":"")+"Y "+(browser.ListView?"Grid":"List")+"  |  C Up Refresh  |  "+(browser.Count==0?(browser.Busy?"Reading...":"No entries"):(browser.PageStart+1).ToString()+"-"+(browser.PageStart+browser.Count).ToString()+" / "+browser.Total.ToString());
        }
        if(lastBrowserStatus!=browser.Status){lastBrowserStatus=browser.Status;browserStatus=Fit(lastBrowserStatus,590,10);}
        if(lastLocationKind!=(int)browser.Kind||lastLocationPath!=browser.Path){lastLocationKind=(int)browser.Kind;lastLocationPath=browser.Path;browserLocation=Fit("SD / "+browser.Path,590,13);}
        if(storageTextRevision!=browser.Revision||storageTextSelection!=browser.Selection){
            var selected=browser.Selected;storageName=Fit(selected?.Title??"",550,16);
            storageGame=Fit(selected==null?"":"Game: "+selected.GameTitle+" ["+selected.GameId+"]",390,13);
            storageSize=selected?.Detail??"";lastLaunchTitle=selected?.Title??"";browserLaunchTitle=Fit(lastLaunchTitle,480,24);
            if(browser.ListView&&storageTextRevision!=browser.Revision)for(int i=0;i<browser.Count;i++){
                var item=browser.Items[i]!;storageNames[i]=Fit(item.Title,browser.Kind==IplBrowserKind.MemoryCards?207:395,13.8f);storageGames[i]=Fit(item.GameTitle,166,12);
            }
            storageTextRevision=browser.Revision;storageTextSelection=browser.Selection;
        }
        string mode=IplSwiss.VideoMode(shell.Video.Applied);if(mode!=lastVideoMode){lastVideoMode=mode;browserVideoMode="Video mode: "+mode;}
#if IPL_CACHED_FOOTER_TEXT
        if(c.BeginFooterTextCache()){Footer(shell);c.EndFooterTextCache();}
#endif
    }
    private void PresentStorageBrowser(IplShell shell,IplBrowser browser)
    {
        c.Section(IplRenderPhase.Editor);
#if IPL_CACHED_GRID
        if(!c.DrawGridCache())
#endif
            Grid();
        Center(browser.Title,320,24,29,White);
        bool memory=browser.Kind==IplBrowserKind.MemoryCards;
        if(memory){for(int slot=0;slot<2;slot++){float x=slot==0?40:328;Bubble(x,63,272,28,11,slot==browser.Slot?0x3d999660u:0x1d334160u);Center((slot==0?"L  ":"R  ")+browser.SlotCaption(slot),x+136,68,11,slot==browser.Slot?White:0x9bbebdccu);}}
        else Center(browserLocation,320,61,13,0x9bbebdccu);
        if(c is IIplBrowserCanvas cards){
            c.Section(IplRenderPhase.Capture);c.CaptureBackdrop();c.Section(IplRenderPhase.Frost);cards.DrawBrowserFrost();c.Section(IplRenderPhase.Editor);
            if(!cards.DrawBrowserCache(5))StorageSurface(null,true);
        }else StorageSurface(null,true);
        if(browser.Selected!=null){float x=IplStorageLayout.X(browser.Selection,browser.ListView),y=IplStorageLayout.Y(browser.Selection,browser.ListView),size=IplStorageLayout.IconSize(browser.ListView);
            Bubble(browser.ListView?40:x-1,y-1,browser.ListView?560:size+2,size+2,browser.ListView?7:6,0x54dfc657u);
        }
        if(c is IIplBrowserCanvas art)art.DrawArtworks();
        if(browser.ListView){
            Text(memory?"Save":"Filename",78,96,11,0x8ebbbcccu);if(memory)Text("Game",297,96,11,0x8ebbbcccu);Text("Size",565,96,11,0x8ebbbcccu);
            for(int i=0;i<browser.Count;i++){
                var item=browser.Items[i]!;float y=IplStorageLayout.Y(i,true),focus=browser.Focus(i);focus=focus*focus*(3-2*focus);
                uint color=i==browser.Selection?White:0xbfcfd9ffu;Text(storageNames[i]??"",78,y+5,12.2f*(1+.13f*focus),color);
                if(memory)Text(storageGames[i]??"",297,y+6,12,0x9bc5cdffu);
                Text(item.Detail,593-TextWidth(item.Detail,11),y+6,11,color);
            }
        }
        if(browser.Selected!=null){
            Text(storageName,44,362,16,White);
            if(memory&&browser.Selected.Kind==IplItemKind.Save)Text(storageGame,44,383,13,0x9bc5cdffu);
            Text(storageSize,593-TextWidth(storageSize,13),383,13,0xb4ded8ffu);
        }else Center(browser.Busy?"Reading storage...":"No entries",320,217,18,0xaacbd3ffu);
        if(browser.Panel!=IplBrowserPanel.Grid)BrowserDialog(shell,browser);
        Center(browserStatus,320,400,10,0xa7bdc9ddu);
        c.Section(IplRenderPhase.Footer);Footer(shell);c.Section(IplRenderPhase.None);
        Center(browserCounter,320,460,11,0x8eaaaec0u);
    }
}
