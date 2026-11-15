using Dolphin.System;

// Pure navigation shared with host tests; resource creation stays in Program.
internal sealed class IplSelection
{
    internal static readonly string[] Categories={"STARTUP","MENU","CARDS","AMBIENCE","SAMPLES"};
    internal const int VisibleRows=6;
    internal static readonly string[] Names={"Normal","One controller Z","Four controller Z","Turn side","Turn center","Enter menu","Back","Cancel edit","Confirm edit","Change value","Change field","Change row","Open editor"};
    internal static readonly SystemMenuSound[] Effects={SystemMenuSound.TurnSide,SystemMenuSound.TurnCenter,SystemMenuSound.Select,SystemMenuSound.Back,SystemMenuSound.OptionCancel,SystemMenuSound.OptionConfirm,SystemMenuSound.OptionMove1,SystemMenuSound.OptionMove2,SystemMenuSound.OptionMove3,SystemMenuSound.OptionSelect};
    internal static readonly SystemTexture[] Textures={SystemTexture.LogoTop,SystemTexture.LogoLeft,SystemTexture.LogoRight,SystemTexture.Environment,SystemTexture.Tiles,SystemTexture.Wordmark};
    internal static readonly string[] TextureNames={"Logo top","Logo left","Logo right","Environment","Tiles","Wordmark"};
    private readonly int[] selected=new int[5];
    private readonly int[] counts={3,10,0,0,0};
    internal int Category {get;private set;}
    internal int Selected=>selected[Category];
    internal int Count=>counts[Category];
    internal int FirstVisible=>Selected/VisibleRows*VisibleRows;
    internal int Texture {get;private set;}
    internal void SetSampleCount(int count)
    {counts[4]=count;if(selected[4]>=count)selected[4]=0;}
    internal void Move(int direction)
    {int count=Count;if(count>0)selected[Category]=(Selected+direction+count)%count;}
    internal void NextCategory(int direction)=>Category=(Category+direction+Categories.Length)%Categories.Length;
    internal void NextTexture()=>Texture=(Texture+1)%Textures.Length;
}
