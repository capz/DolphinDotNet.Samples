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
    internal const string Variant="skip-transparent-triangles";
    internal const int Frames=96, Columns=17;
    internal static readonly int[] Samples=new int[Frames*Columns];
    private static readonly byte[] line=new byte[256];
    private static readonly int[] ordered=new int[Frames];
    internal static int Count;
    internal static bool Active,Ready,NeedsExport;
    internal static int CaptureUs,BlurUs;
    private static int warmup;
    internal static string Status="";
    internal static string Summary="";
    internal static void Start()
    { Count=0;warmup=30;Active=true;Ready=NeedsExport=false;Status="Recording frame times";Summary=""; }
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
        if(++Count==Frames)
        {
            Active=false;Ready=NeedsExport=true;Status="Frame times captured";
            long total=0;
            for(int i=0;i<Frames;i++)
            {
                int value=Samples[i*Columns];total+=value;
                int j=i;while(j>0&&ordered[j-1]>value){ordered[j]=ordered[j-1];j--;}
                ordered[j]=value;
            }
            Summary="Frame mean "+(total/Frames)+" us | p95 "+ordered[91]+" us";
        }
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
#if IPL_LOG_SD2SP2 || IPL_LOG_SDA || IPL_LOG_SDB || IPL_LOG_GCLOADER
#if IPL_LOG_SD2SP2
            const StorageDevice storage=StorageDevice.SdSerialPort2;
#elif IPL_LOG_SDA
            const StorageDevice storage=StorageDevice.SdSlotA;
#elif IPL_LOG_SDB
            const StorageDevice storage=StorageDevice.SdSlotB;
#else
            const StorageDevice storage=StorageDevice.GcLoader;
#endif
            try
            {
                if(!Storage.IsMounted(storage)&&!Storage.Mount(storage))
                {Status="SD unavailable; frame times retained in RAM";return;}
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
            // Recover write errors; probes require a known attached adapter.
            catch(Exception) { }
        Status="No writable SD; frame times retained in RAM";
#else
        // Probing an absent EXI adapter can block inside the native driver.
        // Select a known attached target at build time for physical SD logging.
        Status="Captured in RAM; SD export disabled in this build";
#endif
    }
}
