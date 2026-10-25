"""Unrigged reference-based cat. Editable quad surface, no Unity asset writes."""
import bpy
import bmesh
import math
import os
import json
from mathutils import Vector, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "PawArena_BaseCat_Review.blend")
PREVIEW = os.path.join(os.path.dirname(HERE), "Previews", "Characters", "BaseReview")
REFERENCE = "C:/Users/MY PC/Downloads/Bảng tham chiếu mèo mướp low-poly.png"
TAU = math.tau


def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


def move(obj, coll):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)
    return obj


def mesh_object(name, verts, faces, material, coll):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    if material:
        mesh.materials.append(material)
    for p in mesh.polygons:
        p.use_smooth = True
    return obj


def srgb(hexcode):
    values = [int(hexcode[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in values)


def material(name, hexcode, roughness=.5):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*srgb(hexcode), 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = m.diffuse_color
    p.inputs["Roughness"].default_value = roughness
    p.inputs["Specular IOR Level"].default_value = .3
    return m


class Field:
    """Small shader expression builder; markings remain editable Blender nodes."""
    def __init__(self, tree, socket):
        self.tree, self.socket = tree, socket

    def op(self, operation, other=None):
        n = self.tree.nodes.new("ShaderNodeMath")
        n.operation = operation
        self.tree.links.new(self.socket, n.inputs[0])
        if other is not None:
            if isinstance(other, Field):
                self.tree.links.new(other.socket, n.inputs[1])
            else:
                n.inputs[1].default_value = other
        return Field(self.tree, n.outputs[0])

    def __add__(self, other): return self.op("ADD", other)
    def __sub__(self, other): return self.op("SUBTRACT", other)
    def __mul__(self, other): return self.op("MULTIPLY", other)
    def __truediv__(self, other): return self.op("DIVIDE", other)
    def __neg__(self): return self * -1
    def __radd__(self, other): return self + other
    def __rmul__(self, other): return self * other
    def __rsub__(self, other): return (-self) + other
    def abs(self): return self.op("ABSOLUTE")
    def sin(self): return self.op("SINE")
    def maximum(self, other): return self.op("MAXIMUM", other)
    def minimum(self, other): return self.op("MINIMUM", other)
    def clamp(self): return self.maximum(0).minimum(1)


def smooth(a, b, x):
    t = ((x - a) / (b - a)).clamp()
    return t * t * (3 - 2 * t)


def ellipse(x, z, cx, cz, rx, rz, softness=.025):
    a, b = (x - cx) / rx, (z - cz) / rz
    return 1 - smooth(1-softness, 1+softness, a*a + b*b)


def mix_color(tree, fac, a, b):
    n = tree.nodes.new("ShaderNodeMixRGB")
    n.blend_type = "MIX"
    tree.links.new(fac.socket, n.inputs[0])
    for val, index in ((a, 1), (b, 2)):
        if isinstance(val, str):
            n.inputs[index].default_value = (*srgb(val), 1)
        else:
            tree.links.new(val, n.inputs[index])
    return n.outputs[0]


def fur_material():
    m = material("Fur | orange tabby - editable markings", "F3AC48", .56)
    tree = m.node_tree
    coord = tree.nodes.new("ShaderNodeTexCoord")
    sep = tree.nodes.new("ShaderNodeSeparateXYZ")
    tree.links.new(coord.outputs["Object"], sep.inputs[0])
    x, y, z = [Field(tree, s) for s in sep.outputs]
    ax = x.abs()
    head = smooth(1.32, 1.37, z)
    front = 1 - smooth(-.08, .03, y)
    arm = smooth(.39, .46, ax) * smooth(.98, 1.05, z) * (1-smooth(1.33, 1.38, z))
    leg = 1 - smooth(.48, .60, z)

    # Forehead stripes continue over the crown and taper down the back.
    back = smooth(-.03, .15, y)
    start = 1.89 - back * .34
    taper = smooth(start, start+.23, z)
    stripe_width = .045 * taper
    center = 1-smooth(stripe_width-.004, stripe_width+.004, ax)
    outer = 1-smooth(stripe_width*.80-.004, stripe_width*.80+.004,
                      (ax-(.155 + .015*(z*7).sin())).abs())
    head_stripe = center.maximum(outer) * smooth(start-.01, start+.03, z) * head
    arm_stripe = (1-smooth(.035, .044, ((ax-.56)*34).sin().abs()))
    # Wider regular arm bands with a subtle diagonal slant.
    arm_phase = ((ax + (z-1.18)*.10)*35).sin()
    arm_stripe = smooth(.43, .50, arm_phase) * arm
    leg_stripe = smooth(.42, .49, ((z + ax*.12)*33).sin()) * leg
    side_a = ellipse(ax, z, .38, .63, .22, .038, .12)
    side_b = ellipse(ax, z, .39, .84, .20, .035, .12)
    side_c = ellipse(ax, z, .40, 1.02, .17, .032, .12)
    sides = side_a.maximum(side_b).maximum(side_c) * (1-head) * (1-arm)
    spine = (1-smooth(.055, .07, ax)) * smooth(.55, .65, z) * back * (1-head)
    stripes = head_stripe.maximum(arm_stripe).maximum(leg_stripe).maximum(sides).maximum(spine)
    base = mix_color(tree, stripes, "FFAA29", "D8791E")

    belly_width = .255 * (1 - (z-.78)*.40)
    belly = ellipse(x, z, 0, .865, belly_width, .47, .02) * front * (1-head)
    muzzle = ellipse(x, z, 0, 1.405, .365, .19, .055)
    blaze = ellipse(x, z, 0, 1.565, .135, .30, .09)
    face = muzzle.maximum(blaze) * head * front
    hands = smooth(1.005, 1.02, ax) * arm
    feet = 1-smooth(.185, .20, z)
    cream = belly.maximum(face).maximum(hands).maximum(feet)
    final = mix_color(tree, cream, base, "FFF0D8")
    # Inner ear color lies on the ear surface, not on a second triangular slab.
    a = (.268, 2.072)
    b = (.436, 2.012)
    c = (.478, 2.292)
    denom = (b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
    ba = ((b[1]-c[1])*(ax-c[0])+(c[0]-b[0])*(z-c[1]))/denom
    bb = ((c[1]-a[1])*(ax-c[0])+(a[0]-c[0])*(z-c[1]))/denom
    bc = 1-ba-bb
    inner = smooth(-.02,.035,ba.minimum(bb).minimum(bc)) * (1-smooth(.005,.025,y))
    final = mix_color(tree,inner,final,"EFA391")
    tree.links.new(final, tree.nodes.get("Principled BSDF").inputs["Base Color"])
    return m


def subdivision(obj, levels=2):
    mod = obj.modifiers.new("Smooth surface - editable control cage", "SUBSURF")
    mod.levels = levels
    mod.render_levels = levels
    return mod


# The same ring chain flows from the torso through the neck into the head.
PROFILE = [
    (.46, .370, .255, 0), (.55, .395, .285, 0),
    (.70, .401, .298, -.008), (.86, .372, .277, 0),
    (1.02, .335, .241, 0), (1.17, .320, .222, 0),
    (1.32, .290, .206, 0), (1.345, .330, .225, -.015),
    (1.365, .402, .275, -.015), (1.415, .472, .317, -.015),
    (1.53, .511, .349, -.015), (1.68, .521, .362, -.015),
    (1.83, .497, .348, -.015), (1.96, .426, .303, -.015),
    (2.05, .305, .227, -.015), (2.095, .157, .121, -.015),
]


def body_mesh(mat, coll):
    verts, faces, rings = [], [], []
    n = 32
    for z, rx, ry, cy in PROFILE:
        ring = []
        for j in range(n):
            t = TAU*j/n
            ring.append(len(verts))
            verts.append((rx*math.cos(t), cy + ry*math.sin(t), z))
        rings.append(ring)
    for k in range(len(rings)-1):
        for j in range(n):
            # Open two shoulder windows, not overlapping primitive parts.
            shoulder = k in (4, 5) and (j >= 28 or j < 4 or 12 <= j < 20)
            ear_root = k in (13, 14) and (j >= 30 or j < 2 or 14 <= j < 18)
            if not shoulder and not ear_root:
                faces.append((rings[k][j], rings[k][(j+1)%n],
                              rings[k+1][(j+1)%n], rings[k+1][j]))
    top = len(verts)
    verts.append((0, -.015, 2.11))
    for j in range(n):
        faces.append((rings[-1][j], rings[-1][(j+1)%n], top))

    def bridge(a, b):
        assert len(a) == len(b)
        for j in range(len(a)):
            faces.append((a[j], a[(j+1)%len(a)], b[(j+1)%len(a)], b[j]))

    # Ears grow from windows in the crown. These share vertices with the head.
    for sign, start in ((1,30),(-1,14)):
        js = [(start+j)%n for j in range(5)]
        boundary = [rings[13][j] for j in js]
        boundary += [rings[14][js[-1]],rings[15][js[-1]]]
        boundary += [rings[15][j] for j in reversed(js[:-1])]
        boundary += [rings[14][js[0]]]
        original = [Vector(verts[v]) for v in boundary]
        center = sum(original,Vector())/len(original)
        tip = Vector((sign*.493,.018,2.345))
        previous = boundary
        for t in (.22,.48,.72,.89,.96):
            ring=[]
            for p in original:
                point=center.lerp(tip,t)+(p-center)*(1-t)
                ring.append(len(verts))
                verts.append(tuple(point))
            bridge(previous,ring)
            previous=ring
        cap=len(verts)
        verts.append(tuple(tip))
        for j in range(len(previous)):
            faces.append((previous[j],previous[(j+1)%len(previous)],cap))

    # Shoulder boundary loops become arm rings with no seam or separate spheres.
    for sign, start in ((1, 28), (-1, 12)):
        js = [(start+j)%n for j in range(9)]
        boundary = [rings[4][j] for j in js]
        boundary += [rings[5][js[-1]], rings[6][js[-1]]]
        boundary += [rings[6][j] for j in reversed(js[:-1])]
        boundary += [rings[5][js[0]]]
        angles = [math.atan2((verts[v][2]-1.17)/.15, verts[v][1]/.17) for v in boundary]
        previous = boundary
        for xx, radius_y, radius_z in [(.43,.148,.151),(.51,.145,.146),(.65,.142,.142),
                                      (.80,.139,.139),(.94,.136,.136),(1.045,.134,.134),
                                      (1.12,.132,.131),(1.17,.118,.117),(1.195,.084,.083)]:
            ring = []
            for a in angles:
                ring.append(len(verts))
                verts.append((sign*xx, radius_y*math.cos(a), 1.17+radius_z*math.sin(a)))
            bridge(previous, ring)
            previous = ring
        cap = len(verts)
        verts.append((sign*1.205, 0, 1.17))
        for j in range(len(previous)):
            faces.append((previous[j], previous[(j+1)%len(previous)], cap))

    # A shared crotch edge divides the torso into two continuous leg openings.
    crotch = []
    for j in range(1, 8):
        yy = .255*(1-2*j/8)
        crotch.append(len(verts))
        verts.append((0, yy, .46-.075*(1-(yy/.255)**2)))
    for sign in (1, -1):
        if sign == 1:
            boundary = [rings[0][j%n] for j in range(24, 41)] + crotch
        else:
            boundary = [rings[0][j] for j in range(8,25)] + list(reversed(crotch))
        angles = [math.atan2(verts[v][1]/.20, (verts[v][0]-sign*.239)/.18) for v in boundary]
        previous = boundary
        for zz, rx, ry, cy in [(.36,.181,.201,0),(.27,.176,.197,0),
                                (.205,.177,.198,-.005),(.16,.190,.242,-.045),
                                (.085,.194,.257,-.062),(.035,.188,.249,-.065),
                                (.018,.163,.222,-.062)]:
            ring = []
            for a in angles:
                # Round-square paws, still part of the leg mesh.
                power = .78 if zz < .18 else 1
                ca, sa = math.cos(a), math.sin(a)
                px = rx*math.copysign(abs(ca)**power, ca)
                py = ry*math.copysign(abs(sa)**power, sa)
                if py < -.12 and zz < .16:
                    grooves = math.exp(-((px-.065)/.012)**2) + math.exp(-((px+.065)/.012)**2)
                    py += .012*grooves*math.sin(math.pi*min(1,zz/.18))
                ring.append(len(verts))
                verts.append((sign*.239+px, cy+py, zz))
            bridge(previous, ring)
            previous = ring
        cap = len(verts)
        verts.append((sign*.239, -.06, .018))
        for j in range(len(previous)):
            faces.append((previous[j], previous[(j+1)%len(previous)], cap))
    obj = mesh_object("CAT | continuous head body arms legs paws", verts, faces, mat, coll)
    subdivision(obj, 2)
    obj["design_stage"] = "Base approval only. No rig, weights, or export."
    obj["topology"] = "One connected manifold control surface; shared shoulder and crotch loops."
    return obj


def bevel_triangle(name, points, thickness, mat, coll, bevel=.012):
    front = [Vector(p) for p in points]
    verts = [tuple(p) for p in front] + [tuple(p+Vector((0,thickness,0))) for p in front]
    faces = [(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)]
    obj = mesh_object(name, verts, faces, mat, coll)
    mod = obj.modifiers.new("Soft edge", "BEVEL")
    mod.width = bevel
    mod.segments = 3
    mod = obj.modifiers.new("Weighted corner normals", "WEIGHTED_NORMAL")
    mod.keep_sharp = True
    return obj


def ears(fur, pink, coll):
    for s in (-1, 1):
        pts = [(s*.202,-.115,2.015),(s*.505,-.095,1.94),(s*.488,-.015,2.345)]
        bevel_triangle("Ear %s | outer" % s, pts, .15, fur, coll, .025)
        pts = [(s*.264,-.125,2.035),(s*.466,-.111,1.999),(s*.478,-.032,2.293)]
        bevel_triangle("Ear %s | pink inset" % s, pts, .014, pink, coll, .013)


def sphere(name, center, scale, mat, coll, segments=32, rings=20, rot=0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    o = move(bpy.context.object, coll)
    o.name = name
    o.scale = scale
    o.rotation_euler.z = rot
    o.data.materials.append(mat)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def line(name, points, radius, mat, coll):
    c = bpy.data.curves.new(name, "CURVE")
    c.dimensions = "3D"
    c.resolution_u = 16
    c.bevel_depth = radius
    c.bevel_resolution = 3
    spline = c.splines.new("BEZIER")
    spline.bezier_points.add(len(points)-1)
    for bp, p in zip(spline.bezier_points, points):
        bp.co = p
        bp.handle_left_type = "AUTO"
        bp.handle_right_type = "AUTO"
    o = bpy.data.objects.new(name,c)
    coll.objects.link(o)
    c.materials.append(mat)
    return o


def face_y(x, z):
    for i in range(7, len(PROFILE)-1):
        a, b = PROFILE[i], PROFILE[i+1]
        if a[0] <= z <= b[0]:
            t = (z-a[0])/(b[0]-a[0])
            rx, ry = a[1]*(1-t)+b[1]*t, a[2]*(1-t)+b[2]*t
            return -.015 - ry*math.sqrt(max(.01, 1-(x/rx)**2)) - .009
    return -.34


def face(coll, mats):
    nose = [(-.039,-.390,1.564),(.039,-.390,1.564),(0,-.397,1.532)]
    bevel_triangle("Nose | tiny rounded triangle", nose, .025, mats["nose"], coll,.009)
    line("Mouth | center", [(0,-.389,1.539),(0,face_y(0,1.507)-.004,1.507)], .006, mats["mouth"],coll)
    for s in (-1,1):
        pts = [(0,1.507),(.030,1.482),(.066,1.484),(.091,1.505)]
        line("Mouth | smile %s"%s, [(s*x,face_y(x,z)-.003,z) for x,z in pts],.006,mats["mouth"],coll)
        for j in range(3):
            inner_x, outer_x = .339, .445
            iz = 1.529-j*.039
            oz = iz + (.024 if j==0 else (-.009 if j==2 else .007))
            pts = [(s*inner_x, face_y(inner_x,iz)-.005,iz),
                   (s*.39, face_y(.39,(iz+oz)/2)-.006,(iz+oz)/2),
                   (s*outer_x, face_y(outer_x,oz)-.005,oz)]
            whisker=line("Whisker %s %s"%(s,j),pts,.0042,mats["mouth"],coll)
            whisker.data.splines[0].bezier_points[0].radius=.65
            whisker.data.splines[0].bezier_points[-1].radius=.15


def conforming_eyes(body,coll):
    mat=material("Eyes | conforming sapphire surface", "1E5077",.28)
    tree=mat.node_tree
    coord=tree.nodes.new("ShaderNodeTexCoord")
    sep=tree.nodes.new("ShaderNodeSeparateXYZ")
    tree.links.new(coord.outputs["UV"],sep.inputs[0])
    u,v=[Field(tree,sep.outputs[i]) for i in (0,1)]
    iris=mix_color(tree,smooth(.18,.8,v),"337EAB","17496B")
    pupil=ellipse(u,v,.50,.55,.398,.420,.035)
    col=mix_color(tree,pupil,iris,"0A2033")
    shine=ellipse(u,v,.38,.75,.112,.105,.04)
    col=mix_color(tree,shine,col,"FFFFFF")
    shine2=ellipse(u,v,.70,.235,.035,.032,.04)
    col=mix_color(tree,shine2,col,"91C7DD")
    tree.links.new(col,tree.nodes.get("Principled BSDF").inputs["Base Color"])
    tree.nodes.get("Principled BSDF").inputs["Specular IOR Level"].default_value=.28
    bpy.context.view_layer.update()
    evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    for sign in (-1,1):
        verts,faces,uvs=[],[],[]
        cx,cz=sign*.245,1.681
        sides=48
        hit,pos,normal,index=evaluated.ray_cast(Vector((cx,-2,cz)),Vector((0,1,0)))
        assert hit
        verts.append((cx,pos.y-.029,cz))
        uvs.append((.5,.5))
        for k,r in enumerate((.2,.4,.6,.78,.92,1.0)):
            for j in range(sides):
                a=TAU*j/sides
                x,z=cx+.135*r*math.cos(a),cz+.154*r*math.sin(a)
                hit,pos,normal,index=evaluated.ray_cast(Vector((x,-2,z)),Vector((0,1,0)))
                assert hit,"Eye projection missed the head"
                y=pos.y-.003-.026*(1-r*r)
                verts.append((x,y,z))
                uvs.append((.5+.5*r*math.cos(a),.5+.5*r*math.sin(a)))
            if k:
                for j in range(sides):
                    faces.append((1+(k-1)*sides+j,1+(k-1)*sides+(j+1)%sides,
                                  1+k*sides+(j+1)%sides,1+k*sides+j))
            else:
                for j in range(sides):
                    faces.append((0,1+j,1+(j+1)%sides))
        obj=mesh_object("Eye %s | flush curved surface"%sign,verts,faces,mat,coll)
        layer=obj.data.uv_layers.new(name="Eye local")
        for poly in obj.data.polygons:
            for li in poly.loop_indices:
                layer.data[li].uv=uvs[obj.data.loops[li].vertex_index]


def toe_creases(body,coll):
    mat=material("Paws | subtle toe creases","C4B6A1",.85)
    evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    for side in (-1,1):
        for offset in (-.065,.065):
            pts=[]
            for z in (.032,.052,.082,.107,.121):
                x=side*.239+offset
                hit,pos,normal,index=evaluated.ray_cast(Vector((x,-2,z)),Vector((0,1,0)))
                if hit:
                    pts.append((x,pos.y-.0014,z))
            obj=line("Paw %s | toe crease %s"%(side,offset),pts,.0024,mat,coll)
            points=obj.data.splines[0].bezier_points
            points[0].radius=.2
            points[-1].radius=.1


def paws(coll, mats):
    for s in (-1,1):
        # Pads are positioned on the outer end of the mitten, not separate hands.
        sphere("Palm %s | central pad"%s, (s*1.207,-.003,1.143), (.008,.049,.042),mats["pink"],coll,20,12)
        for j in range(4):
            a = math.radians(30+j*40)
            yy = .076*math.cos(a)
            zz = 1.17+.073*math.sin(a)
            sphere("Palm %s | toe pad %s"%(s,j), (s*1.200,yy,zz),(.009,.019,.020),mats["pink"],coll,16,10)


def tail(coll):
    mat = material("Tail | banded tabby", "F3AC48",.55)
    t = mat.node_tree
    uv = t.nodes.new("ShaderNodeTexCoord")
    sep = t.nodes.new("ShaderNodeSeparateXYZ")
    t.links.new(uv.outputs["UV"],sep.inputs[0])
    u = Field(t,sep.outputs[0])
    stripe = smooth(.22,.30,(u*TAU*4.5).sin())
    col = mix_color(t,stripe,"F5B456","DD862E")
    cap = smooth(.91,.94,u)
    col = mix_color(t,cap,col,"FFF0D8")
    t.links.new(col,t.nodes.get("Principled BSDF").inputs["Base Color"])
    verts, faces, uvvalues = [], [], []
    count, sides = 28, 16
    # Cubic curve: emerges from the rump, sweeps backwards, then curls up.
    p0,p1,p2,p3 = [Vector(p) for p in [(0,.20,.52),(0,.86,.43),(0,1.00,.70),(0,.98,1.11)]]
    for k in range(count+1):
        u = k/count
        center = (1-u)**3*p0+3*(1-u)**2*u*p1+3*(1-u)*u*u*p2+u**3*p3
        tangent = (3*(1-u)**2*(p1-p0)+6*(1-u)*u*(p2-p1)+3*u*u*(p3-p2)).normalized()
        cross = tangent.cross(Vector((1,0,0))).normalized()
        radius = .115 - .012*u
        if u>.88:
            radius *= math.sqrt(max(.015,1-((u-.88)/.125)**2))
        for j in range(sides):
            a=TAU*j/sides
            verts.append(tuple(center+Vector((1,0,0))*math.cos(a)*radius+cross*math.sin(a)*radius))
            uvvalues.append((u,j/sides))
        if k:
            for j in range(sides):
                faces.append(((k-1)*sides+j,(k-1)*sides+(j+1)%sides,k*sides+(j+1)%sides,k*sides+j))
    faces += [tuple(reversed(range(sides))),tuple(count*sides+j for j in range(sides))]
    o=mesh_object("Tail | continuous curved tube",verts,faces,mat,coll)
    layer=o.data.uv_layers.new(name="Tail length")
    for p in o.data.polygons:
        for li in p.loop_indices:
            layer.data[li].uv=uvvalues[o.data.loops[li].vertex_index]
    subdivision(o,2)
    return o


def add_stage(coll):
    floor=material("Studio | slate", "28333F",.85)
    bpy.ops.mesh.primitive_plane_add(size=200)
    p=move(bpy.context.object,coll)
    p.name="Studio floor"
    p.data.materials.append(floor)
    p.location.z=.018
    for name,loc,power,size in [("Key",(-3.5,-4.5,6),650,4),
                               ("Fill",(4,-2,3.7),350,3),
                               ("Rim",(1,3.5,5),700,3),
                               ("Front softbox",(0,-4,1.7),130,4)]:
        data=bpy.data.lights.new(name,"AREA")
        data.energy=power
        data.shape="DISK"
        data.size=size
        o=bpy.data.objects.new(name,data)
        coll.objects.link(o)
        o.location=loc
        o.rotation_euler=(Vector((0,0,1.1))-o.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new("Review camera")
    cam=bpy.data.objects.new("Review camera",data)
    coll.objects.link(cam)
    data.type="ORTHO"
    data.ortho_scale=2.82
    bpy.context.scene.camera=cam
    return cam


def camera(cam,location,target,scale=2.82):
    cam.location=location
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=scale


def render(cam,name,location,target,scale=2.82):
    camera(cam,location,target,scale)
    scene=bpy.context.scene
    scene.render.filepath=os.path.join(PREVIEW,name+".png")
    bpy.ops.render.render(write_still=True)


def review_sheet(cat, main_scene):
    """Render four linked model instances, not a painted approximation of the asset."""
    sheet=bpy.data.scenes.new("BASE REVIEW | four views")
    sheet.world=main_scene.world
    sheet.render.engine="BLENDER_EEVEE"
    sheet.render.resolution_x=2400
    sheet.render.resolution_y=740
    sheet.render.resolution_percentage=100
    sheet.render.image_settings.file_format="PNG"
    sheet.view_settings.view_transform=main_scene.view_settings.view_transform
    sheet.view_settings.look=main_scene.view_settings.look
    sheet.view_settings.exposure=main_scene.view_settings.exposure
    label_mat=material("Review labels", "D5E2EC",.9)
    p=label_mat.node_tree.nodes.get("Principled BSDF")
    p.inputs["Emission Color"].default_value=(*srgb("D5E2EC"),1)
    p.inputs["Emission Strength"].default_value=.8
    def label(text,x,z,size):
        data=bpy.data.curves.new(text,"FONT")
        data.body=text
        data.size=size
        data.align_x="CENTER"
        data.materials.append(label_mat)
        obj=bpy.data.objects.new(text,data)
        sheet.collection.objects.link(obj)
        obj.location=(x,-.5,z)
        obj.rotation_euler=(math.pi/2,0,0)
    for x,angle,name in [(-4.05,0,"FRONT"),(-1.35,-.56,"THREE-QUARTER"),
                         (1.35,-math.pi/2,"SIDE"),(4.05,math.pi,"BACK")]:
        instance=bpy.data.objects.new(name,None)
        instance.instance_type="COLLECTION"
        instance.instance_collection=cat
        instance.location.x=x
        instance.rotation_euler.z=angle
        sheet.collection.objects.link(instance)
        label(name,x,-.25,.125)
    label("PAW ARENA  /  BASE CAT",0,2.77,.18)
    label("FORM REVIEW    -    T-POSE    -    NO RIG",0,2.55,.09)
    for name,loc,energy,size in [("Sheet key",(-4,-5,7),1800,8),
                               ("Sheet fill",(5,-3,5),1250,8),
                               ("Sheet rim",(0,3,7),1800,9),
                               ("Sheet front",(0,-6,2),350,7)]:
        data=bpy.data.lights.new(name,"AREA")
        data.energy=energy
        data.shape="DISK"
        data.size=size
        obj=bpy.data.objects.new(name,data)
        sheet.collection.objects.link(obj)
        obj.location=loc
        obj.rotation_euler=(Vector((0,0,1.1))-obj.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new("Turnaround camera")
    cam=bpy.data.objects.new("Turnaround camera",data)
    sheet.collection.objects.link(cam)
    data.type="ORTHO"
    camera(cam,(0,-15,1.23),(0,0,1.23),11.4)
    sheet.camera=cam
    sheet.render.filepath=os.path.join(PREVIEW,"00_BaseCat_ReviewSheet.png")
    bpy.context.window.scene=sheet
    bpy.ops.render.render(write_still=True)
    bpy.context.window.scene=main_scene


def validate(body, coll):
    mesh=body.data
    bm=bmesh.new()
    bm.from_mesh(mesh)
    nonmanifold=sum(not e.is_manifold for e in bm.edges)
    bm.free()
    adjacency=[[] for _ in mesh.vertices]
    for e in mesh.edges:
        a,b=e.vertices
        adjacency[a].append(b)
        adjacency[b].append(a)
    seen=set()
    components=0
    for i in range(len(adjacency)):
        if i in seen: continue
        components+=1
        stack=[i]
        seen.add(i)
        while stack:
            for j in adjacency[stack.pop()]:
                if j not in seen:
                    seen.add(j)
                    stack.append(j)
    assert nonmanifold==0, f"Open/non-manifold body edges: {nonmanifold}"
    assert components==1, f"Disconnected body components: {components}"
    assert not any(o.type=="ARMATURE" for o in bpy.data.objects)
    assert not any(m.type=="ARMATURE" for o in coll.objects for m in o.modifiers)
    evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    result={"stage":"Unrigged base for visual approval; not a finished game asset",
            "body_control_vertices":len(mesh.vertices),"body_control_faces":len(mesh.polygons),
            "body_connected_components":components,"body_nonmanifold_edges":nonmanifold,
            "body_subdivision_faces":len(evaluated.polygons),"armatures":0,
            "unity_files_modified":False,
            "notes":["Body, head, arms, legs and paws form one manifold quad-dominant surface.",
                     "Ears share the head topology; eyes, mouth, pads and tail are separate details.",
                     "Procedural markings are for approval; production UV bake follows approved form.",
                     "No rigging, skin weights, deformation checks, animation, or FBX export in this revision."]}
    body.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
    with open(os.path.join(HERE,"PawArena_BaseCat_Review_Report.json"),"w",encoding="utf-8") as f:
        json.dump(result,f,indent=2)
    print("BASE_REVIEW_VALIDATION",json.dumps(result))


def main():
    os.makedirs(PREVIEW,exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    cat=collection("01 | BASE CAT - NO RIG")
    stage=collection("02 | REVIEW STUDIO")
    refs=collection("03 | REFERENCE - hidden in renders")
    mats={"pink":material("Inner ears and paw pads","F1A598",.65),
          "blue":material("Eyes | sapphire iris","23638D",.23),
          "navy":material("Eyes | deep navy pupil","081D31",.20),
          "white":material("Eyes | catchlight","FFFFFF",.18),
          "blue_light":material("Eyes | lower blue glint","72B5D0",.3),
          "nose":material("Nose | dusty rose","B46D57",.48),
          "mouth":material("Mouth and whiskers | cocoa","50352D",.7)}
    fur=fur_material()
    body=body_mesh(fur,cat)
    conforming_eyes(body,cat)
    toe_creases(body,cat)
    face(cat,mats)
    paws(cat,mats)
    tail(cat)
    if os.path.isfile(REFERENCE):
        image=bpy.data.images.load(REFERENCE,check_existing=True)
        image.pack()
        obj=bpy.data.objects.new("Approved visual reference | supplied by user",None)
        refs.objects.link(obj)
        obj.empty_display_type="IMAGE"
        obj.data=image
        obj.empty_display_size=4.5
        obj.location=(4,1,1.5)
        obj.rotation_euler=(math.pi/2,0,0)
        obj.hide_render=True
        obj.hide_set(True)
    cam=add_stage(stage)
    scene=bpy.context.scene
    scene.render.engine="BLENDER_EEVEE"
    scene.render.resolution_x=1000
    scene.render.resolution_y=1000
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value=(.30,.37,.48,1)
    bg.inputs["Strength"].default_value=.30
    scene.view_settings.view_transform="Khronos PBR Neutral"
    scene.view_settings.look="None"
    scene.view_settings.exposure=-.6
    scene.render.film_transparent=False
    scene.unit_settings.system="METRIC"
    scene["review_scope"]="Approve the base shape first. No rig in this file."
    scene["reference_priority"]="User-supplied orange tabby turnaround sheet."
    validate(body,cat)
    render(cam,"01_Front",(0,-8,1.18),(0,0,1.18))
    render(cam,"02_ThreeQuarter",(4,-7,2.9),(0,0,1.15),2.92)
    render(cam,"03_Side",(8,0,1.18),(0,.18,1.18),2.82)
    render(cam,"04_Back",(0,8,1.18),(0,0,1.18))
    clay=material("Review only | clay override", "A7B0BB",.8)
    bpy.context.view_layer.material_override=clay
    render(cam,"05_Clay",(4,-7,2.9),(0,0,1.15),2.92)
    bpy.context.view_layer.material_override=None
    review_sheet(cat,scene)
    camera(cam,(4,-7,2.9),(0,0,1.15),2.92)
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active=body
    # Open into a useful material-colored review view, without studio clutter.
    stage.hide_viewport=True
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                space=area.spaces.active
                space.shading.type="MATERIAL"
                space.overlay.show_floor=False
                space.overlay.show_extras=False
                space.region_3d.view_distance=4.1
                space.region_3d.view_location=(0,0,1.15)
                space.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("BASE_REVIEW_SAVED",OUT)


if __name__=="__main__":
    main()
