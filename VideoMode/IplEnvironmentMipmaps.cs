using System;

// GX RGBA8 uses separate AR/GB planes in each 4x4, 64-byte tile. Even the
// final 2x2 and 1x1 mip levels occupy a full tile with replicated edge texels.
internal static class IplEnvironmentMipmaps
{
    internal const int BaseSize=256,Levels=9,TileBytes=64,ChainBytes=349632;
    internal static int LevelSize(int level)
    {
        if(level<0||level>=Levels)throw new ArgumentOutOfRangeException(nameof(level));
        return BaseSize>>level;
    }
    internal static int LevelBytes(int size)
    {
        if(size<1||size>BaseSize||(size&(size-1))!=0)throw new ArgumentOutOfRangeException(nameof(size));
        int tiles=(size+3)/4;return tiles*tiles*TileBytes;
    }
    internal static int LevelOffset(int level)
    {
        LevelSize(level);int offset=0;
        for(int i=0;i<level;i++)offset+=LevelBytes(BaseSize>>i);
        return offset;
    }
    // The source is four tiles in TL/TR/BL/BR order for an 8x8 region. At
    // source sizes4/2, only its first tile is read. Average each channel in
    // the preceding level, rounding once to the nearest stored byte.
    internal static void DownsampleTile(byte[] source,int sourceSize,byte[] destination)
    {
        if(sourceSize<2||sourceSize>BaseSize||(sourceSize&(sourceSize-1))!=0)throw new ArgumentOutOfRangeException(nameof(sourceSize));
        if(source==null||source.Length<(sourceSize>=8?256:TileBytes))throw new ArgumentException("Incomplete source tiles.",nameof(source));
        if(destination==null||destination.Length<TileBytes)throw new ArgumentException("Incomplete destination tile.",nameof(destination));
        int extent=Math.Min(4,sourceSize/2);
        for(int y=0;y<4;y++)for(int x=0;x<4;x++)
        {
            int sx=Math.Min(x,extent-1)*2,sy=Math.Min(y,extent-1)*2;
            int a=PixelOffset(sx,sy),b=PixelOffset(sx+1,sy),c=PixelOffset(sx,sy+1),d=PixelOffset(sx+1,sy+1),at=(y*4+x)*2;
            destination[at]=Average(source,a,b,c,d);
            destination[at+1]=Average(source,a+1,b+1,c+1,d+1);
            destination[32+at]=Average(source,a+32,b+32,c+32,d+32);
            destination[33+at]=Average(source,a+33,b+33,c+33,d+33);
        }
    }
    private static int PixelOffset(int x,int y)=>((x/4)+(y/4)*2)*TileBytes+((y%4)*4+x%4)*2;
    private static byte Average(byte[] source,int a,int b,int c,int d)=>(byte)((source[a]+source[b]+source[c]+source[d]+2)/4);
}
