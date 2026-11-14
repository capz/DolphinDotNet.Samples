#ifndef IPL_ANALYSIS_H
#define IPL_ANALYSIS_H
#include <stdint.h>
#include <string.h>

#define IPL_BYTES 0x200000u
#define IPL_CIPHER_END 0x1aff00u

static uint32_t ipl_crc32(const uint8_t *bytes,unsigned count)
{
    uint32_t table[256];
    for(unsigned i=0;i<256;i++){
        uint32_t v=i;
        for(unsigned bit=0;bit<8;bit++)v=(v>>1)^((v&1)?0xedb88320u:0);
        table[i]=v;
    }
    uint32_t crc=0xffffffffu;
    for(unsigned i=0;i<count;i++)crc=(crc>>8)^table[(crc^bytes[i])&255];
    return ~crc;
}

// IPL cipher reversed by Segher Boessenkool, copyright 2008.
// Same MIT-licensed reference as the SDK; see LICENSE.cipher.txt.
static void ipl_descramble(uint8_t *bytes)
{
    uint16_t t=0x2953,u=0xd9c2,v=0x3ff1;
    unsigned x=1;
    for(unsigned at=0x100;at<IPL_CIPHER_END;at++){
        unsigned acc=0;
        for(unsigned bit=0;bit<8;bit++){
            unsigned t0=t&1,t1=(t>>1)&1,u0=u&1,u1=(u>>1)&1,v0=v&1;
            x^=t1^v0^(u0|u1)^((t0^u1^v0)&(t0^u0));
            if(t0==u0){v>>=1;if(v0)v^=0xb3d0;}
            if(!t0){u>>=1;if(u0)u^=0xfb10;}
            t>>=1;if(t0)t^=0xa740;
            acc=(acc<<1)|x;
        }
        bytes[at]^=(uint8_t)acc;
    }
}

// CRC-32 over decoded [0x100, 0x200000), checked against the same three private
// reference revisions used for SDK validation. Unknown is not proof of damage:
// other legitimate revisions are not represented in this small diagnostic map.
// These fingerprints contain no firmware or resources.
static const char *ipl_profile(uint32_t crc)
{
    switch(crc){
    case 0x8f87bdb8:return "NTSC10";
    case 0xf4fa4503:return "NTSC12";
    case 0x23072730:return "PAL12";
    default:return "UNKNOWN";
    }
}

// Signature counts are diagnostic hints, not validation or SDK acceptance.
static unsigned ipl_yay0_count(const uint8_t *bytes)
{
    unsigned count=0;
    for(unsigned at=0x100;at+4<=IPL_CIPHER_END;at++)
        if(!memcmp(bytes+at,"Yay0",4))count++;
    return count;
}
#endif
