import bpy
import os


PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BLEND_PATH = os.path.join(PROJECT_ROOT, "Blender", "PawArena_Lobby.blend")
EXPORT_DIR = os.path.join(
    PROJECT_ROOT,
    "Assets",
    "_Game",
    "Models",
    "Environment",
    "BlenderExports",
)

EXPORTS = (
    ("PF_BlockGround", "PF_BlockGround", "PF_BlockGround.fbx"),
    ("PF_Leaderboard_MostWins", "PF_LeaderboardBoard", "PF_LeaderboardBoard.fbx"),
    ("PF_WoodCrate_Left01", "PF_WoodCrate", "PF_WoodCrate.fbx"),
    ("PF_LikePillar", "PF_LikePillar", "PF_LikePillar.fbx"),
    ("PF_AlienWheel", "PF_AlienWheel", "PF_AlienWheel.fbx"),
    ("PF_ArenaDirtPatch", "PF_ArenaDirtPatch", "PF_ArenaDirtPatch.fbx"),
    ("PF_AlienEventPortal", "PF_AlienEventPortal", "PF_AlienEventPortal.fbx"),
    ("PF_AlienEggPedestal", "PF_AlienEggPedestal", "PF_AlienEggPedestal.fbx"),
    ("PlayerPreview_01", "PF_PlayerCapsuleReference", "PF_PlayerCapsuleReference.fbx"),
)


def hierarchy(root):
    result = [root]
    for child in root.children:
        result.extend(hierarchy(child))
    return result


def export_root(source_name, export_name, filename):
    root = bpy.data.objects.get(source_name)
    if root is None:
        raise RuntimeError(f"Missing Blender root: {source_name}")

    original_name = root.name
    original_location = root.location.copy()
    original_rotation = root.rotation_euler.copy()
    original_scale = root.scale.copy()

    root.name = export_name
    root.location = (0, 0, 0)
    root.rotation_euler = (0, 0, 0)
    root.scale = (1, 1, 1)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in hierarchy(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root

    filepath = os.path.join(EXPORT_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        use_mesh_modifiers=True,
        use_triangles=True,
        add_leaf_bones=False,
        bake_anim=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        # Keep the FBX hierarchy intact. Unity's generated prefab flattens the
        # imported root correction without changing the relative child transforms.
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="AUTO",
        embed_textures=False,
    )

    root.name = original_name
    root.location = original_location
    root.rotation_euler = original_rotation
    root.scale = original_scale
    print("UNITY_FBX=" + filepath)


def main():
    os.makedirs(EXPORT_DIR, exist_ok=True)
    if os.path.normcase(bpy.data.filepath) != os.path.normcase(BLEND_PATH):
        bpy.ops.wm.open_mainfile(filepath=BLEND_PATH)

    for source_name, export_name, filename in EXPORTS:
        export_root(source_name, export_name, filename)

    print("UNITY_EXPORT_COMPLETE=" + EXPORT_DIR)


if __name__ == "__main__":
    main()
