#if IPL_HARDWARE_RUNNER
using System;
using System.Threading;
using System.Threading.Tasks;
using Dolphin.Network;
using Dolphin.Graphics;

// DNDP v2: post-window binary telemetry. No socket/file/debugger operation is
// called while sampling. Keep payload and async allocations below the SDK heap.
internal static class IplHardwareReporter
{
    internal static Task Initialize()=>NetworkRuntime.StartAsync(new NetworkOptions {OperationTimeoutMilliseconds=30000},CancellationToken.None);
    private static void U32(byte[] bytes,int at,uint value)
    {bytes[at]=(byte)(value>>24);bytes[at+1]=(byte)(value>>16);bytes[at+2]=(byte)(value>>8);bytes[at+3]=(byte)value;}
    private static uint Crc(byte[] bytes,int count)
    {
        uint crc=0xffffffff;
        for(int i=0;i<count;i++)
        {crc^=bytes[i];for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}
        return ~crc;
    }
    internal static async Task Send()
    {
        int length=IplFrameProfiler.Count*IplFrameProfiler.Columns*4;
        var payload=new byte[length];var header=new byte[64];var ack=new byte[4];
        for(int i=0;i<length/4;i++)U32(payload,i*4,(uint)IplFrameProfiler.Samples[i]);
        header[0]=68;header[1]=78;header[2]=68;header[3]=80; // DNDP
        header[5]=2;header[7]=64;
        for(int i=0;i<32;i++)header[8+i]=(byte)IplHardwareRunConfig.Token[i];
        U32(header,40,IplFrameProfiler.Columns);U32(header,44,(uint)IplFrameProfiler.Count);
        U32(header,48,(uint)length);U32(header,52,Crc(payload,length));
        // PAL output can use a 480-line EFB. Validate resolved output separately.
        var display=GraphicsDevice.Default.Display;
        U32(header,56,display.Resolution==GraphicsResolution.R640x576?576u:display.Resolution==GraphicsResolution.R320x240?240u:480u);
        U32(header,60,Crc(header,60));
        try
        {
            using var client=new TcpClient();
            await client.ConnectAsync(IPAddress.Parse(IplHardwareRunConfig.Host),IplHardwareRunConfig.Port);
            client.NoDelay=true;var stream=client.GetStream();
            await stream.WriteAsync(header,0,header.Length,CancellationToken.None);
            await stream.WriteAsync(payload,0,payload.Length,CancellationToken.None);
            int read=0;
            while(read<4)
            {int n=await stream.ReadAsync(ack,read,4-read,CancellationToken.None);if(n==0)throw new InvalidOperationException("Bridge closed before telemetry acknowledgement.");read+=n;}
            if(ack[0]!=79||ack[1]!=75||ack[2]!=65||ack[3]!=89)throw new InvalidOperationException("Bridge rejected frame telemetry.");
        }
        finally {if(NetworkRuntime.IsReady)NetworkRuntime.Stop();}
    }
}
#endif
