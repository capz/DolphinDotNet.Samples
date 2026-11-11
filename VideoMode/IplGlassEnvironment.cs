using System;

// Authored optical lookups, evaluated once while creating native textures.
// Packed colors are RRGGBBAA. No arrays, allocations or per-frame evaluation.
internal static class IplGlassEnvironment
{
    internal static uint Reflection(float u,float v)
    {
        // Narrow light sources and their dimmer secondary returns. These are
        // normal-space studio reflections, not lines painted onto cube edges.
        float top=Band(v,.070f,.006f,.004f)*Window(u,.12f,.91f,.025f);
        float topReturn=Band(v,.092f,.0025f,.002f)*Window(u,.09f,.94f,.03f);
        float topBlue=Band(v,.120f,.002f,.003f)*Window(u,.13f,.89f,.03f);
        float left=Band(u,.070f,.004f,.003f)*Window(v,.12f,.88f,.035f);
        float leftReturn=Band(u,.089f,.002f,.002f)*Window(v,.10f,.91f,.035f);
        float right=Band(u,.930f,.005f,.003f)*Window(v,.10f,.88f,.035f);
        float rightReturn=Band(u,.906f,.002f,.003f)*Window(v,.13f,.92f,.03f);
        float bottom=Band(v,.930f,.0045f,.003f)*Window(u,.10f,.91f,.03f);
        float bottomReturn=Band(v,.907f,.002f,.003f)*Window(u,.10f,.92f,.03f);
        float bottomBlue=Band(v,.880f,.002f,.003f)*Window(u,.15f,.85f,.03f);
        float crown=Band(v,.265f,.015f,.012f)*Window(u,.13f,.88f,.035f);
        float crownReturn=Band(v,.298f,.002f,.003f)*Window(u,.10f,.90f,.03f);
        float leftOuter=Band(u,.165f,.003f,.004f)*Window(v,.08f,.98f,.025f);
        float rightOuter=Band(u,.955f,.003f,.004f)*Window(v,.08f,.98f,.025f);
        float rightRear=Band(u,.835f,.002f,.003f)*Window(v,.12f,.94f,.03f);
        float bottomOuter=Band(v,.982f,.0035f,.003f)*Window(u,.08f,.95f,.03f);
        float bottomRear=Band(v,.725f,.002f,.003f)*Window(u,.12f,.90f,.03f);
        float edge=Band(u,.030f,.002f,.003f)+Band(u,.970f,.002f,.003f);
        float shoulder=Band(v,.265f,.027f,.020f)*Window(u,.10f,.93f,.03f)+
            Band(u,.165f,.018f,.020f)*Window(v,.05f,.99f,.03f)+
            Band(u,.955f,.018f,.020f)*Window(v,.05f,.99f,.03f)+
            Band(v,.975f,.020f,.020f)*Window(u,.10f,.94f,.03f);
        float upperRight=Blob(u,v,.85f,.39f,.065f,.085f);
        float upperLeft=Blob(u,v,.28f,.39f,.055f,.080f);
        float lowerRight=Blob(u,v,.85f,.85f,.065f,.075f);
        float lowerLeft=Blob(u,v,.28f,.85f,.055f,.075f);
        float glint=Blob(u,v,.75f,.070f,.16f,.021f);
        float warmCorner=Blob(u,v,.080f,.900f,.050f,.065f);
        float coolCorner=Blob(u,v,.923f,.104f,.037f,.065f);
        float warm=1-u,cool=u;
        float silver=shoulder*28+crown*(95+140*Blob(u,v,.78f,.265f,.28f,.07f))+edge*70+top*215+left*110+right*180+bottom*100+glint*65;
        float r=7+silver+upperRight*170+upperLeft*150+lowerRight*95+lowerLeft*170+crownReturn*75+leftOuter*80+rightOuter*145+rightRear*40+bottomOuter*65+bottomRear*65+topReturn*90+topBlue*23+leftReturn*47+rightReturn*20+
            bottomReturn*(warm*105+cool*30)+bottomBlue*28+warmCorner*115+coolCorner*65;
        float g=10+silver+upperRight*195+upperLeft*140+lowerRight*150+lowerLeft*130+crownReturn*110+leftOuter*140+rightOuter*185+rightRear*55+bottomOuter*95+bottomRear*65+topReturn*100+topBlue*32+leftReturn*32+rightReturn*76+
            bottomReturn*(warm*67+cool*96)+bottomBlue*29+warmCorner*76+coolCorner*110;
        float b=14+silver+upperRight*200+upperLeft*170+lowerRight*170+lowerLeft*105+crownReturn*120+leftOuter*190+rightOuter*190+rightRear*130+bottomOuter*115+bottomRear*125+topReturn*112+topBlue*99+leftReturn*112+rightReturn*96+
            bottomReturn*(warm*24+cool*80)+bottomBlue*115+warmCorner*29+coolCorner*116;
        return Color(r,g,b);
    }

    // The separate position lookup is composed independently of Fresnel. It
    // represents transmitted/collected colored light, leaving the center dark.
    // Position texgen .25*viewPosition+.5 corresponds to view range [-2,+2].
    internal static uint Field(float viewX,float viewY)
    {
        float teal=Blob(viewX,viewY,.90f,.80f,.65f,1.05f);
        float amber=Blob(viewX,viewY,-.99f,-.18f,.88f,1.30f);
        float violet=Blob(viewX,viewY,.15f,-.90f,1.06f,.72f);
        float blue=Blob(viewX,viewY,.85f,-.72f,.70f,.53f);
        float interior=1-.70f*Smooth((Math.Max(Math.Abs(viewX),Math.Abs(viewY))-1.12f)/.21f);
        return Color(interior*(5+teal*77+amber*143+violet*85+blue*4),
                     interior*(9+teal*190+amber*81+violet*45+blue*24),
                     interior*(13+teal*172+amber*16+violet*190+blue*58));
    }

    private static float Blob(float x,float y,float cx,float cy,float rx,float ry)
    {
        float dx=(x-cx)/rx,dy=(y-cy)/ry;
        float q=Math.Max(0,1-dx*dx-dy*dy);
        return q*q;
    }
    private static float Smooth(float t)
    {t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
    private static float Band(float x,float center,float halfWidth,float feather)
    {return 1-Smooth((Math.Abs(x-center)-halfWidth)/feather);}
    private static float Window(float x,float lo,float hi,float feather)
    {return Smooth((x-lo)/feather)*Smooth((hi-x)/feather);}
    private static uint Color(float r,float g,float b)
    {
        int red=Math.Max(0,Math.Min(255,(int)(r+.5f)));
        int green=Math.Max(0,Math.Min(255,(int)(g+.5f)));
        int blue=Math.Max(0,Math.Min(255,(int)(b+.5f)));
        return ((uint)red<<24)|((uint)green<<16)|((uint)blue<<8)|255u;
    }
}
