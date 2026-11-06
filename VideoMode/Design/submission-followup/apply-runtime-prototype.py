"""Controlled .217 native-bundle backport of the SDK color-index prototype.
Never modifies installed packages. Pass a COPIED --bundle and the SDK PR checkout.
The old draw_state counts bindings internally; the new SDK counts at GX_Begin.
"""
import argparse
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--bundle',type=Path,required=True);p.add_argument('--sdk',type=Path,required=True);a=p.parse_args()
root=a.bundle
if 'dnd_draw_backend_mesh_colors' in (root/'include/dnd_draw.h').read_text():raise SystemExit('Already patched')
inc=(a.sdk/'include/dnd_indexed_mesh.inc').read_text().replace('        dnd_performance_add_draw(start==0?state.bindings:0);\n','')
(root/'include/dnd_indexed_mesh.inc').write_text(inc)
for rel in ('include/dnd_draw.h','source/draw_resources.c','source/graphics_gx.c'):
 f=root/rel;s=f.read_text()
 if rel.endswith('.h'):
  at=s.index('void dnd_draw_backend_mesh_changed(void);');s=s[:at]+'bool dnd_draw_backend_mesh_colors(const DndDrawVertex*,const uint16_t*,int,const DndDrawTexture*,const DndDrawMaterial*,const float*);\n'+s[at:]
 else:
  stub='bool dnd_draw_backend_mesh_colors(const DndDrawVertex *v,const uint16_t *i,int n,const DndDrawTexture *t,const DndDrawMaterial *m,const float *w){(void)v;(void)i;(void)n;(void)t;(void)m;(void)w;return false;}\n'
  if 'resources' in rel:
   at=s.index('__attribute__((weak)) bool dnd_draw_backend_mesh(');s=s[:at]+'__attribute__((weak)) '+stub+s[at:]
   old='if(r->uniform_color&&dnd_draw_backend_mesh(v,r->indices?idx:NULL,count,r->mesh_color,tex?&tx:NULL,&material,transform))'
   if old not in s:raise SystemExit('Expected .217 mesh dispatch')
   s=s.replace(old,'if(r->uniform_color?dnd_draw_backend_mesh(v,r->indices?idx:NULL,count,r->mesh_color,tex?&tx:NULL,&material,transform):dnd_draw_backend_mesh_colors(v,r->indices?idx:NULL,count,tex?&tx:NULL,&material,transform))')
  else:
   at=s.index('bool dnd_draw_backend_mesh(const DndDrawVertex *v');s=s[:at]+stub+s[at:]
 f.write_text(s)
