using System;

internal static class IplDetailLayoutTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Move(IplShell shell,int x,int y){shell.Direction(0,0,0);shell.Direction(x,y,.016f);}
    private static IplShell Open(ShellFace face,int row=0)
    {
        var shell=new IplShell(new FakeOutput());shell.TurnTo(face);shell.Tick(1);shell.Confirm();
        if(face!=ShellFace.Video){shell.Row=row;shell.Confirm();}
        return shell;
    }
    private static void Bounds(IplShell shell)
    {
        int active=IplDetailLayout.Active(shell),count=IplDetailLayout.Count(shell);
        for(int part=0;part<count;part++){
            string value=IplDetailLayout.Value(shell,part);float cell=IplDetailLayout.Cell(shell,part);
            float width=(value.Length*6-1)*cell,x=IplDetailLayout.CenterX(shell,part),y=IplDetailLayout.ValueY+3.5f*cell;
            Check(cell==(part==active?6:5),"Every value editor uses the Date editor's active/inactive block scale.");
            for(int step=0;step<150;step++){
                shell.Time=step*.12f;shell.Tick(.18f);var pose=IplSelectorPose.For(shell,part,width);
                for(int sx=-1;sx<=1;sx+=2)for(int sy=-1;sy<=1;sy+=2){
                    var p=pose.Project(sx*width/2,sy*3.5f*cell,x,y);
                    Check(p.X>40&&p.X<600&&p.Y>158&&p.Y<248,"Rocking block values must clear the viewport and arrow bands.");
                    if(count==3&&shell.Face==ShellFace.Video)
                        Check(Math.Abs(p.X-x)<83,"Video fields must remain in their independently selectable columns.");
                }
            }
        }
    }
    internal static void Run()
    {
        var video=Open(ShellFace.Video);
        Check(IplDetailLayout.Count(video)==3&&IplDetailLayout.Label(video,0)=="Output"&&IplDetailLayout.Label(video,1)=="Scan"&&IplDetailLayout.Label(video,2)=="Aspect","Video presents all three named parts together.");
        Check(IplDetailLayout.Value(video,1)=="I","Scan uses a readable block symbol instead of a miniature long word.");
        Move(video,1,0);Move(video,0,-1);Check(IplDetailLayout.Value(video,1)=="AUTO"&&video.Value(1)=="Auto","Auto has a readable symbolic and explanatory value.");
        for(int row=0;row<3;row++){Bounds(video);Move(video,1,0);}
        var date=Open(ShellFace.Calendar,1);for(int part=0;part<3;part++){Bounds(date);Move(date,1,0);}
        var time=Open(ShellFace.Calendar);for(int part=0;part<2;part++){Bounds(time);Move(time,1,0);}
        foreach(int row in new[]{0,1,2}){
            var options=Open(ShellFace.Options,row);
            if(row==1){Move(options,0,1);Check(options.Value(1)=="Pronounced","The full Motion choice must fit the shared value editor.");}
            Bounds(options);
        }
        Console.WriteLine("Detail layout: shared Calendar-scale values, three independent Video columns, readable scan symbols and animated bounds passed.");
    }
}
