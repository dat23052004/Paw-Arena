"""Eight fitted cosmetic gears. Never modifies the approved source cat or animations."""
import bpy, math, os, sys, json, hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from RigApprovedCat import body_weights
ROOT = os.path.dirname(os.path.dirname(HERE))
SOURCE = os.path.join(HERE, 'PawArena_Cat_Abilities.blend')
OUTPUT = os.path.join(HERE, 'PawArena_Cat_Gear.blend')
EXPORT = os.path.join(ROOT, 'Assets/_Project/Models/Characters/Cat/Gear')
PREVIEW = os.path.join(ROOT, 'Blender/Previews/Characters/Gear')
TAU = math.tau
PARTS = []
CATALOG = []

def material(name, color, metal=0, rough=.48):
    c = tuple(int(color[i:i+2],16)/255 for i in (0,2,4))
    lin = tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in c)
    m=bpy.data.materials.new('Gear_'+name); m.diffuse_color=(*lin,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*lin,1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    m['srgb']=list(c); m['metallic']=metal; m['roughness']=rough
    return m

def mesh(name, vs, fs, mat, smooth=True):
    d=bpy.data.meshes.new(name); d.from_pydata(vs,[],fs); d.update()
    o=bpy.data.objects.new(name,d); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    for p in d.polygons: p.use_smooth=smooth
    PARTS.append(o); return o

def tube(name, points, radius, mat, sides=8, closed=False, flatten=1):
    ps=[Vector(p) for p in points]; vs=[]; fs=[]; n=len(ps)
    for i,p in enumerate(ps):
        t=(ps[(i+1)%n]-ps[(i-1)%n] if closed else ps[min(n-1,i+1)]-ps[max(0,i-1)]).normalized()
        guide=Vector((0,0,1)) if abs(t.z)<.88 else Vector((0,1,0))
        a=t.cross(guide).normalized(); b=t.cross(a).normalized()
        r=radius[i] if isinstance(radius,list) else radius
        for j in range(sides):
            ang=j*TAU/sides; vs.append(p+r*(math.cos(ang)*a+math.sin(ang)*b*flatten))
    for i in range(n if closed else n-1):
        for j in range(sides): fs.append((i*sides+j,i*sides+(j+1)%sides,((i+1)%n)*sides+(j+1)%sides,((i+1)%n)*sides+j))
    if not closed: fs += [tuple(reversed(range(sides))),tuple((n-1)*sides+j for j in range(sides))]
    return mesh(name,vs,fs,mat)

def curve_points(points, samples=8):
    ps=[Vector(points[0])]+[Vector(p) for p in points]+[Vector(points[-1])]; out=[]
    for k in range(1,len(ps)-2):
        a,b,c,d=ps[k-1:k+3]
        for j in range(samples):
            t=j/samples
            out.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    return out+[Vector(points[-1])]

def cord(name, points, r, mat, sides=8, flatten=1):
    return tube(name,curve_points(points),r,mat,sides,flatten=flatten)

def ellipsoid(name, center, scale, mat, seg=20, rings=12):
    vs=[]; fs=[]
    for i in range(rings+1):
        t=math.pi*i/rings
        for j in range(seg):
            a=TAU*j/seg; vs.append((center[0]+scale[0]*math.sin(t)*math.cos(a),center[1]+scale[1]*math.sin(t)*math.sin(a),center[2]+scale[2]*math.cos(t)))
    for i in range(rings):
        for j in range(seg): fs.append((i*seg+j,i*seg+(j+1)%seg,(i+1)*seg+(j+1)%seg,(i+1)*seg+j))
    return mesh(name,vs,fs,mat)

def ribbon(name, points, widths, mat):
    ps=curve_points(points,6); vs=[]; fs=[]
    for i,p in enumerate(ps):
        u=i/(len(ps)-1); w=widths[0]*(1-u)+widths[-1]*u
        tangent=(ps[min(i+1,len(ps)-1)]-ps[max(i-1,0)]).normalized()
        across=Vector((0,1,0)).cross(tangent).normalized()
        for j in range(5):
            v=j/4-.5; q=p+across*(w*v)+Vector((0,-.009*math.cos(v*math.pi*2)*math.sin(u*math.pi),0)); vs.append(q)
    for i in range(len(ps)-1):
        for j in range(4): fs.append((i*5+j,i*5+j+1,(i+1)*5+j+1,(i+1)*5+j))
    o=mesh(name,vs,fs,mat); m=o.modifiers.new('Cloth thickness','SOLIDIFY'); m.thickness=.009
    return o

def loop(rx,ry,z,n=96): return [(rx*math.sin(i*TAU/n),-ry*math.cos(i*TAU/n),z) for i in range(n)]

def band(name, rx,ry,z,height,mat):
    vs=[]; fs=[]; n=96
    for level in range(5):
        t=level/4; zz=z+height*(t-.5); bulge=.008*math.sin(t*math.pi)
        for j in range(n):
            a=TAU*j/n; vs.append(((rx+bulge)*math.sin(a),-(ry+bulge)*math.cos(a),zz))
    for i in range(4):
        for j in range(n): fs.append((i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j))
    o=mesh(name,vs,fs,mat); m=o.modifiers.new('Fabric shell','SOLIDIFY'); m.thickness=.012
    return o

def knot(center, mat, r=.045):
    x,y,z=center
    for s in [-1,1]:
        cord('Folded cloth knot',[(x,y-.01,z),(x+s*.11,y-.01,z+.06),(x+s*.145,y+.008,z+.015),(x+s*.085,y-.035,z-.055),(x,y-.025,z)],r,mat,10,.65)
    for off in [-.025,0,.025]:
        cord('Knot center wrap',[(x+off,y+.014,z+.065),(x+off,y-.065,z+.045),(x+off,y-.072,z-.035),(x+off,y+.012,z-.055)],.018,mat,8)

def headband(bvh, white, red):
    vs=[]; n=128; rows=7
    for k in range(rows):
        z=1.945+.128*k/(rows-1)
        for j in range(n):
            a=j*TAU/n; d=Vector((math.sin(a),-math.cos(a),0)); p,normal,_,_=bvh.ray_cast(Vector((0,-.015,z)),d,1)
            if p is None: p=Vector((.44*d.x,.29*d.y,z))
            vs.append(p+d*(.015+.003*math.sin(k*math.pi)))
    fs=[(k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j) for k in range(rows-1) for j in range(n)]
    o=mesh('Fitted white forehead band',vs,fs,white); o.modifiers.new('Fabric thickness','SOLIDIFY').thickness=.008
    # Emblem conforms to the band, not a floating sphere.
    disk=[(0,-.321,2.009)]
    for i in range(48):
        a=i*TAU/48; x=.049*math.cos(a); z=2.009+.049*math.sin(a)
        p,_,_,_=bvh.ray_cast(Vector((x,-1,z)),Vector((0,1,0)),2)
        disk.append((x,p.y-.023,z))
    disk[0]=(0,bvh.ray_cast(Vector((0,-1,2.009)),Vector((0,1,0)),2)[0].y-.023,2.009)
    mesh('Red sun emblem',disk,[(0,1+i,1+(i+1)%48) for i in range(48)],red)
    knot((.405,.185,1.99),white,.029)
    ribbon('Headband tie A',[(.41,.20,1.99),(.52,.23,1.85),(.54,.20,1.74)],[.09,.12],white)
    ribbon('Headband tie B',[(.43,.21,1.99),(.59,.20,1.94),(.64,.17,1.87)],[.075,.10],white)

def mawashi(mat, accent=None):
    band('Continuous waist wrap',.416,.317,.715,.195,mat)
    for i in range(3):
        pts=[(x,y,z+.009*math.sin(j*TAU/96+1)) for j,(x,y,z) in enumerate(loop(.423,.324,.646+i*.065))]
        tube('Rolled cloth fold',pts,.032,mat,10,True,.65)
    knot((0,-.36,.687),mat)
    ribbon('Belt tie left',[(-.015,-.382,.68),(-.10,-.403,.56),(-.12,-.40,.45)],[.09,.115],mat)
    ribbon('Belt tie right',[(.015,-.39,.68),(.10,-.42,.56),(.105,-.42,.49)],[.09,.12],mat)
    if accent:
        for i in range(12):
            a=TAU*i/12; pts=[]
            for da,dz in [(0,.044),(.08,0),(0,-.044),(-.08,0),(0,.044)]:
                pts.append((.461*math.sin(a+da),-.365*math.cos(a+da),.72+dz))
            tube('Woven gold diamond',pts,.009,accent,6)

def fan(black,gold,red):
    cx,cy,cz=-1.16,-.125,1.68
    rim=[]; n=96
    for i in range(n):
        a=TAU*i/n; r=.225*(1+.105*math.cos(4*a)); rim.append((cx+r*math.sin(a),cy,cz+r*math.cos(a)))
    vs=[(cx,cy,cz)]+rim
    o=mesh('Gyoji gunbai paddle',vs,[(0,1+i,1+(i+1)%n) for i in range(n)],black)
    o.modifiers.new('Solid fan paddle','SOLIDIFY').thickness=.028
    tube('Gold paddle rim',rim,.012,gold,8,True)
    ellipsoid('Gold front medallion',(cx,cy-.027,cz),(.098,.014,.098),gold,32,16)
    ellipsoid('Gold back medallion',(cx,cy+.027,cz),(.098,.014,.098),gold,32,16)
    cord('Lacquer handle',[(cx,cy,1.02),(cx,cy,1.45),(cx,cy,1.68)],.024,black,12)
    for z in [1.04,1.10,1.31,1.39]:
        tube('Handle gold collar',[(cx+.026*math.sin(i*TAU/24),cy+.026*math.cos(i*TAU/24),z) for i in range(24)],.007,gold,6,True)
    cord('Red silk cord',[(cx,cy,1.06),(cx-.06,cy-.01,.99),(cx-.06,cy-.01,.90)],.009,red)
    ellipsoid('Tassel binding',(cx-.06,cy-.01,.90),(.024,.024,.035),gold)
    for j in range(14):
        a=j*TAU/14
        cord('Tassel silk',[(cx-.06+.012*math.sin(a),cy-.01+.012*math.cos(a),.89),(cx-.06+.025*math.sin(a),cy-.01+.025*math.cos(a),.84),(cx-.06+.041*math.sin(a),cy-.01+.035*math.cos(a),.77)],.006,red,5)

def topknot(black,highlight,gold):
    # Scalp shell terminates above the eyes; ears remain exposed.
    vs=[]; fs=[]; n=64; rows=14
    for i in range(rows+1):
        for j in range(n):
            a=j*TAU/n; edge=1.13+.18*(1-math.cos(a)); t=.02+(edge-.02)*i/rows
            vs.append((.455*math.sin(t)*math.sin(a),-.335*math.sin(t)*math.cos(a)-.012,1.85+.353*math.cos(t)))
    for i in range(rows):
        for j in range(n): fs.append((i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j))
    o=mesh('Sculpted hair cap',vs,fs,black); o.modifiers.new('Hair shell','SOLIDIFY').thickness=.012
    for j in range(22):
        a=j*TAU/22; pts=[]; radii=[]
        edge=1.13+.18*(1-math.cos(a))
        for i in range(20):
            u=i/19; t=edge*(1-u)+.12*u; aa=a+.36*u
            pts.append((.462*math.sin(t)*math.sin(aa),-.342*math.sin(t)*math.cos(aa)-.012,1.85+.36*math.cos(t)))
            radii.append(.008+.022*math.sin(math.pi*u)**.6)
        tube('Swept hair lock',pts,radii,black,8)
        tube('Hair strand sheen',[(x,y-.002,z+.015) for x,y,z in pts[2:-2]],.0035,highlight,5)
    for j in range(6):
        x=(j-2.5)*.028
        cord('Folded topknot lock',[(x,.015,2.16),(x,.15,2.26),(x,.13,2.40),(x,-.055,2.42),(x,-.07,2.32),(x,.035,2.27)],.038,black,10)
    tube('Topknot gold tie',[(.115*math.sin(i*TAU/32),.035+.065*math.cos(i*TAU/32),2.235) for i in range(32)],.015,gold,8,True)

def apron(red,gold):
    band('Ceremonial waistband',.42,.326,.76,.11,red)
    for z in [.715,.805]: tube('Waist piping',loop(.427,.334,z),.009,gold,6,True)
    vs=[]; fs=[]; nx=32; nz=14
    def pos(u,v):
        x=u*(.30+.045*v); y=-.337-.06*v+.06*u*u; z=.73-.37*v
        return (x,y-.006*math.cos(u*math.pi*8)*v,z)
    for j in range(nz+1):
        for i in range(nx+1): vs.append(pos(2*i/nx-1,j/nz))
    for j in range(nz):
        for i in range(nx):
            a=j*(nx+1)+i; fs.append((a,a+1,a+nx+2,a+nx+1))
    o=mesh('Flared ceremonial apron',vs,fs,red); o.modifiers.new('Brocade thickness','SOLIDIFY').thickness=.014
    for side in [-1,1]: tube('Apron gold border',[(x,y-.009,z) for x,y,z in [pos(side,v/24) for v in range(25)]],.012,gold)
    for v in [.07,.88]: tube('Horizontal embroidery',[(x,y-.012,z) for x,y,z in [pos(2*i/64-1,v) for i in range(65)]],.009,gold)
    for j in range(41):
        x,y,z=pos(2*j/40-1,1)
        cord('Gold fringe',[(x,y,z+.008),(x+.003,y-.005,z-.04),(x,y-.006,z-.07)],.005,gold,5)
    # Embroidered rising sun and stylized wave crests, all geometry follows the apron.
    for r in [.075,.115]:
        tube('Rising sun embroidery',[(r*math.cos(i*math.pi/32),-.376,.52+r*math.sin(i*math.pi/32)) for i in range(33)],.008,gold,6)
    for row in range(3):
        pts=[]
        for i in range(65):
            x=-.27+.54*i/64; z=.437+row*.027+.022*math.sin(x*28+row*.8)
            v=(.73-z)/.37; u=x/(.30+.045*v); _,y,_=pos(u,v); pts.append((x,y-.016,z))
        tube('Wave embroidery',pts,.006,gold,6)
    for side in [-1,1]:
        ribbon('Ceremonial side sash',[(side*.35,.05,.76),(side*.42,.08,.59),(side*.43,.05,.47)],[.08,.105],red)

def rope(white,gold):
    # Three continuous helices form a true twisted rope, not stacked disconnected beads.
    for strand in range(3):
        ps=[]
        for i in range(512):
            a=TAU*i/512; t=24*a+strand*TAU/3; radial=Vector((math.sin(a),-math.cos(a),0))
            ps.append(Vector((.442*math.sin(a),-.35*math.cos(a),.775))+radial*(.027*math.cos(t))+Vector((0,0,.027*math.sin(t))))
        tube('Continuous twisted rope strand',ps,.025,white,7,True)
    for x in [-.29,-.10,.10,.29]:
        y=-.35*math.sqrt(1-(x/.46)**2)-.02
        cord('Paper hanger',[(x,y,.76),(x,y-.005,.69)],.008,white,6)
        ps=[(x,y-.012,.69),(x-.035,y-.018,.63),(x+.018,y-.025,.58),(x-.025,y-.03,.52),(x+.025,y-.035,.47)]
        ribbon('Folded shide paper',ps,[.042,.044],white)
    knot((.35,.24,.77),white,.047)
    for side in [-1,1]:
        cord('Rope knot tail',[(.35,.27,.77),(.36+side*.04,.31,.65),(.36+side*.08,.32,.59)],.027,white,10)

def finish(name,slot,rarity,rig):
    bpy.ops.object.select_all(action='DESELECT')
    for o in PARTS:
        bpy.context.view_layer.objects.active=o; o.select_set(True)
        for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
        o.select_set(False)
    for o in PARTS: o.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0]; bpy.ops.object.join(); o=bpy.context.object
    o.name='GEAR_'+name; o.data.name='MSH_Gear_'+name
    # Material colors atlas: one draw call per item, including metal/roughness map.
    original=list(o.data.materials); n=len(original)
    uv=o.data.uv_layers.new(name='GearPaletteUV')
    for p in o.data.polygons:
        for li in p.loop_indices: uv.data[li].uv=((p.material_index+.5)/n,.5)
    for v in o.data.vertices:
        if slot==0: weights={'Head':1}
        elif slot==2: weights={'RightHand':1}
        else:
            # Cosmetic fabric is not the underlying skin: never borrow calf/foot weights.
            z=max(v.co.z,.365); spine=max(0,min(1,(z-.66)/.16))
            weights={'Hips':1-spine,'Spine':spine}
            if name=='Kesho_Mawashi' and z<.62:
                amount=.23*max(0,min(1,(.62-z)/.25))
                left=.5+.5*math.tanh(v.co.x/.10)
                weights={'Hips':1-amount,'LeftUpperLeg':amount*left,'RightUpperLeg':amount*(1-left)}
            weights={k:w for k,w in weights.items() if w>1e-6}
            total=sum(weights.values()); weights={k:w/total for k,w in weights.items()}
        # Waist accessories do not borrow tail weights or move with arms.
        for bone,w in weights.items():
            vg=o.vertex_groups.get(bone) or o.vertex_groups.new(name=bone); vg.add([v.index],w,'REPLACE')
    o.parent=rig; m=o.modifiers.new('Shared cat rig deformation','ARMATURE'); m.object=rig
    coll=bpy.data.collections.new('GEAR | '+name); bpy.context.scene.collection.children.link(coll)
    for c in list(o.users_collection): c.objects.unlink(o)
    coll.objects.link(o); o['gear_slot']=['Head','Waist','RightHand'][slot]
    o['cosmetic_only']=True
    CATALOG.append({'name':name,'slot':slot,'rarity':rarity,'object':o.name,'vertices':len(o.data.vertices),
                    'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),
                    'materials':[{'name':m.name,'color':list(m['srgb']),'metallic':m['metallic'],'roughness':m['roughness']} for m in original]})
    PARTS.clear(); return o

def main():
    os.makedirs(EXPORT,exist_ok=True); os.makedirs(PREVIEW,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=SOURCE)
    if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    scene=bpy.context.scene; rig=bpy.data.objects['RIG_Cat']; body=bpy.data.objects['SK_Cat']
    rig.data.pose_position='REST'; bpy.context.view_layer.update()
    bvh=BVHTree.FromPolygons([v.co for v in body.data.vertices],[list(p.vertices) for p in body.data.polygons])
    white=material('Ivory','F4F0E9'); red=material('Crimson','CA1839'); gold=material('Gold','E8AD3C',.72,.29)
    black=material('Charcoal','181A24',.05,.34); indigo=material('Indigo','263EAA',.08,.39)
    hair=material('Hair','11131D',.12,.26); highlight=material('HairSheen','343849',.1,.35)
    headband(bvh,white,red); finish('Hachimaki',0,0,rig)
    mawashi(black); finish('Black_Mawashi',1,0,rig)
    mawashi(indigo); finish('Indigo_Mawashi',1,0,rig)
    mawashi(red,gold); finish('Crimson_Mawashi',1,1,rig)
    fan(black,gold,red); finish('Gyoji_Fan',2,1,rig)
    topknot(hair,highlight,gold); finish('Topknot',0,1,rig)
    apron(red,gold); finish('Kesho_Mawashi',1,2,rig)
    rope(white,gold); finish('Yokozuna_Rope',1,3,rig)
    # Duplicate only rig data for a clean deformation skeleton export.
    data=rig.data.copy(); out=bpy.data.objects.new('CatArmature',data); scene.collection.objects.link(out)
    bpy.ops.object.select_all(action='DESELECT'); out.select_set(True); bpy.context.view_layer.objects.active=out
    bpy.ops.object.mode_set(mode='EDIT')
    for b in list(data.edit_bones):
        if b.name.startswith('CTRL_'): data.edit_bones.remove(b)
    bpy.ops.object.mode_set(mode='OBJECT'); data.pose_position='REST'
    for entry in CATALOG:
        o=bpy.data.objects[entry['object']]; o.hide_set(False); o.parent=out; o.modifiers[0].object=out
        bpy.ops.object.select_all(action='DESELECT'); out.select_set(True); o.select_set(True); bpy.context.view_layer.objects.active=out
        bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT,'SK_Gear_'+entry['name']+'.fbx'),use_selection=True,
            object_types={'ARMATURE','MESH'},add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,
            axis_forward='-Z',axis_up='Y',apply_unit_scale=True,mesh_smooth_type='FACE',use_triangles=True,path_mode='AUTO')
        o.parent=rig; o.modifiers[0].object=rig
        assert all(abs(sum(g.weight for g in v.groups)-1)<1e-5 for v in o.data.vertices)
    bpy.data.objects.remove(out,do_unlink=True)
    manifest={'source_sha256':hashlib.sha256(open(SOURCE,'rb').read()).hexdigest(),'base_vertices':len(body.data.vertices),
              'gear':CATALOG,'notes':'Three independent cosmetic slots. No cloth simulation, combat or new bones.'}
    with open(os.path.join(HERE,'Exports','Gear_Manifest.json'),'w',encoding='utf8') as f: json.dump(manifest,f,indent=2)
    studio=bpy.data.collections.get('02 | REVIEW STUDIO'); studio.hide_viewport=False
    for o in scene.objects:
        if o.type=='EMPTY': o.hide_render=True
    scene.render.engine='CYCLES'; scene.cycles.samples=24
    scene.render.resolution_x=640; scene.render.resolution_y=640; scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    cam=scene.camera; cam.data.type='ORTHO'; cam.data.ortho_scale=2.95
    cam.location=(3.3,-7,3.1); cam.rotation_euler=(Vector((0,0,1.18))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.name='CAT | SUMO GEAR WORKSHOP'
    scene['README']='Eight fitted accessories. Toggle GEAR collections. Shared RIG_Cat and original 26 actions. Head / Waist / RightHand slots.'
    for e in CATALOG: bpy.data.objects[e['object']].hide_render=True
    for name in ['Hachimaki','Crimson_Mawashi','Gyoji_Fan']: bpy.data.objects['GEAR_'+name].hide_render=False
    for e in CATALOG: bpy.data.objects[e['object']].hide_set(bpy.data.objects[e['object']].hide_render)
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT)
    if '--no-render' in sys.argv: return
    for e in CATALOG:
        for other in CATALOG: bpy.data.objects[other['object']].hide_render=other is not e
        scene.render.filepath=os.path.join(PREVIEW,e['name']+'.png'); bpy.ops.render.render(write_still=True)
    for e in CATALOG: bpy.data.objects[e['object']].hide_render=e['name'] not in ['Hachimaki','Crimson_Mawashi','Gyoji_Fan']
    scene.render.filepath=os.path.join(PREVIEW,'Combined.png'); bpy.ops.render.render(write_still=True)
    print('GEAR_COMPLETE',json.dumps(manifest),flush=True)

if __name__=='__main__': main()
