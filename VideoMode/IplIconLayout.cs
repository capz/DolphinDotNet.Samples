using System;

// Bounds include the complete blocks, excluding unused bitmap rows/columns.
// A centered 1.10-wide/tall icon stays at least .31 from the .86 cavity walls.
internal readonly struct IplIconLayout
{
    internal const float MaximumSpan=1.20f;
    internal readonly float Cell,RowStep,Gap,Left,Top;
    private IplIconLayout(float cell,float rowStep,float gap,float left,float top)
    {Cell=cell;RowStep=rowStep;Gap=gap;Left=left;Top=top;}

    internal static IplIconLayout Create(string[] bitmap)
    {
        int minX=int.MaxValue,minY=int.MaxValue,maxX=-1,maxY=-1;
        for(int y=0;y<bitmap.Length;y++)for(int x=0;x<bitmap[y].Length;x++)if(bitmap[y][x]=='1')
        {minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
        if(maxX<0)throw new ArgumentException("An icon needs visible blocks.");
        const float rowRatio=.114f/.122f,gapRatio=.012f/.122f;
        float width=maxX-minX+1-gapRatio,height=(maxY-minY+1)*rowRatio-gapRatio;
        float cell=MaximumSpan/Math.Max(width,height),step=cell*rowRatio,gap=cell*gapRatio;
        return new IplIconLayout(cell,step,gap,-width*cell/2-minX*cell,height*cell/2+minY*step);
    }

    // The face determines orientation only. Every icon plane passes through
    // the volume's origin, instead of sitting on the outside face at depth 1.
    internal static IplPoint Point(ShellFace face,float u,float v)
    {
        if(face==ShellFace.Video)return new IplPoint(0,v,-u);
        if(face==ShellFace.Options)return new IplPoint(0,v,u);
        if(face==ShellFace.Calendar)return new IplPoint(u,0,v);
        if(face==ShellFace.Play)return new IplPoint(u,0,-v);
        if(face==ShellFace.Memory)return new IplPoint(-u,v,0);
        return new IplPoint(u,v,0);
    }
}
