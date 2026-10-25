"""Second cat animation set: interaction, recovery and match presentation."""
import bpy
import math
import os
import sys
import json
import copy
from mathutils import Vector, Matrix, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import BuildCatAnimations as core
import CatAbilityPoses as abilities

SOURCE = core.OUTPUT
OUTPUT = os.path.join(HERE, 'PawArena_Cat_Animations_Complete.blend')
EXPORT = os.path.join(HERE, 'Exports', 'CatAnimations02')
PREVIEW = os.path.join(os.path.dirname(HERE), 'Previews', 'Characters', 'Animations02')
CLIPS = [
    ('Grab_Start', 18, False, 15), ('Grab_Hold', 36, True, 10),
    ('Grab_Release', 15, False, 8), ('Lift', 30, False, 24),
    ('Carry_Idle', 40, True, 11), ('Carry_Move', 32, True, 9),
    ('Throw', 27, False, 13), ('Struggle_Grab', 30, True, 8),
    ('Struggle_Carried', 24, True, 7),
    ('GetUp_FaceUp', 54, False, 20), ('GetUp_FaceDown', 48, False, 18),
    ('RingOut_Fall', 30, True, 8), ('Spawn', 30, False, 16),
    ('Ready', 36, True, 10), ('Victory', 60, False, 31),
    ('Defeat', 48, False, 31), ('Hit_Back', 18, False, 5),
    ('Stunned', 48, True, 13),
]
ORIGINAL_CLIPS = list(core.CLIPS)
CLIPS.extend(abilities.CLIPS)


def pose():
    p = core.neutral()
    p.update(body_rotation=(0, 0, 0), floor_align=0, tail_tuck=0)
    return p


def variant(p, **values):
    result = copy.deepcopy(p)
    result.update(values)
    return result


def grip(t=0, carry=False):
    p = pose()
    w = math.sin(math.tau * t)
    p['hips'] = (.005 * w, 0, -.045 + .003 * w)
    p['spine'] = (3 if not carry else -3, 0, 0)
    p['head'] = (-2 if not carry else 2, 0, -.5 * w)
    for side, s in [('left', 1), ('right', -1)]:
        p[side + '_hand'] = (s * .40, -.53, (1.35 if carry else 1.02) + .005 * w)
        p[side + '_pole'] = (s * .76, .30, .89)
    p['ears'] = (w, -w)
    p['tail_sway'] = 2 * w
    p['tail_phase'] = math.tau * t
    return p


def ready(t=0):
    p = pose()
    w = math.sin(math.tau * t)
    p['hips'] = (.012 * w, 0, -.055 + .005 * w)
    p['spine'] = (3, 0, 1.2 * w)
    p['head'] = (-2, 0, -1.2 * w)
    for side, s in [('left', 1), ('right', -1)]:
        p[side + '_hand'] = (s * .48, -.31, 1.03 + .012 * w)
        p[side + '_pole'] = (s * .69, .35, .85)
    p['tail_sway'] = 3 * w
    p['tail_phase'] = math.tau * t
    return p


def recover_pose(t, face_up):
    flat = pose()
    flat['floor_align'] = 1
    flat['body_rotation'] = (-90 if face_up else 90, 0, 0)
    flat['left_hand'] = (.62, -.05, .60)
    flat['right_hand'] = (-.62, -.05, .60)
    flat['tail_tuck'] = 110 if face_up else 78
    flat['ears'] = (-6, 6)
    curl = variant(flat,
        body_rotation=(-57 if face_up else 64, 0, -7 if face_up else 0),
        hips=(0, 0, -.11), spine=(15 if face_up else 8, 0, 0), head=(8, 0, 0),
        left_foot=(.25, -.15, .23), right_foot=(-.25, -.15, .23),
        left_hand=(.58, .40 if face_up else -.45, .46 if face_up else .85),
        right_hand=(-.58, .40 if face_up else -.45, .46 if face_up else .85),
        tail_tuck=110 if face_up else 65)
    kneel = variant(flat, body_rotation=(14, 0, 0), hips=(0, .02, -.13),
        spine=(7, 0, 0), head=(-5, 0, 0), left_hand=(.60, -.30, .59),
        right_hand=(-.60, -.30, .59), left_foot=(.239, -.06, .155),
        right_foot=(-.239, .04, .155), tail_tuck=25, ears=(-2, 2))
    stand = pose()
    stand['floor_align'] = 1
    return core.keys(t, [(0, flat), (.15, flat), (.42, curl), (.68, kneel), (1, stand)])


def pose_for(name, t):
    if name.startswith('Ability_'):
        return abilities.pose_for(name, t, sys.modules[__name__])
    p = pose()
    w = math.sin(math.tau * t)
    if name == 'Grab_Start':
        anticipation = variant(p, hips=(0, .012, -.07), spine=(-2, 0, 0))
        return core.keys(t, [(0, p), (.22, anticipation), (.78, grip()), (1, grip())])
    if name == 'Grab_Hold':
        return grip(t)
    if name == 'Grab_Release':
        return core.keys(t, [(0, grip()), (.28, variant(grip(), left_hand=(.64, -.43, 1.0), right_hand=(-.64, -.43, 1.0))), (1, p)])
    if name == 'Lift':
        squat = variant(grip(), hips=(0, .015, -.12), spine=(8, 0, 0), head=(-6, 0, 0),
                        left_hand=(.40, -.53, .98), right_hand=(-.40, -.53, .98),
                        left_foot=(.239, 0, .155), right_foot=(-.239, 0, .155), tail_pitch=4)
        return core.keys(t, [(0, grip()), (.28, squat), (.82, grip(0, True)), (1, grip(0, True))])
    if name == 'Carry_Idle':
        return grip(t, True)
    if name == 'Carry_Move':
        p = grip(t, True)
        walk = core.pose_for('Walk', t)
        for key in ('hips', 'hips_rot', 'left_foot', 'right_foot'):
            p[key] = walk[key]
        p['hips_rot'] = (0, 0, 0)
        p['tail_sway'] = 3 * w
        return p
    if name == 'Throw':
        start = grip(0, True)
        windup = variant(start, hips=(0, .012, -.075), spine=(-7, 0, 0), head=(3, 0, 0),
                         left_hand=(.40, -.41, 1.47), right_hand=(-.40, -.41, 1.47),
                         tail_pitch=4)
        release = variant(start, hips=(0, -.005, -.048), spine=(10, 0, 0), head=(-7, 0, 0),
                          left_hand=(.43, -.66, 1.37), right_hand=(-.43, -.66, 1.37),
                          tail_pitch=-3, ears=(-5, 5))
        follow = variant(release, left_hand=(.59, -.51, 1.05), right_hand=(-.59, -.51, 1.05), spine=(7, 0, 0))
        return core.keys(t, [(0, start), (7/27, windup), (12/27, release), (17/27, follow), (1, p)])
    if name in ('Struggle_Grab', 'Struggle_Carried'):
        carried = name.endswith('Carried')
        p['hips'] = (0, 0, -.028)
        p['spine'] = (3, 0, 4 * w)
        p['head'] = (-2, 2 * w, -8 * w)
        p['left_hand'] = (.69, -.16 - .09 * w, .94 + .06 * w)
        p['right_hand'] = (-.69, -.16 + .09 * w, .94 - .06 * w)
        p['ears'] = (-5 + w, 5 - w)
        p['tail_tuck'] = 78
        p['tail_sway'] = 2 * w
        p['tail_phase'] = math.tau * t
        if carried:
            p['left_foot'] = (.26, .05 + .06 * w, .20 + .04 * w)
            p['right_foot'] = (-.26, .05 - .06 * w, .20 - .04 * w)
        return p
    if name.startswith('GetUp_'):
        return recover_pose(t, name == 'GetUp_FaceUp')
    if name == 'RingOut_Fall':
        p = core.flight(t, True)
        p.update(body_rotation=(-10 + 3 * w, 0, 4 * w), floor_align=0, tail_tuck=0)
        p['left_hand'] = (.79, -.10 - .08 * w, 1.48 + .05 * w)
        p['right_hand'] = (-.79, -.10 + .08 * w, 1.48 - .05 * w)
        p['left_foot'] = (.27, -.05 + .07 * w, .23 + .05 * w)
        p['right_foot'] = (-.27, -.05 - .07 * w, .23 - .05 * w)
        p['head'] = (4, 0, -3 * w)
        p['ears'] = (-8 + w, 8 - w)
        p['tail_sway'] = 6 * w
        return p
    if name == 'Spawn':
        start = variant(p, hips=(0, 0, -.115), spine=(7, 0, 0), head=(6, 0, 0),
                        left_hand=(.66, -.08, .53), right_hand=(-.66, -.08, .53),
                        left_foot=(.239, 0, .155), right_foot=(-.239, 0, .155), ears=(-5, 5))
        alert = variant(ready(), head=(-6, 0, -10), tail_sway=4)
        return core.keys(t, [(0, start), (.35, alert), (.65, variant(alert, head=(-3, 0, 8))), (1, ready())])
    if name == 'Ready':
        return ready(t)
    if name == 'Victory':
        prep = variant(p, hips=(0, 0, -.085), spine=(5, 0, 0), head=(3, 0, 0), tail_pitch=3)
        cheer = variant(p, hips=(0, 0, -.02), spine=(-3, 0, -3), head=(-5, 0, 3),
                        left_hand=(.62, -.10, 1.68), right_hand=(-.62, -.10, 1.68),
                        left_pole=(.84, .10, 1.30), right_pole=(-.84, .10, 1.30),
                        ears=(3, -3), tail_sway=5)
        cheer_other = variant(cheer, spine=(-3, 0, 3), head=(-5, 0, -3), tail_sway=-5)
        return core.keys(t, [(0, p), (.16, prep), (.36, cheer), (.52, cheer_other), (.67, cheer), (1, p)])
    if name == 'Defeat':
        slump = variant(p, hips=(0, 0, -.045), spine=(8, 0, 0), head=(18, 0, 0),
                        left_hand=(.59, -.14, .52), right_hand=(-.59, -.14, .52),
                        ears=(-12, 12), tail_pitch=-5)
        return core.keys(t, [(0, p), (.35, slump), (.70, variant(slump, head=(20, 0, -5))), (1, slump)])
    if name == 'Hit_Back':
        recoil = variant(p, hips=(0, -.012, -.06), spine=(15, 0, 0), chest=(5, 0, 0), head=(7, 0, 0),
                         left_hand=(.69, -.39, 1.02), right_hand=(-.69, -.39, 1.02),
                         ears=(-7, 7), tail_pitch=6)
        settle = variant(p, spine=(-3, 0, 0), head=(-3, 0, 0), tail_pitch=-2)
        return core.keys(t, [(0, p), (4/18, recoil), (10/18, settle), (1, p)])
    if name == 'Stunned':
        p['hips'] = (.025 * w, .005, -.065)
        p['spine'] = (4, 2 * w, 4 * w)
        p['head'] = (5 + 3 * math.sin(math.tau * t), -4 * w, -7 * w)
        p['left_hand'] = (.66, -.10, .65 + .035 * w)
        p['right_hand'] = (-.66, -.10, .65 - .035 * w)
        p['ears'] = (-7 + 2 * w, 7 - 2 * w)
        p['tail_sway'] = -4 * w
        p['tail_phase'] = math.tau * t
        return p
    raise ValueError(name)


def remap_body(rig, transform):
    controls = [pb for pb in rig.pose.bones if pb.name.startswith('CTRL_') and pb.name != 'CTRL_Root']
    desired = {pb.name: transform @ pb.matrix for pb in controls}
    desired['CTRL_Root'] = rig.pose.bones['CTRL_Root'].matrix.copy()
    for side in ('Left', 'Right'):
        rig['Leg_IK_' + side] = 0.0
    rig.update_tag()
    bpy.context.view_layer.update()
    for pb in controls:
        if pb.parent:
            pb.matrix_basis = (pb.bone.matrix_local.inverted() @ pb.parent.bone.matrix_local
                               @ desired[pb.parent.name].inverted() @ desired[pb.name])
        else:
            pb.matrix_basis = pb.bone.matrix_local.inverted() @ desired[pb.name]
    bpy.context.view_layer.update()


def apply_pose(rig, mesh, p):
    core.apply_pose(rig, p)
    if p['tail_tuck']:
        core.set_rotation(rig, 'Tail_01', (0, 0, p['tail_tuck']))
        bpy.context.view_layer.update()
    if any(abs(a) > .00001 for a in p['body_rotation']) or p['floor_align']:
        hips = rig.pose.bones['CTRL_Hips'].head.copy()
        rotation = Euler(tuple(math.radians(a) for a in p['body_rotation']), 'XYZ').to_matrix().to_4x4()
        remap_body(rig, Matrix.Translation(hips) @ rotation @ Matrix.Translation(-hips))
    if p['floor_align']:
        evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        surface = evaluated.to_mesh()
        bottom = min(v.co.z for v in surface.vertices)
        evaluated.to_mesh_clear()
        remap_body(rig, Matrix.Translation((0, 0, .015 - bottom)))


def author(rig, mesh):
    rig.animation_data.action = None
    actions = {}
    scene = bpy.context.scene
    for name, duration, loop, representative in CLIPS:
        action = bpy.data.actions.new('Cat_' + name)
        action.use_fake_user = True
        action.use_frame_range = True
        action.frame_start, action.frame_end = 1, duration + 1
        action['loop'] = loop
        action['duration_seconds'] = duration / 30
        action['motion'] = 'Fixed Root; pelvis contains authored pose recovery. Gameplay drives travel.'
        action.asset_mark()
        action.asset_data.description = f'Paw Arena / {name} / {duration / 30:.2f} s'
        core.choose(rig, action)
        previous = {}
        for frame in range(1, duration + 2):
            scene.frame_set(frame)
            apply_pose(rig, mesh, pose_for(name, (frame - 1) / duration))
            for pb in rig.pose.bones:
                if not pb.name.startswith('CTRL_'):
                    continue
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
        for curve in core.fcurves(action):
            for key in curve.keyframe_points:
                key.interpolation = 'LINEAR'
            if loop:
                curve.modifiers.new('CYCLES')
        action.pose_markers.new('START').frame = 1
        action.pose_markers.new('LOOP SEAM' if loop else 'END').frame = duration + 1
        if name == 'Throw':
            action.pose_markers.new('RELEASE GUIDE / NO GAMEPLAY EVENT').frame = 13
        if name == 'Grab_Start':
            action.pose_markers.new('GRIP CONTACT GUIDE').frame = 15
        for label, frame in abilities.MARKERS.get(name, []):
            action.pose_markers.new(label + ' / NO GAMEPLAY EVENT').frame = frame
        actions[name] = action
        print('AUTHORED_02', name, flush=True)
    return actions


def validate(rig, mesh, actions):
    report = {'fps': 30, 'root_motion': False, 'clips': []}
    rest = [v.co.copy() for v in mesh.data.vertices]
    edges = [(e.vertices[0], e.vertices[1], (rest[e.vertices[0]] - rest[e.vertices[1]]).length) for e in mesh.data.edges]
    edges = [e for e in edges if e[2] > .0001]
    for name, duration, loop, representative in CLIPS:
        core.choose(rig, actions[name])
        max_stretch, min_z, max_root = 0, 10, 0
        endpoints = []
        for frame in range(1, duration + 2):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            max_root = max(max_root, rig.pose.bones['Root'].head.length)
            if frame in (1, duration + 1):
                endpoints.append([pb.matrix.copy() for pb in rig.pose.bones if not pb.name.startswith('CTRL_')])
            if frame in (1, representative, duration + 1) or frame % 4 == 0:
                evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
                surface = evaluated.to_mesh()
                assert all(math.isfinite(c) for v in surface.vertices for c in v.co), name
                min_z = min(min_z, min(v.co.z for v in surface.vertices))
                max_stretch = max(max_stretch, max((surface.vertices[a].co - surface.vertices[b].co).length / length for a,b,length in edges))
                evaluated.to_mesh_clear()
        seam = max(abs(a[i][j]-b[i][j]) for a,b in zip(*endpoints) for i in range(4) for j in range(4))
        assert max_root < .000001, (name, 'root drift')
        assert max_stretch < 7, (name, 'skin spike', max_stretch)
        if loop:
            assert seam < .00001, (name, 'loop mismatch', seam)
        entry = dict(name='Cat_'+name, frames=[1,duration+1], duration_seconds=duration/30,
                     loop=loop, maximum_edge_stretch=max_stretch, lowest_vertex_z_m=min_z,
                     root_drift_m=max_root, endpoint_matrix_difference=seam)
        report['clips'].append(entry)
        print('VALIDATED_02', json.dumps(entry), flush=True)
    with open(os.path.join(EXPORT,'Blender_Validation.json'),'w',encoding='utf-8') as stream:
        json.dump(report,stream,indent=2)
    return report


def configure_core():
    core.EXPORT = EXPORT
    core.PREVIEW = PREVIEW
    core.CLIPS = CLIPS


def main():
    os.makedirs(EXPORT, exist_ok=True)
    os.makedirs(PREVIEW, exist_ok=True)
    reuse = '--render-only' in sys.argv or '--export-only' in sys.argv
    bpy.ops.wm.open_mainfile(filepath=OUTPUT if reuse else SOURCE)
    rig, mesh = bpy.data.objects['RIG_Cat'], bpy.data.objects['SK_Cat']
    if reuse:
        actions = {name:bpy.data.actions['Cat_'+name] for name,*_ in CLIPS}
    else:
        actions = author(rig, mesh)
        validate(rig, mesh, actions)
    configure_core()
    if '--export-only' in sys.argv or (not reuse and '--draft' not in sys.argv):
        core.export(rig, mesh, actions)
    if not reuse:
        core.choose(rig, actions['Ready'])
        scene = bpy.context.scene
        scene.frame_start,scene.frame_end = 1,37
        scene.frame_set(1)
        count = len(ORIGINAL_CLIPS) + len(CLIPS)
        scene.name = f'CAT | ANIMATION LIBRARY {count} CLIPS'
        rig['Animation_Info'] = f'{count} editable Cat_ actions: 10 Pack01 + {len(CLIPS)} Pack02. Source files unchanged.'
        bpy.data.collections['02 | REVIEW STUDIO'].hide_viewport = True
        bpy.ops.wm.save_as_mainfile(filepath=OUTPUT)
        with open(os.path.join(EXPORT,'Clip_Manifest.json'),'w',encoding='utf-8') as stream:
            json.dump({'fps':30,'clips':[{'name':n,'frames':d,'loop':l,'representative':r} for n,d,l,r in CLIPS]},stream,indent=2)
    if '--no-render' not in sys.argv and '--export-only' not in sys.argv:
        core.render_previews(rig, actions, '--movie' in sys.argv)
    print('PACK02_DONE',OUTPUT,flush=True)


if __name__ == '__main__':
    main()
