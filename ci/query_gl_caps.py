import ctypes as C,json
x=C.CDLL('libX11.so.6');g=C.CDLL('libGL.so.1')
x.XOpenDisplay.argtypes=[C.c_char_p];x.XOpenDisplay.restype=C.c_void_p
d=x.XOpenDisplay(None);assert d
count=C.c_int();attrs=(C.c_int*7)(0x8011,1,0x8010,4,0x8012,1,0)
g.glXChooseFBConfig.argtypes=[C.c_void_p,C.c_int,C.POINTER(C.c_int),C.POINTER(C.c_int)];g.glXChooseFBConfig.restype=C.POINTER(C.c_void_p)
f=g.glXChooseFBConfig(d,0,attrs,C.byref(count));assert count.value
config=f[0]
g.glXCreateNewContext.argtypes=[C.c_void_p,C.c_void_p,C.c_int,C.c_void_p,C.c_int];g.glXCreateNewContext.restype=C.c_void_p
ctx=g.glXCreateNewContext(d,config,0x8014,None,1);assert ctx
pattrs=(C.c_int*5)(0x8041,64,0x8040,64,0)
g.glXCreatePbuffer.argtypes=[C.c_void_p,C.c_void_p,C.POINTER(C.c_int)];g.glXCreatePbuffer.restype=C.c_ulong
p=g.glXCreatePbuffer(d,config,pattrs)
g.glXMakeContextCurrent.argtypes=[C.c_void_p,C.c_ulong,C.c_ulong,C.c_void_p];assert g.glXMakeContextCurrent(d,p,p,ctx)
g.glGetString.restype=C.c_char_p
g.glGetString.argtypes=[C.c_uint]
a=C.c_int();g.glGetIntegerv(0x8D57,C.byref(a))
r={'renderer':g.glGetString(0x1F01).decode(),'version':g.glGetString(0x1F02).decode(),'max_samples':a.value}
r['sample_shading']='GL_ARB_sample_shading' in g.glGetString(0x1F03).decode()
buffer=C.c_uint();g.glGenRenderbuffers(1,C.byref(buffer));g.glBindRenderbuffer(0x8D41,buffer)
r['requested_to_actual_samples']={}
r['texture_requested_to_actual_samples']={}
for samples in [2,4,8]:
    if samples>a.value:continue
    g.glRenderbufferStorageMultisample(0x8D41,samples,0x8058,64,64)
    resolved=C.c_int();g.glGetRenderbufferParameteriv(0x8D41,0x8CAB,C.byref(resolved))
    r['requested_to_actual_samples'][str(samples)]=resolved.value
    texture=C.c_uint();g.glGenTextures(1,C.byref(texture));g.glBindTexture(0x9100,texture)
    g.glTexImage2DMultisample(0x9100,samples,0x8058,64,64,1)
    actual=C.c_int();g.glGetTexLevelParameteriv(0x9100,0,0x9106,C.byref(actual))
    r['texture_requested_to_actual_samples'][str(samples)]=actual.value
for name,fmt in [('rgba8',0x8058),('rgb565',0x8D62),('depth32f',0x8CAC)]:
    n=C.c_int();g.glGetInternalformativ(0x9102,fmt,0x9380,1,C.byref(n));v=(C.c_int*max(1,n.value))();g.glGetInternalformativ(0x9102,fmt,0x80A9,n.value,v);r[name+'_sample_counts']=list(v)[:n.value]
r['dolphin_aa_modes']=sorted({1}|(set(r['rgba8_sample_counts'])&set(r['depth32f_sample_counts'])))
print(json.dumps(r))
