using System;
using Dolphin.Graphics;

internal sealed partial class GameCubeCanvas
{
    // Payloads stay in native RAM. Only a single 5 KiB tiled cell lives on the
    // 256 KiB managed heap, even for 105 visible saves and eight animated frames.
    private NativeBuffer? storagePixels,storageAnimation;
    private Texture2D? storageAtlas;
    private Mesh? storageMesh;
    private byte[]? storageCell;
    private readonly bool[] storageLoaded=new bool[127];
    private readonly int[] storageDurations=new int[8];
    private int storageContent=-1,storageKind=-1;
    private static int StorageCell(IplBrowser browser,int item)=>browser.Kind==IplBrowserKind.MemoryCards?browser.PageStart+item:item;
    private int storageRevision=-1,storageCursor,storageSelected=-1,storageFrames,storageFramesLoaded,storageFrame;
    private int storagePending;
    private bool storageBounces,storageMode;
    private void ReleaseStorageArtwork()
    {
        storageMesh?.Dispose();storageMesh=null;storageAtlas?.Dispose();storageAtlas=null;
        storagePixels?.Dispose();storagePixels=null;storageAnimation?.Dispose();storageAnimation=null;
        storageCell=null;storageMode=false;storageRevision=storageContent=storageKind=-1;storageSelected=-1;
    }
    private void WriteStorageCell(int slot)
    {for(int row=0;row<9;row++)storagePixels!.Write(storageCell!,row*IplStorageIcons.TileRowBytes,IplStorageIcons.TileRowOffset(slot,row),IplStorageIcons.TileRowBytes);}
    private void ReadStorageCell(int slot)
    {for(int row=0;row<9;row++)storagePixels!.Read(storageCell!,IplStorageIcons.TileRowOffset(slot,row),row*IplStorageIcons.TileRowBytes,IplStorageIcons.TileRowBytes);}
    private void StorageMesh(IplBrowser browser)
    {
        storageMesh?.Dispose();storageMesh=null;if(browser.Count==0)return;
        var indices=new ushort[browser.Count*6];
        for(int i=0;i<browser.Count;i++){
            float x=IplStorageLayout.X(i,browser.ListView),y=IplStorageLayout.Y(i,browser.ListView),size=IplStorageLayout.IconSize(browser.ListView);
            int cell=StorageCell(browser,i);float u=cell%14*36/512f,v=cell/14*36/512f;
            int at=i*4;glass[at]=new VertexPositionColorTexture(World(new IplPoint(x,y)),Color32.White,new Vector2(u,v));
            glass[at+1]=new VertexPositionColorTexture(World(new IplPoint(x+size,y)),Color32.White,new Vector2(u+36/512f,v));
            glass[at+2]=new VertexPositionColorTexture(World(new IplPoint(x+size,y+size)),Color32.White,new Vector2(u+36/512f,v+36/512f));
            glass[at+3]=new VertexPositionColorTexture(World(new IplPoint(x,y+size)),Color32.White,new Vector2(u,v+36/512f));
            int t=i*6;indices[t]=(ushort)at;indices[t+1]=(ushort)(at+1);indices[t+2]=(ushort)(at+2);indices[t+3]=(ushort)at;indices[t+4]=(ushort)(at+2);indices[t+5]=(ushort)(at+3);
        }
        for(int i=browser.Count*4;i<glass.Length;i++)glass[i]=new VertexPositionColorTexture(default,Color32.White,default);
        storageMesh=Mesh.Create(glass,indices);
    }
    private void UpdateStorageArtwork(IplBrowser browser)
    {
        storageMode=true;
        if(storagePixels==null){storagePixels=NativeBuffer.Allocate(512*512*4);storageAtlas=Texture2D.CreateNative(512,512,NativeTextureFormat.Rgba8,true);storageCell=new byte[IplStorageIcons.CellBytes];}
        bool upload=false;
        bool reset=storageContent!=browser.ContentRevision||storageKind!=(int)browser.Kind||browser.Kind!=IplBrowserKind.MemoryCards&&storageRevision!=browser.Revision;
        if(storageRevision!=browser.Revision||reset){
            if(reset){storageSelected=-1;storageAnimation?.Dispose();storageAnimation=null;storageFramesLoaded=0;Array.Clear(storageLoaded,0,storageLoaded.Length);}
            storageContent=browser.ContentRevision;storageKind=(int)browser.Kind;storageRevision=browser.Revision;storageCursor=0;
            IplItemKind previous=(IplItemKind)(-1);bool wrote=false;
            for(int i=0;i<browser.Count;i++){int cell=StorageCell(browser,i);if(storageLoaded[cell])continue;var kind=browser.ItemKind(i);if(kind!=previous){IplStorageIcons.Raster(storageCell!,kind);previous=kind;}WriteStorageCell(cell);wrote=true;}
            StorageMesh(browser);upload=wrote||storagePending>0;
        }
        if(!browser.Busy&&browser.Count>0){
            if(storageSelected!=StorageCell(browser,browser.Selection)){
                if(storageAnimation!=null&&storageFramesLoaded>0&&storageSelected>=0){storageAnimation.Read(storageCell!,0,0,IplStorageIcons.CellBytes);WriteStorageCell(storageSelected);upload=true;}
                storageAnimation?.Dispose();storageAnimation=null;storageSelected=StorageCell(browser,browser.Selection);storageFramesLoaded=0;storageFrame=0;
                storageFrames=0;
                if(browser.Kind==IplBrowserKind.MemoryCards)try{
                    storageFrames=browser.ArtworkFrames(browser.Selection);storageBounces=browser.ArtworkBounces(browser.Selection);
                    for(int f=0;f<storageFrames;f++)storageDurations[f]=browser.ArtworkDuration(browser.Selection,f);
                    if(storageFrames>0)storageAnimation=NativeBuffer.Allocate(storageFrames*IplStorageIcons.CellBytes);
                    if(storageFrames>0&&storageLoaded[storageSelected]){ReadStorageCell(storageSelected);storageAnimation!.Write(storageCell!,0,0,IplStorageIcons.CellBytes);storageFramesLoaded=1;}
                }catch(Exception){storageFrames=0;}
            }
            // One bounded card read per frame; SD uses small native file-type icons.
            if(storageFramesLoaded<storageFrames&&storageAnimation!=null){
                var image=browser.Artwork(browser.Selection,storageFramesLoaded);
                IplStorageIcons.Raster(storageCell!,browser.ItemKind(browser.Selection),image?.Bytes);
                storageAnimation.Write(storageCell!,0,storageFramesLoaded*IplStorageIcons.CellBytes,IplStorageIcons.CellBytes);
                if(storageFramesLoaded==0){WriteStorageCell(storageSelected);storageLoaded[storageSelected]=true;upload=true;}
                storageFramesLoaded++;
            }else if(browser.Kind==IplBrowserKind.MemoryCards){
                while(storageCursor<browser.Count&&storageLoaded[StorageCell(browser,storageCursor)])storageCursor++;
                if(storageCursor<browser.Count){var image=browser.Artwork(storageCursor);IplStorageIcons.Raster(storageCell!,browser.ItemKind(storageCursor),image?.Bytes);WriteStorageCell(StorageCell(browser,storageCursor));storageLoaded[StorageCell(browser,storageCursor)]=true;storageCursor++;storagePending++;if(storagePending>=8||storageCursor==browser.Count)upload=true;}
            }
            int frame=0;
            if(storageFrames>1&&!browser.ReducedMotion){int period=storageBounces?storageFrames*2-2:storageFrames,total=0;for(int n=0;n<period;n++)total+=storageDurations[n<storageFrames?n:period-n];int time=total>0?(int)(browser.AnimationTime*60)%total:0;
                for(int n=0;n<period;n++){frame=n<storageFrames?n:period-n;if(time<storageDurations[frame])break;time-=storageDurations[frame];}}
            if(frame!=storageFrame&&frame<storageFramesLoaded&&storageAnimation!=null){storageAnimation.Read(storageCell!,frame*IplStorageIcons.CellBytes,0,IplStorageIcons.CellBytes);WriteStorageCell(storageSelected);storageFrame=frame;upload=true;}
        }
        if(upload){storageAtlas!.ReplaceNative(storagePixels!);storagePending=0;}
    }
    private void DrawStorageArtwork()
    {
        if(storageMesh==null||storageAtlas==null)return;
        draw.End();renderer.Begin3D(camera);browserArtworkMaterial.Texture=storageAtlas;renderer.DrawMesh(storageMesh,browserArtworkMaterial,identity);renderer.End3D();draw.Begin();
    }
}
