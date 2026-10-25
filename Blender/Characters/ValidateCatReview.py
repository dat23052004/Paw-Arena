"""Read-only checks of the saved, deliberately unrigged approval model."""
import bpy
import bmesh
import math
import os
from mathutils.kdtree import KDTree

root = os.path.dirname(os.path.abspath(__file__))
path = os.path.join(root, "PawArena_BaseCat_Review.blend")
bpy.ops.wm.open_mainfile(filepath=path)
assert not any(obj.type == "ARMATURE" for obj in bpy.data.objects)
assert not bpy.data.actions
body = bpy.data.objects["CAT | continuous head body arms legs paws"]
assert not body.vertex_groups
assert all(mod.type != "ARMATURE" for mod in body.modifiers)
bm = bmesh.new()
bm.from_mesh(body.data)
assert all(edge.is_manifold for edge in bm.edges)
assert all(face.calc_area() > 1e-9 for face in bm.faces)
assert bm.calc_volume(signed=True) > 0
bm.free()
tree = KDTree(len(body.data.vertices))
for vert in body.data.vertices:
    assert all(math.isfinite(c) for c in vert.co)
    tree.insert(vert.co, vert.index)
tree.balance()
max_error = 0
for vert in body.data.vertices:
    x, y, z = vert.co
    _, _, error = tree.find((-x, y, z))
    max_error = max(max_error, error)
assert max_error < 1e-5, f"Body symmetry mismatch: {max_error}"
for obj in bpy.data.collections["01 | BASE CAT - NO RIG"].objects:
    if obj.type == "MESH":
        assert all(p.area > 1e-10 for p in obj.data.polygons), obj.name
assert bpy.data.scenes.get("BASE REVIEW | four views")
assert any(img.packed_file for img in bpy.data.images if "low-poly" in img.name)
print("PASS: saved file has no rig/actions/weights; body is manifold, nondegenerate, outward-facing and symmetric.")
print("Control vertices:", len(body.data.vertices), "faces:", len(body.data.polygons), "mirror error:", max_error)
print("Packed reference and four-view review scene found.")
