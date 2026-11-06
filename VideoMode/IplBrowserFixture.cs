#if IPL_BROWSER_REVIEW
using System;
// Original synthetic UI data, never presented as an indexed physical library.
internal sealed class IplBrowserFixture : IIplBrowserSource
{
    public int Total=>9;public bool Busy=>false;public bool LaunchRequested=>false;
    public string Status=>"UI fixture: synthetic titles/artwork; no games or GC Loader attached.";
    public void Begin(IplBrowserKind kind,string path,int slot,bool rescan){}
    public void Tick(){}
    public int ReadPage(int start,IplBrowserItem?[] items){int n=Math.Min(6,9-start);for(int i=0;i<n;i++)items[i]=new IplBrowserItem("gcloader:/games/Fixture "+(start+i)+".iso",new[]{"Aurora Islands","Paper Comet","Coral Circuit","Moonlit Garden","Glasswing","Orbit Workshop","Violet Harbor","Sunset Rally","Crystal Atlas"}[start+i],"Synthetic UI fixture",IplItemKind.Game,0,"GABE01");return n;}
    public IplArtwork? Artwork(IplBrowserItem item){int seed=item.Title.Length;var pixels=new byte[6144];int at=0;for(int ty=0;ty<32;ty+=4)for(int tx=0;tx<96;tx+=4)for(int y=ty;y<ty+4;y++)for(int x=tx;x<tx+4;x++){int r=(20+(x*2+seed*19)%170)*31/255,g=(35+(y*4+seed*11)%170)*31/255,b=(60+(x+y*2)%180)*31/255;int pixel=0x8000|r<<10|g<<5|b;pixels[at++]=(byte)(pixel>>8);pixels[at++]=(byte)pixel;}return new IplArtwork(96,32,pixels,IplArtworkFormat.Rgb5A3);}

    public void Open(IplBrowserItem item,VideoModeSettings video){throw new InvalidOperationException("Fixture only; no executable is launched.");}
    public string Describe(IplBrowserItem item)=>"Synthetic UI fixture.";
    public void Dispose(){}
}
#endif
