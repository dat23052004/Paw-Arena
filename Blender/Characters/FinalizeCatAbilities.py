"""Audit the requested sixteen clips and preserve the original ten exactly."""
import bpy
import os
import sys
import json
import hashlib
import math

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import BuildCatAnimations as core
from BuildCatAbilities import CLIPS, OUTPUT, EXPORT


def digest(value):
    return hashlib.sha256(json.dumps(value,sort_keys=True).encode()).hexdigest()


def snapshot():
    rig, mesh = bpy.data.objects['RIG_Cat'], bpy.data.objects['SK_Cat']
    result = {
        'mesh':digest([[tuple(v.co), [(g.group,g.weight) for g in v.groups]] for v in mesh.data.vertices]),
        'topology':digest([tuple(p.vertices) for p in mesh.data.polygons]),
        'rig_rest':digest([(b.name,b.parent.name if b.parent else '',list(map(tuple,b.matrix_local))) for b in rig.data.bones]),
        'first_ten':{}
    }
    for name,*_ in core.CLIPS:
        action = bpy.data.actions['Cat_'+name]
        result['first_ten'][name] = digest([(fc.data_path,fc.array_index,
            [(tuple(p.co),p.interpolation) for p in fc.keyframe_points]) for fc in core.fcurves(action)])
    return result


bpy.ops.wm.open_mainfile(filepath=core.OUTPUT)
original = snapshot()
bpy.ops.wm.open_mainfile(filepath=OUTPUT)
current = snapshot()
assert current == original, 'Source mesh, rig or original ten clips changed'
rig, mesh = bpy.data.objects['RIG_Cat'], bpy.data.objects['SK_Cat']
actions = [a for a in bpy.data.actions if a.name.startswith('Cat_')]
assert len(actions) == 26
assert sum(a.name.startswith('Cat_Ability_') for a in actions) == 16
report = {'passed':True, 'total_actions':26, 'new_ability_actions':16,
          'original_ten_unchanged':True, 'mesh_weights_topology_and_rest_rig_unchanged':True,
          'fingerprints':current, 'clips':[]}
for name,duration,loop,_ in CLIPS:
    core.choose(rig,bpy.data.actions['Cat_'+name])
    min_z, max_z = 100,-100
    for frame in range(1,duration+2):
        bpy.context.scene.frame_set(frame)
        evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        surface = evaluated.to_mesh()
        assert all(math.isfinite(c) for v in surface.vertices for c in v.co)
        min_z = min(min_z,min(v.co.z for v in surface.vertices))
        max_z = max(max_z,max(v.co.z for v in surface.vertices))
        evaluated.to_mesh_clear()
        assert rig.pose.bones['Root'].head.length < .000001
    assert min_z >= -.0001, (name,'floor penetration',min_z)
    report['clips'].append({'name':name, 'checked_frames':duration+1, 'min_vertex_z_m':min_z, 'max_vertex_z_m':max_z})
with open(os.path.join(EXPORT,'Ability_Final_Audit.json'),'w',encoding='utf-8') as stream:
    json.dump(report,stream,indent=2)
guide = bpy.data.texts.get('READ ME | 16 Abilities') or bpy.data.texts.new('READ ME | 16 Abilities')
guide.clear()
with open(os.path.join(EXPORT,'HUONG_DAN_KY_NANG.md'),encoding='utf-8') as stream:
    guide.write(stream.read())
core.choose(rig,bpy.data.actions['Cat_Ability_FluffUp'])
bpy.context.scene.frame_start,bpy.context.scene.frame_end = 1,28
bpy.context.scene.frame_set(1)
bpy.data.collections['02 | REVIEW STUDIO'].hide_viewport = True
bpy.ops.wm.save_as_mainfile(filepath=OUTPUT)
print('ABILITY_FINAL_AUDIT_PASS',len(actions),flush=True)
