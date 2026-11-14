// Native AA is useful for the 3D cube, but its shorter EFB softens flat text.
// Keep page selection separate from SDK display changes, which happen between
// frames. An allocation failure disables further AA requests for this session.
internal sealed class IplAntialiasingPolicy
{
    internal bool Enabled {get;private set;}
    internal bool CubeVisible {get;private set;}=true;
    internal IplAntialiasingPolicy(bool enabled){Enabled=enabled;}
    internal bool Wanted(VideoModeSettings video)=>Enabled&&CubeVisible&&video.Resolution!=2;
    internal bool SetCubeVisible(bool visible)
    {
        if(CubeVisible==visible)return false;
        CubeVisible=visible;return true;
    }
    internal void Disable(){Enabled=false;}
}
