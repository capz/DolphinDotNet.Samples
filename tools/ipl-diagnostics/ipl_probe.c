#include <gccore.h>
#include <ogc/exi.h>
#include <ogc/lwp_watchdog.h>
#include <stdio.h>
#include "dnd_console.h"
#include "dnd_graphics.h"
#include "dnd_platform.h"
#include "dnd_system_firmware.h"
#include "ipl_analysis.h"

#ifndef IPL_DIAG_SDK_VERSION
#define IPL_DIAG_SDK_VERSION "UNKNOWN"
#endif
static uint8_t rom[IPL_BYTES] ATTRIBUTE_ALIGN(32);
// DMA storage has static lifetime, including every timeout/error path.
static uint8_t page[1024] ATTRIBUTE_ALIGN(32);
static char results[5][79];
static char revision[79]="HEADER UNAVAILABLE";
static const char *labels[]={"SDK32","DMA32","IMM16","DMA16","AGAIN"};

static void frame(void)
{
    dnd_graphics_begin_frame(.025f,.035f,.06f,1);
    dnd_graphics_begin_overlay();dnd_console_render();dnd_graphics_end_frame();
}
static uint64_t now_us(void){return ticks_to_microsecs(gettime());}
static void reg_write(unsigned reg,uint32_t value)
{
    ((volatile uint32_t*)0xcc006800)[reg]=value;
    __asm__ volatile("eieio" ::: "memory");
}
static uint32_t reg_read(unsigned reg){return ((volatile uint32_t*)0xcc006800)[reg];}
static int wait_transfer(uint64_t deadline)
{
    for(unsigned polls=0;polls<1000000;polls++){
        if(!(reg_read(3)&1))return 1;
        if(now_us()>=deadline)return 0;
    }
    return 0;
}

// Bounded equivalents of libogc's ROM transaction, comparing DMA and immediate
// reads. Never call __SYS_ReadROM: its retry loop has no deadline.
static int alternate_page(unsigned offset,int dma,int speed)
{
    if(!EXI_Lock(EXI_CHANNEL_0,EXI_DEVICE_1,NULL))return -1;
    int selected=0,result=-2;
    uint64_t deadline=now_us()+20000;
    if(!EXI_Select(EXI_CHANNEL_0,EXI_DEVICE_1,speed)){result=-1;goto cleanup;}
    selected=1;
    reg_write(4,offset<<6);reg_write(3,0x35);
    if(!wait_transfer(deadline))goto cleanup;
    if(dma){
        DCInvalidateRange(page,sizeof(page));
        reg_write(1,(uint32_t)MEM_VIRTUAL_TO_PHYSICAL(page));
        reg_write(2,sizeof(page));reg_write(3,3);
        if(!wait_transfer(deadline))goto cleanup;
        __asm__ volatile("sync" ::: "memory");
    }else{
        for(unsigned at=0;at<sizeof(page);at+=4){
            reg_write(4,0xffffffffu);reg_write(3,0x31);
            if(!wait_transfer(deadline))goto cleanup;
            uint32_t value=reg_read(4);
            page[at]=value>>24;page[at+1]=value>>16;
            page[at+2]=value>>8;page[at+3]=value;
        }
    }
    result=sizeof(page);
cleanup:
    if(selected){
        if(result<0)reg_write(3,0);
        if(!EXI_Deselect(EXI_CHANNEL_0)&&result>=0)result=-1;
    }
    EXI_Unlock(EXI_CHANNEL_0);
    return result;
}

static void read_mode(unsigned mode)
{
    char text[79];
    snprintf(text,sizeof(text),"READING %s - PLEASE WAIT",labels[mode]);
    dnd_console_write_line(text);frame();
    unsigned quantum=(mode==0||mode==4)?4096:1024;
    for(unsigned at=0;at<IPL_BYTES;at+=quantum){
        int result;
        if(mode==0||mode==4)result=dnd_firmware_read_raw(rom+at,at,quantum);
        else{
            result=alternate_page(at,mode!=2,mode==1?EXI_SPEED32MHZ:EXI_SPEED16MHZ);
            if(result==(int)quantum)memcpy(rom+at,page,quantum);
        }
        if(result!=(int)quantum){
            snprintf(results[mode],sizeof(results[mode]),"%-5s ERROR %d AT %06X",labels[mode],result,at);
            memset(rom,0,sizeof(rom));memset(page,0,sizeof(page));
            return;
        }
        if(!(at&0xffff))frame();
    }
    if(mode==0){
        unsigned end=0;while(end<256&&rom[end])end++;
        if(end<8||end==256)snprintf(revision,sizeof(revision),"HEADER INVALID - ROM NOT IDENTIFIED");
        else{
            unsigned start=end>52?end-52:0;
            unsigned i=0;
            for(;start+i<end;i++){
                unsigned c=rom[start+i];revision[i]=c>=32&&c<127?(char)c:' ';
            }
            revision[i]=0;
        }
    }
    uint32_t raw=ipl_crc32(rom,IPL_BYTES),plain=ipl_crc32(rom+0x100,IPL_BYTES-0x100);
    unsigned before=ipl_yay0_count(rom);
    const char *profile=ipl_profile(plain);char encoding='P';
    ipl_descramble(rom);
    uint32_t decoded=ipl_crc32(rom+0x100,IPL_BYTES-0x100);
    unsigned after=ipl_yay0_count(rom);
    if(!strcmp(profile,"UNKNOWN")){profile=ipl_profile(decoded);encoding='S';}
    if(!strcmp(profile,"UNKNOWN"))encoding='?';
    snprintf(results[mode],sizeof(results[mode]),"%-5s %08X %08X %02u/%02u %s/%c",labels[mode],raw,decoded,before,after,profile,encoding);
    // No raw bytes or extracted assets are displayed, persisted or sent.
    memset(rom,0,sizeof(rom));memset(page,0,sizeof(page));
}

void dnd_ipl_probe_run(intptr_t cipher_result)
{
    dnd_console_set_visible(1);
    for(unsigned mode=0;mode<5;mode++)read_mode(mode);
    dnd_console_init();
    char text[79];
    snprintf(text,sizeof(text),"IPL CHECK 01 - SDK %s - CIPHER %s %ld",IPL_DIAG_SDK_VERSION,cipher_result?"FAIL":"PASS",(long)cipher_result);
    dnd_console_write_line(text);
    dnd_console_write_line(revision);
    dnd_console_write_line("MODE  ROM CRC  XOR CRC  YAY P/S PROFILE");
    for(unsigned mode=0;mode<5;mode++)dnd_console_write_line(results[mode]);
    dnd_console_write_line("P PLAIN  S SCRAMBLED  ? UNKNOWN - START EXIT");
    dnd_platform_wait_forever();
}
