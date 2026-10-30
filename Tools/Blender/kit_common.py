# Author : Baptiste Vu Liem
# Shared toolkit of the Goobyr FPS environment kit: a mesh accumulator (Kit) with bevelled primitives,
# vertex colours ("Col", BYTE_COLOR / CORNER, alpha = tint mask), booleans, FBX export, the
# kit manifest and the "GoobyrKit" work scene. kit_buildings / kit_props / kit_nature build on it.
# Conventions: metres, Blender Z up, a piece's front faces +Y, origin at the bottom centre.
import bpy
import bmesh
import json
import math
import os
import random
import traceback
from mathutils import Matrix, Vector

PROJECT = r"D:\_Project\_GamesUnity\_Isart\Goobyr FPS"
KIT_ROOT = os.path.join(PROJECT, "Assets", "_Game", "Art", "Resources", "Kit")
MANIFEST = os.path.join(KIT_ROOT, "manifest.json")
SCENE_NAME = "GoobyrKit"
PREVIEW_MATERIAL = "GoobyrKit_Preview"
BEVEL = 0.02
BEVEL_ANGLE = 40.0


# ---------------------------------------------------------------- colours
def fixed(r, g, b):
    """A fixed-colour detail (tint mask 0)."""
    return (r, g, b, 0.0)


def tinted(r, g, b):
    """A main surface the builders recolour (tint mask 1): author it light."""
    return (r, g, b, 1.0)


def shade(col, k):
    """Same colour, brightness scaled by k (keeps the tint mask)."""
    return (min(col[0] * k, 1.0), min(col[1] * k, 1.0), min(col[2] * k, 1.0), col[3])


GLASS = fixed(0.55, 0.7, 0.8)
GLOW = fixed(1.0, 0.92, 0.75)


# ---------------------------------------------------------------- transforms
def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation((x, y, z))


def R(axis, degrees):
    return Matrix.Rotation(math.radians(degrees), 4, axis)


def S(x, y=None, z=None):
    y = x if y is None else y
    z = x if z is None else z
    return Matrix.Diagonal((x, y, z, 1.0))


IDENTITY = Matrix.Identity(4)


# ---------------------------------------------------------------- bmesh helpers
def bevel_bm(bm, width, angle=BEVEL_ANGLE):
    """1-segment chamfer on every edge sharper than `angle` degrees."""
    if width <= 0:
        return bm
    limit = math.radians(angle)
    edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > limit]
    if edges:
        bmesh.ops.bevel(bm, geom=edges, offset=width, offset_type="OFFSET", segments=1, profile=0.5,
                        affect="EDGES", clamp_overlap=True)
    return bm


def bm_box(sx, sy, sz):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
    return bm


def bm_cylinder(r1, r2, h, n):
    """Cylinder / cone centred on the origin along Z."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=n, radius1=r1, radius2=r2, depth=h)
    return bm


def bm_lathe(profile, n, closed=False):
    """Surface of revolution around Z. profile = [(radius, z), ...] bottom to top.
    Open profiles are capped; closed=True joins the last point back to the first (rings, basins)."""
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        if r <= 1e-5:
            rings.append([bm.verts.new((0.0, 0.0, z))])
            continue
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n), z))
                      for i in range(n)])
    pairs = list(zip(rings, rings[1:]))
    if closed:
        pairs.append((rings[-1], rings[0]))
    for a, b in pairs:
        if len(a) == 1 and len(b) == 1:
            continue
        for i in range(n):
            j = (i + 1) % n
            if len(a) == 1:
                bm.faces.new((a[0], b[i], b[j]))
            elif len(b) == 1:
                bm.faces.new((a[i], b[0], a[j]))
            else:
                bm.faces.new((a[i], a[j], b[j], b[i]))
    if not closed:
        for ring in (rings[0], rings[-1]):
            if len(ring) > 2:
                bm.faces.new(ring)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def bm_prism(points, d0, d1, axis="Y"):
    """Extrude a 2D polygon. axis Y: points are (x, z); axis Z: (x, y); axis X: (y, z)."""
    def place(p, d):
        if axis == "Y":
            return (p[0], d, p[1])
        if axis == "Z":
            return (p[0], p[1], d)
        return (d, p[0], p[1])

    bm = bmesh.new()
    a = [bm.verts.new(place(p, d0)) for p in points]
    b = [bm.verts.new(place(p, d1)) for p in points]
    n = len(points)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(a)
    bm.faces.new(b)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def bm_blob(r, subdiv, jitter, rng):
    """Jittered icosphere: foliage puffs and rocks. subdiv 0 = 20 faces, 1 = 80, 2 = 320."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv + 1, radius=r)
    for v in bm.verts:
        v.co *= 1.0 + rng.uniform(-jitter, jitter)
    return bm


def bm_tube(points, radii, n, cap=True, closed=False):
    """Sweep a ring of n sides along a polyline, radius per point (parallel-transported frame, no twist)."""
    bm = bmesh.new()
    pts = [Vector(p) for p in points]
    count = len(pts)

    def tangent(i):
        if closed:
            d = pts[(i + 1) % count] - pts[i - 1]
        elif i == 0:
            d = pts[1] - pts[0]
        elif i == count - 1:
            d = pts[i] - pts[i - 1]
        else:
            d = pts[i + 1] - pts[i - 1]
        return d.normalized()

    d0 = tangent(0)
    side = d0.cross(Vector((0, 0, 1)) if abs(d0.z) < 0.95 else Vector((1, 0, 0))).normalized()
    rings, prev = [], d0
    for i, p in enumerate(pts):
        d = tangent(i)
        side = (prev.rotation_difference(d) @ side).normalized()
        up = side.cross(d).normalized()
        prev = d
        rings.append([bm.verts.new(p + (side * math.cos(2 * math.pi * k / n) + up * math.sin(2 * math.pi * k / n)) * radii[i])
                      for k in range(n)])
    pairs = list(zip(rings, rings[1:]))
    if closed:
        pairs.append((rings[-1], rings[0]))
    for a, b in pairs:
        for k in range(n):
            m = (k + 1) % n
            bm.faces.new((a[k], a[m], b[m], b[k]))
    if cap and not closed:
        bm.faces.new(rings[0])
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def bm_hull(points):
    """Convex hull of a point cloud (roof layers, gems, rocks)."""
    bm = bmesh.new()
    verts = [bm.verts.new(p) for p in points]
    res = bmesh.ops.convex_hull(bm, input=verts)
    loose = {e for e in res["geom_interior"] + res["geom_unused"] if isinstance(e, bmesh.types.BMVert)}
    if loose:
        bmesh.ops.delete(bm, geom=list(loose), context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def bm_boolean(base, cutters, operation="DIFFERENCE"):
    """Boolean (EXACT solver) of a bmesh with a list of cutter bmeshes. Frees the inputs."""
    scene = kit_scene()
    cut = bmesh.new()
    for c in cutters:
        _append_bm(cut, c)
        c.free()
    objs = []
    for label, bm in (("_bool_base", base), ("_bool_cut", cut)):
        me = bpy.data.meshes.new(label)
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(label, me)
        scene.collection.objects.link(ob)
        objs.append(ob)
    mod = objs[0].modifiers.new("Bool", "BOOLEAN")
    mod.operation = operation
    mod.solver = "EXACT"
    mod.object = objs[1]
    objs[1].hide_set(True)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    depsgraph.update()
    result = bpy.data.meshes.new_from_object(objs[0].evaluated_get(depsgraph))
    out = bmesh.new()
    out.from_mesh(result)
    bpy.data.meshes.remove(result)
    for ob in objs:
        me = ob.data
        bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.meshes.remove(me)
    return out


def _append_bm(dst, src):
    """Copy the geometry of src into dst."""
    remap = {v: dst.verts.new(v.co) for v in src.verts}
    for f in src.faces:
        dst.faces.new([remap[v] for v in f.verts])


# ---------------------------------------------------------------- the accumulator
class Kit:
    """Accumulates coloured parts; build() joins them into one flat-shaded mesh object."""

    def __init__(self):
        self.verts = []
        self.faces = []
        self.cols = []
        self.glow = None

    # --- low level
    def add(self, bm, col, m=None, bevel=0.0, angle=BEVEL_ANGLE):
        """Append a bmesh. `col` is an RGBA tuple, or a function face -> RGBA (picked by normal)."""
        bevel_bm(bm, bevel, angle)
        if m is not None:
            bm.transform(m)
            if m.to_3x3().determinant() < 0:
                bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        bm.normal_update()
        bm.verts.index_update()
        base = len(self.verts)
        self.verts.extend(v.co.copy() for v in bm.verts)
        for f in bm.faces:
            self.faces.append([base + v.index for v in f.verts])
            self.cols.append(col(f) if callable(col) else col)
        bm.free()
        return self

    def merge(self, other, m=IDENTITY):
        """Place another Kit (a sub-assembly) inside this one."""
        base = len(self.verts)
        self.verts.extend(m @ v for v in other.verts)
        flip = m.to_3x3().determinant() < 0
        for f, c in zip(other.faces, other.cols):
            idx = [base + i for i in f]
            self.faces.append(idx[::-1] if flip else idx)
            self.cols.append(c)
        if other.glow is not None:
            self.glow_kit().merge(other.glow, m)
        return self

    def glow_kit(self):
        if self.glow is None:
            self.glow = Kit()
        return self.glow

    # --- primitives (all centred on `c`; `m` is a local rotation/scale applied before the move)
    def box(self, c, s, col, bevel=BEVEL, m=None):
        bm = bm_box(*s)
        bevel_bm(bm, min(bevel, min(s) * 0.45))
        return self.add(bm, col, T(*c) @ (m or IDENTITY))

    def cyl(self, c, r, h, col, n=12, r2=None, axis="Z", bevel=BEVEL, m=None):
        bm = bm_cylinder(r, r if r2 is None else r2, h, n)
        bevel_bm(bm, min(bevel, h * 0.45, r * 0.45), 360.0 / n + 5.0)
        return self.add(bm, col, T(*c) @ (m or IDENTITY) @ _axis_matrix(axis))

    def lathe(self, c, profile, col, n=12, axis="Z", m=None, closed=False):
        return self.add(bm_lathe(profile, n, closed), col, T(*c) @ (m or IDENTITY) @ _axis_matrix(axis))

    def torus(self, c, major, minor, col, seg=16, n=6, axis="Z", m=None):
        ring = [(major * math.cos(2 * math.pi * i / seg), major * math.sin(2 * math.pi * i / seg), 0.0) for i in range(seg)]
        return self.add(bm_tube(ring, [minor] * seg, n, closed=True), col, T(*c) @ (m or IDENTITY) @ _axis_matrix(axis))

    def prism(self, points, d0, d1, col, axis="Y", bevel=BEVEL, m=None, angle=BEVEL_ANGLE):
        bm = bm_prism(points, d0, d1, axis)
        return self.add(bm, col, m, bevel, angle)

    def blob(self, c, r, col, rng, sub=1, jitter=0.12, scale=(1, 1, 1), m=None):
        bm = bm_blob(r, sub, jitter, rng)
        return self.add(bm, col, T(*c) @ (m or IDENTITY) @ S(*scale))

    def hull(self, points, col, bevel=0.0, angle=BEVEL_ANGLE):
        return self.add(bm_hull(points), col, None, bevel, angle)

    def tube(self, points, radii, col, n=8, cap=True, closed=False):
        if not isinstance(radii, (list, tuple)):
            radii = [radii] * len(points)
        return self.add(bm_tube(points, radii, n, cap, closed), col)

    def beam(self, a, b, w, d, col, bevel=BEVEL):
        """Box from point a to point b: braces, rafters, rails. The cross-section's d side stays in the vertical
        plane through the axis, w is horizontal (for a beam in the XZ plane, w is the depth along Y)."""
        a, b = Vector(a), Vector(b)
        axis = b - a
        rot = axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
        return self.box((a + b) / 2, (w, d, axis.length), col, bevel, rot)

    # --- output
    def tris(self):
        return sum(len(f) - 2 for f in self.faces)

    def build(self, name, collection):
        bm = bmesh.new()
        layer = bm.loops.layers.color.new("Col")
        verts = [bm.verts.new(v) for v in self.verts]
        for f, c in zip(self.faces, self.cols):
            try:
                face = bm.faces.new([verts[i] for i in f])
            except ValueError:
                continue  # degenerate / duplicate face
            face.smooth = False
            for loop in face.loops:
                loop[layer] = c
        bm.normal_update()  # triangulation projects each ngon on its normal
        ngons = [f for f in bm.faces if len(f.verts) > 4]
        if ngons:
            bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = False
        me.color_attributes.active_color_name = "Col"
        ob = bpy.data.objects.new(name, me)
        collection.objects.link(ob)
        return ob


def _axis_matrix(axis):
    if axis == "X":
        return R("Y", 90)
    if axis == "Y":
        return R("X", -90)
    return IDENTITY


# ---------------------------------------------------------------- scene, preview, export
def kit_scene():
    scene = bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME)
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


def style_collection(scene, style):
    name = "Kit_" + style
    coll = bpy.data.collections.get(name)
    if coll is None:
        coll = bpy.data.collections.new(name)
    if coll.name not in scene.collection.children:
        scene.collection.children.link(coll)
    return coll


def clear_style(scene, style):
    """Remove the previous build of a style (only objects living in GoobyrKit)."""
    coll = style_collection(scene, style)
    for ob in list(coll.objects):
        if any(s != scene for s in ob.users_scene):
            continue
        me = ob.data
        bpy.data.objects.remove(ob, do_unlink=True)
        if me is not None and me.users == 0:
            bpy.data.meshes.remove(me)
    return coll


def preview_material():
    """Viewport/render material: vertex colour (sRGB bytes) x object tint (object colour), like the toon shader."""
    mat = bpy.data.materials.get(PREVIEW_MATERIAL)
    if mat is not None and mat.get("kit_version") == 2:
        return mat
    if mat is not None:
        mat.name = PREVIEW_MATERIAL + "_old"
    mat = bpy.data.materials.new(PREVIEW_MATERIAL)
    mat["kit_version"] = 2
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    info = nt.nodes.new("ShaderNodeObjectInfo")
    mix_t = nt.nodes.new("ShaderNodeMix")
    mix_t.data_type = "RGBA"
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs[0].default_value = 1.0
    nt.links.new(attr.outputs["Alpha"], mix_t.inputs[0])
    mix_t.inputs[6].default_value = (1, 1, 1, 1)
    nt.links.new(info.outputs["Color"], mix_t.inputs[7])
    nt.links.new(attr.outputs["Color"], mul.inputs[6])
    nt.links.new(mix_t.outputs[2], mul.inputs[7])
    nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    return mat


def export_fbx(ob, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    view_layer = bpy.context.view_layer
    for o in view_layer.objects:
        if o is not None:
            o.select_set(False)
    ob.select_set(True)
    view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE",
                             colors_type="SRGB", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
                             use_custom_props=False)
    ob.select_set(False)


def unity_size(ob):
    """Bounding size in Unity axes: x width (Blender X), y height (Blender Z), z depth (Blender Y)."""
    xs = [v.co.x for v in ob.data.vertices]
    ys = [v.co.y for v in ob.data.vertices]
    zs = [v.co.z for v in ob.data.vertices]
    return [round(max(xs) - min(xs), 3), round(max(zs) - min(zs), 3), round(max(ys) - min(ys), 3)]


class Piece:
    """A kit piece description: build() returns a Kit."""

    def __init__(self, name, kind, build, notes="", tint=None):
        self.name = name
        self.kind = kind
        self.build = build
        self.notes = notes
        self.tint = tint


def run_style(style, pieces, only=None, columns=8, spacing=7.0, row=0):
    """Build, export and record every piece of a style inside GoobyrKit. Returns manifest entries."""
    prev = bpy.context.window.scene
    entries, failures = [], []
    try:
        scene = kit_scene()
        bpy.context.window.scene = scene
        coll = clear_style(scene, style) if only is None else style_collection(scene, style)
        mat = preview_material()
        for i, piece in enumerate(pieces):
            if only is not None and piece.name not in only:
                continue
            try:
                entries.append(_make_piece(style, piece, coll, mat, i, columns, spacing, row))
            except Exception:
                failures.append(piece.name)
                print("FAILED", style, piece.name)
                print(traceback.format_exc())
        write_manifest(style, entries, partial=only is not None)
    finally:
        bpy.context.window.scene = prev
    print(style, "built", len(entries), "pieces; failed:", failures)
    return entries, failures


def _make_piece(style, piece, coll, mat, index, columns, spacing, row):
    full = style + "_" + piece.name
    for stale in (full, full + "_Glow"):
        old = bpy.data.objects.get(stale)
        if old is not None and all(s.name == SCENE_NAME for s in old.users_scene):
            me = old.data
            bpy.data.objects.remove(old, do_unlink=True)
            if me is not None and me.users == 0:
                bpy.data.meshes.remove(me)
    kit = piece.build()
    ob = kit.build(full, coll)
    ob.data.materials.append(mat)
    folder = os.path.join(KIT_ROOT, style)
    export_fbx(ob, os.path.join(folder, piece.name + ".fbx"))
    size = unity_size(ob)
    ob.data.calc_loop_triangles()
    tris = len(ob.data.loop_triangles)
    notes = piece.notes
    glow_ob = None
    if kit.glow is not None and kit.glow.faces:
        glow_ob = kit.glow.build(full + "_Glow", coll)
        glow_ob.data.materials.append(mat)
        export_fbx(glow_ob, os.path.join(folder, piece.name + "_Glow.fbx"))
        notes = (notes + " " if notes else "") + "Glow companion: " + piece.name + "_Glow.fbx (same pivot)."
    # Display layout only (after export, so the FBX keeps the pivot at the origin).
    offset = Vector(((index % columns) * spacing, -(row + index // columns) * spacing, 0.0))
    tint = piece.tint or (1.0, 1.0, 1.0)
    for o in (ob, glow_ob):
        if o is not None:
            o.location = offset
            o.color = (tint[0], tint[1], tint[2], 1.0)
    entry = {"style": style, "name": piece.name, "size": size, "kind": piece.kind, "notes": notes, "tris": tris}
    if piece.tint is not None:
        entry["tint"] = [round(t, 3) for t in piece.tint]
    return entry


def write_manifest(style, entries, partial=False):
    data = {"pieces": []}
    if os.path.exists(MANIFEST):
        with open(MANIFEST, encoding="utf-8") as f:
            data = json.load(f)
    replaced = {(e["style"], e["name"]) for e in entries}
    keep = [e for e in data.get("pieces", [])
            if (e["style"], e["name"]) not in replaced and (partial or e["style"] != style)]
    data = {
        "version": 1,
        "units": "metres; size = Unity axes [x width, y height, z depth]",
        "conventions": "Front faces Unity +Z after the 180 deg import turn. Origin at bottom centre (buildings: on the "
                       "wall centre line). Vertex colour Col: rgb = colour, alpha = tint mask (1 = recolourable).",
        "pieces": sorted(keep + entries, key=lambda e: (e["style"], e["name"])),
    }
    os.makedirs(KIT_ROOT, exist_ok=True)
    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)


def rng_for(name):
    return random.Random(sum(ord(c) * (i + 1) for i, c in enumerate(name)))


# ---------------------------------------------------------------- preview renders (quality checks)
def render_preview(names, path, direction=(0.3, 1.0, 0.4), size=(1280, 720), lens=50.0, gap=0.8):
    """Render the named GoobyrKit objects side by side (seen from +Y, their front) into a PNG.
    Object positions and the window scene are restored afterwards."""
    prev = bpy.context.window.scene
    scene = kit_scene()
    try:
        bpy.context.window.scene = scene
        obs = [bpy.data.objects[n] for n in names if n in bpy.data.objects]
        saved = {ob.name: ob.location.copy() for ob in obs}
        x = 0.0
        for ob in obs:
            xs = [c[0] for c in ob.bound_box]
            ob.location = (x - min(xs), 0.0, 0.0)
            x += max(xs) - min(xs) + gap
        bpy.context.view_layer.update()
        pts = [ob.matrix_world @ Vector(c) for ob in obs for c in ob.bound_box]
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        centre, radius = (lo + hi) / 2, (hi - lo).length / 2
        cam = bpy.data.objects.get("KitCam") or bpy.data.objects.new("KitCam", bpy.data.cameras.new("KitCam"))
        sun = bpy.data.objects.get("KitSun") or bpy.data.objects.new("KitSun", bpy.data.lights.new("KitSun", "SUN"))
        for helper in (cam, sun):
            if helper.name not in scene.collection.objects:
                scene.collection.objects.link(helper)
        cam.data.lens = lens
        d = Vector(direction).normalized()
        dist = radius / math.tan(cam.data.angle / 2) * 1.05
        cam.location = centre + d * dist
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        cam.data.clip_end = dist * 4
        sun.data.energy = 2.4
        sun.rotation_euler = (math.radians(55), 0.0, math.radians(150))
        scene.camera = cam
        world = scene.world or bpy.data.worlds.new("KitWorld")
        scene.world = world
        world.use_nodes = True
        bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
        bg.inputs[0].default_value = (0.62, 0.72, 0.85, 1.0)
        bg.inputs[1].default_value = 0.7
        hidden = []
        for ob in scene.objects:
            if ob.type == "MESH" and ob not in obs and not ob.hide_render:
                ob.hide_render = True
                hidden.append(ob)
        for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
            try:
                scene.render.engine = engine
                break
            except TypeError:
                continue
        scene.render.resolution_x, scene.render.resolution_y = size
        scene.render.resolution_percentage = 100
        scene.render.filepath = path
        scene.render.image_settings.file_format = "PNG"
        scene.view_settings.view_transform = "Standard"
        bpy.ops.render.render(write_still=True, scene=scene.name)
        for ob in hidden:
            ob.hide_render = False
        for ob in obs:
            ob.location = saved[ob.name]
    finally:
        bpy.context.window.scene = prev
    return path
