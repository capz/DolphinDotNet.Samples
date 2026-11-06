using System;
internal static class IplNumberCacheStateTests
{
    private static void Check(bool value){if(!value)throw new Exception("Printed-number invalidation failed");}
    internal static void Run()
    {
        var state=new IplNumberCacheState();var values=new int[IplPerformanceStats.Count];
        Check(state.Update(values)==3);Check(state.Update(values)==0);
        values[IplPerformanceStats.Frame]=9;Check(state.Update(values)==0);
        values[IplPerformanceStats.Frame]=10;Check(state.Update(values)==1);
        values[(int)IplRenderPhase.Labels]=10;Check(state.Update(values)==2);
        values[(int)IplRenderPhase.Labels]=19;Check(state.Update(values)==0);
        values[(int)IplRenderPhase.Labels]=-1;Check(state.Update(values)==2);
        values[(int)IplRenderPhase.Labels]=-99;Check(state.Update(values)==0);
        values[(int)IplRenderPhase.None]=1234;Check(state.Update(values)==0);
        values[IplPerformanceStats.Frame]=int.MaxValue;Check(state.Update(values)==1);
        Console.WriteLine("Printed-number cache: initial state, precision, independent groups, clamping and unused scopes passed.");
    }
}
