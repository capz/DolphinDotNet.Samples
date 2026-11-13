using System;
using System.Reflection;

internal static class IplCubeMotionTests
{
    private static void Check(bool valid,string message){if(!valid)throw new Exception(message);}
    private static void Near(float value,float expected,string message)=>Check(Math.Abs(value-expected)<.000002f,message);
    private sealed class Preferences : IIplPreferencesStore
    {
        public string Status=>"Fixture";
        public bool Load(out IplPreferences value){value=new IplPreferences(new VideoModeSettings(1,1,false),0,2);return true;}
        public bool Save(IplPreferences value)=>true;
    }
    internal static void Run()
    {
        // Recorded original idle offsets: prevent a new default from silently
        // changing the existing camera float while adding a stronger option.
        var a=IplCubeMotion.At(1,0);Near(a.Yaw,0,"Subtle starts at the original yaw");Near(a.Pitch,0,"Subtle starts at the original pitch");Near(a.Bob,0,"Subtle starts at the original center");
        a=IplCubeMotion.At(1,1);Near(a.Yaw,.02316724f,"Subtle keeps its original yaw at one second");Near(a.Pitch,.015477738f,"Subtle keeps its original pitch at one second");Near(a.Bob,.6442177f,"Subtle keeps its original float at one second");
        double subtleYaw=0,strongYaw=0,subtlePitch=0,strongPitch=0,subtleBob=0,strongBob=0;
        var previous=IplCubeMotion.At(2,0);
        for(int frame=0;frame<=3600;frame++){
            float time=frame/60f;var off=IplCubeMotion.At(0,time);var subtle=IplCubeMotion.At(1,time);var strong=IplCubeMotion.At(2,time);
            Check(off.Yaw==0&&off.Pitch==0&&off.Bob==0,"Disabled must remove idle motion at every phase");
            Check(Math.Abs(strong.Yaw)<=.08701f&&Math.Abs(strong.Pitch)<=.07001f&&Math.Abs(strong.Bob)<=3.501f,"Pronounced motion must retain a bounded envelope");
            Check(Math.Abs(strong.Yaw-previous.Yaw)<.01f&&Math.Abs(strong.Pitch-previous.Pitch)<.01f&&Math.Abs(strong.Bob-previous.Bob)<.1f,"Pronounced motion must remain smooth between frames");
            subtleYaw+=subtle.Yaw*subtle.Yaw;strongYaw+=strong.Yaw*strong.Yaw;
            subtlePitch+=subtle.Pitch*subtle.Pitch;strongPitch+=strong.Pitch*strong.Pitch;
            subtleBob+=subtle.Bob*subtle.Bob;strongBob+=strong.Bob*strong.Bob;previous=strong;
        }
        Check(strongYaw>subtleYaw*8&&strongPitch>subtlePitch*8&&strongBob>subtleBob*8,"Pronounced must produce visibly larger angular and vertical motion");
        var project=typeof(IplRenderer).GetMethod("Project",BindingFlags.Instance|BindingFlags.NonPublic)!;
        for(int face=0;face<6;face++){
            var shell=new IplShell(new FakeOutput(),new Preferences());shell.TurnTo((ShellFace)face);shell.Tick(1);
            var renderer=new IplRenderer(new IplProof{OmitGlassGeometry=true});
            for(int sample=0;sample<90;sample++){
                shell.Time=sample*.67f;renderer.Prepare(shell);
                for(int corner=0;corner<8;corner++){
                    var p=(IplPoint)project.Invoke(renderer,new object[]{new IplPoint((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)})!;
                    Check(p.X>140&&p.X<505&&p.Y>28&&p.Y<383,"Pronounced cube motion must preserve screen and footer margins on every side");
                }
            }
        }
        Console.WriteLine("Cube motion: unchanged Subtle, stationary Disabled, stronger smooth Pronounced and all-side projected margins passed.");
    }
}
