using Dolphin.System;

// Pure navigation shared with host tests; resource creation stays in Program.
internal sealed class IplSelection
{
    internal static readonly string[] Names={"Startup","Turn side","Turn center","Enter menu","Back","Cancel edit","Confirm edit","Change value","Change field","Change row","Open editor"};
    internal static readonly SystemMenuSound[] Effects={SystemMenuSound.TurnSide,SystemMenuSound.TurnCenter,SystemMenuSound.Select,SystemMenuSound.Back,SystemMenuSound.OptionCancel,SystemMenuSound.OptionConfirm,SystemMenuSound.OptionMove1,SystemMenuSound.OptionMove2,SystemMenuSound.OptionMove3,SystemMenuSound.OptionSelect};
    internal static readonly SystemTexture[] Textures={SystemTexture.LogoTop,SystemTexture.LogoLeft,SystemTexture.LogoRight,SystemTexture.Environment,SystemTexture.Tiles,SystemTexture.Wordmark};
    internal static readonly string[] TextureNames={"Logo top","Logo left","Logo right","Environment","Tiles","Wordmark"};
    internal int Selected {get;private set;}
    internal int Texture {get;private set;}
    internal void Move(int direction)=>Selected=(Selected+direction+Names.Length)%Names.Length;
    internal void NextTexture()=>Texture=(Texture+1)%Textures.Length;
}
