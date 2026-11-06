using System;

internal static class IplStorageIcons
{
    internal const int AtlasSize=512,AtlasColumns=14,CellBytes=36*36*4,TileRowBytes=36*4*4;
    // GX RGBA8 is 4x4 tiles: sixteen A/R pairs followed by sixteen G/B pairs.
    internal static int TileRowOffset(int cell,int row)=>((cell/AtlasColumns*9+row)*128+cell%AtlasColumns*9)*64;
    internal static void Raster(byte[] output,IplItemKind kind,byte[]? rgba=null)
    {
        for(int y=0;y<36;y++)for(int x=0;x<36;x++){
            float dx=Math.Max(5-x,Math.Max(0,x-30)),dy=Math.Max(5-y,Math.Max(0,y-30));
            float edge=5-(float)Math.Sqrt(dx*dx+dy*dy);int alpha=(int)(Math.Max(0,Math.Min(1,edge))*220);
            int r=28+y/2,g=67+y/3,b=86+y/2;
            if(y<3){r=157;g=227;b=230;alpha=alpha*3/4;}
            bool inside=x>=2&&x<34&&y>=2&&y<34;
            if(inside&&rgba!=null&&rgba.Length==4096){int i=((y-2)*32+x-2)*4,a=rgba[i+3];r=(rgba[i]*a+r*(255-a))/255;g=(rgba[i+1]*a+g*(255-a))/255;b=(rgba[i+2]*a+b*(255-a))/255;alpha=Math.Max(alpha,a);}
            else if(inside&&rgba==null){
                bool shape=kind==IplItemKind.Directory?(x>=6&&x<30&&y>=12&&y<27||x>=7&&x<17&&y>=8&&y<13):
                    kind==IplItemKind.Game?((x-18)*(x-18)+(y-18)*(y-18)<115&&(x-18)*(x-18)+(y-18)*(y-18)>10):
                    kind==IplItemKind.Save?(x>=8&&x<28&&y>=9&&y<28&&!(x>22&&y<14)):
                    (x>=10&&x<27&&y>=7&&y<29&&!(x>21&&y<12));
                if(shape){r=kind==IplItemKind.Directory?175:130;g=kind==IplItemKind.Directory?170:189;b=kind==IplItemKind.Directory?120:205;alpha=235;}
                if(kind==IplItemKind.Dol&&Math.Abs(x-18)+Math.Abs(y-18)<7){r=67;g=115;b=151;}
                if(kind==IplItemKind.CardFormat&&Math.Abs(x-18)<2&&y>=10&&y<25){r=225;g=179;b=119;}
            }
            // Smooth roundover crops icon corners as well as the glass cell.
            alpha=(int)(alpha*Math.Max(0,Math.Min(1,edge)));
            int tile=(y/4*9+x/4)*64,pixel=(y%4*4+x%4)*2;
            output[tile+pixel]=(byte)alpha;output[tile+pixel+1]=(byte)r;output[tile+32+pixel]=(byte)g;output[tile+33+pixel]=(byte)b;
        }
    }
}
