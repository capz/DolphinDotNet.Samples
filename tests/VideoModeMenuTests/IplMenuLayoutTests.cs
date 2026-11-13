using System;
using System.Collections.Generic;
using System.Reflection;

// Exercise the generated panel/text coordinates, including the selected row's
// maximum type growth. Material appearance is checked separately in Dolphin.
internal static class IplMenuLayoutTests
{
    private sealed class Bounds
    {
        internal readonly IplPoint Center;
        internal float Left=float.MaxValue,Top=float.MaxValue,Right=float.MinValue,Bottom=float.MinValue;
        internal Bounds(IplPoint center){Center=center;}
        internal void Add(IplPoint p){Left=Math.Min(Left,p.X);Top=Math.Min(Top,p.Y);Right=Math.Max(Right,p.X);Bottom=Math.Max(Bottom,p.Y);}
    }
    private readonly struct Ink
    {
        internal readonly string Text;
        internal readonly float X,Y,Size;
        internal Ink(string text,float x,float y,float size){Text=text;X=x;Y=y;Size=size;}
    }
    private sealed class Preferences : IIplPreferencesStore
    {
        public string Status=>"Fixture";
        public bool Load(out IplPreferences value){value=new IplPreferences(new VideoModeSettings(1,1,false),0,true,2);return true;}
        public bool Save(IplPreferences value)=>true;
    }
    private static void Check(bool valid,string message){if(!valid)throw new Exception(message);}
    private static void Fits(Ink ink,Bounds panel,string context)
    {
        Check(ink.X>=panel.Left+4&&ink.X+IplRenderer.TextWidth(ink.Text,ink.Size)<=panel.Right-4,context+" text must clear the side walls");
        // The atlas cell is 40 pixels high at 28-point reference size. This
        // deliberately includes its transparent padding and the text shadow.
        Check(ink.Y>=panel.Top+2&&ink.Y+ink.Size*40/28<=panel.Bottom-.25f,context+" text must remain inside its panel");
    }
    private static void Cube()
    {
        foreach(var face in new[]{ShellFace.Options,ShellFace.Memory})for(int selected=0;selected<(face==ShellFace.Options?3:2);selected++){
            var shell=new IplShell(new FakeOutput(),new Preferences());shell.TurnTo(face);shell.Tick(1);shell.Time=0;shell.Row=selected;
            var panels=new List<Bounds>();var ink=new List<Ink>();
            var canvas=new IplProof{
                OmitGlassGeometry=true,
                PanelSink=(a,b,c)=>{
                    if(panels.Count==0||Math.Abs(panels[panels.Count-1].Center.Y-a.Y)>.01f)panels.Add(new Bounds(a));
                    var panel=panels[panels.Count-1];panel.Add(a);panel.Add(b);panel.Add(c);
                },
                TextSink=(text,x,y,size)=>ink.Add(new Ink(text,x,y,size))
            };
            var renderer=new IplRenderer(canvas);renderer.Prepare(shell);renderer.Present(shell);
            Check(panels.Count==shell.Rows,"Every settings/storage preview must generate its complete panel masks");
            for(int row=0;row<shell.Rows;row++){
                var panel=panels[row];
                Check(panel.Left>195&&panel.Right<449&&panel.Top>130&&panel.Bottom<326,"Settings/storage cube panels must clear the glass perimeter");
                if(row>0)Check(panel.Top>panels[row-1].Bottom+4,"Cube rows must retain visible separation");
                foreach(string text in new[]{shell.Label(row),shell.Value(row)}){
                    bool found=false;foreach(var item in ink)if(item.Text==text&&item.Y>=panel.Top&&item.Y<panel.Bottom){Fits(item,panel,"Cube "+text);found=true;}
                    Check(found,"Every cube option label/value must be rendered");
                }
            }
        }
    }
    private static void Flat()
    {
        var rowShape=typeof(IplRenderer).GetMethod("FlatRowShape",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var cacheStyle=typeof(IplRenderer).GetMethod("FlatStyle",BindingFlags.Static|BindingFlags.NonPublic)!;
        int settingsStyle=(int)cacheStyle.Invoke(null,new object[]{3})!,storageStyle=(int)cacheStyle.Invoke(null,new object[]{2})!;
        Check(settingsStyle!=storageStyle&&settingsStyle>1&&settingsStyle!=5&&storageStyle>1&&storageStyle!=5,"Settings/storage menu caches must not alias each other, game or file-browser cards");
        foreach(var face in new[]{ShellFace.Options,ShellFace.Memory})for(int selected=0;selected<(face==ShellFace.Options?3:2);selected++){
            var shell=new IplShell(new FakeOutput(),new Preferences());shell.TurnTo(face);shell.Tick(1);shell.Confirm();shell.Row=selected;shell.Tick(.18f);
            var ink=new List<Ink>();var canvas=new IplProof{TextSink=(text,x,y,size)=>ink.Add(new Ink(text,x,y,size))};
            var renderer=new IplRenderer(canvas);renderer.Prepare(shell);renderer.Present(shell);
            Bounds? previous=null;
            for(int row=0;row<shell.Rows;row++){
                var panel=new Bounds(default);canvas.TriangleSink=(ax,ay,bx,by,cx,cy,color)=>{panel.Add(new IplPoint(ax,ay));panel.Add(new IplPoint(bx,by));panel.Add(new IplPoint(cx,cy));};
                rowShape.Invoke(renderer,new object?[]{shell.Rows,row,null,true});canvas.TriangleSink=null;
                Check(panel.Left>=60&&panel.Right<=580&&panel.Top>=76&&panel.Bottom<=390,"Flat settings/storage rows must retain title, footer and screen margins");
                if(previous!=null)Check(panel.Top>previous.Bottom+8,"Flat rows must have clear separation");
                foreach(string text in new[]{shell.Label(row),shell.Value(row)}){
                    bool found=false;foreach(var item in ink)if(item.Text==text&&item.Y>=panel.Top&&item.Y<panel.Bottom){Fits(item,panel,"Flat "+text);found=true;}
                    Check(found,"Every flat option label/value must be rendered");
                }
                previous=panel;
            }
        }
    }
    internal static void Run(){Cube();Flat();Console.WriteLine("Settings/storage layout: three Options rows, two top-Storage actions, selected text growth, wall/footer margins and cache isolation passed.");}
}
