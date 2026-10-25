import bpy
import os

root = os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(root, "PawArena_BaseCat_Review.blend"))
scene = bpy.context.scene
bpy.data.collections["02 | REVIEW STUDIO"].hide_viewport = False
scene.view_settings.view_transform = "Khronos PBR Neutral"
scene.view_settings.look = "None"
scene.view_settings.exposure = -.6
scene.render.filepath = os.path.join(os.path.dirname(root), "Previews", "Characters", "BaseReview", "LightingCheck.png")
bpy.ops.render.render(write_still=True)
