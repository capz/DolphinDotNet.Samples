using System;
using System.IO;
using Dolphin.Diagnostics;
using Dolphin.Graphics;
using Dolphin.Storage;
using Dolphin.Timing;

// Bounded, allocation-free sampling. Storage access occurs only after the last
// measured frame. Clock timestamps are unclamped microseconds, unlike shell dt.
internal static class IplFrameProfiler
{
    internal const string Variant="shared-blur-mesh";
    internal const int Frames=96, Columns=17;
    internal static readonly int[] Samples=new int[Frames*Columns];
    private static readonly byte[] line=new byte[256];
    internal static int Count;
    internal static bool Active,Ready;
    internal static int CaptureUs,BlurUs;
    private static int warmup;
    internal static string Status="";
    internal static void Start()
    { Count=0;warmup=30;Active=true;Ready=false;Status="Recording frame times"; }
    internal static int Since(long start)=>(int)(Clock.Timestamp-start);
    internal static void Record(long start,int prepare,int submit,int end,GraphicsDevice device,IplShell shell)
    {
        if(!Active)return;
        if(warmup-->0)return;
        int at=Count*Columns;
        var gpu=GpuProfiler.Snapshot();
        Samples[at]=Since(start);Samples[at+1]=prepare;Samples[at+2]=submit;Samples[at+3]=end;
        Samples[at+4]=GpuProfiler.GpuWaitMicroseconds;Samples[at+5]=GpuProfiler.RetraceWaitMicroseconds;
        Samples[at+6]=CaptureUs;Samples[at+7]=BlurUs;
        Samples[at+8]=gpu.DrawSubmissions;Samples[at+9]=gpu.Triangles;
        Samples[at+10]=GpuProfiler.BeginFrameCopyMicroseconds;Samples[at+11]=GpuProfiler.GateReacquireMicroseconds;
        Samples[at+12]=device.Width;Samples[at+13]=device.Height;Samples[at+14]=(int)device.Display.Scan;
        Samples[at+15]=(int)shell.Face;Samples[at+16]=shell.Editing?1:0;
        if(++Count==Frames){Active=false;Ready=true;Status="Frame times captured";}
    }
    // ASCII formatting avoids hundreds of temporary strings on the 256 KiB heap.
    private static void Text(Dolphin.IO.FileStream stream,string value)
    {
        int at=0;
        for(int i=0;i<value.Length;i++)
        {line[at++]=(byte)value[i];if(at==line.Length){stream.Write(line,0,at);at=0;}}
        if(at>0)stream.Write(line,0,at);
    }
    internal static void Export()
    {
        if(!Ready)return;
        for(int candidate=0;candidate<4;candidate++)
        {
            var storage=candidate==0?StorageDevice.SdSerialPort2:candidate==1?StorageDevice.SdSlotB:candidate==2?StorageDevice.SdSlotA:StorageDevice.GcLoader;
            try
            {
                if(!Storage.IsMounted(storage)&&!Storage.Mount(storage))continue;
                Storage.CreateDirectory(storage,"videomode-perf");
                int number=0;string path;
                do {path="videomode-perf/frame-"+number+".csv";number++;}while(Storage.FileExists(storage,path)&&number<1000);
                using(var stream=Storage.Open(storage,path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    Text(stream,"# variant="+Variant+"; units=us; warmup=30; samples=96; environment=unspecified\n");
                    Text(stream,"frame_us,prepare_us,submit_us,end_us,gpu_wait_us,retrace_us,capture_us,blur_us,draws,triangles,begin_copy_us,gate_us,width,height,scan,face,editing\n");
                    for(int row=0;row<Count;row++)for(int col=0;col<Columns;col++)
                    {
                        int value=Samples[row*Columns+col],end=16,at=end;
                        do {line[--at]=(byte)('0'+value%10);value/=10;}while(value>0);
                        line[end]=(byte)(col==Columns-1?'\n':',');stream.Write(line,at,end-at+1);
                    }
                }
                Status="Saved "+Storage.GetPath(storage,path);return;
            }
            // Logging must never interrupt a video preview or strand the UI.
            catch(Exception) { }
        }
        Status="No writable SD; frame times retained in RAM";
    }
}
