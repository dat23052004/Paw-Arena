"""Build ONLY the sixteen requested skill clips; retain the approved first ten."""
import os
import sys
import json
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import BuildCatAnimations02 as base
import CatAbilityPoses as abilities

CLIPS = list(abilities.CLIPS)
OUTPUT = os.path.join(HERE, 'PawArena_Cat_Abilities.blend')
EXPORT = os.path.join(HERE, 'Exports', 'CatAbilities')
PREVIEW = os.path.join(os.path.dirname(HERE), 'Previews', 'Characters', 'Abilities')


def configure():
    base.CLIPS, base.EXPORT, base.PREVIEW = CLIPS, EXPORT, PREVIEW
    base.configure_core()


def main():
    os.makedirs(EXPORT, exist_ok=True)
    os.makedirs(PREVIEW, exist_ok=True)
    configure()
    reuse = '--render-only' in sys.argv or '--export-only' in sys.argv
    bpy.ops.wm.open_mainfile(filepath=OUTPUT if reuse else base.SOURCE)
    rig, mesh = bpy.data.objects['RIG_Cat'], bpy.data.objects['SK_Cat']
    if reuse:
        actions = {name:bpy.data.actions['Cat_'+name] for name,*_ in CLIPS}
        requested = [arg.split('=',1)[1] for arg in sys.argv if arg.startswith('--clip=')]
        if requested:
            assert all(name in actions for name in requested)
            base.core.CLIPS = [clip for clip in CLIPS if clip[0] in requested]
    else:
        actions = base.author(rig, mesh)
        report = base.validate(rig, mesh, actions)
        assert len(report['clips']) == 16
        assert min(c['lowest_vertex_z_m'] for c in report['clips']) >= -.0001
        for a,b in [('Ability_Pounce_Start','Ability_Pounce_Air'),
                    ('Ability_Pounce_Air','Ability_Pounce_Land'),
                    ('Ability_Anchor_Start','Ability_Anchor_Hold'),
                    ('Ability_Anchor_Hold','Ability_Anchor_End')]:
            base.core.choose(rig,actions[a])
            bpy.context.scene.frame_set(int(actions[a].frame_end))
            end = {pb.name:pb.matrix.copy() for pb in rig.pose.bones}
            base.core.choose(rig,actions[b])
            bpy.context.scene.frame_set(1)
            error = max(abs(pb.matrix[i][j]-end[pb.name][i][j]) for pb in rig.pose.bones for i in range(4) for j in range(4))
            assert error < .00001, ('phase seam',a,b,error)
        base.core.choose(rig,actions['Ability_FluffUp'])
        scene = bpy.context.scene
        scene.frame_start,scene.frame_end = 1,28
        scene.frame_set(1)
        scene.name = 'CAT | 10 BASE + 16 ABILITY CLIPS'
        scene['animation_pack'] = 'Sixteen ability body animations from GDD v1.1. No additional combat clips.'
        rig['Animation_Info'] = '26 actions: first 10 unchanged + 16 ability clips. No gameplay, effects or facial animation.'
        assert len([a for a in bpy.data.actions if a.name.startswith('Cat_')]) == 26
        bpy.data.collections['02 | REVIEW STUDIO'].hide_viewport = True
        bpy.ops.wm.save_as_mainfile(filepath=OUTPUT)
        with open(os.path.join(EXPORT,'Clip_Manifest.json'),'w',encoding='utf-8') as stream:
            json.dump({'fps':30, 'source':'Paw Arena GDD v1.1 / section 5',
                       'clips':[{'name':n,'frames':d,'loop':l,'representative':r} for n,d,l,r in CLIPS]},stream,indent=2)
    if '--export-only' in sys.argv or (not reuse and '--draft' not in sys.argv):
        base.core.export(rig, mesh, actions)
    if '--no-render' not in sys.argv and '--export-only' not in sys.argv:
        base.core.render_previews(rig, actions, '--movie' in sys.argv)
    print('ABILITY_PACK_DONE',OUTPUT,flush=True)


if __name__ == '__main__':
    main()
