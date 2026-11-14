using System;
using Dolphin.Diagnostics;
using Dolphin.System;

// Run the published managed cipher on the actual PowerPC target, independently
// of EXI and Dolphin's already-decoded ROM. Only generated data is used here.
internal static class Program
{
    private static int Main()
    {
        DebugOverlay.WriteLine("IPL CHECK 01 - CHECKING PUBLISHED SDK CIPHER");
        var bytes=new byte[FirmwareImage.ImageBytes];
        FirmwareImage.Transform(bytes);
        uint hash=2166136261;
        for(int i=0;i<bytes.Length;i++)hash=unchecked((hash^bytes[i])*16777619);
        // Full-image FNV-1a of the independently generated IPL keystream.
        if(hash!=0x35795a57u)return 1;
        FirmwareImage.Transform(bytes);
        for(int i=0;i<bytes.Length;i++)if(bytes[i]!=0)return 2;

        const string header="Nintendo GameCube IPL synthetic";
        for(int i=0;i<header.Length;i++)bytes[i]=(byte)header[i];
        for(int at=4096;at<=8192;at+=4096)
        {
            // Two separate literal-only, four-byte Yay0 records.
            bytes[at]=89;bytes[at+1]=97;bytes[at+2]=121;bytes[at+3]=48;
            bytes[at+7]=4;bytes[at+11]=20;bytes[at+15]=20;
            bytes[at+16]=240;
            for(int i=0;i<4;i++)bytes[at+20+i]=(byte)(65+i);
        }
        FirmwareImage.Transform(bytes);
        using(var image=FirmwareImage.Read(bytes))
        using(var catalog=image.CreateCatalog())
        {
            if(image.Encoding!=FirmwareEncoding.Scrambled||catalog.Count!=2||catalog.Extract(0)[0]!=65)return 3;
        }
        DebugOverlay.WriteLine("IPL SDK CIPHER AND SCRAMBLED CATALOG PASS");
        return 0;
    }
}
