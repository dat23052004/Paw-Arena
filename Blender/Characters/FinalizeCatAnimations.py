"""Embed the handoff guide in the editable animation file, retaining all clips."""
import bpy
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from BuildCatAnimations import OUTPUT, EXPORT, CLIPS, choose

bpy.ops.wm.open_mainfile(filepath=OUTPUT)
rig = bpy.data.objects['RIG_Cat']
assert len(bpy.data.actions) == 10
assert all(bpy.data.actions.get('Cat_' + name) for name, *_ in CLIPS)
guide = bpy.data.texts.get('READ ME | Animation Pack 01')
guide.clear()
with open(os.path.join(EXPORT, 'HUONG_DAN_ANIMATION.md'), encoding='utf-8') as stream:
    guide.write(stream.read())
choose(rig, bpy.data.actions['Cat_Idle'])
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 73
bpy.context.scene.frame_set(1)
bpy.data.collections['02 | REVIEW STUDIO'].hide_viewport = True
rig['Animation_Info'] = '10 Cat_ actions; arms FK, feet IK. 30 fps. In-place. See embedded Vietnamese guide.'
bpy.ops.wm.save_as_mainfile(filepath=OUTPUT)
print('FINAL_ANIMATION_FILE', OUTPUT)
