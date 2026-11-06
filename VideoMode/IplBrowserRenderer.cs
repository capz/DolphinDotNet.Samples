using System;
internal sealed partial class IplRenderer
{
    private static readonly string[] cardActionLabels={"Cancel","Copy","Move","Delete"};
    private readonly string?[] browserTitles=new string?[6];
    private readonly string[] browserFirst=new string[6],browserSecond=new string[6];
    private bool browserMaskReady;
    private string browserLocation="",lastLocationPath="";private int lastLocationKind=-1;
    private int counterStart=-1,counterCount=-1,counterTotal=-1;
    private bool counterBusy;
    private string lastLaunchTitle="",lastVideoMode="";
    private string browserCounter="",browserStatus="",lastBrowserStatus="",browserLaunchTitle="",browserVideoMode="";
    private static float CardX(int i)=>40+i%3*192;
    private static float CardY(int i)=>100+i/3*146;
    private void Bubble(float x,float y,float width,float height,float radius,uint color)
    {
        if((color&255)==0)return;
        radius=Math.Min(radius,Math.Min(width,height)/2);
        IplPoint center=new IplPoint(x+width/2,y+height/2),first=default,previous=default;
        for(int corner=0;corner<4;corner++)for(int j=0;j<=8;j++){
            float cx=corner==0||corner==3?x+radius:x+width-radius,yy=corner<2?y+height-radius:y+radius;
            var d=panelDirections[corner*9+j];var point=new IplPoint(cx+d.X*radius,yy+d.Y*radius);
            if(corner==0&&j==0)first=point;else Tri(center,previous,point,color);previous=point;
        }
        Tri(center,previous,first,color);
    }
    private static readonly float[] cardRows={0,2,6,12,24,48,66,84,108,120,126,130,132};
    private static float CardInset(float y){float edge=Math.Min(y,132-y);return edge>=24?0:24-(float)Math.Sqrt(Math.Max(0,576-(24-edge)*(24-edge)));}
    private static uint CardLight(float x,float y)
    {
        float teal=IplLighting.ExpNegative((x-.82f)*(x-.82f)*5+(y-.18f)*(y-.18f)*5);
        float warm=IplLighting.ExpNegative((x-.12f)*(x-.12f)*8+(y-.64f)*(y-.64f)*6);
        float violet=IplLighting.ExpNegative((x-.60f)*(x-.60f)*6+(y-1.06f)*(y-1.06f)*12);
        float gloss=IplLighting.ExpNegative((y-.035f)*(y-.035f)*1100)*(0.28f+0.5f*teal);
        return Rgba((int)(23+36*warm+20*teal+32*violet+115*gloss),(int)(39+25*warm+61*teal+8*violet+115*gloss),(int)(55+14*warm+52*teal+70*violet+118*gloss),(int)(96+28*gloss));
    }
    private void CardTriangle(IplPoint a,uint ca,IplPoint b,uint cb,IplPoint d,uint cd)
    {if(!(c is IIplBrowserCanvas cards)||!cards.BrowserColorTriangle(a,ca,b,cb,d,cd))Tri(a,b,d,cb);}
    private void GlassCard(int index)
    {
        float x=CardX(index),y=CardY(index);
        for(int row=0;row<12;row++){
            float ya=cardRows[row],yb=cardRows[row+1],leftA=CardInset(ya),leftB=CardInset(yb);
            for(int column=0;column<8;column++){
                float a=column/8f,b=(column+1)/8f;
                var p=new IplPoint(x+leftA+(176-2*leftA)*a,y+ya);var q=new IplPoint(x+leftA+(176-2*leftA)*b,y+ya);
                var r=new IplPoint(x+leftB+(176-2*leftB)*a,y+yb);var t=new IplPoint(x+leftB+(176-2*leftB)*b,y+yb);
                uint cp=CardLight((p.X-x)/176,ya/132),cq=CardLight((q.X-x)/176,ya/132),cr=CardLight((r.X-x)/176,yb/132),ct=CardLight((t.X-x)/176,yb/132);
                CardTriangle(p,cp,q,cq,t,ct);CardTriangle(p,cp,t,ct,r,cr);
            }
        }
        for(int i=0;i<12;i++){float xx=26+i*10;float light=IplLighting.ExpNegative((xx-104)*(xx-104)/2800);Line(x+xx,y+.8f,x+xx+10,y+.8f,1,Rgba(220,253,255,(int)(light*151)));}
        Line(x+35,y+131.2f,x+115,y+131.2f,.8f,0x9f8ce02cu);
        Bubble(x+13,y+13,150,54,12,0x03081024);
    }
    private void BrowserMask(IIplBrowserCanvas canvas,int index)
    {
        float x=CardX(index),y=CardY(index);var center=new IplPoint(x+88,y+66);IplPoint first=default,previous=default;
        for(int corner=0;corner<4;corner++)for(int j=0;j<=7;j++){float a=(180-corner*90-j*90f/7)*.017453293f;float xx=corner==0||corner==3?x+24:x+152,yy=corner<2?y+108:y+24;var point=new IplPoint(xx+(float)Math.Cos(a)*24,yy+(float)Math.Sin(a)*24);if(corner==0&&j==0)first=point;else canvas.BrowserMaskTriangle(center,previous,point);previous=point;}canvas.BrowserMaskTriangle(center,previous,first);
    }
    private void BrowserSheen(){for(int i=0;i<6;i++){float x=CardX(i),y=CardY(i);Line(x+30,y+16.7f,x+113,y+16.7f,.8f,0xe5ffff64);Line(x+35,y+18.2f,x+135,y+18.2f,.7f,0xd5fdff18);}}
    private void BrowserCards(){for(int i=0;i<6;i++)GlassCard(i);}
    private string Fit(string text,float maximum,float size)
    {if(TextWidth(text,size)<=maximum)return text;int n=text.Length;while(n>0&&TextWidth(text.Substring(0,n)+"...",size)>maximum)n--;return text.Substring(0,n)+"...";}
    private void PrepareBrowser(IplShell shell)
    {
        var browser=shell.Browser;if(browser==null)return;
        if(lastLocationKind!=(int)browser.Kind||lastLocationPath!=browser.Path){lastLocationKind=(int)browser.Kind;lastLocationPath=browser.Path;browserLocation=browser.Kind==IplBrowserKind.Games?"GC Loader  /  games":Fit("SD / "+browser.Path,590,13);}
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
        if(browser.Kind==IplBrowserKind.MemoryCards){
            for(int slot=0;slot<2;slot++){float x=slot==0?40:328;Bubble(x,63,272,28,11,slot==browser.Slot?0x3d999660u:0x1d334160u);Center((slot==0?"L  ":"R  ")+browser.SlotCaption(slot),x+136,68,11,slot==browser.Slot?White:0x9bbebdccu);}
        }else Center(browserLocation,320,61,13,0x9bbebdccu);
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
        if(browser.Busy&&browser.Panel!=IplBrowserPanel.Progress)Center("Reading storage...",320,385,14,0x9ae6dcffu);
        else Center(browserStatus,320,385,11,0xa7bdc9ddu);
        c.Section(IplRenderPhase.Footer);Footer(shell);c.Section(IplRenderPhase.None);
        Center(browserCounter,320,460,11,0x8eaaaec0u);
    }
    private void DialogLines(string text,float y,int maximum)
    {int line=0,start=0;while(start<text.Length&&line<maximum){int end=text.IndexOf('\n',start);if(end<0)end=text.Length;Text(Fit(text.Substring(start,end-start),476,13),81,y+line*19,13,0xc5d6e4ffu);start=end+1;line++;}}
    private void BrowserDialog(IplShell shell,IplBrowser browser)
    {
        Rect(0,0,640,404,0x000000bbu);Bubble(50,95,540,272,28,0x91c7d759);Bubble(52,97,536,268,26,0x0a1524f8);
        Center(browserLaunchTitle,320,119,24,White);
        if(browser.Panel==IplBrowserPanel.Launch){
            bool game=browser.Selected?.Kind==IplItemKind.Game;
            Center(game?"Open with Swiss":"Open application",320,173,19,0x80e6d5ffu);
            Center(game?browserVideoMode:"This application will replace the glass menu.",320,211,game?17:15,0xb7ccd6ffu);
            Center(game?"The selected game keeps your confirmed video mode.":"Its own video and storage settings apply.",320,244,13,0xa6b8c9ffu);
            Bubble(111,295,184,42,17,browser.Choice==0?0x3c7b7fafu:0x223743b0u);Bubble(345,295,184,42,17,browser.Choice==1?0x3c7b7fafu:0x223743b0u);
            Center("Cancel",203,303,19,White);Center("Open",437,303,19,White);
        }else if(browser.Panel==IplBrowserPanel.CardActions){
            DialogLines(browser.Information,163,3);
            for(int i=0;i<4;i++){float x=75+i*124;bool enabled=i!=1&&i!=2||i==1&&browser.CanCopy||i==2&&browser.CanMove;Bubble(x,290,118,43,15,i==browser.Choice?0x3c7b7fafu:0x223743b0u);Center(cardActionLabels[i],x+59,301,17,enabled?White:0x71818bffu);}
        }else if(browser.Panel==IplBrowserPanel.CardConfirm){
            bool format=browser.CardAction==IplCardAction.Format;
            string question=format?(browser.FormatStage==1?"Format this memory card?":"Erase every saved file on this card?"):browser.CardAction==IplCardAction.Delete?"Permanently delete this saved file?":browser.CardAction==IplCardAction.Move?"Move to the other memory card?":"Copy to the other memory card?";
            Center(question,320,174,19,0xd4e9e6ffu);
            Center(format?"All saves on the selected card will be lost.":browser.CardAction==IplCardAction.Delete?"This cannot be undone.":"The destination copy is verified before completion.",320,218,14,0xb7ccd6ffu);
            if(browser.CardAction==IplCardAction.Move)Center("The source is deleted only after verification.",320,243,13,0xa6b8c9ffu);
            Bubble(111,295,184,42,17,browser.Choice==0?0x3c7b7fafu:0x223743b0u);Bubble(345,295,184,42,17,browser.Choice==1?0x865045b0u:0x223743b0u);
            Center("Cancel",203,303,19,White);Center(format?(browser.FormatStage==1?"Continue":"Erase card"):browser.CardAction==IplCardAction.Delete?"Delete":browser.CardAction==IplCardAction.Move?"Move":"Copy",437,303,19,White);
        }else if(browser.Panel==IplBrowserPanel.Progress){
            Center("Copying and verifying...",320,178,21,0x80e6d5ffu);
            Bubble(110,233,420,12,6,0x213f52ffu);float width=420*browser.TransferProgress/100f;if(width>1)Bubble(110,233,width,12,Math.Min(6,width/2),0x6ce2caffu);
            Center(browser.TransferProgress.ToString()+"%",320,267,18,White);
            Center("Keep both cards inserted. B cancels safely.",320,310,14,0xb7ccd6ffu);
        }else{
            string text=browser.Information;int line=0,start=0;while(start<text.Length&&line<9){int end=text.IndexOf('\n',start);if(end<0)end=text.Length;Text(Fit(text.Substring(start,end-start),476,13),81,171+line*18,13,0xc5d6e4ffu);start=end+1;line++;}
        }
    }
}
