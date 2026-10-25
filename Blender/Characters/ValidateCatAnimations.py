"""Round-trip every animation-only FBX and compare bone trajectories to Blender."""
import bpy
import json
import os
import sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from BuildCatAnimations import FPS, choose
if '--abilities' in sys.argv:
    from BuildCatAbilities import CLIPS, EXPORT, OUTPUT
else:
    from BuildCatAnimations import CLIPS, EXPORT, OUTPUT

bpy.ops.wm.open_mainfile(filepath=OUTPUT)
rig = bpy.data.objects['RIG_Cat']
expected = {}
for name, duration, loop, representative in CLIPS:
    choose(rig, bpy.data.actions['Cat_' + name])
    poses = {}
    for frame in sorted({1, representative, duration // 2 + 1, duration + 1}):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        poses[frame] = {pb.name: tuple(rig.matrix_world @ pb.head) for pb in rig.pose.bones
                        if not pb.name.startswith('CTRL_')}
    expected[name] = poses

report = {'status': 'PASS', 'fps': FPS, 'clips': []}
for name, duration, loop, representative in CLIPS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    bpy.ops.import_scene.fbx(filepath=os.path.join(EXPORT, 'AN_Cat_' + name + '.fbx'), anim_offset=0)
    rigs = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
    assert len(rigs) == 1, name
    imported = rigs[0]
    # Blender's FBX importer auto-connects a sole child (Root -> Hips), which
    # suppresses valid animated Hips translation. FBX/Unity have no such flag.
    bpy.context.view_layer.objects.active = imported
    imported.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for bone in imported.data.edit_bones:
        bone.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    assert len(imported.data.bones) == 30
    assert not any(b.name.startswith('CTRL_') for b in imported.data.bones)
    assert imported.animation_data and imported.animation_data.action
    action = imported.animation_data.action
    assert abs(action.frame_range[0] - 1) < .01, (name, list(action.frame_range))
    assert abs(action.frame_range[1] - (duration + 1)) < .01, (name, list(action.frame_range))
    error = 0
    worst = None
    for frame, positions in expected[name].items():
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        for bone, point in positions.items():
            actual = imported.matrix_world @ imported.pose.bones[bone].head
            distance = (actual - Vector(point)).length
            if distance > error:
                error = distance
                worst = (frame, bone, point, tuple(actual))
    print('ROUND_TRIP_MAX', name, error, worst, 'FPS', bpy.context.scene.render.fps, flush=True)
    assert error < .001, (name, 'export trajectory mismatch', error)
    entry = {'name': name, 'bone_count': 30, 'frames': list(action.frame_range),
             'max_sampled_bone_position_error_m': error, 'sampled_frames': list(expected[name])}
    report['clips'].append(entry)
    print('ROUND_TRIP', json.dumps(entry), flush=True)
with open(os.path.join(EXPORT, 'FBX_Animation_Validation.json'), 'w', encoding='utf-8') as f:
    json.dump(report, f, indent=2)
print('ANIMATION_FBX_ROUND_TRIP_PASS', flush=True)
