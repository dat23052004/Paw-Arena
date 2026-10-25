import bpy
import os

bpy.ops.wm.open_mainfile(filepath=os.path.join(os.path.dirname(__file__),'PawArena_Cat_Rigged.blend'))
bpy.context.scene.frame_set(100)
mesh=bpy.data.objects['SK_Cat']
evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
posed=evaluated.to_mesh()
rank=[]
for edge in mesh.data.edges:
    a,b=edge.vertices
    rest=(mesh.data.vertices[a].co-mesh.data.vertices[b].co).length
    dist=(posed.vertices[a].co-posed.vertices[b].co).length
    if rest>1e-5: rank.append((dist/rest,dist,a,b))
for ratio,dist,a,b in sorted(rank,reverse=True)[:12]:
    print('EDGE',ratio,dist)
    for i in (a,b):
        v=mesh.data.vertices[i]
        print(i,tuple(round(x,4) for x in v.co),[(mesh.vertex_groups[g.group].name,round(g.weight,4)) for g in v.groups])
evaluated.to_mesh_clear()
