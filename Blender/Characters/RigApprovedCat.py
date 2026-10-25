"""Build an animation control rig and a clean export from the approved cat."""
import bpy
import math
import os
import json
import sys
from mathutils import Vector, Quaternion

HERE=os.path.dirname(os.path.abspath(__file__))
ROOT=os.path.dirname(os.path.dirname(HERE))
SOURCE=os.path.join(HERE,"PawArena_BaseCat_Review.blend")
OUTPUT=os.path.join(HERE,"PawArena_Cat_Rigged.blend")
PREVIEW=os.path.join(os.path.dirname(HERE),"Previews","Characters","RigReview")
EXPORT=os.path.join(HERE,"Exports","CatRig")
BODY_NAME="CAT | continuous head body arms legs paws"


def smooth(a,b,v):
    t=max(0,min(1,(v-a)/(b-a)))
    return t*t*(3-2*t)


def activate(obj):
    if bpy.context.object and bpy.context.object.mode!='OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj


def normalize(weights):
    keep=sorted(((k,v) for k,v in weights.items() if v>1e-6),key=lambda item:-item[1])[:4]
    total=sum(v for _,v in keep)
    assert total>0
    return {k:v/total for k,v in keep}


def mix(a,b,t):
    result={k:v*(1-t) for k,v in a.items()}
    for k,v in b.items(): result[k]=result.get(k,0)+v*t
    return result


def tail_point(t):
    p0,p1,p2,p3=[Vector(p) for p in [(0,.20,.52),(0,.86,.43),(0,1.00,.70),(0,.98,1.11)]]
    return (1-t)**3*p0+3*(1-t)**2*t*p1+3*(1-t)*t*t*p2+t**3*p3


def chain_weights(value, anchors):
    if value<=anchors[0][0]: return {anchors[0][1]:1}
    for (a,na),(b,nb) in zip(anchors,anchors[1:]):
        if value<=b:
            if na==nb: return {na:1}
            t=smooth(a,b,value)
            return {na:1-t,nb:t}
    return {anchors[-1][1]:1}


def body_weights(co):
    x,y,z=co
    ax=abs(x)
    side='Left' if x>=0 else 'Right'
    # Classify the head BEFORE limbs: wide cheeks must never follow the arms.
    if z>=1.36:
        ear=smooth(2.015,2.245,z)*smooth(.22,.32,ax)
        return normalize({'Head':1-ear,side+'Ear':ear})
    trunk=chain_weights(z,[(.66,'Hips'),(.82,'Spine'),(1.04,'Chest'),(1.27,'Chest'),(1.34,'Neck'),(1.36,'Head')])
    if z>.94 and ax>.25:
        arm=chain_weights(ax,[(.29,side+'Shoulder'),(.46,side+'UpperArm'),
                              (.60,side+'UpperArm'),(.82,side+'LowerArm'),
                              (.97,side+'LowerArm'),(1.085,side+'Hand')])
        amount=smooth(.25,.46,ax)*smooth(.94,1.07,z)*(1-smooth(1.32,1.36,z))
        return normalize(mix(trunk,arm,amount))
    if z<.63:
        leg=chain_weights(z,[(.18,side+'Foot'),(.26,side+'LowerLeg'),
                             (.265,side+'LowerLeg'),(.385,side+'UpperLeg')])
        toe=(1-smooth(-.265,-.18,y))*(1-smooth(.12,.19,z))*.55
        leg=mix(leg,{side+'Toes':1},toe)
        # The crotch is shared by both thighs, rather than hanging rigidly off Hips.
        center=(1-smooth(.035,.17,ax))*smooth(.28,.44,z)
        left=smooth(-.17,.17,x)
        leg=mix(leg,{'LeftUpperLeg':left,'RightUpperLeg':1-left},center)
        amount=(1-smooth(.40,.63,z))*(.65+.35*smooth(.025,.17,ax))
        return normalize(mix({'Hips':1},leg,amount))
    return normalize(trunk)


def refresh_weights(mesh):
    all_weights=[]
    for v in mesh.data.vertices:
        if any(mesh.vertex_groups[g.group].name.startswith('Tail_') for g in v.groups):
            all_weights.append({mesh.vertex_groups[g.group].name:g.weight for g in v.groups})
        else:
            all_weights.append(body_weights(v.co))
    mesh.vertex_groups.clear()
    groups={}
    for v,weights in zip(mesh.data.vertices,all_weights):
        for name,w in weights.items():
            if name not in groups: groups[name]=mesh.vertex_groups.new(name=name)
            groups[name].add([v.index],w,'REPLACE')


def prepare_meshes(cat):
    # Bake the approved subdivision shape, not the earlier unsuccessful model.
    objects=[o for o in cat.objects if o.type in {'MESH','CURVE'}]
    for obj in objects:
        name=obj.name
        activate(obj)
        if obj.type=='CURVE':
            bpy.ops.object.convert(target='MESH')
            obj=bpy.context.object
        for modifier in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        rest=obj.data.attributes.new('cat_rest_position','FLOAT_VECTOR','POINT')
        for v in obj.data.vertices: rest.data[v.index].vector=v.co
        weights=[]
        if name==BODY_NAME:
            weights=[body_weights(v.co) for v in obj.data.vertices]
        elif name.startswith('Tail'):
            samples=[tail_point(i/160) for i in range(161)]
            for v in obj.data.vertices:
                index=min(range(161),key=lambda i:(v.co-samples[i]).length_squared)
                weights.append(chain_weights(index/160,[(.1+i*.2,f'Tail_{i+1:02d}') for i in range(5)]))
        elif name.startswith('Palm'):
            weights=[{'LeftHand' if v.co.x>0 else 'RightHand':1} for v in obj.data.vertices]
        elif name.startswith('Paw'):
            weights=[body_weights(v.co) for v in obj.data.vertices]
        else:
            weights=[{'Head':1} for v in obj.data.vertices]
        groups={name:obj.vertex_groups.new(name=name) for name in set(k for w in weights for k in w)}
        for v,w in zip(obj.data.vertices,weights):
            for name,weight in normalize(w).items(): groups[name].add([v.index],weight,'REPLACE')
    # Freeze 3D-generated fur coordinates to the bind surface for deformation.
    for mat in bpy.data.materials:
        if not mat.use_nodes: continue
        tree=mat.node_tree
        for link in list(tree.links):
            if link.from_node.type=='TEX_COORD' and link.from_socket.name=='Object':
                attr=tree.nodes.new('ShaderNodeAttribute')
                attr.attribute_name='cat_rest_position'
                tree.links.new(attr.outputs['Vector'],link.to_socket)
    meshes=[o for o in cat.objects if o.type=='MESH']
    activate(bpy.data.objects[BODY_NAME])
    for obj in meshes: obj.select_set(True)
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name='SK_Cat'
    return obj


def make_bones():
    bones=[]
    def add(name,head,tail,parent=None): bones.append((name,Vector(head),Vector(tail),parent))
    add('Root',(0,0,0),(0,0,.18))
    add('Hips',(0,0,.53),(0,0,.72),'Root')
    add('Spine',(0,0,.72),(0,0,.94),'Hips')
    add('Chest',(0,0,.94),(0,0,1.19),'Spine')
    add('Neck',(0,0,1.19),(0,0,1.36),'Chest')
    add('Head',(0,0,1.36),(0,0,2.06),'Neck')
    for side,s in [('Left',1),('Right',-1)]:
        add(side+'Shoulder',(s*.10,0,1.17),(s*.32,0,1.17),'Chest')
        add(side+'UpperArm',(s*.32,0,1.17),(s*.72,-.012,1.17),side+'Shoulder')
        add(side+'LowerArm',(s*.72,-.012,1.17),(s*1.04,0,1.17),side+'UpperArm')
        add(side+'Hand',(s*1.04,0,1.17),(s*1.20,0,1.17),side+'LowerArm')
        add(side+'UpperLeg',(s*.239,0,.53),(s*.239,-.016,.30),'Hips')
        add(side+'LowerLeg',(s*.239,-.016,.30),(s*.239,0,.145),side+'UpperLeg')
        add(side+'Foot',(s*.239,0,.145),(s*.239,-.19,.07),side+'LowerLeg')
        add(side+'Toes',(s*.239,-.19,.07),(s*.239,-.31,.07),side+'Foot')
        add(side+'Ear',(s*.32,0,2.025),(s*.493,.018,2.345),'Head')
    for i in range(5): add(f'Tail_{i+1:02d}',tail_point(i/5),tail_point((i+1)/5),'Hips' if i==0 else f'Tail_{i:02d}')
    add('HeadGearSocket',(0,0,2.11),(0,0,2.23),'Head')
    return bones


def driver(constraint,rig,prop):
    fc=constraint.driver_add('influence')
    d=fc.driver
    d.type='SCRIPTED'
    v=d.variables.new()
    v.name='switch'
    v.targets[0].id=rig
    v.targets[0].data_path='["'+prop+'"]'
    d.expression='switch'


def create_rig(cat,bones):
    data=bpy.data.armatures.new('Cat_Humanoid_Skeleton')
    rig=bpy.data.objects.new('RIG_Cat',data)
    cat.objects.link(rig)
    rig.show_in_front=True
    activate(rig)
    bpy.ops.object.mode_set(mode='EDIT')
    for prefix in ('','CTRL_'):
        for name,head,tail,parent in bones:
            if prefix and name=='HeadGearSocket': continue
            b=data.edit_bones.new(prefix+name)
            b.head,b.tail=head,tail
            b.use_deform=not prefix
            if parent: b.parent=data.edit_bones[prefix+parent]
            b.inherit_scale='FIX_SHEAR'
    for side,s in [('Left',1),('Right',-1)]:
        for part,source,pole in [('Hand',side+'Hand',(s*.72,-.65,1.17)),
                                ('Foot',side+'Foot',(s*.239,-.65,.30))]:
            src=data.edit_bones[source]
            b=data.edit_bones.new('CTRL_'+side+part+'_IK')
            b.head,b.tail=src.head,src.tail
            b.parent=data.edit_bones['CTRL_Root']
            b.use_deform=False
            b=data.edit_bones.new('CTRL_'+side+part+'_Pole')
            b.head=pole
            b.tail=Vector(pole)+Vector((0,0,.10))
            b.parent=data.edit_bones['CTRL_Root']
            b.use_deform=False
    bpy.ops.object.mode_set(mode='OBJECT')
    for name,_,_,_ in bones:
        if name=='HeadGearSocket': continue
        c=rig.pose.bones[name].constraints.new('COPY_TRANSFORMS')
        c.name='Animation control -> export bone'
        c.target=rig
        c.subtarget='CTRL_'+name
        c.owner_space='POSE'
        c.target_space='POSE'
    for side in ('Left','Right'):
        for kind,part,lower in [('Arm','Hand','LowerArm'),('Leg','Foot','LowerLeg')]:
            prop=kind+'_IK_'+side
            rig[prop]=0.0
            rig.id_properties_ui(prop).update(min=0,max=1,description='0 = FK rotation; 1 = IK target and pole')
            pb=rig.pose.bones['CTRL_'+side+lower]
            c=pb.constraints.new('IK')
            c.name=kind+' IK - two joints, no stretch'
            c.target=rig
            c.subtarget='CTRL_'+side+part+'_IK'
            c.pole_target=rig
            c.pole_subtarget='CTRL_'+side+part+'_Pole'
            c.chain_count=2
            c.use_stretch=False
            c.iterations=100
            driver(c,rig,prop)
            c2=rig.pose.bones['CTRL_'+side+part].constraints.new('COPY_ROTATION')
            c2.name='IK end orientation'
            c2.target=rig
            c2.subtarget='CTRL_'+side+part+'_IK'
            c2.owner_space='POSE'
            c2.target_space='POSE'
            driver(c2,rig,prop)
    for pb in rig.pose.bones:
        pb.rotation_mode='QUATERNION'
        pb.ik_stretch=0
        if pb.name.startswith('CTRL_'):
            pb.lock_scale=(True,True,True)
            pb.lock_location=(not (pb.name.endswith('_IK') or pb.name.endswith('_Pole') or pb.name in ('CTRL_Root','CTRL_Hips')),)*3
    return rig


def shapes(rig):
    coll=bpy.data.collections.new('03 | CONTROL SHAPES - hidden render')
    bpy.context.scene.collection.children.link(coll)
    coll.hide_render=True
    def shape(name,kind):
        if kind=='circle':
            vs=[(math.cos(i*math.tau/32),0,math.sin(i*math.tau/32)) for i in range(32)]
            es=[(i,(i+1)%32) for i in range(32)]
        else:
            vs=[(x,y,z) for x in (-1,1) for y in (-1,1) for z in (-1,1)]
            es=[(i,j) for i in range(8) for j in range(i+1,8) if sum(vs[i][k]!=vs[j][k] for k in range(3))==1]
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata(vs,es,[])
        obj=bpy.data.objects.new(name,mesh)
        coll.objects.link(obj)
        obj.hide_render=True
        obj.hide_set(True)
        return obj
    circle=shape('WGT_Ring','circle')
    box=shape('WGT_Box','box')
    collections={n:rig.data.collections.new(n) for n in ['Deform / export','Controls / body','Controls / FK limbs','Controls / IK targets','Controls / ears + tail']}
    for b in rig.data.bones:
        pb=rig.pose.bones[b.name]
        if not b.name.startswith('CTRL_'):
            collections['Deform / export'].assign(b)
            b.color.palette='THEME05'
            continue
        name=b.name[5:]
        if name.endswith(('_IK','_Pole')):
            group='Controls / IK targets'
            pb.custom_shape=box
            size=.07 if name.endswith('_Pole') else .13
            b.color.palette='THEME04'
        else:
            pb.custom_shape=circle
            if name in ('Root','Hips','Spine','Chest','Neck','Head'):
                group='Controls / body'
                size={'Root':.65,'Hips':.40,'Spine':.28,'Chest':.32,'Neck':.17,'Head':.54}[name]
            elif 'Tail' in name or 'Ear' in name:
                group='Controls / ears + tail'
                size=.10
            else:
                group='Controls / FK limbs'
                size=.12
            b.color.palette='THEME03' if name.startswith('Left') else ('THEME01' if name.startswith('Right') else 'THEME09')
        collections[group].assign(b)
        pb.use_custom_shape_bone_size=False
        pb.custom_shape_scale_xyz=(size,)*3
    collections['Deform / export'].is_visible=False


def calibrate_ik(rig):
    errors={}
    for side in ('Left','Right'):
        for kind,lower in [('Arm','LowerArm'),('Leg','LowerLeg')]:
            prop=kind+'_IK_'+side
            rig[prop]=1.0
            rig.update_tag()
            pb=rig.pose.bones['CTRL_'+side+lower]
            c=next(c for c in pb.constraints if c.type=='IK')
            rest_head=pb.bone.head_local.copy()
            rest_tail=pb.bone.tail_local.copy()
            def cost(angle):
                c.pole_angle=angle
                bpy.context.view_layer.update()
                return (pb.head-rest_head).length_squared+(pb.tail-rest_tail).length_squared
            candidates=[(-math.pi+i*math.tau/64) for i in range(65)]
            best=min(candidates,key=cost)
            for step in (.02,.002,.0002,.00002):
                best=min([best+j*step for j in range(-6,7)],key=cost)
            error=math.sqrt(cost(best))
            errors[prop]=error
            assert error<.002,(prop,error)
            rig[prop]=0.0
            rig.update_tag()
            bpy.context.view_layer.update()
    return errors


def rotate(rig,name,axis,angle):
    pb=rig.pose.bones['CTRL_'+name]
    local=pb.bone.matrix_local.to_3x3().inverted()@Vector(axis)
    pb.rotation_quaternion=Quaternion(local,math.radians(angle))


def translate(rig,name,delta):
    pb=rig.pose.bones['CTRL_'+name]
    pb.location=pb.bone.matrix_local.to_3x3().inverted()@Vector(delta)


def reset_pose(rig):
    for pb in rig.pose.bones:
        pb.location=(0,0,0)
        pb.rotation_quaternion=(1,0,0,0)
        pb.scale=(1,1,1)
    for side in ('Left','Right'):
        for kind in ('Arm','Leg'): rig[kind+'_IK_'+side]=0.0
    rig.update_tag()
    bpy.context.view_layer.update()


POSES=[(1,'01 - Bind T-pose'),(20,'02 - Relaxed FK'),(40,'03 - Guard / elbow bend'),
       (60,'04 - Crouch / leg IK'),(80,'05 - Step / foot IK'),(100,'06 - Head ears tail'),(120,'07 - Hand IK reach')]


def set_pose(rig,frame):
    reset_pose(rig)
    if frame==1: return
    for side,s in [('Left',1),('Right',-1)]:
        rotate(rig,side+'UpperArm',(0,1,0),s*65)
        rig['Leg_IK_'+side]=1.0
    if frame==40:
        for side,s in [('Left',1),('Right',-1)]:
            rotate(rig,side+'UpperArm',(0,1,0),s*40)
            rotate(rig,side+'LowerArm',(0,0,1),-s*90)
    elif frame==60:
        translate(rig,'Hips',(0,.015,-.115))
        rotate(rig,'Spine',(1,0,0),12)
        for side,s in [('Left',1),('Right',-1)]:
            rotate(rig,side+'UpperArm',(0,1,0),s*40)
            rotate(rig,side+'LowerArm',(0,0,1),-s*75)
    elif frame==80:
        translate(rig,'Hips',(0,0,-.04))
        translate(rig,'LeftFoot_IK',(0,-.15,.12))
        translate(rig,'RightFoot_IK',(0,.10,0))
        rotate(rig,'LeftUpperArm',(0,1,0),80)
        rotate(rig,'RightUpperArm',(0,1,0),-40)
    elif frame==100:
        rotate(rig,'Head',(0,0,1),28)
        rotate(rig,'LeftEar',(0,1,0),-16)
        rotate(rig,'RightEar',(0,1,0),12)
        for i in range(5): rotate(rig,f'Tail_{i+1:02d}',(0,0,1),12)
    elif frame==120:
        for side,s in [('Left',1),('Right',-1)]:
            rotate(rig,side+'UpperArm',(0,1,0),0)
            rig['Arm_IK_'+side]=1.0
            translate(rig,side+'Hand_IK',(-s*.32,-.28,-.13))
    rig.update_tag()
    bpy.context.view_layer.update()


def create_checks(rig):
    scene=bpy.context.scene
    scene.frame_start=1
    scene.frame_end=120
    for frame,label in POSES:
        scene.frame_set(frame)
        set_pose(rig,frame)
        for pb in rig.pose.bones:
            if pb.name.startswith('CTRL_'):
                pb.keyframe_insert('location',frame=frame,group=pb.name)
                pb.keyframe_insert('rotation_quaternion',frame=frame,group=pb.name)
        for side in ('Left','Right'):
            for kind in ('Arm','Leg'): rig.keyframe_insert(data_path='["'+kind+'_IK_'+side+'"]',frame=frame,group='IK FK switches')
        marker=scene.timeline_markers.new(label,frame=frame)
    rig.animation_data.action.name='Cat_Rig_Validation_Poses_NOT_GAME_ANIMATION'
    rig.animation_data.action.use_fake_user=True
    scene.frame_set(1)


def bake_color(mesh):
    activate(mesh)
    scene=bpy.context.scene
    scene.frame_set(1)
    if not mesh.data.uv_layers.get('Cat_Atlas'):
        mesh.data.uv_layers.new(name='Cat_Atlas')
    layer=mesh.data.uv_layers['Cat_Atlas']
    mesh.data.uv_layers.active=layer
    # Existing eye and tail UVs stay available to their source shaders.
    for mat in mesh.data.materials:
        for node in list(mat.node_tree.nodes):
            if node.type=='TEX_COORD':
                for link in list(node.outputs['UV'].links):
                    uvnode=mat.node_tree.nodes.new('ShaderNodeUVMap')
                    uvnode.uv_map='Eye local' if mat.name.startswith('Eyes | conforming') else 'Tail length'
                    mat.node_tree.links.new(uvnode.outputs['UV'],link.to_socket)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    layer.active_render=True
    image=bpy.data.images.new('Cat_TabBy_BaseColor',width=4096,height=4096,alpha=False)
    image.colorspace_settings.name='sRGB'
    image.filepath_raw=os.path.join(EXPORT,'Cat_BaseColor.png')
    image.file_format='PNG'
    for mat in mesh.data.materials:
        node=mat.node_tree.nodes.new('ShaderNodeTexImage')
        node.name='BAKE_TARGET'
        node.image=image
        mat.node_tree.nodes.active=node
        node.select=True
    scene.render.engine='CYCLES'
    scene.cycles.samples=1
    scene.render.bake.use_pass_direct=False
    scene.render.bake.use_pass_indirect=False
    scene.render.bake.use_pass_color=True
    scene.render.bake.margin=12
    bpy.ops.object.bake(type='DIFFUSE')
    image.save()
    image.pack()
    eye_indices={i for i,m in enumerate(mesh.data.materials) if m.name.startswith('Eyes | conforming')}
    assignments=[1 if p.material_index in eye_indices else 0 for p in mesh.data.polygons]
    mats=[]
    for name,roughness in [('M_Cat_Coat',.58),('M_Cat_Eyes',.28)]:
        mat=bpy.data.materials.new(name)
        mat.use_nodes=True
        tree=mat.node_tree
        p=tree.nodes.get('Principled BSDF')
        p.inputs['Roughness'].default_value=roughness
        p.inputs['Specular IOR Level'].default_value=.28
        tex=tree.nodes.new('ShaderNodeTexImage')
        tex.image=image
        tex.location=(-350,200)
        uv=tree.nodes.new('ShaderNodeUVMap')
        uv.uv_map='Cat_Atlas'
        uv.location=(-600,200)
        tree.links.new(uv.outputs[0],tex.inputs['Vector'])
        tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
        mats.append(mat)
    mesh.data.materials.clear()
    for mat in mats: mesh.data.materials.append(mat)
    for p,i in zip(mesh.data.polygons,assignments): p.material_index=i
    # Export only the baked atlas UV, avoiding importer UV-channel ambiguity.
    for uv in list(mesh.data.uv_layers):
        if uv.name!='Cat_Atlas': mesh.data.uv_layers.remove(uv)
    scene.render.engine='BLENDER_EEVEE'


def validate(mesh,rig,ik_errors):
    results={}
    for v in mesh.data.vertices:
        weights=[g.weight for g in v.groups if g.weight>1e-6]
        assert 1<=len(weights)<=4
        assert abs(sum(weights)-1)<1e-5
    bind=[v.co.copy() for v in mesh.data.vertices]
    scene=bpy.context.scene
    for frame,label in POSES:
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        result=evaluated.to_mesh()
        assert len(result.vertices)==len(bind)
        assert all(math.isfinite(c) for v in result.vertices for c in v.co)
        distances=[(v.co-p).length for v,p in zip(result.vertices,bind)]
        if frame==1: assert max(distances)<1e-5,max(distances)
        results[label]={'max_displacement':max(distances)}
        stretch=0
        for edge in mesh.data.edges:
            a,b=edge.vertices
            rest_length=(bind[a]-bind[b]).length
            if rest_length>.0001:
                stretch=max(stretch,(result.vertices[a].co-result.vertices[b].co).length/rest_length)
        results[label]['maximum_edge_stretch']=stretch
        assert stretch<8,(label,'skin spike or discontinuity',stretch)
        if frame in (20,40,60,80,120):
            # Each end effector must agree with its IK target whenever enabled.
            for side in ('Left','Right'):
                for kind,part,lower in [('Arm','Hand','LowerArm'),('Leg','Foot','LowerLeg')]:
                    if rig[kind+'_IK_'+side]>.99:
                        error=(rig.pose.bones[side+lower].tail-rig.pose.bones['CTRL_'+side+part+'_IK'].head).length
                        results[label][kind+'_IK_'+side+'_error']=error
                        assert error<.006,(label,kind,side,error)
        evaluated.to_mesh_clear()
    scene.frame_set(1)
    report={'weights':'Normalized; maximum four deform influences per vertex',
            'bind_pose':'Matches approved evaluated shape within 0.00001 m',
            'skinning':'Linear blend, matching the usual Unity skinning model',
            'vertices':len(mesh.data.vertices),'triangles':sum(len(p.vertices)-2 for p in mesh.data.polygons),
            'control_bones':sum(b.name.startswith('CTRL_') for b in rig.data.bones),
            'export_bones':sum(not b.name.startswith('CTRL_') for b in rig.data.bones),
            'ik_calibration':ik_errors,'poses':results,
            'unity_avatar_validation':'Not run in Unity; humanoid hierarchy prepared, not claimed as a validated Avatar.'}
    with open(os.path.join(HERE,'PawArena_Cat_Rig_Report.json'),'w',encoding='utf-8') as f: json.dump(report,f,indent=2)
    print('RIG_VALIDATION',json.dumps(report))


def previews(mesh,rig):
    scene=bpy.context.scene
    cam=scene.camera
    cam.location=(4,-7,2.9)
    cam.rotation_euler=(Vector((0,0,1.13))-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=2.95
    scene.render.resolution_x=900
    scene.render.resolution_y=900
    for frame,label in POSES:
        scene.frame_set(frame)
        scene.render.filepath=os.path.join(PREVIEW,label.split(' - ')[0]+'_'+label.split(' - ')[1].replace(' / ','_').replace(' ','_')+'.png')
        bpy.ops.render.render(write_still=True)
    scene.frame_set(1)


def export_fbx(mesh,rig):
    # A separate export skeleton: never leak Blender control bones or constraints.
    scene=bpy.context.scene
    scene.frame_set(1)
    data=rig.data.copy()
    export_rig=bpy.data.objects.new('CatArmature',data)
    scene.collection.objects.link(export_rig)
    activate(export_rig)
    bpy.ops.object.mode_set(mode='EDIT')
    for b in list(data.edit_bones):
        if b.name.startswith('CTRL_'): data.edit_bones.remove(b)
    bpy.ops.object.mode_set(mode='OBJECT')
    export_mesh=mesh.copy()
    export_mesh.data=mesh.data.copy()
    scene.collection.objects.link(export_mesh)
    export_mesh.parent=export_rig
    export_mesh.modifiers.get('Skin deformation | linear').object=export_rig
    activate(export_rig)
    export_mesh.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT,'SK_PawArena_Cat.fbx'),use_selection=True,
        object_types={'ARMATURE','MESH'},add_leaf_bones=False,use_armature_deform_only=True,
        bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        mesh_smooth_type='FACE',use_triangles=True,path_mode='COPY',embed_textures=True)
    bpy.data.objects.remove(export_mesh,do_unlink=True)
    bpy.data.objects.remove(export_rig,do_unlink=True)


def main():
    os.makedirs(PREVIEW,exist_ok=True)
    os.makedirs(EXPORT,exist_ok=True)
    prepared=os.path.join(HERE,'_Cat_Rig_Prepared.blend')
    reuse='--reuse-prepared' in sys.argv
    bpy.ops.wm.open_mainfile(filepath=prepared if reuse else SOURCE)
    scene=bpy.context.scene
    scene.name='CAT | ANIMATION RIG'
    # The review-only sheet is retained in the approved source, not in this rig.
    for other in list(bpy.data.scenes):
        if other!=scene: bpy.data.scenes.remove(other)
    cat=bpy.data.collections['01 | RIGGED CAT' if reuse else '01 | BASE CAT - NO RIG']
    cat.name='01 | RIGGED CAT'
    stage=bpy.data.collections['02 | REVIEW STUDIO']
    stage.hide_viewport=False
    if reuse:
        mesh=bpy.data.objects['SK_Cat']
        refresh_weights(mesh)
    else:
        mesh=prepare_meshes(cat)
        bake_color(mesh)
        bpy.ops.wm.save_as_mainfile(filepath=prepared)
    rig=create_rig(cat,make_bones())
    mesh.parent=rig
    modifier=mesh.modifiers.new('Skin deformation | linear','ARMATURE')
    modifier.object=rig
    modifier.use_deform_preserve_volume=False
    errors=calibrate_ik(rig)
    shapes(rig)
    create_checks(rig)
    validate(mesh,rig,errors)
    previews(mesh,rig)
    export_fbx(mesh,rig)
    scene.frame_set(1)
    stage.hide_viewport=True
    activate(rig)
    bpy.ops.object.mode_set(mode='POSE')
    rig['Rig_Info']='FK: rotate CTRL bones. IK: set Arm_IK/Leg_IK property to 1 and move target; poles set bend direction.'
    rig['Source']='Approved PawArena_BaseCat_Review.blend; source unchanged.'
    scene['review_scope']='Rigged approved base; validation poses are not production gameplay animations.'
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT)
    print('RIG_SAVED',OUTPUT)


if __name__=='__main__': main()
