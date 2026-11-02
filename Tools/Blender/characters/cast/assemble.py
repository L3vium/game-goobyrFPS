# Author : Baptiste Vu Liem
"""bpy helpers of the cast generator: eyeballs, colour attributes, materials on parts, collections."""
import math

import bmesh
import bpy
import numpy as np

import blmesh


def collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def eyeball(name, centre, r, outward_deg=-2.0, side=1.0, coll=None, segments=(40, 28)):
    """UV sphere whose pole looks forward (-Y); UVs = planar front projection (iris at 0.5, 0.5)."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments[0], v_segments=segments[1], radius=r)
    # pole along -Y
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=_rot_x(math.radians(90)))
    uv = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        for l in f.loops:
            co = l.vert.co
            l[uv].uv = (0.5 + co.x / (2 * r) * side, 0.5 + co.z / (2 * r))
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=_rot_z(math.radians(side * outward_deg)))
    bmesh.ops.translate(bm, verts=bm.verts, vec=tuple(centre))
    bm.to_mesh(me)
    bm.free()
    me.shade_smooth()
    ob = bpy.data.objects.new(name, me)
    (coll or bpy.context.scene.collection).objects.link(ob)
    ob["cast_material"] = "Eyes"
    return ob


def _rot_x(a):
    from mathutils import Matrix
    return Matrix.Rotation(a, 3, "X")


def _rot_z(a):
    from mathutils import Matrix
    return Matrix.Rotation(a, 3, "Z")


def set_colours(ob, rgb, name="Col"):
    me = ob.data
    attr = me.color_attributes.get(name) or me.color_attributes.new(name, "FLOAT_COLOR", "POINT")
    rgba = np.ones((len(me.vertices), 4), np.float32)
    rgba[:, :3] = rgb
    attr.data.foreach_set("color", rgba.ravel())
    return attr


def verts(ob):
    co = np.empty(len(ob.data.vertices) * 3, np.float32)
    ob.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def assign(ob, mat):
    ob.data.materials.clear()
    ob.data.materials.append(mat)


def build(part, coll, occluders=(), depth=0.002):
    return blmesh.build_part(part, occluders=occluders, depth=depth, collection=coll)
