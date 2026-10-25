"""Source integrity, deformation checks and actual posed preview renders."""
import bpy, os, sys, json, math
from mathutils import Vector
HERE=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,HERE)
from BuildCatSkins import fingerprint
import BuildCatAnimations as core
PREVIEW=os.path.join(os.path.dirname(HERE),'Previews','Characters','Gear')

def main():
    bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE,'PawArena_Cat_Abilities.blend'))
    before=fingerprint(bpy.data.objects['SK_Cat'],bpy.data.objects['RIG_Cat'])
    bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE,'PawArena_Cat_Gear.blend'))
    rig=bpy.data.objects['RIG_Cat']; mesh=bpy.data.objects['SK_Cat']; scene=bpy.context.scene
    after=fingerprint(mesh,rig)
    assert before==after, 'Approved base, rig, weights, UV or animation changed'
    gears=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('GEAR_')]
    assert len(gears)==8
    rest={o.name:[v.co.copy() for v in o.data.vertices] for o in gears}
    for o in gears:
        assert o.data.uv_layers.active is not None
        assert all(0<len(v.groups)<=4 and abs(sum(g.weight for g in v.groups)-1)<1e-5 for v in o.data.vertices)
        o.hide_set(False)
    rig.data.pose_position='POSE'
    actions=[a for a in bpy.data.actions if a.name.startswith('Cat_')]
    report={'source_integrity':'identical geometry, topology, UV, weights, rest rig and all action curves','gear_count':8,'actions':[],'max_edge_stretch':{}}
    for action in actions:
        core.choose(rig,action); start,end=action.frame_range
        frames=[int(start+(end-start)*t) for t in [0,.25,.5,.75,1]]
        for frame in frames:
            scene.frame_set(frame); bpy.context.view_layer.update(); dg=bpy.context.evaluated_depsgraph_get()
            for o in gears:
                evaluated=o.evaluated_get(dg); data=evaluated.to_mesh(); coords=[v.co for v in data.vertices]
                assert all(math.isfinite(c) and abs(c)<10 for v in coords for c in v), (action.name,frame,o.name)
                maxstretch=1
                for edge in o.data.edges:
                    i,j=edge.vertices; length=(rest[o.name][i]-rest[o.name][j]).length
                    if length>.005: maxstretch=max(maxstretch,(coords[i]-coords[j]).length/length)
                report['max_edge_stretch'][o.name]=max(report['max_edge_stretch'].get(o.name,1),maxstretch)
                evaluated.to_mesh_clear()
        report['actions'].append({'name':action.name,'sampled_frames':frames})
    with open(os.path.join(HERE,'Exports','Gear_Validation.json'),'w') as f: json.dump(report,f,indent=2)
    render_cases=[('Cat_Idle',19,['Topknot','Yokozuna_Rope']),('Cat_Run',6,['Hachimaki','Kesho_Mawashi']),('Cat_Punch_Right',7,['Hachimaki','Crimson_Mawashi','Gyoji_Fan'])]
    for name,frame,visible in render_cases:
        action=bpy.data.actions.get(name)
        if action is None: continue
        core.choose(rig,action); scene.frame_set(frame)
        for o in gears: o.hide_render=o.name.removeprefix('GEAR_') not in visible
        scene.render.filepath=os.path.join(PREVIEW,'Pose_'+name+'.png'); bpy.ops.render.render(write_still=True)
    print('GEAR_VALIDATED',json.dumps(report),flush=True)

if __name__=='__main__': main()
