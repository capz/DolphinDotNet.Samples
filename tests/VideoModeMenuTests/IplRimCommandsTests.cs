using System;

internal static class IplRimCommandsTests
{
    internal static void Run()
    {
        var data=IplRimCommands.Create();
        if(data.Length%32!=0||data[0]!=0x90||(data[1]*256+data[2])!=IplRimCommands.Quads*6)
            throw new Exception("Invalid GX triangle command header/alignment");
        int at=3;
        int[] winding={0,1,2,1,3,2};
        for(int quad=0;quad<IplRimCommands.Quads;quad++)for(int vertex=0;vertex<6;vertex++)
        {
            int position=data[at]*256+data[at+1],color=data[at+2]*256+data[at+3];at+=4;
            if(position!=quad*4+winding[vertex]||color!=quad||position>=IplRimCommands.PositionFloats/2)
                throw new Exception("GX index stream must preserve quad winding, flat color and array bounds");
        }
        for(;at<data.Length;at++)if(data[at]!=0)throw new Exception("GX list padding must contain NOPs");
        Console.WriteLine("Indexed rim command list: header, alignment, bounds, winding, independent colors and NOP padding passed.");
    }
}
