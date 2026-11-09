using System;

// A reusable GX_TRIANGLES command list. Position and color have independent
// 16-bit indices, so one flat segment color covers both triangles of each quad.
internal static class IplRimCommands
{
    internal const int Quads=128,PositionFloats=Quads*8,ColorBytes=Quads*4;
    internal static byte[] Create()
    {
        var bytes=new byte[(3+Quads*6*4+31)/32*32];
        bytes[0]=0x90;bytes[1]=(byte)(Quads*6>>8);bytes[2]=(byte)(Quads*6&255);
        int at=3;
        for(int quad=0;quad<Quads;quad++)for(int vertex=0;vertex<6;vertex++)
        {
            int corner=vertex==0?0:vertex==1||vertex==3?1:vertex==4?3:2;
            int position=quad*4+corner;
            bytes[at++]=(byte)(position>>8);bytes[at++]=(byte)position;
            bytes[at++]=(byte)(quad>>8);bytes[at++]=(byte)quad;
        }
        return bytes;
    }
}
