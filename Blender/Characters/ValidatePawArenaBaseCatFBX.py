import bpy
import os


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(os.path.dirname(SCRIPT_DIR))
FBX_PATH = os.path.join(
    PROJECT_ROOT,
    "Assets",
    "_Game",
    "Models",
    "Characters",
    "BlenderExports",
    "SK_PawArena_BaseCat.fbx",
)

REQUIRED_BONES = {
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


def main():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    bpy.ops.import_scene.fbx(filepath=FBX_PATH)

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected 1 armature after FBX import, found {len(armatures)}")
    if not meshes:
        raise RuntimeError("No mesh objects found after FBX import")

    rig = armatures[0]
    imported_bones = set(rig.data.bones.keys())
    missing = sorted(REQUIRED_BONES - imported_bones)
    if missing:
        raise RuntimeError("FBX missing required bones: " + ", ".join(missing))

    skinned_meshes = [
        obj
        for obj in meshes
        if any(modifier.type == "ARMATURE" for modifier in obj.modifiers)
    ]
    if len(skinned_meshes) != len(meshes):
        missing_skin = sorted(obj.name for obj in meshes if obj not in skinned_meshes)
        raise RuntimeError("FBX meshes missing skin modifier: " + ", ".join(missing_skin))

    weighted_meshes = [obj for obj in meshes if len(obj.vertex_groups) > 0]
    if len(weighted_meshes) != len(meshes):
        missing_weights = sorted(obj.name for obj in meshes if obj not in weighted_meshes)
        raise RuntimeError("FBX meshes missing vertex groups: " + ", ".join(missing_weights))

    print("FBX_VALIDATION=PASS")
    print("FBX_ARMATURE=" + rig.name)
    print("FBX_BONE_COUNT=" + str(len(imported_bones)))
    print("FBX_MESH_COUNT=" + str(len(meshes)))
    print("FBX_SKINNED_MESH_COUNT=" + str(len(skinned_meshes)))
    print("FBX_HEAD_GEAR_SOCKET=HeadGearSocket")


if __name__ == "__main__":
    main()
