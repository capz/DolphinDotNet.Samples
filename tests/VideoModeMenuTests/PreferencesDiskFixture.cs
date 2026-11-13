using System;
using System.IO;
// The production store is linked unchanged. Only its filesystem boundary is
// replaced here; this fixture never mounts or writes a user's console media.
namespace Dolphin.Storage
{
    public enum StorageDevice { GcLoader=6 }
    public static class Storage
    {
        internal static string Root="";
        internal static bool Writable=true,FailSlotA;
        public static bool IsMounted(StorageDevice device)=>Root.Length>0;
        public static bool Mount(StorageDevice device)=>Root.Length>0;
        internal static string PathFor(string path)=>System.IO.Path.Combine(Root,path);
        public static bool FileExists(StorageDevice device,string path)=>File.Exists(PathFor(path));
        public static void CreateDirectory(StorageDevice device,string path)=>Directory.CreateDirectory(PathFor(path));
        public static Stream Open(StorageDevice device,string path,FileMode mode,FileAccess access,FileShare share)
        {
            if(access!=FileAccess.Read&&!Writable)throw new IOException("Write protected");
            if(access==FileAccess.Read&&FailSlotA&&path==IplPreferencesStore.SlotA)throw new IOException("Unreadable record");
            return new ShortReads(new FileStream(PathFor(path),mode,access,share));
        }
        private sealed class ShortReads : Stream
        {
            private readonly Stream inner;
            internal ShortReads(Stream inner){this.inner=inner;}
            public override bool CanRead=>inner.CanRead;public override bool CanWrite=>inner.CanWrite;public override bool CanSeek=>inner.CanSeek;
            public override long Length=>inner.Length;public override long Position{get=>inner.Position;set=>inner.Position=value;}
            public override int Read(byte[] buffer,int offset,int count)=>inner.Read(buffer,offset,Math.Min(count,3));
            public override void Write(byte[] buffer,int offset,int count)=>inner.Write(buffer,offset,count);
            public override void Flush()=>inner.Flush();public override long Seek(long offset,SeekOrigin origin)=>inner.Seek(offset,origin);
            public override void SetLength(long value)=>inner.SetLength(value);
            protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
        }
    }
}
internal static class IplPreferencesDiskTests
{
    private static void Check(bool pass){if(!pass)throw new Exception("Preference disk recovery failed");}
    internal static void Run()
    {
        string root=Path.Combine(Path.GetTempPath(),"ipl-prefs-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{
            Dolphin.Storage.Storage.Root=root;
            var store=new IplPreferencesStore();Check(!store.Load(out _));
            var first=new IplPreferences(new VideoModeSettings(1,1,false),0,false,1);
            var second=new IplPreferences(new VideoModeSettings(2,2,true),1,true,3);
            Check(store.Save(first)&&store.Save(second));var boot=new IplPreferencesStore();Check(boot.Load(out var saved)&&saved.Video.Resolution==2&&saved.Sound==1&&saved.GlassBlur==3);
            string b=Dolphin.Storage.Storage.PathFor(IplPreferencesStore.SlotB);
            File.WriteAllBytes(b,new byte[9]);boot=new IplPreferencesStore();Check(boot.Load(out saved)&&saved.Video.Resolution==1&&saved.GlassBlur==1);
            Check(boot.Save(second));Dolphin.Storage.Storage.FailSlotA=true;boot=new IplPreferencesStore();Check(boot.Load(out saved)&&saved.Video.Resolution==2&&saved.GlassBlur==3);
            Dolphin.Storage.Storage.FailSlotA=false;Dolphin.Storage.Storage.Writable=false;Check(!boot.Save(first));
            boot=new IplPreferencesStore();Check(boot.Load(out saved)&&saved.Video.Resolution==2&&saved.GlassBlur==3);
            File.WriteAllBytes(b,new byte[32]);File.WriteAllBytes(Dolphin.Storage.Storage.PathFor(IplPreferencesStore.SlotA),new byte[32]);
            Check(!new IplPreferencesStore().Load(out _));
            Console.WriteLine("Preferences disk: short reads, reboot restore, torn slot fallback, unreadable slot recovery and read-only failures passed.");
        }finally{Dolphin.Storage.Storage.Root="";Dolphin.Storage.Storage.Writable=true;Dolphin.Storage.Storage.FailSlotA=false;Directory.Delete(root,true);}
    }
}
