using System;
internal sealed partial class IplRenderer
{
    private readonly string?[] browserTitles=new string?[6];
    private readonly string[] browserFirst=new string[6],browserSecond=new string[6];
    private bool browserMaskReady;
    private int counterStart=-1,counterCount=-1,counterTotal=-1;
    private bool counterBusy;
    private string lastLaunchTitle="",lastVideoMode="";
    private string browserCounter="",browserStatus="",lastBrowserStatus="",browserLaunchTitle="",browserVideoMode="";
    private static float CardX(int i)=>40+i%3*192;
    private static float CardY(int i)=>100+i/3*146;
    private void Bubble(float x,float y,float width,float height,float radius,uint color)
    {
        if((color&255)==0)return;
        IplPoint center=new IplPoint(x+width/2,y+height/2),first=default,previous=default;
        for(int corner=0;corner<4;corner++)for(int j=0;j<=8;j++){
            float cx=corner==0||corner==3?x+radius:x+width-radius,yy=corner<2?y+height-radius:y+radius;
            var d=panelDirections[corner*9+j];var point=new IplPoint(cx+d.X*radius,yy+d.Y*radius);
            if(corner==0&&j==0)first=point;else Tri(center,previous,point,color);previous=point;
        }
        Tri(center,previous,first,color);
    }
    private void GlassCard(int index)
    {
        float x=CardX(index),y=CardY(index);
        Bubble(x,y,176,132,24,0x9ddce333);
        Bubble(x+1.4f,y+1.4f,173.2f,129.2f,22.6f,0x1f354d80);
        Bubble(x+3,y+64,98,64,20,0x8d632218);
        Bubble(x+77,y+4,94,72,20,0x49e5c71b);
        Bubble(x+35,y+108,107,21,10,0x8368e421);
        Bubble(x+2,y+2,172,61,22,0xa0e1ec20);
        Bubble(x+4,y+3,168,24,18,0xe8ffff14);
        Line(x+27,y+1.5f,x+117,y+1.5f,1.2f,0xeaffffb0);
        Line(x+117,y+1.5f,x+145,y+1.5f,.8f,0xd2f9ff65);
        Line(x+1.6f,y+32,x+1.6f,y+73,1.1f,0x79ddc52e);
        Line(x+31,y+130.8f,x+125,y+130.8f,.9f,0x8b7cde36);
        Bubble(x+12,y+12,152,56,12,0x03080e68);
    }
    private void BrowserMask(IIplBrowserCanvas canvas,int index)
    {
        float x=CardX(index),y=CardY(index);var center=new IplPoint(x+88,y+66);IplPoint first=default,previous=default;
        for(int corner=0;corner<4;corner++)for(int j=0;j<=7;j++){float a=(180-corner*90-j*90f/7)*.017453293f;float xx=corner==0||corner==3?x+24:x+152,yy=corner<2?y+108:y+24;var point=new IplPoint(xx+(float)Math.Cos(a)*24,yy+(float)Math.Sin(a)*24);if(corner==0&&j==0)first=point;else canvas.BrowserMaskTriangle(center,previous,point);previous=point;}canvas.BrowserMaskTriangle(center,previous,first);
    }
    private void BrowserSheen(){for(int i=0;i<6;i++){float x=CardX(i),y=CardY(i);Bubble(x+17,y+16,142,12,6,0xe8ffff16);Line(x+27,y+16.7f,x+113,y+16.7f,.9f,0xe5ffff70);Line(x+33,y+18.2f,x+135,y+18.2f,.7f,0xd5fdff20);}}
    private void BrowserCards(){for(int i=0;i<6;i++)GlassCard(i);}
    private string Fit(string text,float maximum,float size)
    {if(TextWidth(text,size)<=maximum)return text;int n=text.Length;while(n>0&&TextWidth(text.Substring(0,n)+"...",size)>maximum)n--;return text.Substring(0,n)+"...";}
    private void PrepareBrowser(IplShell shell)
    {
        var browser=shell.Browser;if(browser==null)return;
#if IPL_CACHED_GRID
        if(!gridReady&&c.BeginGridCache()){Grid();c.EndGridCache();gridReady=true;}
#endif
        if(c is IIplBrowserCanvas cards){
            if(!browserMaskReady){cards.BeginBrowserMask();for(int i=0;i<6;i++)BrowserMask(cards,i);cards.EndBrowserMask();browserMaskReady=true;}
            if(cards.BeginBrowserCache(0)){BrowserCards();cards.EndBrowserCache();}
            if(cards.BeginBrowserCache(1)){BrowserSheen();cards.EndBrowserCache();}
        }
        if(counterStart!=browser.PageStart||counterCount!=browser.Count||counterTotal!=browser.Total||counterBusy!=browser.Busy){counterBusy=browser.Busy;counterStart=browser.PageStart;counterCount=browser.Count;counterTotal=browser.Total;browserCounter="Y  Rescan  |  "+(browser.Count==0?(browser.Busy?"Reading...":"No entries"):(browser.PageStart+1).ToString()+"-"+(browser.PageStart+browser.Count).ToString()+" / "+browser.Total.ToString());}
        if(lastBrowserStatus!=browser.Status){lastBrowserStatus=browser.Status;browserStatus=Fit(lastBrowserStatus,590,11);}
        if(browser.Selected!=null&&lastLaunchTitle!=browser.Selected.Title){lastLaunchTitle=browser.Selected.Title;browserLaunchTitle=Fit(lastLaunchTitle,480,24);}
        string mode=IplSwiss.VideoMode(shell.Video.Applied);if(mode!=lastVideoMode){lastVideoMode=mode;browserVideoMode="Video mode: "+mode;}
        for(int i=0;i<6;i++){
            string title=browser.Items[i]?.Title??"";if(title==browserTitles[i])continue;browserTitles[i]=title;
            if(TextWidth(title,14)<=146){browserFirst[i]=title;browserSecond[i]="";continue;}
            int at=title.Length;while(at>0&&TextWidth(title.Substring(0,at),14)>146)at--;
            int space=at-1;while(space>0&&title[space]!=' ')space--;if(space>0)at=space;
            browserFirst[i]=title.Substring(0,at).Trim();browserSecond[i]=Fit(title.Substring(at).Trim(),146,14);
        }
#if IPL_CACHED_FOOTER_TEXT
        if(c.BeginFooterTextCache()){Footer(shell);c.EndFooterTextCache();}
#endif
    }
    private void PresentBrowser(IplShell shell)
    {
        var browser=shell.Browser;if(browser==null)return;
        c.Section(IplRenderPhase.Editor);
#if IPL_CACHED_GRID
        if(!c.DrawGridCache())
#endif
            Grid();
        Center(browser.Title,320,24,29,White);
        Center(browser.Kind==IplBrowserKind.Games?"GC Loader  /  games":"GC Loader SD",320,61,13,0x9bbebdccu);
        if(c is IIplBrowserCanvas frost){c.Section(IplRenderPhase.Capture);c.CaptureBackdrop();c.Section(IplRenderPhase.Frost);frost.DrawBrowserFrost();c.Section(IplRenderPhase.Editor);}
        if(!(c is IIplBrowserCanvas cards)||!cards.DrawBrowserCache(0))BrowserCards();
        if(c is IIplBrowserCanvas artPass){artPass.DrawArtworks();if(!artPass.DrawBrowserCache(1))BrowserSheen();}
        for(int i=0;i<6;i++){
            var item=browser.Items[i];if(item==null)continue;float x=CardX(i),y=CardY(i),focus=browser.Focus(i);focus=focus*focus*(3-2*focus);
            if(i==browser.Selection){Bubble(x-2,y-2,180,136,26,0x37cdb116);Bubble(x+39,y+128,98,2,1,0x60f8d4b0);}
            bool art=c is IIplBrowserCanvas images&&images.HasArtwork(i);
            if(!art){uint tint=item.Kind==IplItemKind.Directory?0x4bc2bb4du:0x7296b54du;for(int yy=0;yy<3;yy++)for(int xx=0;xx<5;xx++)if(yy!=1||xx!=2)Bubble(x+57+xx*12,y+23+yy*10,9,8,2,tint);}
            float size=13.3f+.7f*focus;uint color=i==browser.Selection?White:0xbccbd8eeu;
            Center(browserFirst[i]??"",x+88,y+77,size,color);if(browserSecond[i]?.Length>0)Center(browserSecond[i],x+88,y+95,size,color);
            Center(item.Detail,x+88,y+113,10,0x8fb3c7ccu);
        }
        if(browser.Panel!=IplBrowserPanel.Grid)BrowserDialog(shell,browser);
        if(browser.Busy)Center("Reading storage...",320,385,14,0x9ae6dcffu);
        else Center(browserStatus,320,385,11,0xa7bdc9ddu);
        c.Section(IplRenderPhase.Footer);Footer(shell);c.Section(IplRenderPhase.None);
        Center(browserCounter,320,460,11,0x8eaaaec0u);
    }
    private void BrowserDialog(IplShell shell,IplBrowser browser)
    {
        Rect(0,0,640,404,0x000000bbu);Bubble(50,95,540,272,28,0x91c7d759);Bubble(52,97,536,268,26,0x0a1524f8);
        Center(browserLaunchTitle,320,119,24,White);
        if(browser.Panel==IplBrowserPanel.Launch){
            Center("Open with Swiss",320,173,19,0x80e6d5ffu);
            Center(browserVideoMode,320,211,17,0xb7ccd6ffu);
            Center("The selected game keeps your confirmed video mode.",320,244,13,0xa6b8c9ffu);
            Bubble(111,295,184,42,17,browser.Choice==0?0x3c7b7fafu:0x223743b0u);Bubble(345,295,184,42,17,browser.Choice==1?0x3c7b7fafu:0x223743b0u);
            Center("Cancel",203,303,19,White);Center("Open",437,303,19,White);
        }else{
            string text=browser.Information;int line=0,start=0;while(start<text.Length&&line<9){int end=text.IndexOf('\n',start);if(end<0)end=text.Length;Text(Fit(text.Substring(start,end-start),476,13),81,171+line*18,13,0xc5d6e4ffu);start=end+1;line++;}
        }
    }
}
