using System;

internal static class IplEnvironmentMipmapsTests
{
    private static void Check(bool pass,string message)
    {if(!pass)throw new Exception("Environment mipmaps: "+message);}
    private static void Pixel(byte[] source,int x,int y,byte a,byte r,byte g,byte b)
    {
        int tile=(y/4*2+x/4)*64,at=((y%4)*4+x%4)*2;
        source[tile+at]=a;source[tile+at+1]=r;source[tile+32+at]=g;source[tile+33+at]=b;
    }
    private static void Layout()
    {
        int[] sizes={256,128,64,32,16,8,4,2,1};
        int[] offsets={0,262144,327680,344064,348160,349184,349440,349504,349568};
        int end=0;
        for(int level=0;level<sizes.Length;level++)
        {
            int size=IplEnvironmentMipmaps.LevelSize(level),offset=IplEnvironmentMipmaps.LevelOffset(level),bytes=IplEnvironmentMipmaps.LevelBytes(size);
            Check(size==sizes[level]&&offset==offsets[level]&&offset==end,"all levels are contiguous in GX order");
            Check((offset&63)==0&&bytes>=64&&(bytes&63)==0,"every mip includes complete aligned RGBA8 tiles");
            end=offset+bytes;
        }
        Check(end==349632&&end==IplEnvironmentMipmaps.ChainBytes,"the complete chain has the exact native allocation size");
        Check(IplEnvironmentMipmaps.ChainBytes-256*256*4==87488,"one bank adds only the smaller levels");
        Check(IplEnvironmentMipmaps.LevelBytes(4)==64&&IplEnvironmentMipmaps.LevelBytes(2)==64&&IplEnvironmentMipmaps.LevelBytes(1)==64,"small mips retain their full tile storage");
    }
    private static void FlatAndTiles()
    {
        var source=new byte[256];var tile=new byte[64];
        for(int y=0;y<8;y++)for(int x=0;x<8;x++)Pixel(source,x,y,241,37,109,211);
        foreach(int size in new[]{256,128,64,32,16,8,4,2})
        {
            IplEnvironmentMipmaps.DownsampleTile(source,size,tile);
            for(int at=0;at<32;at+=2)
                Check(tile[at]==241&&tile[at+1]==37&&tile[32+at]==109&&tile[33+at]==211,"flat fields preserve every channel at every mip size");
        }
        for(int y=0;y<8;y++)for(int x=0;x<8;x++)
        {byte red=(byte)(10+40*(x/4)+80*(y/4));Pixel(source,x,y,255,red,19,73);}
        IplEnvironmentMipmaps.DownsampleTile(source,8,tile);
        for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            Check(tile[(y*4+x)*2+1]==10+40*(x/2)+80*(y/2),"four source tiles retain TL/TR/BL/BR order");
    }
    private static void ImpulsesAndPadding()
    {
        var source=new byte[256];var tile=new byte[64];
        Pixel(source,0,0,0,255,0,0);Pixel(source,0,2,0,0,128,0);
        Pixel(source,2,0,0,0,0,64);Pixel(source,7,7,80,0,0,0);
        IplEnvironmentMipmaps.DownsampleTile(source,8,tile);
        for(int at=0;at<64;at++)
        {
            int expected=at==1?64:at==40?32:at==35?16:at==30?20:0;
            Check(tile[at]==expected,"box filtering preserves impulse energy and never mixes AR/GB channels");
        }
        for(int y=0;y<4;y++)for(int x=0;x<4;x++)Pixel(source,x,y,200,(byte)(y*4+x),75,30);
        IplEnvironmentMipmaps.DownsampleTile(source,4,tile);
        for(int y=0;y<4;y++)for(int x=0;x<4;x++)
        {
            int at=(y*4+x)*2,red=3+2*Math.Min(x,1)+8*Math.Min(y,1);
            Check(tile[at]==200&&tile[at+1]==red&&tile[32+at]==75&&tile[33+at]==30,"2x2 mip padding replicates the filtered edge texels");
        }
        Array.Copy(tile,source,64);IplEnvironmentMipmaps.DownsampleTile(source,2,tile);
        for(int at=0;at<32;at+=2)
            Check(tile[at]==200&&tile[at+1]==8&&tile[32+at]==75&&tile[33+at]==30,"the final 1x1 average is replicated through its whole tile");
    }
    internal static void Run()
    {
        Layout();FlatAndTiles();ImpulsesAndPadding();
        Console.WriteLine("Environment mipmaps: exact GX layout, aligned small levels, flat fields, tile order, independent channel averages and edge padding passed.");
    }
}
