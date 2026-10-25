"""Render evaluated rig poses and an x-ray skeleton overlay for review."""
import bpy
import math
import os
from mathutils import Vector

HERE=os.path.dirname(os.path.abspath(__file__))
OUT=os.path.join(os.path.dirname(HERE),'Previews','Characters','RigReview','00_Rig_Proof.png')
bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE,'PawArena_Cat_Rigged.blend'))
source=bpy.context.scene
rig=bpy.data.objects['RIG_Cat']
skin=bpy.data.objects['SK_Cat']
proof=bpy.data.scenes.new('Rig proof')
proof.world=source.world
proof.render.engine='BLENDER_EEVEE'
proof.render.resolution_x=2200
proof.render.resolution_y=760
proof.render.resolution_percentage=100
proof.view_settings.view_transform=source.view_settings.view_transform
proof.view_settings.look=source.view_settings.look
proof.view_settings.exposure=source.view_settings.exposure


def material(name,color,emission=False):
    mat=bpy.data.materials.new(name)
    mat.use_nodes=True
    p=mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=.75
    if emission:
        p.inputs['Emission Color'].default_value=(*color,1)
        p.inputs['Emission Strength'].default_value=.8
    return mat


clay=material('Proof clay',(.17,.22,.27))
bone_mat=material('Skeleton overlay',(.10,.85,.65),True)
text_mat=material('Labels',(.70,.82,.93),True)


def label(text,x,z,size):
    data=bpy.data.curves.new(text,'FONT')
    data.body=text
    data.align_x='CENTER'
    data.size=size
    data.materials.append(text_mat)
    obj=bpy.data.objects.new(text,data)
    proof.collection.objects.link(obj)
    obj.location=(x,-.9,z)
    obj.rotation_euler=(math.pi/2,0,0)


def bone_line(a,b):
    # Orthographic x-ray overlay; bone heights and lateral positions are exact.
    curve=bpy.data.curves.new('Skeleton overlay','CURVE')
    curve.dimensions='3D'
    curve.bevel_depth=.009
    curve.bevel_resolution=2
    spline=curve.splines.new('POLY')
    spline.points.add(1)
    for p,co in zip(spline.points,(a,b)): p.co=(*co,1)
    curve.materials.append(bone_mat)
    obj=bpy.data.objects.new('Bone overlay',curve)
    proof.collection.objects.link(obj)


for x,frame,title in [(-3.9,1,'BIND / SKELETON'),(-1.3,40,'GUARD / ELBOWS'),
                      (1.3,60,'CROUCH / LEG IK'),(3.9,80,'STEP / FOOT IK')]:
    source.frame_set(frame)
    bpy.context.view_layer.update()
    depsgraph=bpy.context.evaluated_depsgraph_get()
    evaluated=skin.evaluated_get(depsgraph)
    mesh=bpy.data.meshes.new_from_object(evaluated,depsgraph=depsgraph)
    obj=bpy.data.objects.new(title,mesh)
    proof.collection.objects.link(obj)
    obj.location.x=x
    if frame==1:
        mesh.materials.clear()
        mesh.materials.append(clay)
        for p in mesh.polygons: p.material_index=0
        for pb in rig.pose.bones:
            if pb.name.startswith(('CTRL_','Tail_')) or pb.name=='HeadGearSocket': continue
            bone_line((pb.head.x+x,-.55,pb.head.z),(pb.tail.x+x,-.55,pb.tail.z))
    else:
        obj.rotation_euler.z=-.38
    label(title,x,-.26,.115)
label('PAW ARENA  /  CAT RIG',0,2.76,.18)
label('30 EXPORT BONES    -    FK / IK CONTROLS    -    DEFORMATION CHECKS',0,2.55,.085)
for name,loc,energy,size in [('Key',(-4,-5,7),1800,8),('Fill',(5,-3,5),1250,8),('Rim',(0,3,7),1800,9)]:
    data=bpy.data.lights.new(name,'AREA')
    data.energy=energy
    data.shape='DISK'
    data.size=size
    obj=bpy.data.objects.new(name,data)
    proof.collection.objects.link(obj)
    obj.location=loc
    obj.rotation_euler=(Vector((0,0,1.1))-obj.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Proof camera')
cam=bpy.data.objects.new('Proof camera',data)
proof.collection.objects.link(cam)
cam.location=(0,-15,1.23)
cam.rotation_euler=(Vector((0,0,1.23))-cam.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO'
data.ortho_scale=10.8
proof.camera=cam
proof.render.filepath=OUT
bpy.context.window.scene=proof
bpy.ops.render.render(write_still=True)
