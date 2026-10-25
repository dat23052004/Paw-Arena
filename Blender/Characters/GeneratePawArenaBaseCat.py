import bpy
import math
import os
from mathutils import Vector


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(os.path.dirname(SCRIPT_DIR))
BLEND_PATH = os.path.join(SCRIPT_DIR, "PawArena_BaseCat.blend")
FBX_PATH = os.path.join(
    PROJECT_ROOT,
    "Assets",
    "_Game",
    "Models",
    "Characters",
    "BlenderExports",
    "SK_PawArena_BaseCat.fbx",
)
PREVIEW_DIR = os.path.join(PROJECT_ROOT, "Blender", "Previews", "Characters")
TPPOSE_PREVIEW_PATH = os.path.join(PREVIEW_DIR, "PawArena_BaseCat_TPose.png")
POSED_PREVIEW_PATH = os.path.join(PREVIEW_DIR, "PawArena_BaseCat_Posed.png")
REPORT_PATH = os.path.join(SCRIPT_DIR, "PawArena_BaseCat_Report.txt")

TAU = math.tau


def reset_scene():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def make_collection(name):
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def move_to_collection(obj, collection):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def make_material(name, color, roughness=0.58, metallic=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    return material


def smooth_mesh(obj):
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def apply_object_scale(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)


def add_uv_ellipsoid(
    collection,
    name,
    location,
    scale,
    material,
    segments=24,
    rings=16,
    rotation=(0.0, 0.0, 0.0),
):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    obj.scale = scale
    apply_object_scale(obj)
    obj.data.materials.append(material)
    smooth_mesh(obj)
    return obj


def add_capsule_between(
    collection,
    name,
    start,
    end,
    radius,
    material,
    radial_segments=20,
    rings=12,
):
    start_v = Vector(start)
    end_v = Vector(end)
    direction = end_v - start_v
    center = (start_v + end_v) * 0.5
    length = direction.length
    obj = add_uv_ellipsoid(
        collection,
        name,
        center,
        (radius, radius, max(length * 0.56, radius)),
        material,
        radial_segments,
        rings,
    )
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    return obj


def create_triangular_ear(collection, name, center, side, outer_material, inner_material):
    width = 0.36
    depth = 0.22
    height = 0.55
    cx, cy, cz = center
    outward = 0.05 * side

    vertices = [
        (cx - width * 0.5, cy - depth * 0.5, cz - height * 0.5),
        (cx + width * 0.5, cy - depth * 0.5, cz - height * 0.5),
        (cx + outward, cy - depth * 0.34, cz + height * 0.5),
        (cx - width * 0.5, cy + depth * 0.5, cz - height * 0.5),
        (cx + width * 0.5, cy + depth * 0.5, cz - height * 0.5),
        (cx + outward, cy + depth * 0.34, cz + height * 0.5),
    ]
    faces = [
        (0, 1, 2),
        (5, 4, 3),
        (0, 3, 4, 1),
        (1, 4, 5, 2),
        (2, 5, 3, 0),
    ]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    ear = bpy.data.objects.new(name, mesh)
    collection.objects.link(ear)
    ear.data.materials.append(outer_material)

    inner_scale = 0.67
    inner_vertices = [
        (
            cx + (vertex[0] - cx) * inner_scale,
            cy - depth * 0.515,
            cz + (vertex[2] - cz) * inner_scale - 0.025,
        )
        for vertex in (vertices[0], vertices[1], vertices[2])
    ]
    inner_mesh = bpy.data.meshes.new(name + "_InnerMesh")
    inner_mesh.from_pydata(inner_vertices, [], [(0, 1, 2)])
    inner_mesh.update()
    inner = bpy.data.objects.new(name.replace("Ear", "InnerEar"), inner_mesh)
    collection.objects.link(inner)
    inner.data.materials.append(inner_material)
    return ear, inner


def create_tail_mesh(collection, materials):
    centers = [
        Vector((0.0, 0.30, 1.06)),
        Vector((0.0, 0.62, 0.93)),
        Vector((0.07, 0.91, 0.80)),
        Vector((0.18, 1.12, 1.02)),
        Vector((0.25, 1.12, 1.29)),
    ]
    radii = [0.145, 0.14, 0.125, 0.105, 0.065]
    ring_segments = 12
    vertices = []

    for index, center in enumerate(centers):
        if index == 0:
            tangent = (centers[1] - centers[0]).normalized()
        elif index == len(centers) - 1:
            tangent = (centers[-1] - centers[-2]).normalized()
        else:
            tangent = (centers[index + 1] - centers[index - 1]).normalized()

        reference = Vector((1.0, 0.0, 0.0))
        if abs(tangent.dot(reference)) > 0.92:
            reference = Vector((0.0, 0.0, 1.0))
        axis_a = tangent.cross(reference).normalized()
        axis_b = tangent.cross(axis_a).normalized()

        for segment in range(ring_segments):
            angle = TAU * segment / ring_segments
            offset = (
                axis_a * math.cos(angle) * radii[index]
                + axis_b * math.sin(angle) * radii[index]
            )
            vertices.append(tuple(center + offset))

    faces = []
    material_indices = []
    for ring in range(len(centers) - 1):
        for segment in range(ring_segments):
            next_segment = (segment + 1) % ring_segments
            a = ring * ring_segments + segment
            b = ring * ring_segments + next_segment
            c = (ring + 1) * ring_segments + next_segment
            d = (ring + 1) * ring_segments + segment
            faces.append((a, b, c, d))
            material_indices.append(1 if ring in (1, 3) else 0)

    faces.append(tuple(range(ring_segments - 1, -1, -1)))
    material_indices.append(0)
    final_ring = (len(centers) - 1) * ring_segments
    faces.append(tuple(final_ring + i for i in range(ring_segments)))
    material_indices.append(0)

    mesh = bpy.data.meshes.new("Cat_Tail_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    tail = bpy.data.objects.new("GEO_Cat_Tail", mesh)
    collection.objects.link(tail)
    tail.data.materials.append(materials["fur"])
    tail.data.materials.append(materials["stripe"])
    for polygon, material_index in zip(tail.data.polygons, material_indices):
        polygon.material_index = material_index
        polygon.use_smooth = True
    return tail, len(centers), ring_segments


def create_armature(collection):
    armature_data = bpy.data.armatures.new("PawArena_Cat_Humanoid_Armature")
    rig = bpy.data.objects.new("RIG_PawArena_Cat_Humanoid", armature_data)
    collection.objects.link(rig)
    rig.show_in_front = True
    rig.display_type = "WIRE"
    rig["rig_type"] = "Paw Arena Humanoid Cat"
    rig["rig_version"] = "1.0.0"
    rig["bind_pose"] = "T-Pose"
    rig["unity_animation_type"] = "Humanoid"
    rig["head_gear_socket"] = "HeadGearSocket"

    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def bone(name, head, tail, parent=None, connected=False, deform=True, roll=0.0):
        edit_bone = armature_data.edit_bones.new(name)
        edit_bone.head = head
        edit_bone.tail = tail
        edit_bone.roll = roll
        edit_bone.use_deform = deform
        if parent:
            edit_bone.parent = armature_data.edit_bones[parent]
            edit_bone.use_connect = connected
        return edit_bone

    bone("Root", (0, 0, 0), (0, 0, 0.16), deform=False)
    bone("Hips", (0, 0, 0.96), (0, 0, 1.13), "Root")
    bone("Spine", (0, 0, 1.13), (0, 0, 1.31), "Hips", True)
    bone("Chest", (0, 0, 1.31), (0, 0, 1.48), "Spine", True)
    bone("Neck", (0, 0, 1.48), (0, 0, 1.59), "Chest", True)
    bone("Head", (0, 0, 1.59), (0, 0, 1.96), "Neck", True)

    bone("LeftShoulder", (0.12, 0, 1.45), (0.33, 0, 1.45), "Chest")
    bone("LeftUpperArm", (0.33, 0, 1.45), (0.69, 0, 1.45), "LeftShoulder", True)
    bone("LeftLowerArm", (0.69, 0, 1.45), (1.00, 0, 1.45), "LeftUpperArm", True)
    bone("LeftHand", (1.00, 0, 1.45), (1.20, 0, 1.45), "LeftLowerArm", True)

    bone("RightShoulder", (-0.12, 0, 1.45), (-0.33, 0, 1.45), "Chest")
    bone("RightUpperArm", (-0.33, 0, 1.45), (-0.69, 0, 1.45), "RightShoulder", True)
    bone("RightLowerArm", (-0.69, 0, 1.45), (-1.00, 0, 1.45), "RightUpperArm", True)
    bone("RightHand", (-1.00, 0, 1.45), (-1.20, 0, 1.45), "RightLowerArm", True)

    bone("LeftUpperLeg", (0.23, 0, 1.01), (0.25, 0, 0.63), "Hips")
    bone("LeftLowerLeg", (0.25, 0, 0.63), (0.25, 0, 0.25), "LeftUpperLeg", True)
    bone("LeftFoot", (0.25, 0, 0.25), (0.25, -0.34, 0.17), "LeftLowerLeg", True)
    bone("LeftToes", (0.25, -0.34, 0.17), (0.25, -0.50, 0.15), "LeftFoot", True)

    bone("RightUpperLeg", (-0.23, 0, 1.01), (-0.25, 0, 0.63), "Hips")
    bone("RightLowerLeg", (-0.25, 0, 0.63), (-0.25, 0, 0.25), "RightUpperLeg", True)
    bone("RightFoot", (-0.25, 0, 0.25), (-0.25, -0.34, 0.17), "RightLowerLeg", True)
    bone("RightToes", (-0.25, -0.34, 0.17), (-0.25, -0.50, 0.15), "RightFoot", True)

    bone("LeftEar", (0.25, 0.0, 1.94), (0.36, 0.0, 2.19), "Head")
    bone("RightEar", (-0.25, 0.0, 1.94), (-0.36, 0.0, 2.19), "Head")

    tail_points = [
        (0.0, 0.28, 1.06),
        (0.0, 0.62, 0.93),
        (0.07, 0.91, 0.80),
        (0.18, 1.12, 1.02),
        (0.25, 1.12, 1.29),
    ]
    for index in range(4):
        bone(
            f"Tail_{index + 1:02d}",
            tail_points[index],
            tail_points[index + 1],
            "Hips" if index == 0 else f"Tail_{index:02d}",
            index > 0,
        )

    bone(
        "HeadGearSocket",
        (0, 0, 1.98),
        (0, 0, 2.15),
        "Head",
        deform=False,
    )

    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    return rig


def add_armature_modifier(obj, rig):
    modifier = obj.modifiers.new("PawArena_Rig", "ARMATURE")
    modifier.object = rig
    modifier.use_deform_preserve_volume = True


def assign_rigid_weight(obj, rig, bone_name):
    add_armature_modifier(obj, rig)
    group = obj.vertex_groups.new(name=bone_name)
    group.add(range(len(obj.data.vertices)), 1.0, "REPLACE")


def assign_torso_weights(obj, rig):
    add_armature_modifier(obj, rig)
    groups = {
        name: obj.vertex_groups.new(name=name)
        for name in ("Hips", "Spine", "Chest")
    }
    for vertex in obj.data.vertices:
        world_z = (obj.matrix_world @ vertex.co).z
        if world_z <= 1.10:
            weights = {"Hips": 1.0}
        elif world_z <= 1.28:
            t = (world_z - 1.10) / 0.18
            weights = {"Hips": 1.0 - t, "Spine": t}
        elif world_z <= 1.45:
            t = (world_z - 1.28) / 0.17
            weights = {"Spine": 1.0 - t, "Chest": t}
        else:
            weights = {"Chest": 1.0}
        for name, weight in weights.items():
            groups[name].add([vertex.index], weight, "REPLACE")


def assign_tail_weights(obj, rig, ring_count, ring_segments):
    add_armature_modifier(obj, rig)
    groups = {
        name: obj.vertex_groups.new(name=name)
        for name in ("Tail_01", "Tail_02", "Tail_03", "Tail_04")
    }
    ring_weights = (
        {"Tail_01": 1.0},
        {"Tail_01": 0.45, "Tail_02": 0.55},
        {"Tail_02": 0.45, "Tail_03": 0.55},
        {"Tail_03": 0.45, "Tail_04": 0.55},
        {"Tail_04": 1.0},
    )
    for ring_index in range(ring_count):
        for segment in range(ring_segments):
            vertex_index = ring_index * ring_segments + segment
            for group_name, weight in ring_weights[ring_index].items():
                groups[group_name].add([vertex_index], weight, "REPLACE")


def create_organic_body_surface(collection, materials):
    metaball_data = bpy.data.metaballs.new("PawArena_Cat_OrganicSurface")
    metaball_data.resolution = 0.045
    metaball_data.render_resolution = 0.035
    metaball_data.threshold = 0.62

    metaball_object = bpy.data.objects.new("GEO_Cat_OrganicSurface", metaball_data)
    collection.objects.link(metaball_object)

    def add_element(location, scale, stiffness=2.0):
        element = metaball_data.elements.new()
        element.type = "ELLIPSOID"
        element.co = location
        element.radius = 1.0
        element.size_x = scale[0]
        element.size_y = scale[1]
        element.size_z = scale[2]
        element.stiffness = stiffness
        element.use_scale_stiffness = True

    # Core silhouette: one connected surface from head through torso.
    add_element((0.0, 0.0, 1.12), (0.53, 0.40, 0.59), 2.4)
    add_element((0.0, 0.0, 1.39), (0.45, 0.35, 0.35), 2.2)
    add_element((0.0, -0.005, 1.53), (0.30, 0.27, 0.25), 2.0)
    add_element((0.0, -0.018, 1.75), (0.49, 0.41, 0.42), 2.6)

    # Arms overlap through the shoulder and elbow zones so no spherical seams remain.
    for side in (-1.0, 1.0):
        add_element((0.36 * side, 0.0, 1.44), (0.29, 0.21, 0.22), 2.1)
        add_element((0.54 * side, 0.0, 1.45), (0.27, 0.19, 0.20), 2.0)
        add_element((0.73 * side, 0.0, 1.45), (0.25, 0.18, 0.19), 2.0)
        add_element((0.92 * side, 0.0, 1.45), (0.23, 0.17, 0.18), 2.0)
        add_element((1.10 * side, 0.0, 1.45), (0.22, 0.20, 0.21), 2.2)

        # Legs blend into the pelvis and remain continuous through knee and ankle.
        add_element((0.22 * side, 0.0, 0.98), (0.28, 0.25, 0.32), 2.2)
        add_element((0.24 * side, 0.0, 0.76), (0.26, 0.23, 0.30), 2.0)
        add_element((0.25 * side, 0.0, 0.51), (0.22, 0.21, 0.28), 2.0)
        add_element((0.25 * side, -0.04, 0.29), (0.21, 0.21, 0.22), 2.0)
        add_element((0.25 * side, -0.24, 0.18), (0.26, 0.35, 0.19), 2.2)
        add_element((0.25 * side, -0.46, 0.17), (0.20, 0.19, 0.14), 2.0)

    bpy.ops.object.select_all(action="DESELECT")
    metaball_object.select_set(True)
    bpy.context.view_layer.objects.active = metaball_object
    bpy.ops.object.convert(target="MESH")

    body = bpy.context.object
    body.name = "GEO_Cat_OrganicBody"
    body.data.name = "GEO_Cat_OrganicBody_Mesh"
    body.data.materials.append(materials["fur"])
    body.data.materials.append(materials["cream"])
    smooth_mesh(body)

    for polygon in body.data.polygons:
        center = sum((body.data.vertices[index].co for index in polygon.vertices), Vector())
        center /= len(polygon.vertices)
        is_hand = abs(center.x) > 1.01 and center.z > 1.23
        is_foot = center.z < 0.31 and center.y < 0.05
        is_belly = (
            abs(center.x) < 0.29
            and center.y < -0.29
            and 0.80 < center.z < 1.43
        )
        polygon.material_index = 1 if (is_hand or is_foot or is_belly) else 0

    return body


def assign_organic_body_weights(obj, rig):
    add_armature_modifier(obj, rig)
    bone_names = (
        "Hips",
        "Spine",
        "Chest",
        "Neck",
        "Head",
        "LeftUpperArm",
        "LeftLowerArm",
        "LeftHand",
        "RightUpperArm",
        "RightLowerArm",
        "RightHand",
        "LeftUpperLeg",
        "LeftLowerLeg",
        "LeftFoot",
        "LeftToes",
        "RightUpperLeg",
        "RightLowerLeg",
        "RightFoot",
        "RightToes",
    )
    groups = {name: obj.vertex_groups.new(name=name) for name in bone_names}

    def transition(value, center, width, before_bone, after_bone):
        t = max(0.0, min(1.0, (value - (center - width)) / (width * 2.0)))
        return {before_bone: 1.0 - t, after_bone: t}

    def normalize(weights):
        filtered = {name: weight for name, weight in weights.items() if weight > 0.0001}
        total = sum(filtered.values())
        return {name: weight / total for name, weight in filtered.items()}

    for vertex in obj.data.vertices:
        point = obj.matrix_world @ vertex.co
        x = point.x
        y = point.y
        z = point.z
        absolute_x = abs(x)
        side = "Left" if x >= 0.0 else "Right"

        if absolute_x > 0.30 and z > 1.18:
            upper = f"{side}UpperArm"
            lower = f"{side}LowerArm"
            hand = f"{side}Hand"
            if absolute_x < 0.43:
                weights = transition(absolute_x, 0.34, 0.09, "Chest", upper)
            elif absolute_x < 0.61:
                weights = {upper: 1.0}
            elif absolute_x < 0.77:
                weights = transition(absolute_x, 0.69, 0.08, upper, lower)
            elif absolute_x < 0.92:
                weights = {lower: 1.0}
            elif absolute_x < 1.08:
                weights = transition(absolute_x, 1.00, 0.08, lower, hand)
            else:
                weights = {hand: 1.0}
        elif z < 1.10 and absolute_x > 0.075:
            upper = f"{side}UpperLeg"
            lower = f"{side}LowerLeg"
            foot = f"{side}Foot"
            toes = f"{side}Toes"
            if z > 0.93:
                t = max(0.0, min(1.0, (1.08 - z) / 0.18))
                weights = {"Hips": 1.0 - t, upper: t}
            elif z > 0.70:
                weights = {upper: 1.0}
            elif z > 0.56:
                t = max(0.0, min(1.0, (0.70 - z) / 0.14))
                weights = {upper: 1.0 - t, lower: t}
            elif z > 0.33:
                weights = {lower: 1.0}
            elif z > 0.20:
                t = max(0.0, min(1.0, (0.33 - z) / 0.13))
                weights = {lower: 1.0 - t, foot: t}
            elif y < -0.34:
                t = max(0.0, min(1.0, (-y - 0.34) / 0.16))
                weights = {foot: 1.0 - t, toes: t}
            else:
                weights = {foot: 1.0}
        elif z >= 1.58:
            weights = {"Head": 1.0}
        elif z >= 1.46:
            t = max(0.0, min(1.0, (z - 1.46) / 0.12))
            weights = {"Neck": 1.0 - t, "Head": t}
        elif z >= 1.30:
            t = max(0.0, min(1.0, (z - 1.30) / 0.16))
            weights = {"Spine": 1.0 - t, "Chest": t}
        elif z >= 1.12:
            t = max(0.0, min(1.0, (z - 1.12) / 0.18))
            weights = {"Hips": 1.0 - t, "Spine": t}
        else:
            weights = {"Hips": 1.0}

        for bone_name, weight in normalize(weights).items():
            groups[bone_name].add([vertex.index], weight, "REPLACE")


def join_skinned_meshes(objects, name, rig):
    if not objects:
        raise RuntimeError(f"No mesh parts supplied for {name}")

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()

    joined = objects[0]
    joined.name = name
    joined.data.name = name + "_Mesh"

    old_materials = list(joined.data.materials)
    unique_materials = []
    material_lookup = {}
    material_remap = {}
    for old_index, material in enumerate(old_materials):
        key = material.name if material else f"None_{old_index}"
        if key not in material_lookup:
            material_lookup[key] = len(unique_materials)
            unique_materials.append(material)
        material_remap[old_index] = material_lookup[key]

    remapped_polygon_materials = [
        material_remap.get(polygon.material_index, 0)
        for polygon in joined.data.polygons
    ]
    joined.data.materials.clear()
    for material in unique_materials:
        joined.data.materials.append(material)
    for polygon, material_index in zip(joined.data.polygons, remapped_polygon_materials):
        polygon.material_index = material_index

    armature_modifiers = [modifier for modifier in joined.modifiers if modifier.type == "ARMATURE"]
    if not armature_modifiers:
        add_armature_modifier(joined, rig)
    else:
        armature_modifiers[0].object = rig
        armature_modifiers[0].use_deform_preserve_volume = True
        for extra_modifier in armature_modifiers[1:]:
            joined.modifiers.remove(extra_modifier)

    joined.select_set(False)
    return joined


def create_character_meshes(collection, rig, materials):
    meshes = []

    body = create_organic_body_surface(collection, materials)
    assign_organic_body_weights(body, rig)
    meshes.append(body)

    left_ear, left_inner = create_triangular_ear(
        collection,
        "GEO_Cat_Ear_L",
        (0.27, -0.005, 2.02),
        1,
        materials["fur"],
        materials["inner_ear"],
    )
    right_ear, right_inner = create_triangular_ear(
        collection,
        "GEO_Cat_Ear_R",
        (-0.27, -0.005, 2.02),
        -1,
        materials["fur"],
        materials["inner_ear"],
    )
    for ear_obj, bone_name in (
        (left_ear, "LeftEar"),
        (left_inner, "LeftEar"),
        (right_ear, "RightEar"),
        (right_inner, "RightEar"),
    ):
        assign_rigid_weight(ear_obj, rig, bone_name)
        meshes.append(ear_obj)

    for side, x in (("L", 0.13), ("R", -0.13)):
        muzzle = add_uv_ellipsoid(
            collection,
            f"GEO_Cat_Muzzle_{side}",
            (x, -0.374, 1.64),
            (0.175, 0.12, 0.145),
            materials["cream"],
            20,
            14,
        )
        assign_rigid_weight(muzzle, rig, "Head")
        meshes.append(muzzle)

    chin = add_uv_ellipsoid(
        collection,
        "GEO_Cat_Chin",
        (0.0, -0.342, 1.54),
        (0.19, 0.09, 0.105),
        materials["cream"],
        20,
        12,
    )
    assign_rigid_weight(chin, rig, "Head")
    meshes.append(chin)

    for side, x in (("L", 0.18), ("R", -0.18)):
        eye = add_uv_ellipsoid(
            collection,
            f"GEO_Cat_Eye_{side}",
            (x, -0.367, 1.80),
            (0.115, 0.052, 0.155),
            materials["eye_blue"],
            24,
            16,
        )
        pupil = add_uv_ellipsoid(
            collection,
            f"GEO_Cat_Pupil_{side}",
            (x, -0.416, 1.80),
            (0.071, 0.025, 0.112),
            materials["pupil"],
            20,
            14,
        )
        shine = add_uv_ellipsoid(
            collection,
            f"GEO_Cat_EyeShine_{side}",
            (x - 0.025 * (1 if side == "L" else -1), -0.438, 1.855),
            (0.027, 0.012, 0.037),
            materials["white"],
            12,
            8,
        )
        for obj in (eye, pupil, shine):
            assign_rigid_weight(obj, rig, "Head")
            meshes.append(obj)

    nose = add_uv_ellipsoid(
        collection,
        "GEO_Cat_Nose",
        (0.0, -0.495, 1.665),
        (0.065, 0.035, 0.047),
        materials["nose"],
        16,
        10,
    )
    assign_rigid_weight(nose, rig, "Head")
    meshes.append(nose)

    for index, x in enumerate((-0.09, 0.0, 0.09)):
        stripe = add_uv_ellipsoid(
            collection,
            f"GEO_Cat_ForeheadStripe_{index + 1}",
            (x, -0.344, 1.982 - abs(x) * 0.22),
            (0.032, 0.024, 0.105 - abs(x) * 0.18),
            materials["stripe"],
            12,
            8,
            rotation=(0.0, (0.20 if x < 0 else -0.20 if x > 0 else 0.0), 0.0),
        )
        assign_rigid_weight(stripe, rig, "Head")
        meshes.append(stripe)

    tail, ring_count, ring_segments = create_tail_mesh(collection, materials)
    assign_tail_weights(tail, rig, ring_count, ring_segments)
    meshes.append(tail)

    ear_parts = [obj for obj in meshes if "Ear" in obj.name]
    body_parts = [obj for obj in meshes if obj not in ear_parts]
    body_mesh = join_skinned_meshes(body_parts, "SK_Cat_Body", rig)
    ear_mesh = join_skinned_meshes(ear_parts, "SK_Cat_Ears", rig)
    ear_mesh["gear_visibility_group"] = "Hide for closed hats or full-head hair"
    return [body_mesh, ear_mesh]


def create_headgear_guide(collection, rig):
    guide = bpy.data.objects.new("SOCKET_HeadGear_Guide", None)
    collection.objects.link(guide)
    guide.empty_display_type = "SPHERE"
    guide.empty_display_size = 0.12
    guide.color = (0.2, 0.8, 1.0, 1.0)
    guide.parent = rig
    guide.parent_type = "BONE"
    guide.parent_bone = "HeadGearSocket"
    guide.location = (0.0, 0.0, 0.0)
    guide.rotation_euler = (0.0, 0.0, 0.0)
    guide.hide_render = True
    guide["socket_usage"] = "Rigid hats and short hair"
    return guide


def create_validation_action(rig):
    action = bpy.data.actions.new("ANIM_RigValidation")
    rig.animation_data_create()
    rig.animation_data.action = action

    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")

    for pose_bone in rig.pose.bones:
        pose_bone.rotation_mode = "XYZ"

    frames = (1, 18, 36, 54)
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        for pose_bone in rig.pose.bones:
            pose_bone.location = (0.0, 0.0, 0.0)
            pose_bone.rotation_euler = (0.0, 0.0, 0.0)
            pose_bone.scale = (1.0, 1.0, 1.0)

        if frame == 18:
            rig.pose.bones["LeftUpperArm"].rotation_euler[1] = math.radians(62)
            rig.pose.bones["RightUpperArm"].rotation_euler[1] = math.radians(-62)
            rig.pose.bones["LeftLowerArm"].rotation_euler[1] = math.radians(18)
            rig.pose.bones["RightLowerArm"].rotation_euler[1] = math.radians(-18)
            rig.pose.bones["Head"].rotation_euler[1] = math.radians(-7)
            rig.pose.bones["Tail_01"].rotation_euler[2] = math.radians(9)
        elif frame == 36:
            rig.pose.bones["LeftUpperArm"].rotation_euler[1] = math.radians(40)
            rig.pose.bones["RightUpperArm"].rotation_euler[1] = math.radians(-105)
            rig.pose.bones["RightLowerArm"].rotation_euler[2] = math.radians(-55)
            rig.pose.bones["Head"].rotation_euler[2] = math.radians(-10)
            rig.pose.bones["LeftUpperLeg"].rotation_euler[0] = math.radians(-12)
            rig.pose.bones["RightUpperLeg"].rotation_euler[0] = math.radians(12)
            rig.pose.bones["Tail_02"].rotation_euler[2] = math.radians(-12)

        for pose_bone in rig.pose.bones:
            pose_bone.keyframe_insert(data_path="location", frame=frame, group=pose_bone.name)
            pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=pose_bone.name)
            pose_bone.keyframe_insert(data_path="scale", frame=frame, group=pose_bone.name)

    action.frame_start = 1
    action.frame_end = 54
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 54
    bpy.context.scene.frame_set(1)
    return action


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_preview_scene(collection, materials):
    bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, -0.005))
    floor = bpy.context.object
    floor.name = "PREVIEW_Ground"
    move_to_collection(floor, collection)
    floor.data.materials.append(materials["ground"])

    bpy.ops.object.light_add(type="AREA", location=(3.6, -4.2, 5.2))
    key = bpy.context.object
    key.name = "PREVIEW_KeyLight"
    key.data.energy = 900
    key.data.shape = "DISK"
    key.data.size = 4.0
    move_to_collection(key, collection)
    look_at(key, (0, 0, 1.1))

    bpy.ops.object.light_add(type="AREA", location=(-3.2, -1.8, 3.1))
    fill = bpy.context.object
    fill.name = "PREVIEW_FillLight"
    fill.data.energy = 600
    fill.data.size = 3.0
    move_to_collection(fill, collection)
    look_at(fill, (0, 0, 1.1))

    bpy.ops.object.light_add(type="AREA", location=(0.5, 3.8, 4.0))
    rim = bpy.context.object
    rim.name = "PREVIEW_RimLight"
    rim.data.energy = 850
    rim.data.size = 2.5
    move_to_collection(rim, collection)
    look_at(rim, (0, 0, 1.35))

    camera_data = bpy.data.cameras.new("PREVIEW_Camera")
    camera = bpy.data.objects.new("PREVIEW_Camera", camera_data)
    collection.objects.link(camera)
    camera.location = (3.25, -5.9, 2.55)
    camera_data.lens = 62
    camera_data.sensor_width = 36
    look_at(camera, (0, 0, 1.15))
    bpy.context.scene.camera = camera
    return camera


def configure_render():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.image_settings.color_depth = "8"

    scene.world.color = (0.035, 0.055, 0.085)
    if scene.world.use_nodes:
        background = scene.world.node_tree.nodes.get("Background")
        background.inputs["Color"].default_value = (0.035, 0.055, 0.085, 1.0)
        background.inputs["Strength"].default_value = 0.35

    scene.view_settings.look = "AgX - Medium High Contrast"


def render_previews(rig, camera):
    scene = bpy.context.scene
    scene.frame_set(1)
    camera.location = (0.0, -6.6, 1.75)
    look_at(camera, (0, 0, 1.12))
    scene.render.filepath = TPPOSE_PREVIEW_PATH
    bpy.ops.render.render(write_still=True)

    scene.frame_set(36)
    camera.location = (3.25, -5.9, 2.55)
    look_at(camera, (0, 0, 1.15))
    scene.render.filepath = POSED_PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    scene.frame_set(1)


def validate_character(rig, meshes):
    required_bones = {
        "Root",
        "Hips",
        "Spine",
        "Chest",
        "Neck",
        "Head",
        "LeftShoulder",
        "LeftUpperArm",
        "LeftLowerArm",
        "LeftHand",
        "RightShoulder",
        "RightUpperArm",
        "RightLowerArm",
        "RightHand",
        "LeftUpperLeg",
        "LeftLowerLeg",
        "LeftFoot",
        "LeftToes",
        "RightUpperLeg",
        "RightLowerLeg",
        "RightFoot",
        "RightToes",
        "LeftEar",
        "RightEar",
        "Tail_01",
        "Tail_02",
        "Tail_03",
        "Tail_04",
        "HeadGearSocket",
    }
    actual_bones = set(rig.data.bones.keys())
    missing_bones = sorted(required_bones - actual_bones)
    if missing_bones:
        raise RuntimeError("Missing required bones: " + ", ".join(missing_bones))

    unweighted_vertices = []
    missing_armature_modifiers = []
    for obj in meshes:
        if not any(mod.type == "ARMATURE" and mod.object == rig for mod in obj.modifiers):
            missing_armature_modifiers.append(obj.name)
        for vertex in obj.data.vertices:
            total = sum(element.weight for element in vertex.groups)
            if total < 0.999:
                unweighted_vertices.append(f"{obj.name}:{vertex.index}:{total:.3f}")
                if len(unweighted_vertices) >= 20:
                    break

    if missing_armature_modifiers:
        raise RuntimeError(
            "Meshes missing armature modifier: " + ", ".join(missing_armature_modifiers)
        )
    if unweighted_vertices:
        raise RuntimeError(
            "Unweighted or partially weighted vertices: "
            + ", ".join(unweighted_vertices)
        )

    head_socket = rig.data.bones["HeadGearSocket"]
    if head_socket.use_deform:
        raise RuntimeError("HeadGearSocket must be a non-deforming bone")

    polygon_count = sum(len(obj.data.polygons) for obj in meshes)
    vertex_count = sum(len(obj.data.vertices) for obj in meshes)
    report_lines = [
        "Paw Arena Base Cat validation",
        "================================",
        "Status: PASS",
        "Rig type: Humanoid-compatible biped with cat extension bones",
        f"Rig object: {rig.name}",
        f"Bone count: {len(rig.data.bones)}",
        f"Mesh object count: {len(meshes)}",
        f"Vertex count: {vertex_count}",
        f"Polygon count: {polygon_count}",
        "Bind pose: T-Pose",
        "Body topology: continuous organic torso, arms, hands, legs, feet and head",
        "Joint weighting: blended at shoulders, elbows, hips, knees, ankles and wrists",
        "Extra bones: LeftEar, RightEar, Tail_01..Tail_04, HeadGearSocket",
        "Skin scope: material/color variants plus rigid head gear",
        "Validation: all required bones present; all mesh vertices weighted",
    ]
    with open(REPORT_PATH, "w", encoding="utf-8") as report:
        report.write("\n".join(report_lines) + "\n")
    return report_lines


def save_blend():
    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


def export_fbx(rig, meshes):
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig

    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=True,
        use_mesh_modifiers_render=True,
        mesh_smooth_type="FACE",
        use_triangles=True,
        add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="AUTO",
        embed_textures=False,
    )
    bpy.ops.object.select_all(action="DESELECT")


def main():
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    reset_scene()

    geometry_collection = make_collection("CHARACTER_GEO")
    rig_collection = make_collection("CHARACTER_RIG")
    guide_collection = make_collection("CHARACTER_GUIDES")
    preview_collection = make_collection("PREVIEW_ONLY")

    materials = {
        "fur": make_material("MAT_Cat_Fur_Orange", (0.92, 0.38, 0.07), 0.62),
        "cream": make_material("MAT_Cat_Fur_Cream", (0.95, 0.77, 0.52), 0.68),
        "stripe": make_material("MAT_Cat_Fur_Stripe", (0.43, 0.12, 0.025), 0.66),
        "inner_ear": make_material("MAT_Cat_InnerEar", (0.95, 0.45, 0.48), 0.58),
        "eye_blue": make_material("MAT_Cat_EyeBlue", (0.05, 0.36, 0.62), 0.24),
        "pupil": make_material("MAT_Cat_Pupil", (0.004, 0.008, 0.012), 0.18),
        "nose": make_material("MAT_Cat_Nose", (0.20, 0.035, 0.05), 0.45),
        "white": make_material("MAT_Cat_EyeShine", (1.0, 1.0, 1.0), 0.16),
        "ground": make_material("MAT_PreviewGround", (0.055, 0.075, 0.10), 0.78),
    }

    rig = create_armature(rig_collection)
    meshes = create_character_meshes(geometry_collection, rig, materials)
    create_headgear_guide(guide_collection, rig)
    create_validation_action(rig)
    camera = create_preview_scene(preview_collection, materials)
    configure_render()

    report_lines = validate_character(rig, meshes)
    save_blend()
    render_previews(rig, camera)
    export_fbx(rig, meshes)
    save_blend()

    print("BASE_CAT_BLEND=" + BLEND_PATH)
    print("BASE_CAT_FBX=" + FBX_PATH)
    print("BASE_CAT_TPOSE_PREVIEW=" + TPPOSE_PREVIEW_PATH)
    print("BASE_CAT_POSED_PREVIEW=" + POSED_PREVIEW_PATH)
    print("BASE_CAT_REPORT=" + REPORT_PATH)
    for line in report_lines:
        print("BASE_CAT_VALIDATION=" + line)


if __name__ == "__main__":
    main()
