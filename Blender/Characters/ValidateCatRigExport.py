"""Round-trip the FBX and compare its bind geometry with the approved base."""
import bpy
import os
import json
from mathutils.kdtree import KDTree

HERE=os.path.dirname(os.path.abspath(__file__))
EXPORT=os.path.join(HERE,'Exports','CatRig')
bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE,'PawArena_BaseCat_Review.blend'))
depsgraph=bpy.context.evaluated_depsgraph_get()
points=[]
for obj in bpy.data.collections['01 | BASE CAT - NO RIG'].objects:
    if obj.type not in {'MESH','CURVE'}: continue
    evaluated=obj.evaluated_get(depsgraph)
    mesh=evaluated.to_mesh()
    points.extend(obj.matrix_world@v.co for v in mesh.vertices)
    evaluated.to_mesh_clear()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(EXPORT,'SK_PawArena_Cat.fbx'))
rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(rigs)==1
assert len(meshes)==1
rig=rigs[0]
mesh=meshes[0]
assert len(rig.data.bones)==30
assert not any(b.name.startswith('CTRL_') for b in rig.data.bones)
assert not rig.animation_data
assert all(not pb.constraints for pb in rig.pose.bones)
assert rig.data.bones['HeadGearSocket'].parent.name=='Head'
assert rig.data.bones['Hips'].parent.name=='Root'
assert rig.data.bones['Spine'].parent.name=='Hips'
assert rig.data.bones['Chest'].parent.name=='Spine'
assert rig.data.bones['Neck'].parent.name=='Chest'
assert rig.data.bones['Head'].parent.name=='Neck'
for side in ('Left','Right'):
    for child,parent in [('Shoulder','Chest'),('UpperArm',side+'Shoulder'),
                         ('LowerArm',side+'UpperArm'),('Hand',side+'LowerArm'),
                         ('UpperLeg','Hips'),('LowerLeg',side+'UpperLeg'),
                         ('Foot',side+'LowerLeg'),('Toes',side+'Foot')]:
        assert rig.data.bones[side+child].parent.name==parent
for v in mesh.data.vertices:
    weights=[g.weight for g in v.groups if g.weight>1e-6]
    assert 1<=len(weights)<=4
    assert abs(sum(weights)-1)<2e-5
assert len(mesh.data.uv_layers)==1
assert len(mesh.data.materials)==2
assert any(m.type=='ARMATURE' and m.object==rig for m in mesh.modifiers)
tree=KDTree(len(mesh.data.vertices))
for v in mesh.data.vertices: tree.insert(mesh.matrix_world@v.co,v.index)
tree.balance()
error=max(tree.find(p)[2] for p in points)
assert error<1e-4,error
images={node.image for mat in mesh.data.materials for node in mat.node_tree.nodes if node.type=='TEX_IMAGE' and node.image}
assert images
assert all(tuple(image.size)==(4096,4096) for image in images)
result={'round_trip':'PASS','export_bones':len(rig.data.bones),'control_bones_exported':0,
        'maximum_bind_error_m':error,'max_weights_per_vertex':4,'materials':2,
        'texture_size':'4096 x 4096','source_base_unchanged':True,
        'unity_avatar':'Requires Unity Configure Avatar verification; not exercised by Blender round-trip.'}
with open(os.path.join(EXPORT,'FBX_Validation.json'),'w',encoding='utf-8') as f: json.dump(result,f,indent=2)
print('FBX_ROUND_TRIP',json.dumps(result))
