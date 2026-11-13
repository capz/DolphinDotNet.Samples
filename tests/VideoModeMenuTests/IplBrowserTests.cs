using System;
internal static class IplBrowserTests
{
    private sealed class Source : IIplBrowserSource
    {
        internal int Opened,Reads,Wait;internal VideoModeSettings Video;
        public int Total=>9;public bool Busy=>Wait>0;public bool LaunchRequested {get;private set;}
        public string Status=>"Fixture source";
        public void Begin(IplBrowserKind kind,string path,int slot,bool refresh){}
        public void Tick(){if(Wait>0)Wait--;}
        public int ReadPage(int start,IplBrowserItem?[] output){Reads++;int count=Math.Min(6,9-start);for(int i=0;i<count;i++)output[i]=new IplBrowserItem("gcloader:/games/"+(start+i)+".iso","Fixture "+(start+i),"Fixture",IplItemKind.Game,0,"GABE01");return count;}
        public void Open(IplBrowserItem item,VideoModeSettings video){Opened++;Video=video;LaunchRequested=true;}
        public IplArtwork? Artwork(IplBrowserItem item)=>null;
        public string Describe(IplBrowserItem item)=>item.Path;
        public void Dispose(){Wait=0;}
    }
    private static void Check(bool b,string text){if(!b)throw new Exception(text);}
    private static void Move(IplShell s,int x,int y,bool stick=false){s.Direction(0,0,0);s.Direction(x,y,.016f,stick);}
    internal static void Run()
    {
        var source=new Source();using var browser=new IplBrowser(source);var shell=new IplShell(new FakeOutput(),browser:browser);
        // The library remains available to its internal controller, while the
        // visible top cube side is now Storage.
        shell.TurnTo(ShellFace.Play);shell.Tick(1);shell.Confirm();Check(shell.Page==ShellPage.Browser&&browser.Count==6,"Internal Play controller opens the flat six-card library");
        Move(shell,0,-1);Move(shell,0,-1);Check(browser.PageStart==6&&browser.Selection==0&&browser.Count==3,"Grid crosses page in three columns");Move(shell,-1,0);Check(browser.PageStart==0&&browser.Selection==5,"Previous page boundary retains selection");
        shell.Confirm();Check(browser.Panel==IplBrowserPanel.Launch&&browser.Choice==0&&source.Opened==0,"Opening a game asks before replacing the app");shell.Confirm();Check(browser.Panel==IplBrowserPanel.Grid&&source.Opened==0,"Default Cancel does not launch");
        shell.Confirm();Move(shell,1,0);shell.Confirm();Check(source.Opened==1&&source.Video.Scan==1&&browser.LaunchRequested,"Explicit Open uses applied output");shell.Back();Check(shell.Page==ShellPage.Face&&shell.Face==ShellFace.Play&&!browser.Active,"One B closes the library flow directly to the cube");
        var waiting=new Source{Wait=2};using var pending=new IplBrowser(waiting);pending.Open(IplBrowserKind.Games);Check(pending.Count==0&&pending.Busy,"No stale entries while scanning");pending.Direction(1,0);pending.Tick(.016f);pending.Tick(.016f);Check(pending.Count==6&&!pending.Busy,"Scan completion publishes one bounded page");pending.Refresh();Check(pending.PageStart==0,"Rescan returns to first page");
        Console.WriteLine("Flat game library: internal entry/return, paged grid, pending scan, applied-mode launch and default Cancel passed.");
    }
}
