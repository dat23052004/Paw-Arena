"""Author the first ten cat clips on the approved rig, without changing that rig file."""
import bpy
import math
import os
import json
import sys
from mathutils import Vector, Quaternion, Euler, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, 'PawArena_Cat_Rigged.blend')
OUTPUT = os.path.join(HERE, 'PawArena_Cat_Animations_01.blend')
EXPORT = os.path.join(HERE, 'Exports', 'CatAnimations01')
PREVIEW = os.path.join(os.path.dirname(HERE), 'Previews', 'Characters', 'Animations01')
FPS = 30
TAU = math.tau
CLIPS = [
    ('Idle', 72, True, 19),
    ('Walk', 30, True, 8),
    ('Run', 20, True, 6),
    ('Jump_Start', 12, False, 7),
    ('Jump_Air', 24, True, 7),
    ('Fall', 24, True, 7),
    ('Land', 14, False, 5),
    ('Punch_Left', 18, False, 7),
    ('Punch_Right', 18, False, 7),
    ('Hit_React', 16, False, 4),
]


def neutral():
    return {
        'hips': (0, 0, -.028), 'hips_rot': (0, 0, 0),
        'spine': (0, 0, 0), 'chest': (0, 0, 0), 'head': (0, 0, 0),
        'left_hand': (.61, -.075, .53), 'right_hand': (-.61, -.075, .53),
        'left_hand_rot': (0, 68, -5), 'right_hand_rot': (0, -68, 5),
        'left_pole': (.58, .45, .82), 'right_pole': (-.58, .45, .82),
        'left_foot': (.239, 0, .145), 'right_foot': (-.239, 0, .145),
        'left_foot_rot': (0, 0, 0), 'right_foot_rot': (0, 0, 0),
        'ears': (0, 0), 'tail_pitch': 0, 'tail_sway': 0, 'tail_phase': 0,
    }


def interpolate(a, b, t):
    result = {}
    for key in a:
        if isinstance(a[key], tuple):
            result[key] = tuple(x + (y - x) * t for x, y in zip(a[key], b[key]))
        else:
            result[key] = a[key] + (b[key] - a[key]) * t
    return result


def keys(t, poses):
    if t <= poses[0][0]:
        return poses[0][1].copy()
    for (ta, a), (tb, b) in zip(poses, poses[1:]):
        if t <= tb:
            u = (t - ta) / (tb - ta)
            return interpolate(a, b, u * u * (3 - 2 * u))
    return poses[-1][1].copy()


def flight(t, falling=False):
    p = neutral()
    w = math.sin(TAU * t)
    p['hips'] = (0, 0, -.008 if falling else 0)
    p['spine'] = (7 if falling else -3, 0, 0)
    p['head'] = (-5 if falling else 2, 0, 1.5 * w)
    for side, sign in [('left', 1), ('right', -1)]:
        p[side + '_hand'] = (sign * (.87 if falling else .85),
                            -.08 if falling else -.18,
                            (1.27 if falling else 1.10) + .008 * w)
        p[side + '_hand_rot'] = (-10, sign * 24, -sign * 20)
        p[side + '_pole'] = (sign * .88, -.45, .92)
        p[side + '_foot'] = (sign * .25, .045 if falling else .10,
                            (.205 if falling else .245) + sign * .005 * w)
    p['ears'] = (-4 + 1.2 * w, 4 - 1.2 * w)
    p['tail_pitch'] = -5 if falling else 4
    p['tail_sway'] = 3 * w
    p['tail_phase'] = TAU * t
    return p


def foot_cycle(t, run):
    phase = t % 1
    stance = .40 if run else .60
    front, back = (-.175, .125) if run else (-.13, .13)
    if phase < stance:
        return front + (back - front) * phase / stance, 0.0
    u = (phase - stance) / (1 - stance)
    return back + (front - back) * (.5 - .5 * math.cos(math.pi * u)), \
        (.125 if run else .075) * math.sin(math.pi * u) ** 1.3


def pose_for(name, t):
    p = neutral()
    w = math.sin(TAU * t)
    if name == 'Idle':
        p['hips'] = (.006 * w, 0, -.028 + .004 * w)
        p['spine'] = (.8 * w, 0, .6 * w)
        p['head'] = (-.6 * w, -.5 * w, -1.2 * w)
        for side, s in [('left', 1), ('right', -1)]:
            p[side + '_hand'] = (s * (.61 + .004 * w), -.075, .53 + .008 * w)
        p['ears'] = (1.2 * w, -1.2 * w)
        p['tail_sway'] = 3 * w
        p['tail_phase'] = TAU * t
    elif name in ('Walk', 'Run'):
        run = name == 'Run'
        p['hips'] = ((.018 if run else .022) * math.sin(TAU * (t - .12)), 0,
                     -.070 + .020 * math.cos(2 * TAU * (t - .45)) if run
                     else -.050 + .010 * math.cos(2 * TAU * t))
        p['hips_rot'] = (0, 1.5 * w, -3 * w)
        p['spine'] = (6 if run else 1.5, -1.0 * w, 4 * w)
        p['chest'] = (3 if run else 0, 0, 2 * w)
        p['head'] = (-7 if run else -1.0, -.8 * w, -2 * w)
        for side, s, offset in [('left', 1, 0), ('right', -1, .5)]:
            y, z = foot_cycle(t + offset, run)
            p[side + '_foot'] = (s * .239, y, .145 + z)
            swing = math.cos(TAU * (t + offset))
            p[side + '_hand'] = (s * (.57 if run else .61),
                                -.16 + .18 * swing if run else -.08 + .12 * swing,
                                .72 + .035 * swing if run else .55 + .015 * swing)
            p[side + '_hand_rot'] = (-12 * swing, s * (58 if run else 68), -s * 12)
            p[side + '_pole'] = (s * .58, .45, .82)
        p['ears'] = (2 * math.sin(2 * TAU * t - .7), -2 * math.sin(2 * TAU * t - .7))
        p['tail_sway'] = (4 if run else 3) * math.sin(TAU * t - .7)
        p['tail_pitch'] = -2 if run else 0
        p['tail_phase'] = TAU * t
    elif name == 'Jump_Start':
        crouch = neutral()
        crouch['hips'] = (0, .015, -.125)
        crouch['spine'] = (10, 0, 0)
        crouch['head'] = (-7, 0, 0)
        crouch['left_foot'] = (.239, 0, .155)
        crouch['right_foot'] = (-.239, 0, .155)
        crouch['left_hand'] = (.64, .17, .55)
        crouch['right_hand'] = (-.64, .17, .55)
        crouch['ears'] = (-6, 6)
        crouch['tail_pitch'] = 4
        p = keys(t, [(0, neutral()), (.55, crouch), (1, flight(0))])
    elif name == 'Jump_Air':
        p = flight(t)
    elif name == 'Fall':
        p = flight(t, True)
    elif name == 'Land':
        contact = flight(0, True)
        contact['hips'] = (0, 0, -.015)
        contact['left_foot'] = (.239, 0, .145)
        contact['right_foot'] = (-.239, 0, .145)
        squash = neutral()
        squash['hips'] = (0, .01, -.125)
        squash['spine'] = (12, 0, 0)
        squash['head'] = (-8, 0, 0)
        squash['left_foot'] = (.239, 0, .155)
        squash['right_foot'] = (-.239, 0, .155)
        squash['left_hand'] = (.65, -.17, .57)
        squash['right_hand'] = (-.65, -.17, .57)
        squash['ears'] = (-8, 8)
        squash['tail_pitch'] = -7
        settle = neutral()
        settle['hips'] = (0, 0, -.020)
        settle['tail_pitch'] = 2
        p = keys(t, [(0, contact), (.28, squash), (.78, settle), (1, neutral())])
    elif name.startswith('Punch_'):
        side = 'left' if name == 'Punch_Left' else 'right'
        other = 'right' if side == 'left' else 'left'
        s = 1 if side == 'left' else -1
        anticipation = neutral()
        anticipation['hips'] = (-s * .018, .008, -.055)
        anticipation['hips_rot'] = (0, 0, s * 3)
        anticipation['chest'] = (0, 0, s * 8)
        anticipation['head'] = (0, 0, -s * 6)
        anticipation[side + '_hand'] = (s * .58, .09, .91)
        anticipation[other + '_hand'] = (-s * .49, -.25, .91)
        anticipation[side + '_hand_rot'] = (0, s * 15, -s * 65)
        anticipation[other + '_hand_rot'] = (0, -s * 40, s * 30)
        anticipation['tail_sway'] = s * 3
        impact = neutral()
        impact['hips'] = (s * .018, -.005, -.045)
        impact['hips_rot'] = (0, 0, -s * 4)
        impact['spine'] = (5, 0, -s * 3)
        impact['chest'] = (2, 0, -s * 9)
        impact['head'] = (-5, 0, s * 10)
        impact[side + '_hand'] = (s * .29, -.66, 1.12)
        impact[side + '_hand_rot'] = (0, 0, -s * 86)
        impact[side + '_pole'] = (s * .85, -.30, .92)
        impact[other + '_hand'] = (-s * .48, -.26, .94)
        impact[other + '_hand_rot'] = (0, -s * 40, s * 35)
        impact['tail_sway'] = -s * 3
        impact['ears'] = (-3, 3)
        follow = impact.copy()
        follow[side + '_hand'] = (s * .28, -.67, 1.11)
        recover = neutral()
        recover[side + '_hand'] = (s * .50, -.25, .87)
        recover[side + '_hand_rot'] = (0, s * 45, -s * 35)
        p = keys(t, [(0, neutral()), (3/18, anticipation), (6/18, impact),
                     (8/18, follow), (12/18, recover), (1, neutral())])
    elif name == 'Hit_React':
        recoil = neutral()
        recoil['hips'] = (0, .020, -.075)
        recoil['spine'] = (-11, 0, 3)
        recoil['chest'] = (-5, 0, -2)
        recoil['head'] = (-5, 0, -3)
        recoil['left_hand'] = (.83, .04, .97)
        recoil['right_hand'] = (-.83, .02, .95)
        recoil['left_hand_rot'] = (0, 30, -15)
        recoil['right_hand_rot'] = (0, -30, 15)
        recoil['ears'] = (-9, 9)
        recoil['tail_pitch'] = -7
        recover = neutral()
        recover['hips'] = (0, 0, -.045)
        recover['spine'] = (5, 0, -1)
        recover['head'] = (3, 0, 0)
        recover['tail_pitch'] = 3
        p = keys(t, [(0, neutral()), (3/16, recoil), (7/16, recover), (1, neutral())])
    else:
        raise ValueError(name)
    return p


def set_delta(rig, name, delta):
    pb = rig.pose.bones['CTRL_' + name]
    pb.location = pb.bone.matrix_local.to_3x3().inverted() @ Vector(delta)


def set_position(rig, name, position):
    pb = rig.pose.bones['CTRL_' + name]
    set_delta(rig, name, Vector(position) - pb.bone.head_local)


def set_rotation(rig, name, angles):
    pb = rig.pose.bones['CTRL_' + name]
    rest = pb.bone.matrix_local.to_quaternion()
    q = Euler(tuple(math.radians(v) for v in angles), 'XYZ').to_quaternion()
    pb.rotation_quaternion = rest.inverted() @ q @ rest


def apply_pose(rig, p):
    for pb in rig.pose.bones:
        if pb.name.startswith('CTRL_'):
            pb.location = (0, 0, 0)
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.scale = (1, 1, 1)
    for side in ('Left', 'Right'):
        for kind in ('Arm', 'Leg'):
            rig[kind + '_IK_' + side] = 1.0
    set_delta(rig, 'Hips', p['hips'])
    for name, key in [('Hips', 'hips_rot'), ('Spine', 'spine'), ('Chest', 'chest'), ('Head', 'head')]:
        set_rotation(rig, name, p[key])
    for side, sign in [('Left', 1), ('Right', -1)]:
        low = side.lower()
        set_position(rig, side + 'Hand_IK', p[low + '_hand'])
        set_position(rig, side + 'Foot_IK', p[low + '_foot'])
        set_position(rig, side + 'Hand_Pole', p[low + '_pole'])
        set_rotation(rig, side + 'Hand_IK', p[low + '_hand_rot'])
        set_rotation(rig, side + 'Foot_IK', p[low + '_foot_rot'])
        set_position(rig, side + 'Foot_Pole', (sign * .239, -.65, .30))
        set_rotation(rig, side + 'Ear', (p['ears'][0 if sign == 1 else 1] * sign,
                                        p['ears'][0 if sign == 1 else 1], 0))
    for i in range(5):
        lag = math.sin(p['tail_phase'] - i * .42) - math.sin(-i * .42)
        set_rotation(rig, f'Tail_{i+1:02d}',
                     (p['tail_pitch'] * (.65 if i == 0 else .35),
                      .45 * lag, p['tail_sway'] * (.65 if i == 0 else .40) + .8 * lag))
    rig.update_tag()
    bpy.context.view_layer.update()
    # Use IK for reach planning, then retain its joint positions with minimum-
    # twist FK arm rotations. This avoids the pole solver's axial shoulder roll
    # wringing the continuous shoulder surface when the elbow points backward.
    for side in ('Left', 'Right'):
        upper = rig.pose.bones['CTRL_' + side + 'UpperArm']
        lower = rig.pose.bones['CTRL_' + side + 'LowerArm']
        hand = rig.pose.bones['CTRL_' + side + 'Hand']
        positions = (upper.head.copy(), upper.tail.copy(), lower.tail.copy())
        rig['Arm_IK_' + side] = 0.0
        rig.update_tag()
        bpy.context.view_layer.update()
        for pb, head, tail in [(upper, positions[0], positions[1]), (lower, positions[1], positions[2])]:
            reference = (pb.parent.matrix @ pb.parent.bone.matrix_local.inverted() @ pb.bone.matrix_local).to_quaternion()
            current_direction = reference @ Vector((0, 1, 0))
            desired_direction = (tail - head).normalized()
            q = current_direction.rotation_difference(desired_direction) @ reference
            pb.matrix = Matrix.LocRotScale(head, q, Vector((1, 1, 1)))
            bpy.context.view_layer.update()
        hand.rotation_quaternion = (1, 0, 0, 0)
        bpy.context.view_layer.update()


def fcurves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves


def activate(obj):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def choose(rig, action):
    rig.animation_data_create()
    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]


def author(rig):
    # The rig's animation_data also owns IK influence drivers: preserve them.
    rig.animation_data_create()
    rig.animation_data.action = None
    for track in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(track)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.render.fps_base = 1
    scene.timeline_markers.clear()
    controls = [pb for pb in rig.pose.bones if pb.name.startswith('CTRL_')]
    actions = {}
    for name, duration, loop, representative in CLIPS:
        action = bpy.data.actions.new('Cat_' + name)
        action.use_fake_user = True
        action.use_frame_range = True
        action.frame_start, action.frame_end = 1, duration + 1
        action['loop'] = loop
        action['duration_seconds'] = duration / FPS
        action['motion'] = 'In-place; Root fixed. Hips contain body mechanics only.'
        action.asset_mark()
        action.asset_data.description = f'Paw Arena / {name} / {duration / FPS:.3f} s / ' + ('loop' if loop else 'one-shot')
        choose(rig, action)
        previous = {}
        for frame in range(1, duration + 2):
            scene.frame_set(frame)
            apply_pose(rig, pose_for(name, (frame - 1) / duration))
            for pb in controls:
                q = pb.rotation_quaternion.copy()
                if pb.name in previous and q.dot(previous[pb.name]) < 0:
                    q.negate()
                    pb.rotation_quaternion = q
                previous[pb.name] = q
                pb.keyframe_insert('location', frame=frame, group=pb.name)
                pb.keyframe_insert('rotation_quaternion', frame=frame, group=pb.name)
            for side in ('Left', 'Right'):
                for kind in ('Arm', 'Leg'):
                    rig.keyframe_insert(data_path='["' + kind + '_IK_' + side + '"]', frame=frame, group='IK switches')
        for curve in fcurves(action):
            for point in curve.keyframe_points:
                point.interpolation = 'LINEAR'
            if loop:
                curve.modifiers.new('CYCLES')
        action.pose_markers.new('START').frame = 1
        action.pose_markers.new('LOOP SEAM' if loop else 'END').frame = duration + 1
        if name.startswith('Punch_'):
            action.pose_markers.new('CONTACT GUIDE - no gameplay event').frame = 7
        actions[name] = action
        print('AUTHORED', name, duration + 1, flush=True)
    return actions


def validate(rig, mesh, actions):
    scene = bpy.context.scene
    report = {'fps': FPS, 'root_motion': False, 'clips': [], 'source_rig_preserved': True}
    bones = [pb for pb in rig.pose.bones if not pb.name.startswith('CTRL_')]
    rest = [v.co.copy() for v in mesh.data.vertices]
    edges = [(a, b, (rest[a] - rest[b]).length) for a, b in (e.vertices[:] for e in mesh.data.edges)]
    edges = [(a, b, length) for a, b, length in edges if length > .0001]
    for name, duration, loop, representative in CLIPS:
        choose(rig, actions[name])
        matrices = []
        max_ik = 0
        max_stretch = 0
        min_ground = 10
        max_root = 0
        for frame in range(1, duration + 2):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            max_root = max(max_root, (rig.pose.bones['Root'].matrix.translation - rig.data.bones['Root'].matrix_local.translation).length)
            for side in ('Left', 'Right'):
                for part, lower in [('Hand', 'LowerArm'), ('Foot', 'LowerLeg')]:
                    error = (rig.pose.bones[side + lower].tail - rig.pose.bones['CTRL_' + side + part + '_IK'].head).length
                    max_ik = max(max_ik, error)
            if frame in (1, duration + 1):
                matrices.append([pb.matrix.copy() for pb in bones])
            if frame in {1, representative, duration + 1} or frame % 3 == 0:
                evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
                surface = evaluated.to_mesh()
                assert all(math.isfinite(c) for v in surface.vertices for c in v.co), name
                max_stretch = max(max_stretch, max((surface.vertices[a].co - surface.vertices[b].co).length / length for a, b, length in edges))
                min_ground = min(min_ground, min(v.co.z for v in surface.vertices))
                evaluated.to_mesh_clear()
        seam = max(abs(a[i][j] - b[i][j]) for a, b in zip(*matrices) for i in range(4) for j in range(4))
        assert max_root < 1e-6, (name, 'root drift', max_root)
        assert max_ik < .012, (name, 'unreachable IK', max_ik)
        assert max_stretch < 7, (name, 'skin spike', max_stretch)
        if loop:
            assert seam < 1e-5, (name, 'loop seam', seam)
        entry = {'name': 'Cat_' + name, 'frames': [1, duration + 1], 'duration_seconds': duration / FPS,
                 'loop': loop, 'max_root_drift_m': max_root, 'max_IK_error_m': max_ik,
                 'max_edge_stretch': max_stretch, 'lowest_sampled_vertex_z_m': min_ground,
                 'start_end_matrix_difference': seam}
        report['clips'].append(entry)
        print('CHECKED', json.dumps(entry), flush=True)
    with open(os.path.join(EXPORT, 'Blender_Validation.json'), 'w', encoding='utf-8') as f:
        json.dump(report, f, indent=2)
    return report


def export(rig, mesh, actions):
    scene = bpy.context.scene
    data = rig.data.copy()
    out = bpy.data.objects.new('CatArmature', data)
    scene.collection.objects.link(out)
    activate(out)
    bpy.ops.object.mode_set(mode='EDIT')
    for bone in list(data.edit_bones):
        if bone.name.startswith('CTRL_'):
            data.edit_bones.remove(bone)
    bpy.ops.object.mode_set(mode='OBJECT')
    out_mesh = mesh.copy()
    out_mesh.data = mesh.data.copy()
    scene.collection.objects.link(out_mesh)
    out_mesh.parent = out
    for mod in out_mesh.modifiers:
        if mod.type == 'ARMATURE':
            mod.object = out
    export_actions = []
    common = dict(use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
                  use_armature_deform_only=True, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                  mesh_smooth_type='FACE', use_triangles=True, path_mode='COPY', embed_textures=False)
    # The reference FBX has an actual T-pose, independently of the active animation.
    activate(out)
    out_mesh.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT, 'SK_PawArena_Cat.fbx'), bake_anim=False, **common)
    export_root = data.bones['Root'].matrix_local.copy()
    for name, duration, loop, representative in CLIPS:
        choose(rig, actions[name])
        baked = bpy.data.actions.new('BAKED_Cat_' + name)
        choose(out, baked)
        previous = {}
        for frame in range(1, duration + 2):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            for pb in out.pose.bones:
                pb.rotation_mode = 'QUATERNION'
                desired = rig.pose.bones[pb.name].matrix.copy()
                # Solve local channels from the complete source pose, not from a
                # partially evaluated destination parent at the previous key.
                if pb.parent:
                    pb.matrix_basis = (pb.bone.matrix_local.inverted() @ pb.parent.bone.matrix_local
                                       @ rig.pose.bones[pb.parent.name].matrix.inverted() @ desired)
                else:
                    pb.matrix_basis = pb.bone.matrix_local.inverted() @ desired
            for pb in out.pose.bones:
                pb.rotation_mode = 'QUATERNION'
                if pb.name != 'Hips' and pb.location.length < .0001:
                    pb.location = (0, 0, 0)
                pb.scale = (1, 1, 1)
                q = pb.rotation_quaternion.copy()
                if pb.name in previous and q.dot(previous[pb.name]) < 0:
                    q.negate()
                    pb.rotation_quaternion = q
                previous[pb.name] = q
                pb.keyframe_insert('location', frame=frame, group=pb.name)
                pb.keyframe_insert('rotation_quaternion', frame=frame, group=pb.name)
                pb.keyframe_insert('scale', frame=frame, group=pb.name)
            bpy.context.view_layer.update()
            pose_error = max((pb.head - rig.pose.bones[pb.name].head).length for pb in out.pose.bones)
            assert pose_error < .0001, (name, frame, 'bake drift', pose_error)
        for curve in fcurves(baked):
            for point in curve.keyframe_points:
                point.interpolation = 'LINEAR'
        scene.frame_start, scene.frame_end = 1, duration + 1
        scene.frame_set(1)
        activate(out)
        # Each animation-only FBX uses exactly the same 30-bone skeleton.
        bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT, 'AN_Cat_' + name + '.fbx'),
            bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
            bake_anim_force_startend_keying=True, bake_anim_step=1, bake_anim_simplify_factor=0,
            **common)
        export_actions.append(baked)
        print('EXPORTED', name, flush=True)
    bpy.data.objects.remove(out_mesh, do_unlink=True)
    bpy.data.objects.remove(out, do_unlink=True)
    for action in export_actions:
        bpy.data.actions.remove(action)


def configure_studio():
    scene = bpy.context.scene
    bpy.data.collections['02 | REVIEW STUDIO'].hide_viewport = False
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    if hasattr(scene, 'eevee'):
        scene.eevee.taa_render_samples = 16
    camera = scene.camera
    camera.location = (4.3, -7.5, 2.9)
    camera.rotation_euler = (Vector((0, 0, 1.15)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 3.08
    return scene


def render_previews(rig, actions, movie=False):
    scene = configure_studio()
    for index, (name, duration, loop, representative) in enumerate(CLIPS, 1):
        choose(rig, actions[name])
        folder = os.path.join(PREVIEW, 'Frames', name)
        os.makedirs(folder, exist_ok=True)
        frames = range(1, duration + 1) if movie else [representative]
        for frame in frames:
            scene.frame_set(frame)
            scene.render.filepath = os.path.join(folder, f'{frame:04d}.png') if movie else os.path.join(PREVIEW, f'{index:02d}_{name}.png')
            bpy.ops.render.render(write_still=True)
        print('RENDERED', name, flush=True)


def main():
    os.makedirs(EXPORT, exist_ok=True)
    os.makedirs(PREVIEW, exist_ok=True)
    render_only = '--render-only' in sys.argv
    export_only = '--export-only' in sys.argv
    bpy.ops.wm.open_mainfile(filepath=OUTPUT if (render_only or export_only) else SOURCE)
    scene = bpy.context.scene
    rig = bpy.data.objects['RIG_Cat']
    mesh = bpy.data.objects['SK_Cat']
    if render_only or export_only:
        actions = {name: bpy.data.actions['Cat_' + name] for name, *_ in CLIPS}
        if export_only:
            export(rig, mesh, actions)
        else:
            render_previews(rig, actions, '--movie' in sys.argv)
        return
    activate(rig)
    actions = author(rig)
    report = validate(rig, mesh, actions)
    if '--draft' not in sys.argv:
        export(rig, mesh, actions)
    choose(rig, actions['Idle'])
    scene.frame_start, scene.frame_end = 1, 73
    scene.frame_set(1)
    scene.name = 'CAT | FIRST TEN ANIMATIONS'
    scene['animation_pack'] = '10 editable actions, 30 fps; actions dropdown / Asset Browser. Root fixed.'
    rig['Animation_Info'] = 'Ten Cat_ actions. Arms use FK with minimal twist; feet use IK. No mid-clip FK/IK switching.'
    notes = bpy.data.texts.new('READ ME | Animation Pack 01')
    notes.write('Ten animation actions: choose a Cat_ action in Dope Sheet > Action Editor.\n'
                'Set the timeline to the action range shown in its custom properties.\n'
                'Idle/Walk/Run/Jump_Air/Fall loop with duplicate endpoint.\n'
                'All clips are in-place; gameplay drives displacement and jump height.\n'
                'Punch contact guide at frame 7 (0.2 s), not a coded gameplay event.\n'
                'Original approved base and rig files are unchanged. No facial rig or blink animation.\n')
    bpy.data.collections['02 | REVIEW STUDIO'].hide_viewport = True
    activate(rig)
    bpy.ops.object.mode_set(mode='POSE')
    for pb in rig.pose.bones:
        pb.select = pb.name == 'CTRL_Hips'
    rig.data.bones.active = rig.data.bones['CTRL_Hips']
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'DOPESHEET_EDITOR':
                area.ui_type = 'DOPESHEET'
                area.spaces.active.mode = 'ACTION'
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT)
    if '--no-render' not in sys.argv:
        render_previews(rig, actions)
    print('ANIMATION_PACK_SAVED', OUTPUT, flush=True)


if __name__ == '__main__':
    main()
