"""Finish the deferred 18 clips, retaining the approved 26 actions and gear source."""
import os
import sys
import json
import math
import hashlib
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import BuildCatAnimations as core
import BuildCatAnimations02 as draft
from BuildCatSkins import fingerprint

CLIPS = [entry for entry in draft.CLIPS if not entry[0].startswith('Ability_')]
SOURCE = os.path.join(HERE, 'PawArena_Cat_Gear.blend')
OUTPUT = os.path.join(HERE, 'PawArena_Cat_Animations_Full.blend')
EXPORT = os.path.join(HERE, 'Exports', 'CatCompletion')
PREVIEW = os.path.join(os.path.dirname(HERE), 'Previews', 'Characters', 'Completion')


def configure():
    draft.CLIPS = CLIPS
    draft.EXPORT = EXPORT
    draft.PREVIEW = PREVIEW
    draft.configure_core()


def surface_stats(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    surface = evaluated.to_mesh()
    points = [v.co.copy() for v in surface.vertices]
    evaluated.to_mesh_clear()
    assert all(math.isfinite(c) for p in points for c in p), obj.name
    return points


def audit(rig, mesh, before):
    after = fingerprint(mesh, rig)
    for key in ('geometry_weights', 'topology', 'uv', 'rest_rig'):
        assert before[key] == after[key], ('protected source changed', key)
    for name, value in before['actions'].items():
        assert after['actions'][name] == value, ('original action changed', name)
    assert len([a for a in bpy.data.actions if a.name.startswith('Cat_')]) == 44
    report = {'passed': True, 'fps': 30, 'new_clips': 18, 'total_clips': 44,
              'original_26_actions_unchanged': True, 'base_rig_uv_weights_unchanged': True,
              'source_sha256': hashlib.sha256(open(SOURCE, 'rb').read()).hexdigest(),
              'clips': [], 'phase_seams': [], 'gear_max_edge_stretch': {}}
    gears = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('GEAR_')]
    edges = {o.name: [(e.vertices[0], e.vertices[1],
                      (o.data.vertices[e.vertices[0]].co - o.data.vertices[e.vertices[1]].co).length)
                     for e in o.data.edges] for o in gears}
    visibility = {o.name: o.hide_get() for o in gears}
    for obj in gears:
        obj.hide_set(False)
    for name, duration, loop, representative in CLIPS:
        core.choose(rig, bpy.data.actions['Cat_' + name])
        min_z, max_root, max_size = 100.0, 0.0, 0.0
        endpoints = []
        for frame in range(1, duration + 2):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            points = surface_stats(mesh)
            min_z = min(min_z, min(p.z for p in points))
            max_size = max(max_size, max(p.length for p in points))
            max_root = max(max_root, rig.pose.bones['Root'].head.length)
            if frame in (1, duration + 1):
                endpoints.append([pb.matrix.copy() for pb in rig.pose.bones if not pb.name.startswith('CTRL_')])
            if frame in {1, representative, duration // 4 + 1, duration // 2 + 1, 3 * duration // 4 + 1, duration + 1}:
                for obj in gears:
                    coords = surface_stats(obj)
                    assert max(v.length for v in coords) < 5, (name, frame, obj.name, 'exploded gear')
                    stretch = max((coords[a] - coords[b]).length / length for a, b, length in edges[obj.name] if length > .005)
                    report['gear_max_edge_stretch'][obj.name] = max(report['gear_max_edge_stretch'].get(obj.name, 1), stretch)
        seam = max(abs(a[i][j] - b[i][j]) for a, b in zip(*endpoints) for i in range(4) for j in range(4))
        assert max_root < 1e-6, (name, 'root drift', max_root)
        assert max_size < 4, (name, 'body spike', max_size)
        assert min_z >= -.005, (name, 'floor penetration', min_z)
        if loop:
            assert seam < 1e-5, (name, 'loop seam', seam)
        report['clips'].append(dict(name=name, checked_frames=duration + 1, loop=loop,
                                   minimum_z=min_z, root_drift=max_root, loop_seam=seam))
        print('FULL_AUDIT', name, min_z, seam, flush=True)
    for left, right in [('Grab_Start', 'Grab_Hold'), ('Grab_Hold', 'Lift'),
                        ('Lift', 'Carry_Idle'), ('Carry_Idle', 'Throw'),
                        ('Grab_Hold', 'Grab_Release'), ('Spawn', 'Ready')]:
        action = bpy.data.actions['Cat_' + left]
        core.choose(rig, action)
        bpy.context.scene.frame_set(int(action.frame_end))
        end = {p.name: p.matrix.copy() for p in rig.pose.bones if not p.name.startswith('CTRL_')}
        core.choose(rig, bpy.data.actions['Cat_' + right])
        bpy.context.scene.frame_set(1)
        error = max(abs(rig.pose.bones[n].matrix[i][j] - m[i][j]) for n, m in end.items() for i in range(4) for j in range(4))
        assert error < 1e-5, (left, right, 'phase seam', error)
        report['phase_seams'].append(dict(from_clip=left, to_clip=right, error=error))
    for obj in gears:
        obj.hide_set(visibility[obj.name])
    with open(os.path.join(EXPORT, 'Completion_Audit.json'), 'w', encoding='utf-8') as stream:
        json.dump(report, stream, indent=2)
    return report


def render(movie=False):
    scene = core.configure_studio()
    rig = bpy.data.objects['RIG_Cat']
    rig.data.pose_position = 'POSE'
    for obj in bpy.data.objects:
        if obj.name.startswith('GEAR_'):
            obj.hide_render = True
    scene.render.resolution_x = scene.render.resolution_y = 480
    scene.render.resolution_percentage = 100
    for index, (name, duration, loop, representative) in enumerate(CLIPS, 1):
        core.choose(rig, bpy.data.actions['Cat_' + name])
        if movie:
            folder = os.path.join(PREVIEW, 'Frames', name)
            os.makedirs(folder, exist_ok=True)
            frames = list(range(1, duration + 1, 2))
            if not loop and frames[-1] != duration + 1:
                frames.append(duration + 1)
            for number, frame in enumerate(frames, 1):
                scene.frame_set(frame)
                scene.render.filepath = os.path.join(folder, f'{number:04d}.png')
                bpy.ops.render.render(write_still=True)
        else:
            scene.frame_set(representative)
            scene.render.filepath = os.path.join(PREVIEW, f'{index:02d}_{name}.png')
            bpy.ops.render.render(write_still=True)
        print('PREVIEW_DONE', name, flush=True)


def main():
    os.makedirs(EXPORT, exist_ok=True)
    os.makedirs(PREVIEW, exist_ok=True)
    configure()
    if '--render' in sys.argv or '--movie' in sys.argv:
        bpy.ops.wm.open_mainfile(filepath=OUTPUT)
        render('--movie' in sys.argv)
        return
    bpy.ops.wm.open_mainfile(filepath=SOURCE)
    rig, mesh = bpy.data.objects['RIG_Cat'], bpy.data.objects['SK_Cat']
    core.activate(rig)
    rig.data.pose_position = 'POSE'
    bpy.context.scene.render.fps = 30
    bpy.context.scene.render.fps_base = 1
    before = fingerprint(mesh, rig)
    assert len([n for n in before['actions'] if n.startswith('Cat_')]) == 26
    actions = draft.author(rig, mesh)
    draft.validate(rig, mesh, actions)
    audit(rig, mesh, before)
    core.export(rig, mesh, actions)
    core.choose(rig, bpy.data.actions['Cat_Ready'])
    scene = bpy.context.scene
    scene.name = 'CAT | COMPLETE 44 CLIPS'
    scene.frame_start, scene.frame_end = 1, 37
    scene.frame_set(1)
    rig['Animation_Info'] = '44 actions: 10 base + 16 original skills unchanged + 18 completed interaction/match clips.'
    scene['animation_pack'] = 'In-place body clips. Gameplay controls displacement, target attachment, release and ragdoll handoff.'
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT)
    manifest = {'fps': 30, 'total_library_clips': 44, 'new_clip_count': 18,
                'clips': [dict(name=n, frames=d, loop=l, representative=r) for n, d, l, r in CLIPS]}
    with open(os.path.join(EXPORT, 'Clip_Manifest.json'), 'w', encoding='utf-8') as stream:
        json.dump(manifest, stream, indent=2)
    print('COMPLETION_SAVED', OUTPUT, flush=True)


if __name__ == '__main__':
    main()
