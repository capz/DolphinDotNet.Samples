using System;

// Authored optical lookups, evaluated once while creating native textures.
// Packed colors are RRGGBBAA. No arrays, allocations or per-frame evaluation.
internal static class IplGlassEnvironment
{
    internal static uint Reflection(float u,float v,bool studio,bool inner)
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
        float crown=Band(v,.325f,.015f,.012f)*Window(u,.13f,.88f,.035f);
        float crownReturn=Band(v,.358f,.002f,.003f)*Window(u,.10f,.90f,.03f);
        float leftOuter=Band(u,.165f,.003f,.004f)*Window(v,.08f,.98f,.025f);
        float rightOuter=Band(u,.955f,.003f,.004f)*Window(v,.08f,.98f,.025f);
        float rightRear=Band(u,.835f,.002f,.003f)*Window(v,.12f,.94f,.03f);
        float bottomOuter=Band(v,.982f,.0035f,.003f)*Window(u,.08f,.95f,.03f);
        float bottomRear=Band(v,.725f,.002f,.003f)*Window(u,.12f,.90f,.03f);
        float edge=Band(u,.030f,.002f,.003f)+Band(u,.970f,.002f,.003f);
        float shoulder=Band(v,.325f,.027f,.020f)*Window(u,.10f,.93f,.03f)+
            Band(u,.165f,.018f,.020f)*Window(v,.05f,.99f,.03f)+
            Band(u,.955f,.018f,.020f)*Window(v,.05f,.99f,.03f)+
            Band(v,.975f,.020f,.020f)*Window(u,.10f,.94f,.03f);
        float glint=Blob(u,v,.75f,.070f,.16f,.021f);
        float warmCorner=Blob(u,v,.080f,.900f,.050f,.065f);
        float coolCorner=Blob(u,v,.923f,.104f,.037f,.065f);
        float warm=1-u,cool=u;
        float silver=shoulder*28+edge*70+top*215+left*110+right*180+bottom*100+glint*65;
        float r=7+silver+crownReturn*75+leftOuter*80+rightOuter*145+rightRear*40+bottomOuter*65+bottomRear*65+topReturn*90+topBlue*23+leftReturn*47+rightReturn*20+
            bottomReturn*(warm*105+cool*30)+bottomBlue*28+warmCorner*115+coolCorner*65;
        float g=10+silver+crownReturn*110+leftOuter*140+rightOuter*185+rightRear*55+bottomOuter*95+bottomRear*65+topReturn*100+topBlue*32+leftReturn*32+rightReturn*76+
            bottomReturn*(warm*67+cool*96)+bottomBlue*29+warmCorner*76+coolCorner*110;
        float b=14+silver+crownReturn*120+leftOuter*190+rightOuter*190+rightRear*130+bottomOuter*115+bottomRear*125+topReturn*112+topBlue*99+leftReturn*112+rightReturn*96+
            bottomReturn*(warm*24+cool*80)+bottomBlue*115+warmCorner*29+coolCorner*116;
        // Reserve half of the LDR range for the ordinary studio environment.
        // Native TEV restores baseline exposure after mixing, while full-range
        // corner sources receive twice their former reflection exposure.
        r*=.5f;g*=.5f;b*=.5f;
        // The first fully covered native top pixel samples V about .325.
        // Expose a finite crown source there; the old .265 line fell mostly
        // between raster samples at the grazing silhouette.
        float crownSource=crown*(110+145*Blob(u,v,.592f,.325f,.020f,.040f));
        r=Math.Max(r,crownSource);g=Math.Max(g,crownSource);b=Math.Max(b,crownSource);
        // Curved, paired returns from the near roundovers. Their elliptical
        // levels couple both normal axes; finite windows keep them localized
        // rather than drawing another rectangular contour around the body.
        CornerReturn(u,v,.31f,.435f,.53f,.70f,.36f,.44f,.09f,.055f,160,false,true,ref r,ref g,ref b);
        CornerReturn(u,v,.82f,.435f,.60f,.70f,.36f,.44f,.09f,.055f,172,true,true,ref r,ref g,ref b);
        CornerReturn(u,v,.31f,.82f,.53f,.54f,.36f,.44f,.09f,.055f,164,false,true,ref r,ref g,ref b);
        CornerReturn(u,v,.82f,.82f,.60f,.54f,.36f,.44f,.09f,.055f,160,true,true,ref r,ref g,ref b);
        // Opposing cavity curves pass through the mapped diagonal/roundover
        // anchors, rather than producing a single spot at the window center.
        CornerReturn(u,v,.620f,.745f,.38656f,.90161f,.28110f,.28110f,.070f,.075f,180,true,false,ref r,ref g,ref b);
        CornerReturn(u,v,.255f,.745f,.48844f,.90161f,.28110f,.28110f,.070f,.075f,180,false,false,ref r,ref g,ref b);
        CornerReturn(u,v,.620f,.025f,.60307f,-.08521f,.11151f,.11151f,.065f,.040f,175,false,false,ref r,ref g,ref b);
        CornerReturn(u,v,.255f,.025f,.27193f,-.08521f,.11151f,.11151f,.065f,.040f,175,true,false,ref r,ref g,ref b);
        float fieldVisibility=studio?StudioReflection(u,v,inner,ref r,ref g,ref b):1;
        return (Color(r,g,b)&0xffffff00u)|(uint)(255*fieldVisibility+.5f);
    }

    // The separate position lookup is composed independently of Fresnel. It
    // represents transmitted/collected colored light, leaving the center dark.
    // Position texgen .25*viewPosition+.5 corresponds to view range [-2,+2].
    internal static uint Field(float viewX,float viewY)
    {
        float teal=Blob(viewX,viewY,.80f,.75f,.90f,1.05f);
        float amber=Blob(viewX,viewY,-.84f,-.14f,.88f,1.05f);
        float violet=Blob(viewX,viewY,-.23f,-.90f,.75f,.72f);
        float blue=Blob(viewX,viewY,.60f,-.75f,.80f,.60f);
        float interior=1-.70f*Smooth((Math.Max(Math.Abs(viewX),Math.Abs(viewY))-.90f)/.10f);
        return Color(interior*(5+teal*83+amber*136+violet*80+blue*4),
                     interior*(9+teal*165+amber*72+violet*45+blue*24),
                     interior*(13+teal*150+amber*16+violet*160+blue*140));
    }

    private static void CornerReturn(float u,float v,float featureU,float featureV,
        float centerU,float centerV,float radiusU,float radiusV,float windowU,float windowV,
        float strength,bool cyan,bool near,ref float r,ref float g,ref float b)
    {
        float x=(u-centerU)/radiusU,y=(v-centerV)/radiusV,q=x*x+y*y;
        float envelope=Blob(u,v,featureU,featureV,windowU,windowV)*Window(v,.004f,.996f,.006f);
        // The near normal domain ends around q=1 at the contour. Its paired
        // sources must sit inside that domain to be visible across the curved
        // wall; the fitted far ellipses use their own independent levels.
        float core=envelope*Band(q,near?.94f:1,.025f,.045f)*strength;
        float secondary=envelope*Band(q,near?.64f:1.13f,.025f,.040f);
        float outer=near?envelope*Band(q,.985f,.012f,.025f)*22:0;
        float haze=envelope*18;
        // Near secondary returns carry a neutral source with restrained color,
        // rather than a low-intensity saturated line that Fresnel would erase.
        float sr=near?(cyan?104:112):(cyan?22:42);
        float sg=near?(cyan?111:106):(cyan?48:30);
        float sb=near?(cyan?115:118):(cyan?58:65);
        r=Math.Max(r,core*.99f+secondary*sr+haze+outer);
        g=Math.Max(g,core+secondary*sg+haze*.95f+outer);
        b=Math.Max(b,core*1.01f+secondary*sb+haze*1.05f+outer);

    }

    private static float StudioReflection(float u,float v,bool inner,ref float r,ref float g,ref float b)
    {
        // A finite white softbox, its dark divider and an occluding ceiling
        // return. Existing normal/depth texgen bends this environment feature
        // around the upper-right roundover as the whole object turns.
        float sx=(u-.807f)/.043f,sy=(v-.447f)/.028f;
        float x=sx+.22f*sy,y=sy-.12f*sx;
        float dx=Math.Max(0,Math.Abs(x)-.78f),dy=Math.Max(0,Math.Abs(y)-.78f);
        float pane=(1-Smooth((dx*dx+dy*dy-.0484f)/.020f))*Window(v,.405f,.482f,.005f);
        float divider=Band(x,.13f,.045f,.035f);
        float ceiling=Smooth((x+y-.50f)/.12f)*.90f;
        float dark=pane*Math.Max(divider,ceiling),light=pane*(1-Math.Max(divider,ceiling));
        r=Math.Max(r,light*121);g=Math.Max(g,light*125);b=Math.Max(b,light*128);
        r=r*(1-.92f*dark)+3*dark;g=g*(1-.92f*dark)+5*dark;b=b*(1-.92f*dark)+8*dark;
        // The broad white ceiling reflection farther along the same crown.
        // Its finite footprint avoids turning the whole top edge into a line.
        float cx=(u-.599f)/.020f+.10f*(v-.360f)/.050f,cy=(v-.360f)/.050f;
        dx=Math.Max(0,Math.Abs(cx)-.78f);dy=Math.Max(0,Math.Abs(cy)-.78f);
        float ceilingSource=1-Smooth((dx*dx+dy*dy-.0484f)/.020f);
        r=Math.Max(r,ceilingSource*246);g=Math.Max(g,ceilingSource*250);b=Math.Max(b,ceilingSource*255);
        float bentCeiling=Blob(u,v,.663f,.409f,.035f,.023f);
        r=Math.Max(r,bentCeiling*246);g=Math.Max(g,bentCeiling*250);b=Math.Max(b,bentCeiling*255);
        // Soft side lights catch the near cavity fillets; finite falloff along
        // V makes them local reflections rather than a uniform inset contour.
        float rightLip=inner?Blob(u,v,.906f,.622f,.023f,.110f):0;
        float leftLip=inner?Blob(u,v,.218f,.622f,.023f,.110f):0;
        r=Math.Max(r,Math.Max(rightLip*90,leftLip*45));
        g=Math.Max(g,Math.Max(rightLip*100,leftLip*55));
        b=Math.Max(b,Math.Max(rightLip*135,leftLip*85));
        // A darker environment return under the white crown must also shade
        // collected colored light. Alpha carries that visibility separately
        // from the material's unchanged Fresnel opacity.
        float fold=Blob(u,v,.790f,.548f,.038f,.065f);
        r=r*(1-.95f*fold)+3*fold;g=g*(1-.95f*fold)+5*fold;b=b*(1-.95f*fold)+8*fold;
        return 1-.97f*Math.Max(dark,fold);
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
