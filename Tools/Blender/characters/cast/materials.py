# Author : Baptiste Vu Liem
"""Look-development materials of the cast (Blender, Eevee/Cycles).

Fabrics, leathers and rubbers use Poly Haven CC0 scans (api.polyhaven.com, cached in
Tools/Blender/out/cast/polyhaven, credited in docs/Credits.md): the scan's albedo is turned into a
luminance pattern and recoloured to the outfit colour, its displacement drives a bump, its ARM map
the roughness. Projection is object-space box mapping (no UVs needed); the export step bakes this
into per-character texture sets. Skin multiplies a tone by the per-vertex blush colours.
"""
import json
import os
import urllib.request

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
CACHE = os.path.join(PROJECT, "Tools", "Blender", "out", "cast", "polyhaven")
API = "https://api.polyhaven.com"
UA = {"User-Agent": "GoobyrFPS-asset-tool/1.0"}
USED = set()


def _get(url):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=120) as res:
        return res.read()


def ph_texture(asset, res="1k"):
    """Paths {diff, disp, arm} of a Poly Haven texture (downloaded once)."""
    USED.add(asset)
    out = os.path.join(CACHE, asset)
    want = {"diff": None, "disp": None, "arm": None}
    if os.path.isdir(out):
        for f in os.listdir(out):
            for k in want:
                if f.startswith("%s_%s_" % (asset, k)):
                    want[k] = os.path.join(out, f)
        if all(want.values()):
            return want
    os.makedirs(out, exist_ok=True)
    files = json.loads(_get("%s/files/%s" % (API, asset)).decode("utf-8"))
    for k in want:
        if k == "diff":
            key = next((c for c in ("Diffuse", "diff", "coll1", "col1") if c in files), "Diffuse")
        elif k == "disp":
            key = "Displacement" if "Displacement" in files else "disp"
        else:
            key = k
        if key not in files:
            continue
        opts = files[key].get(res) or files[key].get("1k") or files[key].get("2k")
        fmt = opts.get("jpg") or opts.get("png")
        ext = "jpg" if "jpg" in opts else "png"
        path = os.path.join(out, "%s_%s_%s.%s" % (asset, k, res, ext))
        if not os.path.exists(path):
            with open(path, "wb") as fh:
                fh.write(_get(fmt["url"]))
        want[k] = path
    return want


# ------------------------------------------------------------------------------------- node helpers

class NB:
    def __init__(self, mat):
        mat.use_nodes = True
        self.nt = mat.node_tree
        for n in list(self.nt.nodes):
            self.nt.nodes.remove(n)
        self.out = self.n("ShaderNodeOutputMaterial")
        self.bsdf = self.n("ShaderNodeBsdfPrincipled")
        self.link(self.bsdf.outputs[0], self.out.inputs[0])

    def n(self, kind, **props):
        node = self.nt.nodes.new(kind)
        for k, v in props.items():
            setattr(node, k, v)
        return node

    def link(self, a, b):
        self.nt.links.new(a, b)

    def inp(self, name, value):
        s = self.bsdf.inputs.get(name)
        if s is not None:
            s.default_value = value

    def value(self, v):
        n = self.n("ShaderNodeValue")
        n.outputs[0].default_value = v
        return n.outputs[0]

    def math(self, op, a, b=None, clamp=False):
        m = self.n("ShaderNodeMath", operation=op, use_clamp=clamp)
        for i, x in enumerate((a, b)):
            if x is None:
                continue
            if isinstance(x, (int, float)):
                m.inputs[i].default_value = x
            else:
                self.link(x, m.inputs[i])
        return m.outputs[0]

    def mix(self, fac, a, b, blend="MIX"):
        m = self.n("ShaderNodeMix", data_type="RGBA", blend_type=blend)
        for sock, x in ((m.inputs[0], fac), (m.inputs[6], a), (m.inputs[7], b)):
            if isinstance(x, (int, float)):
                sock.default_value = x
            elif isinstance(x, tuple):
                sock.default_value = x if len(x) == 4 else x + (1.0,)
            else:
                self.link(x, sock)
        return m.outputs[2]

    def coords(self, scale, kind="Object"):
        tc = self.n("ShaderNodeTexCoord")
        mp = self.n("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = scale if isinstance(scale, tuple) else (scale, scale, scale)
        self.link(tc.outputs[kind], mp.inputs["Vector"])
        return mp.outputs[0]

    def image(self, path, vec, non_color=False, box=True):
        t = self.n("ShaderNodeTexImage")
        t.image = bpy.data.images.load(path, check_existing=True)
        if non_color:
            t.image.colorspace_settings.name = "Non-Color"
        if box:
            t.projection = "BOX"
            t.projection_blend = 0.35
        self.link(vec, t.inputs["Vector"])
        return t

    def noise(self, vec, scale, detail=3.0, rough=0.55):
        t = self.n("ShaderNodeTexNoise")
        t.inputs["Scale"].default_value = scale
        t.inputs["Detail"].default_value = detail
        t.inputs["Roughness"].default_value = rough
        self.link(vec, t.inputs["Vector"])
        return t

    def bump(self, height, strength, distance=0.002, normal=None):
        b = self.n("ShaderNodeBump")
        b.inputs["Strength"].default_value = strength
        b.inputs["Distance"].default_value = distance
        self.link(height, b.inputs["Height"])
        if normal is not None:
            self.link(normal, b.inputs["Normal"])
        return b.outputs[0]


def _srgb(c):
    """sRGB 0..1 -> linear (Blender colour sockets are linear)."""
    def f(x):
        return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4
    return tuple(f(x) for x in c[:3]) + (1.0,)


_CACHE = {}


def get(name, builder, *a, **kw):
    if name in _CACHE and _CACHE[name].name in bpy.data.materials:
        return _CACHE[name]
    mat = bpy.data.materials.new(name)
    builder(mat, *a, **kw)
    _CACHE[name] = mat
    return mat


# ------------------------------------------------------------------------------------- materials

def fabric(mat, colour, ph="cotton_jersey", scale=3.0, rough=0.85, sheen=0.35, bump=0.25, contrast=0.6,
           pattern=None, metal=0.0, coat=0.0):
    """Recoloured Poly Haven fabric. pattern: optional (builder(nb, vec) -> colour socket) overriding the colour."""
    b = NB(mat)
    tex = ph_texture(ph)
    vec = b.coords(scale)
    d = b.image(tex["diff"], vec)
    # luminance pattern around 1.0: colour * (1 + contrast * (lum - mean))
    lum = b.n("ShaderNodeRGBToBW")
    b.link(d.outputs["Color"], lum.inputs[0])
    centred = b.math("SUBTRACT", lum.outputs[0], 0.35)
    k = b.math("MULTIPLY", centred, contrast)
    fac = b.math("ADD", k, 1.0)
    base = pattern(b, b.coords(1.0)) if pattern else None
    col = b.n("ShaderNodeMix", data_type="RGBA", blend_type="MULTIPLY")
    col.inputs[0].default_value = 1.0
    if base is None:
        col.inputs[6].default_value = _srgb(colour)
    else:
        b.link(base, col.inputs[6])
    cr = b.n("ShaderNodeCombineColor")
    for i in range(3):
        b.link(fac, cr.inputs[i])
    b.link(cr.outputs[0], col.inputs[7])
    b.link(col.outputs[2], b.bsdf.inputs["Base Color"])
    # roughness from the scan's ARM (green), rescaled around the requested value
    if tex.get("arm"):
        a = b.image(tex["arm"], vec, non_color=True)
        sep = b.n("ShaderNodeSeparateColor")
        b.link(a.outputs["Color"], sep.inputs[0])
        r = b.math("MULTIPLY", sep.outputs[1], rough / 0.75, clamp=True)
        b.link(r, b.bsdf.inputs["Roughness"])
    else:
        b.inp("Roughness", rough)
    if tex.get("disp") and bump > 0:
        h = b.image(tex["disp"], vec, non_color=True)
        b.link(b.bump(h.outputs["Color"], bump, 0.0015), b.bsdf.inputs["Normal"])
    b.inp("Sheen Weight", sheen)
    b.inp("Sheen Roughness", 0.45)
    b.inp("Metallic", metal)
    b.inp("Coat Weight", coat)
    b.inp("Specular IOR Level", 0.35)


def plain(mat, colour, rough=0.5, metal=0.0, coat=0.0, noise=0.0, bump=0.0, scale=40.0, sheen=0.0):
    b = NB(mat)
    if noise > 0:
        vec = b.coords(scale)
        nz = b.noise(vec, 1.0, 4.0, 0.6)
        v = b.math("SUBTRACT", nz.outputs[0], 0.5)
        f = b.math("MULTIPLY", v, noise)
        f = b.math("ADD", f, 1.0)
        cr = b.n("ShaderNodeCombineColor")
        for i in range(3):
            b.link(f, cr.inputs[i])
        m = b.mix(1.0, _srgb(colour)[:3], cr.outputs[0], "MULTIPLY")
        b.link(m, b.bsdf.inputs["Base Color"])
        if bump > 0:
            b.link(b.bump(nz.outputs[0], bump, 0.001), b.bsdf.inputs["Normal"])
    else:
        b.inp("Base Color", _srgb(colour))
    b.inp("Roughness", rough)
    b.inp("Metallic", metal)
    b.inp("Coat Weight", coat)
    b.inp("Sheen Weight", sheen)


def skin(mat, tone, rough=0.58):
    """Skin for the look-dev and the bake: tone x blush colours (attribute Col), mottling, stubble dots
    (Mask.r), glossier T-zone (Mask.g), pores in the bump."""
    b = NB(mat)
    attr = b.n("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    mask = b.n("ShaderNodeVertexColor")
    mask.layer_name = "Mask"
    msep = b.n("ShaderNodeSeparateColor")
    b.link(mask.outputs["Color"], msep.inputs[0])
    m = b.mix(1.0, _srgb(tone)[:3], attr.outputs["Color"], "MULTIPLY")
    vec = b.coords(1.0)
    nz = b.noise(vec, 90.0, 6.0, 0.6)
    v = b.math("SUBTRACT", nz.outputs[0], 0.5)
    v = b.math("MULTIPLY", v, 0.12)
    v = b.math("ADD", v, 1.0)
    cr = b.n("ShaderNodeCombineColor")
    for i in range(3):
        b.link(v, cr.inputs[i])
    m2 = b.mix(1.0, m, cr.outputs[0], "MULTIPLY")
    # stubble: fine dark dots where the mask says so
    dots = b.noise(vec, 1400.0, 1.0, 0.4)
    dd = b.math("GREATER_THAN", dots.outputs[0], 0.53)
    sd = b.math("MULTIPLY", dd, msep.outputs[0])
    shade = b.math("MULTIPLY", msep.outputs[0], 0.07)
    sd = b.math("ADD", b.math("MULTIPLY", sd, 0.22), shade)
    m3 = b.mix(sd, m2, (0.10, 0.09, 0.09), "MIX")
    b.link(m3, b.bsdf.inputs["Base Color"])
    pores = b.noise(vec, 1100.0, 2.0, 0.5)
    stub_b = b.math("MULTIPLY", dd, msep.outputs[0])
    hb = b.math("ADD", pores.outputs[0], b.math("MULTIPLY", stub_b, 0.6))
    b.link(b.bump(hb, 0.07, 0.0005), b.bsdf.inputs["Normal"])
    r = b.math("SUBTRACT", rough, b.math("MULTIPLY", msep.outputs[1], 0.09))
    b.link(r, b.bsdf.inputs["Roughness"])
    b.inp("Subsurface Weight", 0.12)
    b.inp("Subsurface Radius", (1.0, 0.4, 0.22))
    b.inp("Subsurface Scale", 0.012)
    b.inp("Specular IOR Level", 0.42)
    b.inp("Sheen Weight", 0.08)


def hair(mat, colour, rough=0.48, flow=(1.0, 1.0, 1.0), sheen=0.18, roots=(1.64, 1.80)):
    """Sculpted-hair look: base colour darker at the roots, streaks along the flow axis, soft sheen."""
    b = NB(mat)
    vec = b.coords(tuple(160.0 / max(f, 1e-3) for f in flow))
    nz = b.noise(vec, 1.0, 6.0, 0.65)
    v = b.math("SUBTRACT", nz.outputs[0], 0.5)
    v = b.math("MULTIPLY", v, 0.45)
    v = b.math("ADD", v, 1.0)
    # root-to-tip gradient (world height)
    sep = b.n("ShaderNodeSeparateXYZ")
    b.link(b.coords(1.0), sep.inputs[0])
    mr = b.n("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = roots[0]
    mr.inputs["From Max"].default_value = roots[1]
    mr.inputs["To Min"].default_value = 0.72
    mr.inputs["To Max"].default_value = 1.12
    b.link(sep.outputs[2], mr.inputs["Value"])
    v = b.math("MULTIPLY", v, mr.outputs["Result"])
    cr = b.n("ShaderNodeCombineColor")
    for i in range(3):
        b.link(v, cr.inputs[i])
    m = b.mix(1.0, _srgb(colour)[:3], cr.outputs[0], "MULTIPLY")
    b.link(m, b.bsdf.inputs["Base Color"])
    b.link(b.bump(nz.outputs[0], 0.25, 0.0008), b.bsdf.inputs["Normal"])
    b.inp("Roughness", rough)
    b.inp("Sheen Weight", sheen)
    b.inp("Sheen Tint", _srgb(colour))
    b.inp("Anisotropic", 0.5)
    b.inp("Specular IOR Level", 0.45)


def eye(mat, iris, dead=False):
    """UV-planar eyeball: sclera, iris ring, pupil, limbal ring, wet coat. dead = X pupils."""
    b = NB(mat)
    uv = b.n("ShaderNodeUVMap")
    sub = b.n("ShaderNodeVectorMath", operation="SUBTRACT")
    sub.inputs[1].default_value = (0.5, 0.5, 0.0)
    b.link(uv.outputs[0], sub.inputs[0])
    ln = b.n("ShaderNodeVectorMath", operation="LENGTH")
    b.link(sub.outputs[0], ln.inputs[0])
    ramp = b.n("ShaderNodeValToRGB")
    cr = ramp.color_ramp
    il = _srgb(tuple(min(1.0, x * 1.35) for x in iris))
    idk = _srgb(tuple(x * 0.55 for x in iris))
    stops = [(0.0, (0.01, 0.01, 0.012, 1)), (0.112, (0.01, 0.01, 0.012, 1)), (0.128, idk), (0.17, il),
             (0.235, _srgb(iris)), (0.258, (0.03, 0.025, 0.02, 1)), (0.282, _srgb((0.95, 0.93, 0.91))),
             (0.46, _srgb((0.93, 0.88, 0.86))), (1.0, _srgb((0.85, 0.72, 0.70)))]
    cr.elements[0].position, cr.elements[0].color = stops[0]
    cr.elements[1].position, cr.elements[1].color = stops[-1]
    for pos, c in stops[1:-1]:
        e = cr.elements.new(pos)
        e.color = c
    b.link(ln.outputs["Value"], ramp.inputs[0])
    colour = ramp.outputs[0]
    if dead:
        # X: two crossed bars over the iris area
        sep = b.n("ShaderNodeSeparateXYZ")
        b.link(sub.outputs[0], sep.inputs[0])
        d1 = b.math("ABSOLUTE", b.math("SUBTRACT", sep.outputs[0], sep.outputs[1]))
        d2 = b.math("ABSOLUTE", b.math("ADD", sep.outputs[0], sep.outputs[1]))
        bar = b.math("MINIMUM", d1, d2)
        on = b.math("LESS_THAN", bar, 0.045)
        inr = b.math("LESS_THAN", ln.outputs["Value"], 0.25)
        x = b.math("MULTIPLY", on, inr)
        white = b.mix(b.math("LESS_THAN", ln.outputs["Value"], 0.27), colour, _srgb((0.95, 0.93, 0.91)))
        colour = b.mix(x, white, (0.02, 0.02, 0.02))
    b.link(colour, b.bsdf.inputs["Base Color"])
    b.inp("Roughness", 0.25)
    b.inp("Coat Weight", 1.0)
    b.inp("Coat Roughness", 0.02)
    b.inp("Specular IOR Level", 0.5)


def glass(mat, colour, alpha=0.55):
    """Tinted lens; dark colours read as opaque sunglasses (alpha raised)."""
    if max(colour[:3]) < 0.2:
        alpha = 0.95
    b = NB(mat)
    b.inp("Base Color", _srgb(colour))
    b.inp("Roughness", 0.05)
    b.inp("Transmission Weight", 0.6)
    b.inp("Coat Weight", 1.0)
    b.inp("Alpha", alpha)
    try:
        mat.surface_render_method = "BLENDED"
    except Exception:
        pass


def camo(colours, scale=6.0, kind="woodland", seed=0.0):
    """Pattern builder for fabric(pattern=...): woodland blobs or pixelated digital camo."""
    def build(b, vec):
        v = vec
        if kind == "digital":
            sc = b.n("ShaderNodeVectorMath", operation="SCALE")
            sc.inputs[3].default_value = scale * 6.0
            b.link(vec, sc.inputs[0])
            sn = b.n("ShaderNodeVectorMath", operation="FLOOR")
            b.link(sc.outputs[0], sn.inputs[0])
            dv = b.n("ShaderNodeVectorMath", operation="SCALE")
            dv.inputs[3].default_value = 1.0 / (scale * 6.0)
            b.link(sn.outputs[0], dv.inputs[0])
            v = dv.outputs[0]
        nz = b.n("ShaderNodeTexNoise")
        try:
            nz.noise_dimensions = "4D"
        except Exception:
            pass
        nz.inputs["Scale"].default_value = scale
        nz.inputs["Detail"].default_value = 2.0
        nz.inputs["Roughness"].default_value = 0.5
        if "W" in nz.inputs:
            nz.inputs["W"].default_value = seed
        b.link(v, nz.inputs["Vector"])
        ramp = b.n("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = "CONSTANT"
        n = len(colours)
        els = ramp.color_ramp.elements
        els[0].position, els[0].color = 0.0, _srgb(colours[0])
        els[1].position, els[1].color = 0.40, _srgb(colours[1 % n])
        for i in range(2, n):
            e = els.new(0.40 + (i - 1) * 0.12)
            e.color = _srgb(colours[i])
        b.link(nz.outputs[0], ramp.inputs[0])
        return ramp.outputs[0]
    return build


def stripes(colour_a, colour_b, width=0.02, axis=2):
    """Pattern builder: alternating stripes along a world axis (checks with two calls are not needed)."""
    def build(b, vec):
        sep = b.n("ShaderNodeSeparateXYZ")
        b.link(vec, sep.inputs[0])
        s = b.math("MULTIPLY", sep.outputs[axis], 1.0 / width)
        f = b.math("FRACT", s)
        on = b.math("LESS_THAN", f, 0.5)
        return b.mix(on, _srgb(colour_a), _srgb(colour_b))
    return build


def checks(colour_a, colour_b, size=0.012):
    def build(b, vec):
        t = b.n("ShaderNodeTexChecker")
        t.inputs["Scale"].default_value = 1.0 / size
        t.inputs["Color1"].default_value = _srgb(colour_a)
        t.inputs["Color2"].default_value = _srgb(colour_b)
        b.link(vec, t.inputs["Vector"])
        return t.outputs["Color"]
    return build
