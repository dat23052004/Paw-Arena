"""Read-only round-trip audit of the skin library and original animation data."""
import bpy, os, sys, json, hashlib, math
HERE=os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0,HERE)
import BuildCatSkins as skins
import BuildCatAnimations as core

bpy.ops.wm.open_mainfile(filepath=skins.SOURCE)
original=skins.fingerprint(bpy.data.objects['SK_Cat'],bpy.data.objects['RIG_Cat'])
bpy.ops.wm.open_mainfile(filepath=skins.OUT)
mesh=bpy.data.objects['SK_Cat']; rig=bpy.data.objects['RIG_Cat']
current=skins.fingerprint(mesh,rig)
assert current==original
assert len(bpy.data.scenes)==19
report={'passed':False,'reopened_file':skins.OUT,'protected_data_exact_match':True,'scene_count':19,'skin_checks':[],'animation_checks':[]}
for idx,name in enumerate(skins.NAMES):
    s=bpy.data.scenes[f'{idx+1:02d} | {name}']
    cats=[o for o in s.objects if o.type=='MESH' and o.data==mesh.data]
    assert len(cats)==1
    cat=cats[0]
    assert cat.data.uv_layers.active.name=='Cat_Atlas'
    assert any(m.type=='ARMATURE' and m.object==rig for m in cat.modifiers)
    assert all(slot.link=='OBJECT' and slot.material['skin_name']==name for slot in cat.material_slots)
    images=[n.image for slot in cat.material_slots for n in slot.material.node_tree.nodes if n.type=='TEX_IMAGE']
    assert len(images)==2 and images[0]==images[1]
    assert images[0].packed_file and all(images[0].size)
    assert not cat.hide_render
    report['skin_checks'].append({'skin':name,'shared_mesh':True,'shared_rig':True,'independent_materials':True,'packed_texture':True})
bpy.context.window.scene=bpy.data.scenes['01 | Ginger']
scene=bpy.context.scene
rig.data.pose_position='POSE'
actions=[a for a in bpy.data.actions if a.name.startswith('Cat_')]
assert len(actions)==26
for action in actions:
    core.choose(rig,action)
    frames=sorted({int(action.frame_range[0]),int(sum(action.frame_range)/2),int(action.frame_range[1])})
    for frame in frames:
        scene.frame_set(frame)
        obj=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()); data=obj.to_mesh()
        assert len(data.vertices)==36988
        assert all(math.isfinite(c) for v in data.vertices for c in v.co)
        obj.to_mesh_clear()
    report['animation_checks'].append({'clip':action.name,'sampled_frames':frames,'finite_deformation':True})
report['passed']=True
report['source_sha256']=hashlib.sha256(open(skins.SOURCE,'rb').read()).hexdigest()
with open(os.path.join(skins.EXPORT,'Skin_RoundTrip_Audit.json'),'w',encoding='utf8') as f: json.dump(report,f,indent=2)
print('SKIN_AUDIT_PASS',len(report['skin_checks']),len(report['animation_checks']),flush=True)
