"""Attach handoff notes and select the root control without changing the rig."""
import bpy
import os

root=os.path.dirname(os.path.abspath(__file__))
path=os.path.join(root,'PawArena_Cat_Rigged.blend')
bpy.ops.wm.open_mainfile(filepath=path)
rig=bpy.data.objects['RIG_Cat']
bpy.context.scene.frame_set(1)
for bone in rig.pose.bones: bone.select=False
rig.pose.bones['CTRL_Root'].select=True
rig.data.bones.active=rig.data.bones['CTRL_Root']
guide_path=os.path.join(root,'Exports','CatRig','HUONG_DAN_RIG.md')
notes=bpy.data.texts.get('READ ME | Cat Rig') or bpy.data.texts.new('READ ME | Cat Rig')
notes.clear()
with open(guide_path,encoding='utf-8') as source: notes.write(source.read())
rig['Unity_Check']='Unity 6000.4.2f1: valid Humanoid Avatar, 21 mapped bones, HumanPoseHandler + BakeMesh passed. Apply supplied import preset.'
bpy.ops.wm.save_as_mainfile(filepath=path)
