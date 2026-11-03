# Author : Baptiste Vu Liem
"""Blender side of the cast generator: SDF parts -> mesh objects, hidden-surface culling, helpers."""
import time

import bpy
import numpy as np


def log(*a):
    print("[cast]", *a, flush=True)


def new_object(name, verts, faces, normals=None, collection=None):
    """Mesh object from numpy arrays (faces: list of (M,k) int arrays, k = 3 or 4)."""
    me = bpy.data.meshes.new(name)
    verts = np.asarray(verts, dtype=np.float32)
    me.vertices.add(len(verts))
    me.vertices.foreach_set("co", verts.ravel())
    loops, starts, total = [], [], 0
    for f in faces:
        f = np.asarray(f, dtype=np.int32)
        if len(f) == 0:
            continue
        k = f.shape[1]
        starts.append(np.arange(total, total + len(f) * k, k, dtype=np.int32))
        loops.append(f.ravel())
        total += len(f) * k
    if loops:
        loops = np.concatenate(loops)
        starts = np.concatenate(starts)
        me.loops.add(len(loops))
        me.loops.foreach_set("vertex_index", loops)
        me.polygons.add(len(starts))
        me.polygons.foreach_set("loop_start", starts)
    me.update(calc_edges=True)
    me.validate(clean_customdata=False)
    me.shade_smooth()
    if normals is not None and len(normals) == len(me.vertices):
        me.normals_split_custom_set_from_vertices(np.asarray(normals, dtype=np.float32))
    ob = bpy.data.objects.new(name, me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    return ob


def compact(P, faces, N, keep_face_masks):
    """Keep only the masked faces and the vertices they use."""
    kept = [f[m] for f, m in zip(faces, keep_face_masks)]
    used = np.zeros(len(P), bool)
    for f in kept:
        used[f.ravel()] = True
    remap = -np.ones(len(P), np.int64)
    remap[used] = np.arange(used.sum())
    return P[used], [remap[f] for f in kept if len(f)], (None if N is None else N[used])


def inside_mask(P, occluders, depth):
    inside = np.zeros(len(P), bool)
    for part in occluders:
        lo, hi = part.bounds()
        m = np.all((P >= lo) & (P <= hi), axis=1) & ~inside
        idx = np.nonzero(m)[0]
        if idx.size == 0:
            continue
        d = part.eval_points(P[idx].astype(np.float32))
        inside[idx[d < -depth]] = True
    return inside


def build_part(part, occluders=(), depth=0.002, collection=None, adaptivity=0.0):
    """Mesh a Part, drop the faces buried inside the occluder Parts, create the object."""
    t = time.time()
    P, faces, N = part.mesh(adaptivity=adaptivity)
    if len(P) == 0:
        log("part", part.name, "is EMPTY")
        return None
    nf = sum(len(f) for f in faces)
    if occluders:
        inside = inside_mask(P, occluders, depth)
        masks = [~np.all(inside[f], axis=1) for f in faces]
        P, faces, N = compact(P, faces, N, masks)
        if len(P) == 0:
            log("part", part.name, "fully hidden")
            return None
    ob = new_object(part.name, P, faces, N, collection)
    ob["cast_material"] = part.material
    classify = part.meta.get("classify")
    if classify is not None:
        # per-face material index: classify(centres (M,3), normals (M,3)) -> int array (0 = part.material)
        me = ob.data
        nf = len(me.polygons)
        cen = np.empty(nf * 3, np.float32)
        me.polygons.foreach_get("center", cen)
        nrm = np.empty(nf * 3, np.float32)
        me.polygons.foreach_get("normal", nrm)
        idx = np.asarray(classify(cen.reshape(-1, 3), nrm.reshape(-1, 3)), dtype=np.int32)
        me.polygons.foreach_set("material_index", idx)
        ob["cast_slots"] = ",".join([part.material] + list(part.meta.get("slots", [])))
    for k, v in part.meta.items():
        if isinstance(v, (int, float, str)):
            ob["cast_" + k] = v
    log("part %-24s %7d verts %7d faces (of %d) %5.1fs" % (part.name, len(P), sum(len(f) for f in faces), nf, time.time() - t))
    return ob
