using System;

internal sealed partial class IplRenderer
{
    private bool flatMaskReady;
    private ShellFace flatMaskFace;
    private static float FlatRowY(int rows,int row)=>rows==4?84+76*row:(rows==2?146:112)+82*row;
    private void FlatRowShape(int rows,int row,IIplBrowserCanvas? mask,bool body)
    {
        const float x=76,width=488,height=62,radius=20;
        float y=FlatRowY(rows,row);var center=new IplPoint(320,y+height/2);IplPoint first=default,previous=default;
        for(int corner=0;corner<4;corner++)for(int j=0;j<=8;j++){
            float cx=corner==0||corner==3?x+radius:x+width-radius,cy=corner<2?y+height-radius:y+radius;
            var direction=panelDirections[corner*9+j];var point=new IplPoint(cx+direction.X*radius,cy+direction.Y*radius);
            if(corner==0&&j==0)first=point;
            else if(body)FlatRowTriangle(center,previous,point,x,y,width,height);else mask!.BrowserMaskTriangle(center,previous,point);
            previous=point;
        }
        if(body)FlatRowTriangle(center,previous,first,x,y,width,height);else mask!.BrowserMaskTriangle(center,previous,first);
        if(body)Line(x+25,y+1.2f,x+width-25,y+1.2f,.8f,0xc5ffff50u);
    }
    private void FlatRowTriangle(IplPoint a,IplPoint b,IplPoint d,float x,float y,float width,float height)
        =>CardTriangle(a,CardLight((a.X-x)/width,(a.Y-y)/height),b,CardLight((b.X-x)/width,(b.Y-y)/height),d,CardLight((d.X-x)/width,(d.Y-y)/height));
    private static int FlatStyle(int rows)=>rows==4?6:rows==3?2:rows==2?3:4;
    private void FlatRows(int rows){for(int i=0;i<rows;i++)FlatRowShape(rows,i,null,true);}
    private void PrepareFlatMenu(IplShell shell)
    {
#if IPL_CACHED_GRID
        if(!gridReady&&c.BeginGridCache()){Grid();c.EndGridCache();gridReady=true;}
#endif
        if(c is IIplBrowserCanvas cards){
            if(!flatMaskReady||flatMaskFace!=shell.Face){cards.BeginBrowserMask();for(int i=0;i<shell.Rows;i++)FlatRowShape(shell.Rows,i,cards,false);cards.EndBrowserMask(shell.Rows*108);flatMaskFace=shell.Face;flatMaskReady=true;}
            if(cards.BeginBrowserCache(FlatStyle(shell.Rows))){FlatRows(shell.Rows);cards.EndBrowserCache();}
        }
#if IPL_CACHED_FOOTER_TEXT
        if(c.BeginFooterTextCache()){Footer(shell);c.EndFooterTextCache();}
#endif
    }
    private void PresentFlatMenu(IplShell shell)
    {
        c.Section(IplRenderPhase.Editor);
#if IPL_CACHED_GRID
        if(!c.DrawGridCache())
#endif
            Grid();
        Center(shell.Title,320,28,30,White);
        if(c is IIplBrowserCanvas cards){c.Section(IplRenderPhase.Capture);c.CaptureBackdrop();c.Section(IplRenderPhase.Frost);cards.DrawBrowserFrost();c.Section(IplRenderPhase.Editor);if(!cards.DrawBrowserCache(FlatStyle(shell.Rows)))FlatRows(shell.Rows);}
        else FlatRows(shell.Rows);
        for(int row=0;row<shell.Rows;row++){
            float focus=shell.Focus(row);focus=focus*focus*(3-2*focus);float size=22*(1+.14f*focus),y=FlatRowY(shell.Rows,row);
            uint color=row==shell.Row?White:0xc4d3e5ffu;
            if(row==shell.Row)Line(102,y+60.5f,538,y+60.5f,1.2f,0x73eed393u);
            Text(shell.Label(row),102,y+16,size,color);string value=shell.Value(row);Text(value,538-TextWidth(value,size),y+16,size,color);
        }
        c.Section(IplRenderPhase.Footer);Footer(shell);c.Section(IplRenderPhase.None);
        if(shell.SystemStatus.Length>0)Center(shell.SystemStatus,320,463,11,0xaabbd0ddu);
    }
}
