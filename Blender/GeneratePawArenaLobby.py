import bpy
import math
import os
from mathutils import Vector


ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
BLEND_PATH = os.path.join(ROOT_DIR, "PawArena_Lobby.blend")
PREVIEW_DIR = os.path.join(ROOT_DIR, "Previews")


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)


def make_collection(name):
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def make_material(name, color, metallic=0.0, roughness=0.55, emission_strength=0.0, alpha=1.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, alpha)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*color, alpha)
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    if emission_strength > 0.0:
        emission_input = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
        if emission_input:
            emission_input.default_value = (*color, 1.0)
        strength_input = principled.inputs.get("Emission Strength")
        if strength_input:
            strength_input.default_value = emission_strength
    if alpha < 1.0:
        principled.inputs["Alpha"].default_value = alpha
        if hasattr(material, "surface_render_method"):
            material.surface_render_method = "DITHERED"
        elif hasattr(material, "blend_method"):
            material.blend_method = "BLEND"
        if hasattr(material, "use_transparency_overlap"):
            material.use_transparency_overlap = False
    return material


def move_to_collection(obj, collection):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def make_root(collection, name, location=(0, 0, 0), rotation=(0, 0, 0), scale=(1, 1, 1)):
    root = bpy.data.objects.new(name, None)
    collection.objects.link(root)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 0.35
    root.location = location
    root.rotation_euler = rotation
    root.scale = scale
    return root


def set_materials(obj, materials):
    for material in materials:
        obj.data.materials.append(material)


def apply_bevel(obj, width=0.04):
    if width <= 0:
        return
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new("LowPolyEdgeBevel", "BEVEL")
    modifier.width = width
    modifier.segments = 1
    modifier.limit_method = "ANGLE"
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def add_cube(collection, name, dimensions, location=(0, 0, 0), rotation=(0, 0, 0), material=None,
             parent=None, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(location=(0, 0, 0))
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    obj.parent = parent
    obj.location = location
    obj.rotation_euler = rotation
    obj.dimensions = dimensions
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)
    if material:
        obj.data.materials.append(material)
    apply_bevel(obj, bevel)
    return obj


def add_cylinder(collection, name, radius, depth, vertices=12, location=(0, 0, 0), rotation=(0, 0, 0),
                 material=None, parent=None, scale=(1, 1, 1), bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=(0, 0, 0))
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    obj.parent = parent
    obj.location = location
    obj.rotation_euler = rotation
    obj.scale = scale
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)
    if material:
        obj.data.materials.append(material)
    apply_bevel(obj, bevel)
    return obj


def add_uv_sphere(collection, name, location, scale, material, parent=None, segments=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=(0, 0, 0))
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    obj.parent = parent
    obj.location = location
    obj.scale = scale
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)
    obj.data.materials.append(material)
    return obj


def create_mesh_object(collection, name, vertices, faces, materials, face_materials=None, parent=None):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = parent
    set_materials(obj, materials)
    if face_materials:
        for polygon, material_index in zip(mesh.polygons, face_materials):
            polygon.material_index = material_index
    return obj


def extruded_polygon(collection, name, points_xz, depth, material, parent=None, location=(0, 0, 0), bevel=0.0):
    count = len(points_xz)
    front_y = -depth * 0.5
    back_y = depth * 0.5
    vertices = [(x, front_y, z) for x, z in points_xz]
    vertices.extend((x, back_y, z) for x, z in points_xz)
    faces = [tuple(range(count)), tuple(range(count * 2 - 1, count - 1, -1))]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, next_index, next_index + count, index + count))
    obj = create_mesh_object(collection, name, vertices, faces, [material], parent=parent)
    obj.location = location
    apply_bevel(obj, bevel)
    return obj


def extruded_polygon_ring(collection, name, outer_points_xz, inner_points_xz, depth, material,
                          parent=None, location=(0, 0, 0), bevel=0.0):
    if len(outer_points_xz) != len(inner_points_xz):
        raise ValueError("Outer and inner ring outlines must contain the same number of points")

    count = len(outer_points_xz)
    front_y = -depth * 0.5
    back_y = depth * 0.5
    vertices = [(x, front_y, z) for x, z in outer_points_xz]
    vertices.extend((x, front_y, z) for x, z in inner_points_xz)
    vertices.extend((x, back_y, z) for x, z in outer_points_xz)
    vertices.extend((x, back_y, z) for x, z in inner_points_xz)

    faces = []
    for index in range(count):
        next_index = (index + 1) % count
        outer_front = index
        outer_front_next = next_index
        inner_front = count + index
        inner_front_next = count + next_index
        outer_back = count * 2 + index
        outer_back_next = count * 2 + next_index
        inner_back = count * 3 + index
        inner_back_next = count * 3 + next_index
        faces.extend([
            (outer_front, outer_front_next, inner_front_next, inner_front),
            (outer_back, inner_back, inner_back_next, outer_back_next),
            (outer_front, outer_back, outer_back_next, outer_front_next),
            (inner_front, inner_front_next, inner_back_next, inner_back),
        ])

    obj = create_mesh_object(collection, name, vertices, faces, [material], parent=parent)
    obj.location = location
    apply_bevel(obj, bevel)
    return obj


def join_objects(objects, name):
    valid = [obj for obj in objects if obj and obj.type == "MESH"]
    if not valid:
        return None
    bpy.ops.object.select_all(action="DESELECT")
    for obj in valid:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = valid[0]
    bpy.ops.object.join()
    valid[0].name = name
    valid[0].select_set(False)
    return valid[0]


def duplicate_root_with_children(source_root, collection, name, location, rotation, scale):
    duplicate_root = make_root(collection, name, location, rotation, scale)
    for child in source_root.children:
        duplicate = child.copy()
        if child.data:
            duplicate.data = child.data
        collection.objects.link(duplicate)
        duplicate.parent = duplicate_root
        duplicate.location = child.location.copy()
        duplicate.rotation_euler = child.rotation_euler.copy()
        duplicate.scale = child.scale.copy()
    return duplicate_root


def build_ground(collection, materials):
    root = make_root(collection, "PF_BlockGround")
    # Keep the existing two-metre tile size and expand the lobby to 20 x 20 tiles.
    add_cube(collection, "Ground_Block", (40.0, 40.0, 2.4), (0, 0, -1.2),
             material=materials["ground_side"], parent=root, bevel=0.12)

    tile_size = 2.0
    columns = 20
    rows = 20
    vertices = []
    faces = []
    face_materials = []
    for row in range(rows):
        for column in range(columns):
            x0 = -columns * tile_size * 0.5 + column * tile_size
            y0 = -rows * tile_size * 0.5 + row * tile_size
            start = len(vertices)
            vertices.extend([
                (x0, y0, 0.03),
                (x0 + tile_size, y0, 0.03),
                (x0 + tile_size, y0 + tile_size, 0.03),
                (x0, y0 + tile_size, 0.03),
            ])
            faces.append((start, start + 1, start + 2, start + 3))
            face_materials.append((row + column) % 2)
    tiles = create_mesh_object(collection, "Ground_CheckerTop", vertices, faces,
                               [materials["grass_a"], materials["grass_b"]], face_materials, root)
    tiles["unity_static"] = True

    rim_parts = [
        add_cube(collection, "GrassEdge_Front", (40.0, 0.42, 0.22), (0, -19.79, 0.05),
                 material=materials["grass_edge"], parent=root, bevel=0.03),
        add_cube(collection, "GrassEdge_Back", (40.0, 0.42, 0.22), (0, 19.79, 0.05),
                 material=materials["grass_edge"], parent=root, bevel=0.03),
        add_cube(collection, "GrassEdge_Left", (0.42, 39.2, 0.22), (-19.79, 0, 0.05),
                 material=materials["grass_edge"], parent=root, bevel=0.03),
        add_cube(collection, "GrassEdge_Right", (0.42, 39.2, 0.22), (19.79, 0, 0.05),
                 material=materials["grass_edge"], parent=root, bevel=0.03),
    ]
    join_objects(rim_parts, "Ground_GrassEdge")
    return root


def board_outline(width, height, chamfer):
    half = width * 0.5
    return [
        (-half, 0.0),
        (half, 0.0),
        (half, height - chamfer),
        (half - chamfer, height),
        (-half + chamfer, height),
        (-half, height - chamfer),
    ]


def build_board_source(collection, materials):
    root = make_root(collection, "PF_LeaderboardBoard_Source")
    total_height = 8.0
    width = 4.5
    depth = 0.68
    frame = 0.46
    base_height = 0.5
    board_bottom = base_height * 1.35
    board_height = total_height - board_bottom
    chamfer = min(width * 0.16, board_height * 0.12)

    outer_outline = board_outline(width, board_height, chamfer)
    opening_width = width - frame * 2.0
    opening_height = board_height - frame * 2.0
    opening_chamfer = max(0.12, chamfer - frame * 0.58)
    opening_outline = [
        (x, z + frame)
        for x, z in board_outline(opening_width, opening_height, opening_chamfer)
    ]
    extruded_polygon_ring(collection, "Frame", outer_outline, opening_outline, depth,
                          materials["board_frame"], root, (0, 0, board_bottom), 0.04)

    # Extend the panel under the frame and recess its face slightly. The overlap
    # prevents sub-pixel cracks at the six angled border joints from any view distance.
    panel_overlap = 0.075
    panel_width = opening_width + panel_overlap * 2.0
    panel_height = opening_height + panel_overlap * 2.0
    panel_depth = 0.12
    panel_chamfer = opening_chamfer + panel_overlap * 0.58
    panel_y = -depth * 0.5 + panel_depth * 0.5 + 0.018
    panel = extruded_polygon(collection, "FrontPanel", board_outline(panel_width, panel_height, panel_chamfer),
                            panel_depth, materials["board_panel"], root,
                            (0, panel_y, board_bottom + frame - panel_overlap), 0.018)
    panel["ui_surface"] = True

    add_cube(collection, "BaseLower", (width + 1.25, depth + 1.05, base_height * 0.56),
             (0, 0, base_height * 0.28), material=materials["board_base"], parent=root, bevel=0.06)
    add_cube(collection, "BaseUpper", (width + 0.55, depth + 0.55, base_height * 0.52),
             (0, 0, base_height * 0.82), material=materials["board_frame"], parent=root, bevel=0.05)

    ui_anchor = bpy.data.objects.new("UIAnchor", None)
    collection.objects.link(ui_anchor)
    ui_anchor.parent = root
    ui_anchor.location = (0, -depth * 0.5 - 0.08, board_bottom + board_height * 0.5)
    title_anchor = bpy.data.objects.new("TitleAnchor", None)
    collection.objects.link(title_anchor)
    title_anchor.parent = root
    title_anchor.location = (0, -depth * 0.5 - 0.09, total_height - frame * 2.5)
    return root


def build_dirt_patch(collection, materials):
    root = make_root(collection, "PF_ArenaDirtPatch", (-2.8, -2.2, 0))
    count = 24
    radius = 5.4
    outer = []
    inner = []
    for index in range(count):
        angle = index * math.tau / count
        point = (math.cos(angle) * radius, math.sin(angle) * radius)
        outer.append(point)
        inner.append((point[0] * 0.915, point[1] * 0.915))

    vertices = [(0, 0, 0.055)]
    vertices.extend((x, y, 0.055) for x, y in inner)
    vertices.extend((x, y, 0.057) for x, y in outer)
    faces = []
    face_materials = []
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((0, index + 1, next_index + 1))
        face_materials.append(0)
        inner_a = index + 1
        inner_b = next_index + 1
        outer_a = 1 + count + index
        outer_b = 1 + count + next_index
        faces.append((inner_a, outer_a, outer_b, inner_b))
        face_materials.append(1)
    create_mesh_object(collection, "DirtPatch", vertices, faces,
                       [materials["dirt"], materials["dirt_rim"]], face_materials, root)
    return root


def build_crate_source(collection, materials):
    root = make_root(collection, "PF_WoodCrate_Source")
    size = 1.15
    add_cube(collection, "WoodBody", (size, size, size), (0, 0, size * 0.5),
             material=materials["wood"], parent=root, bevel=0.025)
    frame_parts = []
    rail = 0.13
    brace = 0.085
    face_y = -size * 0.5 - 0.022
    center_z = size * 0.5
    inner = size - rail * 2.0
    diagonal = math.sqrt(inner * inner * 2.0)
    frame_parts.extend([
        add_cube(collection, "Crate_LeftRail", (rail, rail * 0.58, size),
                 (-size * 0.5 + rail * 0.5, face_y, center_z), material=materials["wood_frame"], parent=root),
        add_cube(collection, "Crate_RightRail", (rail, rail * 0.58, size),
                 (size * 0.5 - rail * 0.5, face_y, center_z), material=materials["wood_frame"], parent=root),
        add_cube(collection, "Crate_TopRail", (inner, rail * 0.58, rail),
                 (0, face_y, size - rail * 0.5), material=materials["wood_frame"], parent=root),
        add_cube(collection, "Crate_BottomRail", (inner, rail * 0.58, rail),
                 (0, face_y, rail * 0.5), material=materials["wood_frame"], parent=root),
        add_cube(collection, "Crate_BraceA", (diagonal, brace, brace),
                 (0, face_y - 0.008, center_z), (0, math.radians(45), 0), materials["wood_frame"], root),
        add_cube(collection, "Crate_BraceB", (diagonal, brace, brace),
                 (0, face_y - 0.012, center_z), (0, math.radians(-45), 0), materials["wood_frame"], root),
        add_cube(collection, "Crate_TopBraceA", (diagonal, brace, brace),
                 (0, 0, size + 0.022), (0, 0, math.radians(45)), materials["wood_frame"], root),
        add_cube(collection, "Crate_TopBraceB", (diagonal, brace, brace),
                 (0, 0, size + 0.03), (0, 0, math.radians(-45)), materials["wood_frame"], root),
    ])
    joined = join_objects(frame_parts, "WoodFrame")
    joined.parent = root
    return root


def build_like_hand(collection, parent, material, center=(0, 0, 0)):
    cx, cy, cz = center
    pieces = []
    pieces.append(add_cube(collection, "Hand_Palm", (1.15, 0.62, 1.0), (cx, cy, cz),
                           material=material, parent=parent, bevel=0.18))
    finger_z = [0.34, 0.10, -0.14, -0.37]
    finger_lengths = [0.85, 0.92, 0.82, 0.70]
    for index, (offset_z, length) in enumerate(zip(finger_z, finger_lengths)):
        pieces.append(add_cube(collection, f"Hand_Finger_{index + 1}", (length, 0.58, 0.22),
                               (cx + 0.78, cy, cz + offset_z), material=material, parent=parent, bevel=0.11))
    pieces.append(add_cube(collection, "Hand_Thumb", (0.34, 0.62, 1.15),
                           (cx - 0.48, cy, cz + 0.75), (0, math.radians(-17), 0), material, parent, 0.15))
    hand = join_objects(pieces, "LikeHand")
    hand.parent = parent
    return hand


def build_like_pillar(collection, materials):
    root = make_root(collection, "PF_LikePillar", (7.55, 15.15, 0), (0, 0, math.radians(-6)), (0.90, 0.90, 0.90))
    add_cube(collection, "BasePlatform", (3.9, 2.15, 0.42), (0, 0, 0.21),
             material=materials["pillar_dark"], parent=root, bevel=0.12)
    add_cube(collection, "BaseUpper", (3.45, 1.8, 0.32), (0, 0, 0.53),
             material=materials["pillar_frame"], parent=root, bevel=0.08)
    add_cube(collection, "TextPanelFrame", (3.25, 0.34, 1.15), (0, -0.92, 0.86),
             material=materials["pillar_frame"], parent=root, bevel=0.08)
    panel = add_cube(collection, "TextPanelSurface", (2.82, 0.09, 0.76), (0, -1.105, 0.86),
                     material=materials["pillar_panel"], parent=root, bevel=0.035)
    panel["ui_surface"] = True

    body_z = 1.0 + 5.3 * 0.5
    add_cube(collection, "PillarRoot", (1.35, 0.85, 5.3), (0, 0, body_z),
             material=materials["pillar_frame"], parent=root, bevel=0.09)
    inset_parts = []
    green_rails = []
    segment_height = 5.3 / 5.0
    for index in range(5):
        segment_center = 1.0 + segment_height * (index + 0.5)
        if index == 0:
            add_cube(collection, "GlowingBlockPlaceholder", (0.94, 0.12, segment_height * 0.68),
                     (0, -0.49, segment_center), material=materials["green"], parent=root, bevel=0.035)
        else:
            inset_parts.append(add_cube(collection, f"PillarSegment_{index + 1:02d}",
                                        (0.94, 0.08, segment_height * 0.66), (0, -0.47, segment_center),
                                        material=materials["pillar_inner"], parent=root, bevel=0.025))
        if index < 4:
            green_rails.append(add_cube(collection, f"PillarDivider_{index + 1:02d}",
                                        (1.02, 0.10, 0.055), (0, -0.49, 1.0 + segment_height * (index + 1)),
                                        material=materials["green_dark"], parent=root, bevel=0.018))
    join_objects(inset_parts, "PillarSegments")
    join_objects(green_rails, "PillarDividers")
    add_cube(collection, "TopCap", (1.95, 1.35, 0.32), (0, 0, 6.53),
             material=materials["pillar_frame"], parent=root, bevel=0.09)
    build_like_hand(collection, root, materials["yellow"], (0, -0.05, 7.42))
    return root


def ring_mesh(collection, name, outer_radius, inner_radius, depth, segments, materials, parent, center_z):
    vertices = []
    faces = []
    face_materials = []
    for index in range(segments):
        angle = index * math.tau / segments
        x_out = math.sin(angle) * outer_radius
        z_out = math.cos(angle) * outer_radius + center_z
        x_in = math.sin(angle) * inner_radius
        z_in = math.cos(angle) * inner_radius + center_z
        vertices.extend([
            (x_out, -depth * 0.5, z_out),
            (x_in, -depth * 0.5, z_in),
            (x_out, depth * 0.5, z_out),
            (x_in, depth * 0.5, z_in),
        ])
    for index in range(segments):
        next_index = (index + 1) % segments
        a = index * 4
        b = next_index * 4
        faces.extend([
            (a, b, b + 1, a + 1),
            (a + 2, a + 3, b + 3, b + 2),
            (a, a + 2, b + 2, b),
            (a + 1, b + 1, b + 3, a + 3),
        ])
        material_index = index % len(materials)
        face_materials.extend([material_index] * 4)
    return create_mesh_object(collection, name, vertices, faces, materials, face_materials, parent)


def wheel_segment_mesh(collection, parent, center_z, count, inner_radius, outer_radius, depth, materials):
    vertices = []
    faces = []
    face_materials = []
    gap = math.radians(1.4)
    for index in range(count):
        angle_a = index * math.tau / count + gap
        angle_b = (index + 1) * math.tau / count - gap
        quad = [
            (math.sin(angle_a) * inner_radius, math.cos(angle_a) * inner_radius + center_z),
            (math.sin(angle_a) * outer_radius, math.cos(angle_a) * outer_radius + center_z),
            (math.sin(angle_b) * outer_radius, math.cos(angle_b) * outer_radius + center_z),
            (math.sin(angle_b) * inner_radius, math.cos(angle_b) * inner_radius + center_z),
        ]
        start = len(vertices)
        vertices.extend((x, -depth * 0.5, z) for x, z in quad)
        vertices.extend((x, depth * 0.5, z) for x, z in quad)
        faces.extend([
            (start, start + 1, start + 2, start + 3),
            (start + 7, start + 6, start + 5, start + 4),
            (start, start + 4, start + 5, start + 1),
            (start + 1, start + 5, start + 6, start + 2),
            (start + 2, start + 6, start + 7, start + 3),
            (start + 3, start + 7, start + 4, start),
        ])
        face_materials.extend([index % len(materials)] * 6)
    return create_mesh_object(collection, "WheelSegments", vertices, faces, materials, face_materials, parent)


def build_alien_wheel(collection, materials):
    root = make_root(collection, "PF_AlienWheel", (14.2, 13.0, 0), (0, 0, math.radians(5)), (0.92, 0.92, 0.92))
    center_z = 4.55
    wheel_pivot = make_root(collection, "WheelPivot", (0, 0, center_z))
    wheel_pivot.parent = root
    wheel_pivot["spin_axis_blender"] = "LOCAL_Y"
    wheel_pivot["spin_axis_unity_after_fbx_axis_conversion"] = "LOCAL_Z"
    add_cube(collection, "StandBase", (5.8, 2.2, 0.48), (0, 0.35, 0.24),
             material=materials["alien_dark"], parent=root, bevel=0.15)

    left_points = [(-2.55, 0.38), (-1.60, 0.38), (-1.50, 3.45), (-2.05, 3.15), (-2.45, 1.65), (-2.95, 0.62)]
    right_points = [(-x, z) for x, z in reversed(left_points)]
    extruded_polygon(collection, "Support_Left", left_points, 1.05, materials["alien_purple_dark"], root, bevel=0.10)
    extruded_polygon(collection, "Support_Right", right_points, 1.05, materials["alien_purple_dark"], root, bevel=0.10)

    ring_mesh(collection, "WheelOuterRim", 2.82, 2.31, 0.72, 20,
              [materials["alien_purple"], materials["alien_purple_dark"]], wheel_pivot, 0.0)
    wheel_segment_mesh(collection, wheel_pivot, 0.0, 10, 0.52, 2.28, 0.42,
                       [materials["alien_green_dark"], materials["alien_navy"]])

    divider_parts = []
    for index in range(10):
        angle = index * math.tau / 10
        radius = 1.39
        divider_parts.append(add_cube(
            collection,
            f"WheelDivider_{index + 1:02d}",
            (0.075, 0.54, 1.76),
            (math.sin(angle) * radius, -0.02, math.cos(angle) * radius),
            (0, angle, 0),
            materials["green"],
            wheel_pivot,
            0.018,
        ))
    join_objects(divider_parts, "WheelDividers")

    block_parts = []
    for index in range(14):
        angle = index * math.tau / 14
        radius = 3.02
        block_parts.append(add_cube(
            collection,
            f"RimBlock_{index + 1:02d}",
            (0.62, 0.88, 0.38),
            (math.sin(angle) * radius, 0, math.cos(angle) * radius),
            (0, angle, 0),
            materials["alien_purple"] if index % 2 == 0 else materials["alien_purple_dark"],
            wheel_pivot,
            0.05,
        ))
    join_objects(block_parts, "DecorativeBlocks")

    add_cylinder(collection, "CenterHub", 0.58, 0.82, 12, (0, -0.02, 0),
                 (math.radians(90), 0, 0), materials["green"], wheel_pivot, bevel=0.035)

    pointer_points = [(-0.25, 0), (0.25, 0), (0.42, 0.55), (0, 0.92), (-0.42, 0.55)]
    extruded_polygon(collection, "Pointer", pointer_points, 0.42, materials["green"], root,
                     (0, -0.50, center_z + 2.78), 0.035)

    add_cube(collection, "SpinButtonBase", (2.05, 1.75, 0.68), (0, -2.05, 0.50),
             material=materials["alien_dark"], parent=root, bevel=0.16)
    add_cube(collection, "SpinButtonTop", (1.48, 1.05, 0.30), (0, -2.31, 0.91),
             material=materials["green"], parent=root, bevel=0.10)

    add_cylinder(collection, "UFO_LowerDisc", 2.05, 0.28, 16, (0, 0, 8.25),
                 material=materials["alien_dark"], parent=root, scale=(1.0, 1.0, 0.45), bevel=0.04)
    add_cylinder(collection, "UFO_PurpleRing", 1.72, 0.30, 16, (0, 0, 8.48),
                 material=materials["alien_purple"], parent=root, scale=(1.0, 1.0, 0.42), bevel=0.04)
    add_uv_sphere(collection, "UFO_Dome", (0, 0, 8.64), (1.22, 1.22, 0.36),
                  materials["alien_purple_light"], root, 16, 8)
    add_uv_sphere(collection, "AlienBeacon", (0, 0, 7.73), (0.19, 0.19, 0.19), materials["green"], root, 10, 6)
    return root


def flat_ring_mesh(collection, name, outer_radius, inner_radius, depth, segments, materials, parent,
                   location=(0, 0, 0)):
    vertices = []
    faces = []
    face_materials = []
    for index in range(segments):
        angle = index * math.tau / segments
        cos_angle = math.cos(angle)
        sin_angle = math.sin(angle)
        vertices.extend([
            (cos_angle * outer_radius, sin_angle * outer_radius, -depth * 0.5),
            (cos_angle * inner_radius, sin_angle * inner_radius, -depth * 0.5),
            (cos_angle * outer_radius, sin_angle * outer_radius, depth * 0.5),
            (cos_angle * inner_radius, sin_angle * inner_radius, depth * 0.5),
        ])
    for index in range(segments):
        next_index = (index + 1) % segments
        a = index * 4
        b = next_index * 4
        faces.extend([
            (a + 2, b + 2, b + 3, a + 3),
            (a + 1, b + 1, b, a),
            (a, b, b + 2, a + 2),
            (a + 3, b + 3, b + 1, a + 1),
        ])
        material_index = index % len(materials)
        face_materials.extend([material_index] * 4)
    ring = create_mesh_object(collection, name, vertices, faces, materials, face_materials, parent)
    ring.location = location
    return ring


def diamond_mesh(collection, name, location, radius, height, material, parent):
    half_height = height * 0.5
    vertices = [
        (0, 0, half_height),
        (radius, 0, 0),
        (0, radius, 0),
        (-radius, 0, 0),
        (0, -radius, 0),
        (0, 0, -half_height),
    ]
    faces = [
        (0, 1, 2), (0, 2, 3), (0, 3, 4), (0, 4, 1),
        (5, 2, 1), (5, 3, 2), (5, 4, 3), (5, 1, 4),
    ]
    diamond = create_mesh_object(collection, name, vertices, faces, [material], parent=parent)
    diamond.location = location
    return diamond


def build_alien_event_portal(collection, materials):
    root = make_root(collection, "PF_AlienEventPortal", (10.2, -1.0, 0),
                     (0, 0, math.radians(-4)))
    root["unity_static_except_beam"] = True

    pad_root = make_root(collection, "PortalPad")
    pad_root.parent = root
    add_cylinder(collection, "PortalPad_Base", 4.35, 0.24, 20, (0, 0, 0.12),
                 material=materials["event_dark"], parent=pad_root, bevel=0.06)
    flat_ring_mesh(collection, "PortalPad_OuterRing", 4.08, 3.12, 0.22, 20,
                   [materials["event_silver"], materials["event_blue"]], pad_root, (0, 0, 0.29))
    add_cylinder(collection, "PortalPad_InnerDisc", 3.02, 0.20, 20, (0, 0, 0.31),
                 material=materials["event_green"], parent=pad_root, bevel=0.04)
    flat_ring_mesh(collection, "PortalPad_GlowRing", 3.14, 2.78, 0.08, 20,
                   [materials["crystal_green"]], pad_root, (0, 0, 0.45))

    glyph_parts = []
    for index in range(16):
        angle = index * math.tau / 16
        glyph_parts.append(add_cube(
            collection,
            f"PortalGlyph_{index + 1:02d}",
            (0.50, 0.16, 0.07),
            (math.cos(angle) * 3.60, math.sin(angle) * 3.60, 0.46),
            (0, 0, angle + math.radians(90)),
            materials["crystal_green"],
            pad_root,
            0.025,
        ))
    glyphs = join_objects(glyph_parts, "PortalGlyphs")
    glyphs.parent = pad_root

    pedestal_parts = []
    pedestal_caps = []
    crystals = []
    pedestal_radius = 5.05
    for index, angle_degrees in enumerate((45, 135, 225, 315)):
        angle = math.radians(angle_degrees)
        x = math.cos(angle) * pedestal_radius
        y = math.sin(angle) * pedestal_radius
        pedestal_parts.extend([
            add_cube(collection, f"CrystalPedestal_{index + 1}_Foot", (1.65, 1.65, 0.24),
                     (x, y, 0.12), material=materials["event_dark"], parent=root, bevel=0.07),
            add_cube(collection, f"CrystalPedestal_{index + 1}_Body", (1.25, 1.25, 1.55),
                     (x, y, 0.92), material=materials["event_dark"], parent=root, bevel=0.11),
        ])
        pedestal_caps.append(add_cube(
            collection,
            f"CrystalPedestal_{index + 1}_Cap",
            (1.04, 1.04, 0.16),
            (x, y, 1.70),
            material=materials["event_blue"],
            parent=root,
            bevel=0.05,
        ))
        crystals.append(diamond_mesh(
            collection,
            f"PortalCrystal_{index + 1}",
            (x, y, 2.55),
            0.66,
            1.65,
            materials["crystal_green"],
            root,
        ))
    joined_pedestals = join_objects(pedestal_parts, "CrystalPedestals")
    joined_pedestals.parent = root
    joined_caps = join_objects(pedestal_caps, "CrystalPedestalCaps")
    joined_caps.parent = root
    joined_crystals = join_objects(crystals, "PortalCrystals")
    joined_crystals.parent = root

    beam = add_cylinder(collection, "BeamPlaceholder", 3.02, 8.15, 20, (0, 0, 4.48),
                        material=materials["event_beam"], parent=root)
    beam["replace_with_vfx_in_unity"] = True

    ufo_root = make_root(collection, "EventUFO", (0, 0, 9.15))
    ufo_root.parent = root
    body_parts = [
        add_cylinder(collection, "UFO_BottomPlate", 4.45, 0.42, 20, (0, 0, -0.34),
                     material=materials["event_silver_dark"], parent=ufo_root, bevel=0.07),
        add_cylinder(collection, "UFO_OuterHull", 5.25, 0.32, 20, (0, 0, 0.02),
                     material=materials["event_silver"], parent=ufo_root, bevel=0.08),
        add_cylinder(collection, "UFO_UpperHull", 4.72, 0.46, 20, (0, 0, 0.30),
                     material=materials["event_cream"], parent=ufo_root, bevel=0.09),
        add_cylinder(collection, "UFO_BlueTrim", 5.08, 0.14, 20, (0, 0, 0.18),
                     material=materials["event_blue"], parent=ufo_root, bevel=0.035),
    ]
    joined_body = join_objects(body_parts, "EventUFO_Body")
    joined_body.parent = ufo_root
    add_cylinder(collection, "UFO_UnderGlow", 2.40, 0.18, 20, (0, 0, -0.62),
                 material=materials["event_green"], parent=ufo_root, bevel=0.035)
    add_uv_sphere(collection, "UFO_Dome", (0, 0, 1.08), (2.55, 2.55, 1.12),
                  materials["event_cream"], ufo_root, 20, 8)

    accent_parts = []
    for index in range(10):
        angle = index * math.tau / 10
        accent_parts.append(add_cube(
            collection,
            f"UFO_Accent_{index + 1:02d}",
            (0.14, 0.42, 0.46),
            (math.cos(angle) * 3.25, math.sin(angle) * 3.25, 0.70),
            (0, 0, angle),
            materials["event_blue"],
            ufo_root,
            0.035,
        ))
    accents = join_objects(accent_parts, "EventUFO_Accents")
    accents.parent = ufo_root

    ui_anchor = bpy.data.objects.new("EventUIAnchor", None)
    collection.objects.link(ui_anchor)
    ui_anchor.parent = root
    ui_anchor.location = (0, -3.35, 4.35)
    reward_anchor = bpy.data.objects.new("RewardAnchor", None)
    collection.objects.link(reward_anchor)
    reward_anchor.parent = root
    reward_anchor.location = (0, 0, 0.70)
    return root


def build_egg_pedestal(collection, materials):
    root = make_root(collection, "PF_AlienEggPedestal", (-13.4, -5.6, 0),
                     (0, 0, math.radians(8)))
    add_cylinder(collection, "EggPedestal_Base", 2.15, 0.46, 20, (0, 0, 0.23),
                 material=materials["event_dark"], parent=root, bevel=0.09)
    flat_ring_mesh(collection, "EggPedestal_GlowRing", 1.93, 1.56, 0.16, 20,
                   [materials["event_green"]], root, (0, 0, 0.52))
    add_cylinder(collection, "EggPedestal_Top", 1.55, 0.20, 20, (0, 0, 0.59),
                 material=materials["egg_top"], parent=root, bevel=0.055)

    egg_anchor = bpy.data.objects.new("EggAnchor", None)
    collection.objects.link(egg_anchor)
    egg_anchor.parent = root
    egg_anchor.location = (0, 0, 0.88)
    ui_anchor = bpy.data.objects.new("EggUIAnchor", None)
    collection.objects.link(ui_anchor)
    ui_anchor.parent = root
    ui_anchor.location = (0, -1.65, 1.65)
    root["egg_model_intentionally_omitted"] = True
    return root


def build_player_capsule(collection, name, location, material, rotation_z=0.0):
    root = make_root(collection, name, location, (0, 0, rotation_z))
    capsule = add_uv_sphere(collection, name + "_Capsule", (0, 0, 1.02), (0.58, 0.58, 1.02),
                            material, root, 12, 8)
    capsule["editor_reference_only"] = True
    root["editor_reference_only"] = True
    return root


def add_lighting_and_camera(collection):
    bpy.ops.object.light_add(type="AREA", location=(0, -4, 18))
    key = bpy.context.object
    key.name = "Key_Area"
    move_to_collection(key, collection)
    key.data.energy = 1800
    key.data.shape = "DISK"
    key.data.size = 12
    key.rotation_euler = (math.radians(12), 0, 0)

    bpy.ops.object.light_add(type="SUN", location=(-8, -10, 14))
    sun = bpy.context.object
    sun.name = "Sun"
    move_to_collection(sun, collection)
    sun.data.energy = 2.0
    sun.rotation_euler = (math.radians(28), math.radians(-18), math.radians(-28))

    bpy.ops.object.camera_add(location=(0, -29, 15.5))
    camera = bpy.context.object
    camera.name = "Camera_Preview"
    move_to_collection(camera, collection)
    camera.data.lens = 48
    camera.data.sensor_width = 36
    bpy.context.scene.camera = camera
    return camera


def aim_camera(camera, location, target):
    camera.location = location
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_preview(camera, filename, location, target):
    aim_camera(camera, location, target)
    bpy.context.scene.render.filepath = os.path.join(PREVIEW_DIR, filename)
    bpy.ops.render.render(write_still=True)


def configure_scene():
    scene = bpy.context.scene
    # Blender 5.x exposes Eevee as BLENDER_EEVEE. Keep the fallback so this
    # source file can also be regenerated from older Blender 4.x installs.
    engines = {item.identifier for item in scene.bl_rna.properties["render"].fixed_type.properties["engine"].enum_items}
    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in engines else "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.world.color = (0.18, 0.48, 0.78)
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    if background:
        background.inputs["Color"].default_value = (0.16, 0.48, 0.78, 1.0)
        background.inputs["Strength"].default_value = 0.65
    try:
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        scene.view_settings.look = "Medium High Contrast"
    scene.render.resolution_percentage = 100


def main():
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    reset_scene()

    materials = {
        "ground_side": make_material("MAT_GroundSide", (0.045, 0.055, 0.075), roughness=0.72),
        "grass_a": make_material("MAT_GrassA", (0.24, 0.56, 0.075), roughness=0.76),
        "grass_b": make_material("MAT_GrassB", (0.31, 0.66, 0.095), roughness=0.76),
        "grass_edge": make_material("MAT_GrassEdge", (0.07, 0.30, 0.07), roughness=0.72),
        "board_frame": make_material("MAT_BoardFrame", (0.32, 0.36, 0.35), metallic=0.04, roughness=0.42),
        "board_panel": make_material("MAT_BoardPanel", (0.055, 0.19, 0.52), roughness=0.38),
        "board_base": make_material("MAT_BoardBase", (0.11, 0.14, 0.16), roughness=0.48),
        "dirt": make_material("MAT_Dirt", (0.48, 0.29, 0.09), roughness=0.82),
        "dirt_rim": make_material("MAT_DirtRim", (0.72, 0.55, 0.22), roughness=0.72),
        "wood": make_material("MAT_Wood", (0.68, 0.36, 0.12), roughness=0.72),
        "wood_frame": make_material("MAT_WoodFrame", (0.25, 0.105, 0.03), roughness=0.68),
        "pillar_dark": make_material("MAT_PillarDark", (0.025, 0.06, 0.12), metallic=0.08, roughness=0.34),
        "pillar_frame": make_material("MAT_PillarFrame", (0.035, 0.19, 0.40), metallic=0.12, roughness=0.32),
        "pillar_panel": make_material("MAT_PillarPanel", (0.08, 0.32, 0.58), roughness=0.35),
        "pillar_inner": make_material("MAT_PillarInner", (0.012, 0.035, 0.065), roughness=0.30),
        "green": make_material("MAT_GreenPlaceholder", (0.05, 0.95, 0.18), metallic=0.04, roughness=0.25),
        "green_dark": make_material("MAT_GreenDark", (0.02, 0.42, 0.12), roughness=0.34),
        "yellow": make_material("MAT_YellowLike", (1.0, 0.58, 0.04), roughness=0.36),
        "alien_dark": make_material("MAT_AlienDark", (0.025, 0.04, 0.095), metallic=0.12, roughness=0.30),
        "alien_navy": make_material("MAT_AlienNavy", (0.018, 0.09, 0.12), roughness=0.30),
        "alien_purple": make_material("MAT_AlienPurple", (0.62, 0.04, 0.88), metallic=0.05, roughness=0.30),
        "alien_purple_dark": make_material("MAT_AlienPurpleDark", (0.24, 0.02, 0.42), roughness=0.34),
        "alien_purple_light": make_material("MAT_AlienPurpleLight", (0.88, 0.20, 0.98), roughness=0.28),
        "alien_green_dark": make_material("MAT_AlienGreenDark", (0.02, 0.25, 0.12), roughness=0.34),
        "event_dark": make_material("MAT_EventDark", (0.025, 0.055, 0.075), metallic=0.16, roughness=0.30),
        "event_silver_dark": make_material("MAT_EventSilverDark", (0.24, 0.31, 0.34), metallic=0.24, roughness=0.32),
        "event_silver": make_material("MAT_EventSilver", (0.68, 0.70, 0.62), metallic=0.15, roughness=0.33),
        "event_cream": make_material("MAT_EventCream", (0.83, 0.80, 0.65), metallic=0.05, roughness=0.42),
        "event_blue": make_material("MAT_EventBlueTrim", (0.025, 0.30, 0.82), metallic=0.12, roughness=0.27),
        "event_green": make_material("MAT_EventGreen", (0.05, 0.92, 0.25), roughness=0.24, emission_strength=0.65),
        "event_beam": make_material("MAT_EventBeamPlaceholder", (0.20, 1.0, 0.50), roughness=0.18,
                                    emission_strength=0.35, alpha=0.17),
        "crystal_green": make_material("MAT_CrystalGreen", (0.62, 1.0, 0.12), roughness=0.20,
                                       emission_strength=1.15),
        "egg_top": make_material("MAT_EggPedestalTop", (0.035, 0.095, 0.055), roughness=0.82),
        "player_blue": make_material("MAT_PlayerBlue", (0.08, 0.48, 0.96), roughness=0.36),
        "player_orange": make_material("MAT_PlayerOrange", (1.0, 0.30, 0.035), roughness=0.36),
    }

    ground_collection = make_collection("COL_BlockGround")
    board_collection = make_collection("COL_Leaderboards")
    dirt_collection = make_collection("COL_ArenaDirt")
    crate_collection = make_collection("COL_WoodCrates")
    pillar_collection = make_collection("COL_LikePillar")
    wheel_collection = make_collection("COL_AlienWheel")
    event_collection = make_collection("COL_AlienEventPortal")
    egg_collection = make_collection("COL_AlienEggPedestal")
    player_collection = make_collection("COL_PlayerReferences")
    presentation_collection = make_collection("COL_Presentation")

    build_ground(ground_collection, materials)
    build_dirt_patch(dirt_collection, materials)

    board_source = build_board_source(board_collection, materials)
    board_source.name = "PF_Leaderboard_FriendsSmall"
    board_source.location = (-15.85, 13.50, 0.05)
    board_source.rotation_euler.z = math.radians(-13)
    board_source.scale = (0.74, 0.74, 0.74)
    duplicate_root_with_children(board_source, board_collection, "PF_Leaderboard_MostKOs",
                                 (-10.85, 15.30, 0.05), (0, 0, math.radians(-8)), (1.02, 1.02, 1.02))
    duplicate_root_with_children(board_source, board_collection, "PF_Leaderboard_MostWins",
                                 (-5.20, 16.10, 0.05), (0, 0, 0), (1.06, 1.06, 1.06))
    duplicate_root_with_children(board_source, board_collection, "PF_Leaderboard_WinStreak",
                                 (0.55, 15.45, 0.05), (0, 0, math.radians(8)), (1.02, 1.02, 1.02))

    crate_source = build_crate_source(crate_collection, materials)
    crate_source.name = "PF_WoodCrate_Left01"
    crate_source.location = (-17.35, 11.15, 0.05)
    crate_source.rotation_euler.z = math.radians(12)
    duplicate_root_with_children(crate_source, crate_collection, "PF_WoodCrate_Left02",
                                 (-16.10, 11.30, 0.05), (0, 0, math.radians(-8)), (0.96, 0.96, 0.96))
    duplicate_root_with_children(crate_source, crate_collection, "PF_WoodCrate_Left03_Stacked",
                                 (-16.78, 11.28, 1.18), (0, 0, math.radians(7)), (0.94, 0.94, 0.94))
    duplicate_root_with_children(crate_source, crate_collection, "PF_WoodCrate_Left04_Back",
                                 (-17.48, 12.42, 0.05), (0, 0, math.radians(-14)), (0.90, 0.90, 0.90))
    duplicate_root_with_children(crate_source, crate_collection, "PF_WoodCrate_Right01",
                                 (10.55, 11.45, 0.05), (0, 0, math.radians(18)), (0.92, 0.92, 0.92))

    build_like_pillar(pillar_collection, materials)
    build_alien_wheel(wheel_collection, materials)
    build_alien_event_portal(event_collection, materials)
    build_egg_pedestal(egg_collection, materials)

    player_data = [
        ("PlayerPreview_01", (-6.0, -1.5, 0.06), materials["player_blue"], 22),
        ("PlayerPreview_02", (-3.8, -0.2, 0.06), materials["player_orange"], -18),
        ("PlayerPreview_03", (-1.2, -0.8, 0.06), materials["player_blue"], 36),
        ("PlayerPreview_04", (0.3, -3.1, 0.06), materials["player_orange"], -32),
        ("PlayerPreview_05", (-5.1, -4.5, 0.06), materials["player_orange"], 10),
        ("PlayerPreview_06", (-2.2, -4.9, 0.06), materials["player_blue"], -12),
    ]
    for name, location, material, yaw in player_data:
        build_player_capsule(player_collection, name, location, material, math.radians(yaw))

    camera = add_lighting_and_camera(presentation_collection)
    configure_scene()

    bpy.context.scene["workflow"] = "Low-poly source models for Unity export"
    bpy.context.scene["units_note"] = "1 Blender unit = 1 Unity meter"
    bpy.context.scene["player_references_editor_only"] = True

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    render_preview(camera, "PawArena_Lobby_Overview.png", (0, -48, 25.0), (0, 3.0, 4.4))
    render_preview(camera, "PawArena_Leaderboards.png", (-11.0, -9.0, 11.0), (-6.0, 15.2, 4.5))
    event_collection.hide_render = True
    render_preview(camera, "PawArena_AlienWheel_LikePillar.png", (13.0, -12.0, 10.5), (10.8, 13.6, 4.4))
    event_collection.hide_render = False
    render_preview(camera, "PawArena_AlienEventPortal.png", (11.8, -32.0, 16.0), (10.2, -0.6, 5.2))
    render_preview(camera, "PawArena_AlienEggPedestal.png", (-15.0, -13.0, 5.2), (-13.4, -5.6, 0.9))
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("PAW_ARENA_BLEND=" + BLEND_PATH)
    print("PAW_ARENA_PREVIEWS=" + PREVIEW_DIR)


if __name__ == "__main__":
    main()
