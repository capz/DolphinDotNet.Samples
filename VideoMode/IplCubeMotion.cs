using System;

// Idle pose offsets shared by the glass, centered icon and projected face UI.
// Subtle preserves the original motion; Pronounced adds bounded secondary sway.
internal readonly struct IplCubeMotion
{
    internal readonly float Yaw,Pitch,Bob;
    private IplCubeMotion(float yaw,float pitch,float bob){Yaw=yaw;Pitch=pitch;Bob=bob;}
    internal static IplCubeMotion At(int level,float time)
    {
        if(level==0)return default;
        float yaw=(float)Math.Sin(time/5.3f*6.2831853f);
        float pitch=(float)Math.Sin(time/7.1f*6.2831853f);
        float bob=(float)Math.Sin(time*.7f);
        if(level==1)return new IplCubeMotion(yaw*.025f,pitch*.020f,bob);
        if(level!=2)throw new ArgumentOutOfRangeException(nameof(level));
        return new IplCubeMotion(yaw*.075f+(float)Math.Sin(time/2.2f*6.2831853f)*.012f,
            pitch*.060f+(float)Math.Sin(time/3.4f*6.2831853f)*.010f,
            bob*3+(float)Math.Sin(time*1.8f)*.5f);
    }
}
