import bpy, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from BuildCatAnimations import fcurves, choose
bpy.context.scene.render.fps = 30
bpy.ops.import_scene.fbx(filepath=os.path.join(HERE, 'Exports/CatAnimations01/AN_Cat_Idle.fbx'), anim_offset=0)
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
for frame in (1, 19, 37, 55, 73):
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    print('FRAME', frame)
    for name in ('Root', 'Hips', 'Head', 'LeftFoot'):
        pb = rig.pose.bones[name]
        print(name, 'head', tuple(rig.matrix_world @ pb.head), 'loc', tuple(pb.location), 'connected', pb.bone.use_connect)
for curve in fcurves(rig.animation_data.action):
    if 'Hips' in curve.data_path and 'location' in curve.data_path:
        print('HIPS_CURVE', curve.data_path, curve.array_index, [(float(k.co[0]), float(k.co[1])) for k in curve.keyframe_points][::18])
