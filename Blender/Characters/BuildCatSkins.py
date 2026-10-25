"""Texture-only skin library. Never alters approved geometry, UVs, weights or actions."""
import bpy
import numpy as np
import os, sys, json, hashlib, struct, zlib, shutil, math
from mathutils import Vector

HERE=os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0,HERE)
import BuildCatAnimations as core
SOURCE=os.path.join(HERE,'PawArena_Cat_Abilities.blend')
OUT=os.path.join(HERE,'PawArena_Cat_Skins.blend')
EXPORT=os.path.join(HERE,'Exports','CatSkins')
PREVIEW=os.path.join(os.path.dirname(HERE),'Previews','Characters','Skins')
N=2048
NAMES=['Ginger','Grey','Black','Tuxedo','White','Calico','Pumpkin','Mummy','Russian Blue','Siamese','Tiger','Ninja','Black Widow','Crystal','Gold','Hologram','Skeleton','Snow Leopard','Frankencat']

def digest(x):
    return hashlib.sha256(json.dumps(x,sort_keys=True).encode()).hexdigest()

def fingerprint(mesh,rig):
    return {'geometry_weights':digest([(tuple(v.co),[(g.group,g.weight) for g in v.groups]) for v in mesh.data.vertices]),
            'topology':digest([tuple(p.vertices) for p in mesh.data.polygons]),
            'uv':digest([tuple(l.uv) for l in mesh.data.uv_layers.active.data]),
            'rest_rig':digest([(b.name,b.parent.name if b.parent else '',list(map(tuple,b.matrix_local))) for b in rig.data.bones]),
            'actions':{a.name:digest([(f.data_path,f.array_index,[(tuple(k.co),tuple(k.handle_left),tuple(k.handle_right),k.interpolation) for k in f.keyframe_points]) for f in core.fcurves(a)]) for a in bpy.data.actions}}

def png(path,rgb):
    # Generated texture pixels, bottom-up UV convention -> PNG top-down.
    data=np.ascontiguousarray(np.clip(rgb[::-1]*255+.5,0,255).astype(np.uint8))
    h,w,_=data.shape
    def chunk(t,d): return struct.pack('!I',len(d))+t+d+struct.pack('!I',zlib.crc32(t+d)&0xffffffff)
    raw=b''.join(b'\0'+r.tobytes() for r in data)
    with open(path,'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!2I5B',w,h,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw,6))+chunk(b'IEND',b''))

def raster(mesh):
    cache=os.path.join(EXPORT,'SurfaceCoordinates.npz')
    if os.path.isfile(cache):
        d=np.load(cache)
        return d['xyz'],d['part'],d['valid']
    m=mesh.data
    assert len(m.vertices)==36988
    xyz=np.zeros((N,N,3),np.float32)
    part=np.full((N,N),-1,np.int16)
    coords=np.array([v.co[:] for v in m.vertices],np.float32)
    uv=np.array([l.uv[:] for l in m.uv_layers.active.data])*N-.5
    m.calc_loop_triangles()
    for tri in m.loop_triangles:
        vi=np.array(tri.vertices); p=uv[list(tri.loops)]
        lo=np.maximum(0,np.ceil(p.min(0)).astype(int)); hi=np.minimum(N-1,np.floor(p.max(0)).astype(int))
        if np.any(lo>hi): continue
        xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1))
        den=(p[1,1]-p[2,1])*(p[0,0]-p[2,0])+(p[2,0]-p[1,0])*(p[0,1]-p[2,1])
        if abs(den)<1e-9: continue
        a=((p[1,1]-p[2,1])*(xx-p[2,0])+(p[2,0]-p[1,0])*(yy-p[2,1]))/den
        b=((p[2,1]-p[0,1])*(xx-p[2,0])+(p[0,0]-p[2,0])*(yy-p[2,1]))/den
        c=1-a-b; keep=(a>=-1e-5)&(b>=-1e-5)&(c>=-1e-5)
        v=int(vi[0])
        kind=0 if v<21698 else 1 if v<22276 else 2 if v<24876 else 3 if v<24948 else 4 if v<28078 else 5 if v<29690 else 6
        xyz[yy[keep],xx[keep]]=a[keep,None]*coords[vi[0]]+b[keep,None]*coords[vi[1]]+c[keep,None]*coords[vi[2]]
        part[yy[keep],xx[keep]]=kind
    valid=part>=0
    # Extend surface coordinates across UV gutters; avoids dark seams in mipmaps.
    for _ in range(8):
        pending=~valid
        for dy,dx in [(1,0),(-1,0),(0,1),(0,-1)]:
            near=np.roll(valid,(dy,dx),(0,1)); mask=pending&near
            if dy==1: mask[0]=False
            if dy==-1: mask[-1]=False
            if dx==1: mask[:,0]=False
            if dx==-1: mask[:,-1]=False
            xyz[mask]=np.roll(xyz,(dy,dx),(0,1))[mask]
            part[mask]=np.roll(part,(dy,dx),(0,1))[mask]
            valid[mask]=True; pending[mask]=False
    np.savez_compressed(cache,xyz=xyz,part=part,valid=valid)
    return xyz,part,valid

def rgb(h): return np.array([int(h[i:i+2],16)/255 for i in (0,2,4)],np.float32)
def smooth(a,b,x):
    t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)
def ellipse(x,z,cx,cz,rx,rz,edge=.06):
    return 1-smooth(1-edge,1+edge,((x-cx)/rx)**2+((z-cz)/rz)**2)
def blend(c,m,h):
    target=rgb(h) if isinstance(h,str) else h
    return c*(1-m[...,None])+target*m[...,None]
def line(x,z,ax,az,bx,bz,width):
    t=np.clip(((x-ax)*(bx-ax)+(z-az)*(bz-az))/((bx-ax)**2+(bz-az)**2),0,1)
    d=np.sqrt((x-ax-t*(bx-ax))**2+(z-az-t*(bz-az))**2)
    return 1-smooth(width*.7,width*1.3,d)

def texture(name,xyz,part,base):
    x,y,z=xyz.transpose(2,0,1); ax=np.abs(x)
    front=1-smooth(-.08,.03,y); head=smooth(1.32,1.37,z)
    arm=smooth(.39,.46,ax)*smooth(.98,1.05,z)*(1-smooth(1.33,1.38,z))
    leg=1-smooth(.48,.60,z); tail=part==6
    belly=ellipse(x,z,0,.865,.255*(1-(z-.78)*.4),.47,.02)*front*(1-head)
    face=np.maximum(ellipse(x,z,0,1.405,.365,.19,.055),ellipse(x,z,0,1.565,.135,.30,.09))*head*front
    hands=smooth(1.005,1.02,ax)*arm; feet=1-smooth(.185,.20,z)
    cream=np.maximum.reduce([belly,face,hands,feet])
    back=smooth(-.03,.15,y); start=1.89-back*.34; taper=smooth(start,start+.23,z); w=.045*taper
    stripes=np.maximum(1-smooth(w-.004,w+.004,ax),1-smooth(w*.8-.004,w*.8+.004,np.abs(ax-(.155+.015*np.sin(z*7)))))*smooth(start-.01,start+.03,z)*head
    stripes=np.maximum(stripes,smooth(.43,.50,np.sin((ax+(z-1.18)*.1)*35))*arm)
    stripes=np.maximum(stripes,smooth(.42,.49,np.sin((z+ax*.12)*33))*leg)
    for zz,rx in [(.63,.22),(.84,.20),(1.02,.17)]:
        stripes=np.maximum(stripes,ellipse(ax,z,.39,zz,rx,.038,.12)*(1-head)*(1-arm))
    stripes=np.maximum(stripes,(1-smooth(.055,.07,ax))*smooth(.55,.65,z)*back*(1-head))
    stripes=np.where(tail,smooth(.2,.35,np.sin((y+z*.6)*29)),stripes)
    cream=np.where(tail,smooth(1.01,1.09,z),cream)
    # Match the approved ears' inner-surface triangles.
    a=(.268,2.072); b=(.436,2.012); cc=(.478,2.292)
    den=(b[1]-cc[1])*(a[0]-cc[0])+(cc[0]-b[0])*(a[1]-cc[1])
    ba=((b[1]-cc[1])*(ax-cc[0])+(cc[0]-b[0])*(z-cc[1]))/den
    bb=((cc[1]-a[1])*(ax-cc[0])+(a[0]-cc[0])*(z-cc[1]))/den
    inner=smooth(-.02,.035,np.minimum.reduce([ba,bb,1-ba-bb]))*(1-smooth(.005,.025,y))
    settings={'roughness':.55,'metallic':0.,'emission':0.,'coat':.08}
    eye='blue'; pink='ECA2AC'; mouth='50352D'; nose='B77570'
    c=np.zeros_like(xyz)+rgb('FFA52E')
    if name in ['Grey','Tiger']:
        c[:]=rgb('A9AFB9' if name=='Grey' else 'FF9D24')
        if name=='Tiger':
            stripe2=smooth(.72,.82,np.sin(z*47+np.sin(x*22)*1.6+y*4))*(1-arm)*(1-head)
            stripes=np.maximum(stripes,stripe2)
        c=blend(c,stripes,'626C7C' if name=='Grey' else '24262D'); c=blend(c,cream,'F6F1E8')
    elif name in ['Black','Tuxedo','White','Russian Blue']:
        c[:]=rgb({'Black':'242735','Tuxedo':'252831','White':'F3F0EB','Russian Blue':'828FAF'}[name])
        if name=='Black': c=blend(c,feet,'F5F0EA'); eye='yellow'; mouth='A9A3B4'
        if name=='Tuxedo': c=blend(c,cream,'F6F3ED')
        if name=='Russian Blue': c=blend(c,cream*.12,'CFD8E6')
    elif name=='Calico':
        c[:]=rgb('F8F1E6')
        orange=np.zeros_like(x); dark=np.zeros_like(x)
        for cx,cy,cz,rx,ry,rz in [(-.28,-.15,1.97,.36,.7,.47),(.27,.23,1.72,.30,.30,.30),(-.28,.2,.79,.24,.33,.32),(.22,-.05,.3,.20,.5,.23),(-.7,0,1.19,.19,.5,.3)]:
            d=((x-cx)/rx)**2+((y-cy)/ry)**2+((z-cz)/rz)**2
            orange=np.maximum(orange,1-smooth(.85,1.05,d+.12*np.sin(z*36+x*17)))
        for cx,cy,cz,rx,ry,rz in [(.30,-.22,1.84,.27,.36,.43),(-.2,.25,1.62,.38,.3,.32),(.32,0,1.02,.21,.5,.25),(-.28,.16,.28,.16,.25,.13),(.84,0,1.19,.16,.5,.25)]:
            dark=np.maximum(dark,1-smooth(.85,1.05,((x-cx)/rx)**2+((y-cy)/ry)**2+((z-cz)/rz)**2+.1*np.sin(z*31+y*18)))
        c=blend(c,orange,'F6A237'); c=blend(c,dark,'282C35')
        c=blend(c,face*.88,'FAF1E6'); c=np.where(tail[...,None],blend(c,np.ones_like(x)*.85,'E69A40'),c)
    elif name=='Siamese':
        c[:]=rgb('EADBC1')
        point=np.maximum.reduce([ellipse(x,z,0,1.62,.46,.36)*head*front,feet,smooth(.76,1.1,ax)*arm,1-smooth(.27,.5,z),smooth(2.05,2.22,z)])
        point=np.where(tail,1.,point); c=blend(c,point,'62463E')
        nose='A97670'
    elif name=='Pumpkin':
        groove=(.5+.5*np.cos(np.arctan2(x,y+.03)*10+z*.8))**8
        c[:]=rgb('FF991C'); c=blend(c,groove*.85,'D75A0C')
        c=blend(c,np.maximum.reduce([hands,feet,smooth(2.06,2.28,z),tail.astype(float)]),'389F38')
        # Flat jack-o-lantern motif on belly, not additional geometry.
        eyes=(1-smooth(.0,.018,np.maximum(np.abs(ax-.12)-(.065-(z-.95)*.5),np.maximum(.93-z,z-1.035))))
        smile=(1-smooth(.014,.025,np.abs(z-(.70+.5*x*x+.025*np.sin(x*75)))))*(1-smooth(.21,.23,ax))
        c=blend(c,np.maximum(eyes,smile)*front*(1-head),'FFE195')
        pink='65B73E'
    elif name=='Mummy':
        phase=np.where(arm,(ax+z*.23+y*.3)*44,(z+x*.24+np.sin(y*12)*.012)*49)
        phase=np.where(tail,(y+z*.5)*57,phase)
        band=(phase/(2*np.pi))%1
        c[:]=rgb('E7D6B5'); c=blend(c,1-smooth(.035,.085,np.minimum(band,1-band)),'8C795E')
        c=blend(c,smooth(.60,.95,band)*.22,'FFF3DC')
        folds=(.5+.5*np.sin(x*240+z*80+y*60))*.035; c*=1-folds[...,None]
        opening=ellipse(x,z,0,1.695,.43,.15)*front
        c=blend(c,opening,'252327'); eye='yellow'; mouth='C8B68F'; nose='CAB891'; pink='D9C4A5'
    elif name in ['Ninja','Black Widow']:
        c[:]=rgb('222632' if name=='Ninja' else '202332')
        collar=(1-smooth(.035,.055,np.abs(z-1.32)))*(1-arm)
        cuffs=(1-smooth(.014,.026,np.abs(ax-.94)))*arm
        belt=(1-smooth(.035,.055,np.abs(z-.55)))*(1-arm)
        cross=np.maximum(line(x,z,-.27,1.25,.25,.58,.037),line(x,z,.27,1.25,-.25,.58,.037))*front*(1-head)*(1-arm)
        if name=='Ninja':
            c=blend(c,np.maximum.reduce([collar,cuffs,belt,cross]),'C72E43')
            c=blend(c,ellipse(x,z,0,1.70,.45,.20)*front,'C0AA81'); eye='yellow'
        else:
            web1=1-smooth(.018,.032,np.abs(np.sin(x*25+z*9)))
            web2=1-smooth(.018,.032,np.abs(np.sin(x*25-z*9)))
            web=np.maximum(web1,web2)*(1-head)*(1-arm)*front
            c=blend(c,web,'B72D43'); c=blend(c,np.maximum(collar,cuffs),'F33D52')
            hour=(1-smooth(.03,.045,np.abs(ax-(.85*np.abs(z-.9)))))*smooth(.70,.75,z)*(1-smooth(1.05,1.1,z))*front
            c=blend(c,hour,'F84351'); eye='red'; settings['emission']=.12
        pink='C94B59'; mouth='272935'; nose='36313A'
    elif name in ['Crystal','Hologram']:
        if name=='Crystal':
            u=(x+y*.52)*7; v=(z+y*.32)*7; iu=np.floor(u); iv=np.floor(v)
            tri=((u-iu)+(v-iv)>1).astype(float)
            h=(np.sin(iu*127.1+iv*311.7+tri*73.3)*43758.5453)%1
            palette=np.array([rgb(s) for s in ['7189EA','9E83EF','67C5EA','B49AEC','69A9E5','D8BCFA']])
            c=palette[np.minimum(5,(h*6).astype(int))]
            edge=np.minimum.reduce([u%1,1-u%1,v%1,1-v%1,np.abs(u%1+v%1-1)])
            c=blend(c,1-smooth(.008,.025,edge),'B3E7FF')
            settings.update(roughness=.23,metallic=.25,emission=.13,coat=.65)
        else:
            hue=z*2.7+x*1.6+y*2.1
            c=np.stack([.62+.32*np.sin(hue+q) for q in (0,2.094,4.188)],-1)
            scan=(.5+.5*np.cos(z*200))**12
            c=blend(c,scan*.27,'BFFFFF')
            settings.update(roughness=.24,metallic=.25,emission=.32,coat=.55)
        pink='C7A5F4'; mouth='6263A5'; nose='D9A7E7'
    elif name=='Gold':
        c[:]=rgb('F7C044'); settings.update(roughness=.23,metallic=.92,coat=.2)
        pink='E0A72F'; nose='B47E1D'; mouth='88601D'; eye='gold'
    elif name=='Skeleton':
        c[:]=rgb('23252E'); ink=np.zeros_like(x)
        ink=np.maximum(ink,line(x,z,0,.62,0,1.25,.026))
        for zz,width in [(1.20,.19),(1.10,.23),(1.00,.25),(.90,.23)]:
            rib=1-smooth(.013,.022,np.abs(z-(zz-.65*x*x)))
            ink=np.maximum(ink,rib*(1-smooth(width-.015,width,ax)))
        pelvis=ellipse(x,z,0,.57,.235,.13)-ellipse(x,z,0,.57,.15,.068)
        ink=np.maximum(ink,pelvis)
        ink=np.maximum(ink,line(ax,z,.23,.20,.23,.48,.04))
        ink=np.maximum(ink,line(ax,z,.50,1.19,.98,1.19,.032))
        ink=np.maximum(ink,ellipse(ax,z,.74,1.19,.045,.05))
        c=blend(c,ink*np.maximum(front,back*.8),'EDE8D9')
        skull=ellipse(x,z,0,1.74,.47,.38)*head*front
        c=blend(c,skull,'F3EEE1'); c=blend(c,np.maximum(hands,feet)*.8,'EDE8D9')
        eye='black'; nose='30303A'; mouth='30303A'
    elif name=='Snow Leopard':
        c[:]=rgb('D9DFE8'); c=blend(c,cream,'FAF7EF')
        # Staggered rosettes in three surface axes, varying radius and broken rings.
        u=np.where(arm,x*8,x*10+y*4); v=np.where(arm,z*12+y*6,z*10)
        row=np.floor(v); u=u+.5*(row%2); cellx=np.floor(u)
        h=(np.sin(cellx*127.1+row*311.7)*43758.5453)%1
        dx=u%1-.5; dz=v%1-.5; r=np.sqrt(dx*dx+dz*dz)
        ring=(1-smooth(.035,.07,np.abs(r-(.20+h*.09))))
        broken=smooth(-.6,-.3,np.sin(np.arctan2(dz,dx)*3+h*9))
        spots=np.maximum(ring*broken,(1-smooth(.06,.12,r))*(h>.78))
        c=blend(c,spots*(1-cream*.90),'616B7C')
        c=blend(c,face*.85,'F7F5F0')
    elif name=='Frankencat':
        c[:]=rgb('91C83E')
        jacket=(1-head)*(1-leg)*(1-hands)*smooth(.45,.55,z)
        c=blend(c,jacket,'30343C')
        shirt=(1-smooth(.08,.13,ax))*front*(1-head)*smooth(.55,.64,z)
        c=blend(c,shirt,'D6BE55')
        patch=ellipse(x,z,-.30,.34,.12,.14)
        c=blend(c,patch,'5B9340')
        seam=(1-smooth(.006,.012,np.abs(z-(1.99+x*.15))))*head*front
        stitches=(1-smooth(.10,.20,np.abs(np.sin(x*50))))*(1-smooth(.035,.052,np.abs(z-(1.99+x*.15))))*head*front
        c=blend(c,np.maximum(seam,stitches),'3A4A2B')
        for zz in [.71,.90,1.09]: c=blend(c,ellipse(x,z,.09,zz,.018,.018)*front,'E7D57A')
        pink='C389B9'; eye='yellow'; nose='7C85AB'; mouth='455232'
    # Existing small geometry is retained; only its color is changed.
    c=blend(c,inner,rgb(pink))
    c=np.where((part==2)[...,None],rgb('ADA69D') if name in ['White','Grey','Snow Leopard'] else c*.63,c)
    c=np.where((part==3)[...,None],rgb(nose),c)
    c=np.where((part==4)[...,None],rgb(mouth),c)
    c=np.where((part==5)[...,None],rgb(pink),c)
    eyes=base.copy()
    # Keep the baked pupil, catchlight and iris gradients; recolor only blue pixels.
    iris=np.clip((eyes[:,:,2]-eyes[:,:,0])*5,0,1)*smooth(.12,.30,eyes[:,:,2])
    for mode,h in [('yellow','EED656'),('red','F23F54'),('gold','B98520'),('black','1B2430')]:
        if eye==mode:
            value=np.max(eyes,axis=2)
            target=rgb(h)*(value[...,None]*.75+.25)
            eyes=blend(eyes,iris,target)
    c=np.where((part==1)[...,None],eyes,c)
    return np.clip(c,0,1),settings

def material(name,image,settings,is_eye=False):
    mat=bpy.data.materials.new('SKIN | '+name+(' | Eyes' if is_eye else ' | Coat'))
    mat.use_nodes=True; mat.use_fake_user=True
    tree=mat.node_tree; p=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Roughness'].default_value=.25 if is_eye else settings['roughness']
    p.inputs['Metallic'].default_value=settings['metallic']
    p.inputs['Coat Weight'].default_value=settings['coat']
    p.inputs['Emission Strength'].default_value=settings['emission']
    uv=tree.nodes.new('ShaderNodeUVMap'); uv.uv_map='Cat_Atlas'; uv.location=(-600,150)
    tex=tree.nodes.new('ShaderNodeTexImage'); tex.image=image; tex.location=(-360,150)
    tree.links.new(uv.outputs['UV'],tex.inputs['Vector']); tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
    if settings['emission']>0: tree.links.new(tex.outputs['Color'],p.inputs['Emission Color'])
    mat['skin_name']=name
    return mat

def build():
    os.makedirs(EXPORT,exist_ok=True); os.makedirs(PREVIEW,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=SOURCE)
    mesh=bpy.data.objects['SK_Cat']; rig=bpy.data.objects['RIG_Cat']; scene=bpy.context.scene
    before=fingerprint(mesh,rig)
    xyz,part,valid=raster(mesh)
    sourceimage=bpy.data.images['Cat_TabBy_BaseColor']
    pixels=np.empty(4096*4096*4,np.float32); sourceimage.pixels.foreach_get(pixels)
    base=pixels.reshape(4096,4096,4)[1::2,1::2,:3].copy(); del pixels
    for a in bpy.data.actions: a.use_fake_user=True
    rig.data.pose_position='REST'
    scene.name='01 | Ginger'
    # Unlink the primary mesh from shared collections; each scene owns one visual instance.
    oldcols=list(mesh.users_collection)
    for c in oldcols: c.objects.unlink(mesh)
    studio=bpy.data.collections['02 | REVIEW STUDIO']; studio.hide_viewport=False
    scene.render.engine='BLENDER_EEVEE'; scene.render.resolution_x=640; scene.render.resolution_y=640
    scene.render.resolution_percentage=100; scene.render.image_settings.file_format='PNG'
    scene.render.film_transparent=False
    if hasattr(scene,'eevee'): scene.eevee.taa_render_samples=16
    scene.camera.data.ortho_scale=2.95
    scene.view_settings.exposure=-.35
    records=[]; scenes=[]
    for idx,name in enumerate(NAMES):
        slug=name.replace(' ','_'); path=os.path.join(EXPORT,'Cat_'+slug+'_BaseColor.png')
        if name=='Ginger':
            shutil.copy2(os.path.join(HERE,'Exports','CatAbilities','Cat_BaseColor.png'),path)
            settings={'roughness':.55,'metallic':0.,'emission':0.,'coat':.08}
        else:
            color,settings=texture(name,xyz,part,base)
            color[~valid]=rgb('262D39'); png(path,color); del color
        image=bpy.data.images.load(path,check_existing=False); image.name='T_Cat_'+slug; image.pack()
        mats=[material(name,image,settings,i==1) for i in range(2)]
        if idx==0:
            s=scene; obj=mesh
        else:
            s=scene.copy()
            # scene.copy shares collections but its root objects are separate links.
            for o in list(s.collection.objects):
                if o.type=='MESH' and o.data==mesh.data: s.collection.objects.unlink(o)
            obj=mesh.copy(); obj.data=mesh.data; obj.name='SK_Cat_'+slug
        s.name=f'{idx+1:02d} | {name}'
        s.use_fake_user=True
        s.collection.objects.link(obj)
        for i,mat in enumerate(mats): obj.material_slots[i].link='OBJECT'; obj.material_slots[i].material=mat
        s['Skin']=name; s['Scope']='Materials only. Shared unchanged mesh, UV, rig and 26 actions.'
        s['Texture']=os.path.relpath(path,HERE)
        obj['Skin']=name; obj.hide_render=False; obj.hide_set(False,view_layer=s.view_layers[0])
        assert obj.data is mesh.data
        records.append({'name':name,'scene':s.name,'texture':os.path.basename(path),'size':list(image.size),'settings':settings})
        scenes.append(s)
        print('SKIN_BUILT',name,flush=True)
    after=fingerprint(mesh,rig); assert before==after,'Protected data changed'
    # All scenes use a single mesh datablock and the original rig.
    for s in scenes:
        cats=[o for o in s.objects if o.type=='MESH' and o.data==mesh.data]
        assert len(cats)==1,(s.name,[o.name for o in cats])
        assert cats[0].modifiers.get('Armature') or any(m.type=='ARMATURE' and m.object==rig for m in cats[0].modifiers)
    guide='19 texture/material skins. Choose a numbered Scene in the top bar.\nAll characters share one mesh, one Cat_Atlas UV and the original rig.\nRig is in REST position for skin review. Switch Armature Data > Pose Position to Pose to preview the existing 26 actions.\nNo accessories, new geometry, shape keys, UV edits or animation changes.\nCrystal/Hologram are opaque stylized surface treatments, not transparent geometry.\nMummy/Ninja/Frankencat clothing and seams are painted on the existing surface.\nTextures are packed and also exported beside the blend in Exports/CatSkins.\n'
    text=bpy.data.texts.new('READ ME | 19 Cat Skins'); text.write(guide)
    scene.camera.location=(3.8,-7,2.6)
    scene.camera.rotation_euler=(Vector((0,0,1.16))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    bpy.context.window.scene=scene
    bpy.context.view_layer.objects.active=mesh
    for o in scene.objects: o.select_set(False)
    mesh.select_set(True)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.shading.type='MATERIAL'
                area.spaces.active.region_3d.view_distance=4.0
                area.spaces.active.region_3d.view_location=Vector((0,0,1.15))
    report={'passed':True,'count':len(records),'source':SOURCE,'source_sha256':hashlib.sha256(open(SOURCE,'rb').read()).hexdigest(),'mesh_uv_weights_rig_all_26_actions_unchanged':before==after,'fingerprints':after,'skins':records}
    with open(os.path.join(EXPORT,'Skin_Manifest.json'),'w',encoding='utf8') as f: json.dump(report,f,indent=2)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print('SKIN_LIBRARY_SAVED',OUT,flush=True)

def render():
    bpy.ops.wm.open_mainfile(filepath=OUT)
    subset=[a.split('=',1)[1] for a in sys.argv if a.startswith('--skin=')]
    for idx,name in enumerate(NAMES):
        if subset and name.replace(' ','_') not in subset: continue
        s=bpy.data.scenes[f'{idx+1:02d} | {name}']; bpy.context.window.scene=s
        for view,loc in [('Front',(0,-8,1.18)),('ThreeQuarter',(4,-7,2.7)),('Back',(0,8,1.18))]:
            path=os.path.join(PREVIEW,f'{idx+1:02d}_{name.replace(" ","_")}_{view}.png')
            if os.path.exists(path) and '--force' not in sys.argv: continue
            s.camera.location=loc
            s.camera.rotation_euler=(Vector((0,0,1.18))-s.camera.location).to_track_quat('-Z','Y').to_euler()
            s.render.filepath=path
            bpy.ops.render.render(write_still=True)
            print('RENDERED',name,view,flush=True)

def retain_scenes():
    bpy.ops.wm.open_mainfile(filepath=OUT)
    source=bpy.data.scenes['01 | Ginger']; source.use_fake_user=True
    mesh=bpy.data.objects['SK_Cat']
    for idx,name in enumerate(NAMES[1:],1):
        s=source.copy(); s.name=f'{idx+1:02d} | {name}'; s.use_fake_user=True
        for o in list(s.collection.objects):
            if o.type=='MESH' and o.data==mesh.data: s.collection.objects.unlink(o)
        obj=bpy.data.objects['SK_Cat_'+name.replace(' ','_')]
        s.collection.objects.link(obj)
        s['Skin']=name
        s['Texture']='Exports/CatSkins/Cat_'+name.replace(' ','_')+'_BaseColor.png'
        assert len([o for o in s.objects if o.type=='MESH' and o.data==mesh.data])==1
    bpy.ops.wm.save_as_mainfile(filepath=OUT)

def refresh_textures():
    bpy.ops.wm.open_mainfile(filepath=OUT)
    xyz,part,valid=raster(bpy.data.objects['SK_Cat'])
    original=bpy.data.images.load(os.path.join(HERE,'Exports','CatAbilities','Cat_BaseColor.png'),check_existing=False)
    pixels=np.empty(4096*4096*4,np.float32); original.pixels.foreach_get(pixels)
    base=pixels.reshape(4096,4096,4)[1::2,1::2,:3].copy(); del pixels
    for name in ['Ninja','Snow Leopard','Frankencat']:
        color,_=texture(name,xyz,part,base); color[~valid]=rgb('262D39')
        slug=name.replace(' ','_'); path=os.path.join(EXPORT,'Cat_'+slug+'_BaseColor.png')
        png(path,color)
        old=bpy.data.images['T_Cat_'+slug]
        fresh=bpy.data.images.load(path,check_existing=False); fresh.name='T_Cat_'+slug+'_refined'; fresh.pack()
        for mat in bpy.data.materials:
            if mat.get('skin_name')==name:
                for node in mat.node_tree.nodes:
                    if node.type=='TEX_IMAGE' and node.image==old: node.image=fresh
        old.user_clear(); bpy.data.images.remove(old); fresh.name='T_Cat_'+slug
    bpy.data.images.remove(original)
    for idx,name in enumerate(NAMES):
        s=bpy.data.scenes[f'{idx+1:02d} | {name}']
        s['Texture']='Exports/CatSkins/Cat_'+name.replace(' ','_')+'_BaseColor.png'
    bpy.ops.wm.save_as_mainfile(filepath=OUT)

if __name__=='__main__':
    if '--retain-scenes' in sys.argv: retain_scenes()
    elif '--refresh-textures' in sys.argv: refresh_textures()
    elif '--render' in sys.argv: render()
    else: build()
