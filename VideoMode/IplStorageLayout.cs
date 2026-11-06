using System;

// Logical 640x480 coordinates, shared by navigation, retained artwork and text.
internal static class IplStorageLayout
{
    internal const int Columns=15,Rows=7,Capacity=Columns*Rows,ListRows=9,Cell=36,Step=37;
    internal static int PageSize(bool list)=>list?ListRows:Capacity;
    internal static float X(int item,bool list)=>list?45:43+item%Columns*Step;
    internal static float Y(int item,bool list)=>list?112+item*27:96+item/Columns*Step;
    internal static float IconSize(bool list)=>list?26:Cell;
    internal static string FileSize(long bytes)
    {
        if(bytes<0)return "Size unavailable";
        if(bytes<1024)return bytes.ToString()+" B";
        long unit=1024;string suffix=" KB";
        if(bytes>=1024*1024){unit=1024*1024;suffix=" MB";}
        if(bytes>=1024L*1024*1024){unit=1024L*1024*1024;suffix=" GB";}
        long tenths=(bytes*10+unit/2)/unit;
        return (tenths/10).ToString()+(tenths%10==0?"":"."+(tenths%10).ToString())+suffix;
    }
    internal static string SaveSize(long bytes)
    {long blocks=bytes/8192;return blocks.ToString()+(blocks==1?" block / ":" blocks / ")+(bytes/1024).ToString()+" KB";}
}

internal interface IIplBrowserPageSize
{
    void SetPageSize(int count);
}

// Dense grids retain index data natively and materialize only selected text.
// A list materializes at most nine rows; the game library keeps its own source.
internal interface IIplBrowserCatalog
{
    int ReadCatalogPage(int start,int maximum);
    IplBrowserItem? CatalogItem(int index);
    IplItemKind CatalogKind(int index);
}
