# Author : Baptiste Vu Liem
"""Builds the vegetation texture work set from the Poly Haven cache (system Python: Pillow, numpy, scipy).

    python Tools/Blender/vegetation/prep_textures.py <cache_dir> <work_dir>

* Leaf / twig / frond / grass ATLASES are composited in 2D from real Poly Haven leaf sprites (each
  sprite is cut out of a Poly Haven leaf atlas through its alpha, rotated with its normal map and
  pasted along painted stems / rachis). Every atlas cell has its base at the bottom centre and grows
  upward; <work>/atlas/<name>.json lists the cells in UV space for the Blender builders.
* BARK / snow / rock textures are downscaled to 1024 (birch bark is derived from japanese_sycamore).
Outputs: <work>/atlas/<name>_diff.png (RGBA), _nor_gl.jpg, _arm.jpg, <name>.json; <work>/tex/<id>_*.jpg.
"""
import json
import math
import os
import random
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

SS = 2  # supersampling of the composites
HIRES = {"ceiba", "jungle_a", "jungle_b", "acacia", "birch", "fir", "coconut", "royal", "date", "banana", "treefern"}


# ------------------------------------------------------------------ image io
def _load(path, mode):
    im = Image.open(path)
    return np.asarray(im.convert(mode), dtype=np.float32) / 255.0


def _save_rgb(arr, path, q=92):
    Image.fromarray(np.clip(arr * 255 + 0.5, 0, 255).astype(np.uint8), "RGB").save(path, quality=q)


def _save_rgba(rgb, a, path):
    im = np.dstack([rgb, a[..., None]])
    Image.fromarray(np.clip(im * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA").save(path, optimize=True)


def _resize(arr, w, h):
    if arr.ndim == 2:
        return np.asarray(Image.fromarray(arr.astype(np.float32), "F").resize((w, h), Image.LANCZOS))
    return np.dstack([_resize(arr[..., c], w, h) for c in range(arr.shape[2])])


def lum(rgb):
    return rgb[..., 0] * 0.3 + rgb[..., 1] * 0.59 + rgb[..., 2] * 0.11


def tint(rgb, mul=(1, 1, 1), sat=1.0, val=1.0):
    l = lum(rgb)[..., None]
    out = l + (rgb - l) * sat
    return np.clip(out * np.array(mul, np.float32) * val, 0, 1)


# ------------------------------------------------------------------ sources & sprites
class Source:
    def __init__(self, cache, asset, part=""):
        p = os.path.join(cache, asset, asset + ("_" + part if part else ""))
        self.rgb = _load(p + "_diff.jpg", "RGB")
        self.a = _load(p + "_alpha.png", "L") if os.path.exists(p + "_alpha.png") else np.ones(self.rgb.shape[:2], np.float32)
        n = _load(p + "_nor_gl.jpg", "RGB") * 2 - 1
        self.n = n
        self.rough = _load(p + "_arm.jpg", "RGB")[..., 1] if os.path.exists(p + "_arm.jpg") else np.full(self.a.shape, 0.6, np.float32)
        m = ndimage.binary_dilation(self.a > 0.5, iterations=6)
        self.lab, _ = ndimage.label(m)
        self.objs = ndimage.find_objects(self.lab)

    def sprite(self, cx, cy, mode="up", tint_args=None):
        H, W = self.a.shape
        best, bd = None, 1e9
        for i, s in enumerate(self.objs):
            if s is None:
                continue
            y0, y1, x0, x1 = s[0].start, s[0].stop, s[1].start, s[1].stop
            if (x1 - x0) * (y1 - y0) < 2000:
                continue
            d = ((x0 + x1) / 2 / W - cx) ** 2 + ((y0 + y1) / 2 / H - cy) ** 2
            if d < bd:
                best, bd = i, d
        s = self.objs[best]
        sl = (slice(max(0, s[0].start - 2), min(H, s[0].stop + 2)), slice(max(0, s[1].start - 2), min(W, s[1].stop + 2)))
        mask = (self.lab[sl] == best + 1)
        a = self.a[sl] * mask
        # drop stray UV fragments that the dilation glued to the sprite (keep pieces >= 4 % of the largest)
        lab2, n2 = ndimage.label(a > 0.5)
        if n2 > 1:
            sizes = ndimage.sum(np.ones_like(a), lab2, index=np.arange(1, n2 + 1))
            keep = np.isin(lab2, 1 + np.nonzero(sizes >= sizes.max() * 0.04)[0])
            keep = ndimage.binary_dilation(keep, iterations=2)
            a = a * keep
        rgb = self.rgb[sl].copy()
        # the scans have a dark fringe on the alpha edge: pull colour from 3 px inside the leaf
        inner = ndimage.binary_erosion(a > 0.9, iterations=3)
        if inner.any():
            _, (iy, ix) = ndimage.distance_transform_edt(~inner, return_indices=True)
            edge = ~inner
            rgb[edge] = rgb[iy[edge], ix[edge]]
        if tint_args:
            rgb = tint(rgb, **tint_args)
        return Sprite(rgb, a, self.n[sl], self.rough[sl], mode)


class Sprite:
    """Cut-out leaf: colour, alpha, tangent normal (x right, y up), roughness, base and tip (pixels, x right / y down)."""

    def __init__(self, rgb, a, n, rough, mode):
        self.rgb, self.a, self.n, self.rough = rgb, a, n, rough
        h, w = a.shape
        ys, xs = np.nonzero(a > 0.5)
        if mode == "up":  # stem at the bottom of the source cell
            self.base = np.array([xs[ys > ys.max() - 6].mean(), ys.max()], np.float32)
            self.tip = np.array([xs[ys < ys.min() + 6].mean(), ys.min()], np.float32)
        elif mode == "radial":
            c = np.array([xs.mean(), ys.mean()], np.float32)
            r = max(xs.max() - xs.min(), ys.max() - ys.min()) / 2
            self.base, self.tip = c + np.array([0, r]), c - np.array([0, r])
        else:  # PCA axis; 'leaf' = thin end is the base (petiole), 'blade' = thick end is the base
            pts = np.stack([xs, ys], 1).astype(np.float32)
            c = pts.mean(0)
            u, s_, vt = np.linalg.svd(pts - c, full_matrices=False)
            ax = vt[0]
            t = (pts - c) @ ax
            lo, hi = t.min(), t.max()
            span = hi - lo
            m_lo = (t < lo + span * 0.12).sum()
            m_hi = (t > hi - span * 0.12).sum()
            thin_is_lo = m_lo < m_hi
            base_lo = thin_is_lo if mode == "leaf" else not thin_is_lo
            p_lo, p_hi = c + ax * lo, c + ax * hi
            self.base, self.tip = (p_lo, p_hi) if base_lo else (p_hi, p_lo)
        self.length = float(np.linalg.norm(self.tip - self.base))
        ax = (self.tip - self.base) / max(self.length, 1e-3)
        perp = (xs - self.base[0]) * -ax[1] + (ys - self.base[1]) * ax[0]
        self.width = float(perp.max() - perp.min()) if len(xs) else 1.0


def cut(cache, asset, part_or_coords, coords=None, mode="up", tint_args=None):
    """Loads a Poly Haven atlas, cuts the sprites nearest to `coords` (fractions) and frees the atlas."""
    if coords is None:
        part, coords = "", part_or_coords
    else:
        part = part_or_coords
    src = Source(cache, asset, part)
    out = [src.sprite(cx, cy, mode=mode, tint_args=tint_args) for cx, cy in coords]
    del src
    return out


# ------------------------------------------------------------------ canvas
class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.rgb = np.zeros((h, w, 3), np.float32)
        self.a = np.zeros((h, w), np.float32)
        self.n = np.zeros((h, w, 3), np.float32)
        self.n[..., 2] = 1
        self.rough = np.full((h, w), 0.6, np.float32)
        self.ao = np.ones((h, w), np.float32)

    def _over(self, region, rgb, a, n, rough, shadow=0.0):
        y0, y1, x0, x1 = region
        da = self.a[y0:y1, x0:x1]
        if shadow > 0:
            sh = ndimage.gaussian_filter(a, 3.0 * SS) * shadow
            self.rgb[y0:y1, x0:x1] *= (1 - sh)[..., None]
            self.ao[y0:y1, x0:x1] *= (1 - sh * 0.8)
        oa = a + da * (1 - a)
        wsrc = np.where(oa > 1e-5, a / np.maximum(oa, 1e-5), 0)[..., None]
        self.rgb[y0:y1, x0:x1] = rgb * wsrc + self.rgb[y0:y1, x0:x1] * (1 - wsrc)
        nn = n * wsrc + self.n[y0:y1, x0:x1] * (1 - wsrc)
        nn /= np.maximum(np.linalg.norm(nn, axis=2, keepdims=True), 1e-5)
        self.n[y0:y1, x0:x1] = nn
        self.rough[y0:y1, x0:x1] = rough * wsrc[..., 0] + self.rough[y0:y1, x0:x1] * (1 - wsrc[..., 0])
        self.ao[y0:y1, x0:x1] = self.ao[y0:y1, x0:x1] * (1 - wsrc[..., 0]) + wsrc[..., 0]
        self.a[y0:y1, x0:x1] = oa

    def paste(self, sp, base, ang, length, width_scale=1.0, flip=False, shadow=0.25, mul=None, bend=0.0):
        """Paste sprite with its base at `base` (px), pointing at angle `ang` (radians, 0 = up, + = clockwise)."""
        a_s = (sp.tip - sp.base) / max(sp.length, 1e-3)
        b_s = np.array([-a_s[1], a_s[0]])
        a_c = np.array([math.sin(ang), -math.cos(ang)])
        b_c = np.array([-a_c[1], a_c[0]]) * (-1 if flip else 1)
        sl = length / sp.length
        sw = sl * width_scale
        hh, ww = sp.a.shape
        corners = np.array([[0, 0], [ww, 0], [0, hh], [ww, hh]], np.float32) - sp.base
        cc = [base + (c @ a_s) * sl * a_c + (c @ b_s) * sw * b_c for c in corners]
        cc = np.array(cc)
        x0, y0 = np.floor(cc.min(0) - 2 - abs(bend) * length).astype(int)
        x1, y1 = np.ceil(cc.max(0) + 2 + abs(bend) * length).astype(int)
        x0, y0, x1, y1 = max(0, x0), max(0, y0), min(self.w, x1), min(self.h, y1)
        if x1 <= x0 or y1 <= y0:
            return
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        rx, ry = xx - base[0], yy - base[1]
        l = rx * a_c[0] + ry * a_c[1]
        wv = rx * b_c[0] + ry * b_c[1]
        if bend:  # curve the sprite sideways (quadratic along its length)
            wv = wv - bend * (l ** 2) / max(length, 1)
        sx = sp.base[0] + l / sl * a_s[0] + wv / sw * b_s[0]
        sy = sp.base[1] + l / sl * a_s[1] + wv / sw * b_s[1]
        coords = [sy, sx]
        a = ndimage.map_coordinates(sp.a, coords, order=1, mode="constant", cval=0)
        if a.max() <= 0.01:
            return
        rgb = np.dstack([ndimage.map_coordinates(sp.rgb[..., c], coords, order=1, mode="nearest") for c in range(3)])
        if mul is not None:
            rgb = np.clip(rgb * np.array(mul, np.float32), 0, 1)
        nx = ndimage.map_coordinates(sp.n[..., 0], coords, order=1, mode="nearest")
        ny = -ndimage.map_coordinates(sp.n[..., 1], coords, order=1, mode="nearest")  # to image-down coords
        nz = ndimage.map_coordinates(sp.n[..., 2], coords, order=1, mode="nearest")
        ca, cb = nx * a_s[0] + ny * a_s[1], nx * b_s[0] + ny * b_s[1]
        ix, iy = ca * a_c[0] + cb * b_c[0], ca * a_c[1] + cb * b_c[1]
        n = np.dstack([ix, -iy, nz])
        rough = ndimage.map_coordinates(sp.rough, coords, order=1, mode="nearest")
        self._over((y0, y1, x0, x1), rgb, a, n, rough, shadow)

    def stem(self, pts, w0, w1, color, rough=0.75, shadow=0.15):
        """Tapered cylindrical stem along a polyline (pixels)."""
        pts = np.asarray(pts, np.float32)
        nseg = len(pts) - 1
        rng = np.random.default_rng(len(pts))
        for i in range(nseg):
            p0, p1 = pts[i], pts[i + 1]
            wa = w0 + (w1 - w0) * i / nseg
            wb = w0 + (w1 - w0) * (i + 1) / nseg
            wm = max(wa, wb) + 2
            x0, y0 = np.floor(np.minimum(p0, p1) - wm).astype(int)
            x1, y1 = np.ceil(np.maximum(p0, p1) + wm).astype(int)
            x0, y0, x1, y1 = max(0, x0), max(0, y0), min(self.w, x1), min(self.h, y1)
            if x1 <= x0 or y1 <= y0:
                continue
            yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
            d = p1 - p0
            L2 = max(float(d @ d), 1e-6)
            t = np.clip(((xx - p0[0]) * d[0] + (yy - p0[1]) * d[1]) / L2, 0, 1)
            cx, cy = p0[0] + t * d[0], p0[1] + t * d[1]
            w = wa + (wb - wa) * t
            perp = np.array([-d[1], d[0]]) / math.sqrt(L2)
            sd = (xx - cx) * perp[0] + (yy - cy) * perp[1]
            dist = np.hypot(xx - cx, yy - cy)
            a = np.clip(w - dist + 0.5, 0, 1)
            s = np.clip(sd / np.maximum(w, 0.5), -1, 1)
            nz = np.sqrt(np.clip(1 - s * s, 0, 1))
            n = np.dstack([s * perp[0], -s * perp[1], nz])
            shade = (0.75 + 0.25 * nz)[..., None] * (0.93 + 0.07 * rng.random())
            rgb = np.ones(a.shape + (3,), np.float32) * np.array(color, np.float32) * shade
            self._over((y0, y1, x0, x1), rgb, a, n, np.full(a.shape, rough, np.float32), shadow if i % 3 == 0 else 0)


def curve(p0, ang, length, bend, n=16, wobble=0.0, rng=None):
    """Polyline from p0 heading `ang` (0 = up, clockwise +) bending by `bend` radians over its length."""
    pts = [np.array(p0, np.float32)]
    for i in range(n):
        t = (i + 0.5) / n
        a = ang + bend * t + (rng.normal(0, wobble) if rng is not None and wobble else 0)
        pts.append(pts[-1] + np.array([math.sin(a), -math.cos(a)]) * length / n)
    return pts


def along(pts, t):
    pts = np.asarray(pts)
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    cum = np.concatenate([[0], np.cumsum(seg)])
    L = cum[-1] * t
    i = min(np.searchsorted(cum, L) - 1, len(seg) - 1)
    i = max(i, 0)
    f = (L - cum[i]) / max(seg[i], 1e-6)
    p = pts[i] + (pts[i + 1] - pts[i]) * f
    d = pts[i + 1] - pts[i]
    return p, math.atan2(d[0], -d[1])


# ------------------------------------------------------------------ recipes (cell canvas -> composite)
def r_twig(cv, rng, leaves, stem_col, n=7, spread=(0.55, 1.0), size=(0.32, 0.42), stem_w=0.012, droop=0.0,
           mul_var=0.12, terminal=True, length=0.92, start=0.12, base_ang=0.0, base=None, wscale=1.0):
    """Leafy twig: stem from base growing up, alternate leaves, terminal leaf."""
    H = cv.h
    base = np.array(base if base is not None else (cv.w / 2, H - 2), np.float32)
    L = H * length
    pts = curve(base, base_ang, L * (0.85 if terminal else 1.0), rng.uniform(-0.25, 0.25), wobble=0.04, rng=rng)
    cv.stem(pts, H * stem_w, H * stem_w * 0.4, stem_col)
    side = 1 if rng.random() < 0.5 else -1
    for i in range(n):
        t = start + (1 - start) * (i + rng.uniform(0, 0.6)) / n
        p, a = along(pts, min(t, 1))
        side = -side
        ang = a + side * rng.uniform(*spread) + droop * side
        ln = H * rng.uniform(*size) * (1.05 - 0.35 * t)
        m = 1 + rng.uniform(-mul_var, mul_var)
        cv.paste(leaves[rng.integers(len(leaves))], p, ang, ln, wscale * rng.uniform(0.9, 1.1), flip=rng.random() < 0.5,
                 mul=(m * rng.uniform(0.95, 1.05), m, m * rng.uniform(0.9, 1.05)), bend=rng.uniform(-0.15, 0.15))
    if terminal:
        p, a = along(pts, 1.0)
        cv.paste(leaves[rng.integers(len(leaves))], p, a + rng.uniform(-0.2, 0.2), H * size[1] * 0.85, wscale, flip=rng.random() < 0.5)


def r_cluster(cv, rng, leaves, stem_col, twigs=4, fan=1.1, **kw):
    """Branch end: several leafy twigs fanning out from the base."""
    H, W = cv.h, cv.w
    base = np.array((W / 2, H - 2), np.float32)
    main = curve(base, rng.uniform(-0.1, 0.1), H * 0.45, rng.uniform(-0.2, 0.2), rng=rng)
    cv.stem(main, H * 0.016, H * 0.009, stem_col)
    for k in range(twigs):
        t = 0.35 + 0.65 * k / max(twigs - 1, 1)
        p, a = along(main, t)
        ang = (k / max(twigs - 1, 1) - 0.5) * 2 * fan * 0.6 + rng.uniform(-0.15, 0.15)
        r_twig(cv, rng, leaves, stem_col, base=p, base_ang=ang, length=rng.uniform(0.5, 0.62), **kw)


def r_fir_branch(cv, rng, sprays, stem_col, n=9, size=(0.3, 0.4)):
    H = cv.h
    base = np.array((cv.w / 2, H - 2), np.float32)
    pts = curve(base, 0, H * 0.78, rng.uniform(-0.15, 0.15), rng=rng)
    cv.stem(pts, H * 0.010, H * 0.004, stem_col)
    side = 1
    for i in range(n):
        t = 0.12 + 0.88 * (i + rng.uniform(0, 0.5)) / n
        p, a = along(pts, min(t, 1))
        side = -side
        cv.paste(sprays[rng.integers(len(sprays))], p, a + side * rng.uniform(0.6, 1.0), H * rng.uniform(*size) * (1.15 - 0.45 * t),
                 rng.uniform(0.85, 1.1), flip=rng.random() < 0.5, shadow=0.2)
    p, a = along(pts, 1.0)
    cv.paste(sprays[rng.integers(len(sprays))], p - np.array([math.sin(a), -math.cos(a)]) * H * 0.05, a, H * 0.27, 1.0, shadow=0.2)


def r_frond(cv, rng, leaflets, rachis_col, pairs=34, angle=(0.9, 1.1), max_len=0.36, width=1.0, droop=0.15,
            mul=(1, 1, 1), start=0.08, bare=0.06, ragged=0.0, rachis_w=0.012):
    """Pinnate palm frond: rachis base->tip, leaflets on both sides (length profile peaks at ~40 %)."""
    H, W = cv.h, cv.w
    base = np.array((W / 2, H - 2), np.float32)
    pts = curve(base, 0, H * 0.97, rng.uniform(-0.05, 0.05), rng=rng)
    for i in range(pairs * 2):
        t = start + bare + (1 - start - bare) * (i // 2 + rng.uniform(0, 0.5)) / pairs
        if t > 0.995:
            continue
        p, a = along(pts, t)
        side = 1 if i % 2 == 0 else -1
        prof = math.sin(math.pi * min(1, (t - start) / (1 - start) * 0.85 + 0.12)) ** 0.8
        ln = H * max_len * prof * rng.uniform(0.85, 1.1)
        if ragged and rng.random() < ragged:
            ln *= rng.uniform(0.3, 0.7)
        ang = a + side * rng.uniform(*angle) * (1 - 0.35 * t) + side * droop
        m = rng.uniform(0.88, 1.08)
        cv.paste(leaflets[rng.integers(len(leaflets))], p, ang, ln, width * rng.uniform(0.8, 1.1), flip=rng.random() < 0.5,
                 shadow=0.12, mul=(mul[0] * m, mul[1] * m, mul[2] * m), bend=side * rng.uniform(0.02, 0.1))
    cv.stem(pts, H * rachis_w, H * rachis_w * 0.25, rachis_col, rough=0.5, shadow=0.1)


def r_banana(cv, rng, leaf, tears=10, mul=(1, 1, 1), stalk=0.1):
    """Big paddle leaf: a real leaf sprite stretched to banana proportions, midrib, lateral tears."""
    H, W = cv.h, cv.w
    base = np.array((W / 2, H - 2), np.float32)
    cv.stem([base, base + np.array([0, -H * stalk])], W * 0.035, W * 0.03, (0.45, 0.55, 0.25), rough=0.45, shadow=0)
    ln = H * (0.97 - stalk)
    cv.paste(leaf, base + np.array([0, -H * stalk * 0.8]), 0, ln, W * 0.96 / (leaf.width * ln / leaf.length), shadow=0, mul=mul)
    # tears: thin transparent slits from the margin toward the midrib, following the lateral veins
    y0 = H * (1 - stalk)
    for i in range(tears):
        y = rng.uniform(H * 0.08, y0 - H * 0.05)
        side = -1 if rng.random() < 0.5 else 1
        x_edge = W / 2 + side * W * 0.5
        depth = rng.uniform(0.35, 0.95) * W * 0.47
        x_in = W / 2 + side * (W * 0.5 - depth)
        slope = rng.uniform(0.25, 0.45) * H / W  # veins run up-and-out
        yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
        lo, hi = min(x_in, x_edge), max(x_in, x_edge)
        line_y = y - (np.abs(xx - W / 2) - abs(x_in - W / 2)) * slope * 0.3
        cut = (xx >= lo) & (xx <= hi) & (np.abs(yy - line_y) < SS * rng.uniform(1.0, 2.2))
        cv.a[cut] = 0


def r_fernfrond(cv, rng, fronds, rachis_col, pairs=16, max_len=0.42, angle=(1.0, 1.25), mul=(1, 1, 1)):
    """Bipinnate tree-fern frond: real fern fronds used as the pinnae of a long rachis."""
    r_frond(cv, rng, fronds, rachis_col, pairs=pairs, angle=angle, max_len=max_len, width=0.9, droop=0.05, mul=mul,
            start=0.05, bare=0.1, rachis_w=0.01)


def r_single(cv, rng, sprites, length=0.95, mul=(1, 1, 1), wscale=1.0, idx=None):
    H = cv.h
    sp = sprites[idx if idx is not None else rng.integers(len(sprites))]
    ln = min(H * length, cv.w * 0.94 * sp.length / max(sp.width * wscale, 1))  # keep the sprite inside the cell
    cv.paste(sp, np.array((cv.w / 2, H - 2), np.float32), 0, ln, wscale, shadow=0, mul=mul)


def r_grass(cv, rng, blades, n=14, height=(0.6, 0.98), spread=0.35, width=1.0, heads=None, n_heads=0, mul_fn=None,
            base_w=0.35):
    """Grass clump: blades rooted along the bottom, fanning out; optional seed heads on thin stalks."""
    H, W = cv.h, cv.w
    items = []
    for i in range(n):
        x = W / 2 + rng.uniform(-0.5, 0.5) * W * base_w
        ang = (x - W / 2) / (W * 0.5) * spread + rng.normal(0, spread * 0.4)
        items.append((rng.random(), x, ang))
    for k in range(n_heads):
        items.append((rng.random() * 0.5, None, k))
    for depth, x, ang in sorted(items, key=lambda it: it[0]):
        if x is None and heads:
            x = W / 2 + rng.uniform(-0.3, 0.3) * W * base_w
            a = rng.normal(0, spread * 0.5)
            pts = curve((x, H - 1), a, H * rng.uniform(*height) * 0.95, rng.uniform(-0.2, 0.2), rng=rng)
            cv.stem(pts, SS * 1.3, SS * 0.7, (0.6, 0.58, 0.38), shadow=0)
            panicle(cv, rng, pts, heads)
            continue
        m = mul_fn(rng) if mul_fn else (1, 1, 1)
        cv.paste(blades[rng.integers(len(blades))], np.array((x, H - 1), np.float32), ang, H * rng.uniform(*height),
                 width * rng.uniform(0.8, 1.2), flip=rng.random() < 0.5, shadow=0.12, mul=m, bend=rng.uniform(-0.12, 0.12))


def panicle(cv, rng, pts, col):
    """Seed head on the top 25 % of a stalk: short side branches carrying spikelets."""
    H = cv.h
    col = np.array(col if isinstance(col, (tuple, list)) else (0.62, 0.55, 0.38), np.float32)
    for k in range(14):
        t = 0.74 + 0.26 * k / 14
        p, a = along(pts, t)
        side = 1 if k % 2 else -1
        ln = H * rng.uniform(0.025, 0.05) * (1.2 - t * 0.6)
        br = curve(p, a + side * rng.uniform(0.25, 0.55), ln, side * 0.2, n=4)
        cv.stem(br, SS * 0.6, SS * 0.4, col * 0.9, shadow=0)
        for j in range(3):
            q_, a3 = along(br, 0.35 + 0.3 * j)
            sp = curve(q_, a3 + rng.uniform(-0.3, 0.3), H * 0.012, 0, n=2)
            cv.stem(sp, SS * 1.5, SS * 0.7, col * rng.uniform(0.85, 1.1), shadow=0)


def r_bare(cv, rng, twigs, n=3, mul=(1, 1, 1)):
    H, W = cv.h, cv.w
    for k in range(n):
        cv.paste(twigs[rng.integers(len(twigs))], np.array((W / 2 + rng.uniform(-0.05, 0.05) * W, H - 2), np.float32),
                 rng.uniform(-0.5, 0.5), H * rng.uniform(0.7, 0.97), 1.0, flip=rng.random() < 0.5, shadow=0.1, mul=mul)


# ------------------------------------------------------------------ atlas
def build_atlas(name, size, cells, out_dir, seed=1, snow=False, post=None):
    """cells: list of (x, y, w, h (fractions), kind, recipe, kwargs). Writes textures + json.
    Composites are painted at 1024 x SS; names in HIRES are written at 2048 (canopies seen up close)."""
    out = (2048, 2048) if name in HIRES else size
    W, H = size[0] * SS, size[1] * SS
    at = Canvas(W, H)
    rng = np.random.default_rng(seed)
    meta = []
    for (x, y, w, h, kind, fn, kw) in cells:
        cw, ch = int(w * W), int(h * H)
        cv = Canvas(cw, ch)
        fn(cv, rng, **kw)
        X, Y = int(x * W), int(y * H)
        at.rgb[Y:Y + ch, X:X + cw] = cv.rgb
        at.a[Y:Y + ch, X:X + cw] = cv.a
        at.n[Y:Y + ch, X:X + cw] = cv.n
        at.rough[Y:Y + ch, X:X + cw] = cv.rough
        at.ao[Y:Y + ch, X:X + cw] = cv.ao
        ys, xs = np.nonzero(cv.a > 0.3)
        # tight horizontal / top extent (fractions of the cell) so cards can be trimmed
        ext = [float(xs.min() / cw), float(1 - ys.min() / ch), float(xs.max() / cw)] if len(xs) else [0, 1, 1]
        meta.append({"kind": kind, "rect": [x, 1 - y - h, x + w, 1 - y], "aspect": cw / ch, "extent": ext})
    if post:
        post(at)
    _finish(name, at, out, out_dir, meta)
    if snow:
        sn = snow_layer(at, rng)
        _finish(name + "_snow", sn, out, out_dir, meta)
    return at


def snow_layer(at, rng):
    """Snow clumps resting on the cells: dilated foliage alpha x low-frequency blob noise."""
    s = Canvas(at.w, at.h)
    base = ndimage.binary_dilation(at.a > 0.4, iterations=3 * SS).astype(np.float32)
    base = ndimage.gaussian_filter(base, 2 * SS)
    noise = ndimage.gaussian_filter(rng.random((at.h, at.w)).astype(np.float32), 6 * SS)
    noise = (noise - noise.mean()) / (noise.std() + 1e-6)
    fine = ndimage.gaussian_filter(rng.random((at.h, at.w)).astype(np.float32), 1.5 * SS)
    fine = (fine - fine.mean()) / (fine.std() + 1e-6)
    m = base * 1.6 + noise * 0.6 + fine * 0.18 - 1.6
    a = np.clip(m * 4, 0, 1)
    s.a = a
    h = ndimage.gaussian_filter(a, 2.5 * SS)
    gy, gx = np.gradient(h)
    n = np.dstack([-gx * 6, gy * 6, np.ones_like(h)])
    s.n = n / np.linalg.norm(n, axis=2, keepdims=True)
    shade = 0.82 + 0.18 * np.clip(h, 0, 1)
    s.rgb = np.dstack([0.86 * shade, 0.9 * shade, 0.97 * shade]) + fine[..., None] * 0.015
    s.rough = np.full(a.shape, 0.55, np.float32)
    s.ao = 0.85 + 0.15 * np.clip(h, 0, 1)
    return s


def _finish(name, cv, size, out_dir, meta):
    w, h = size
    a = _resize(cv.a, w, h)
    rgb = _resize(cv.rgb * cv.a[..., None], w, h) / np.maximum(a[..., None], 1e-4)  # premultiplied downscale
    n = _resize(cv.n, w, h)
    rough = _resize(cv.rough, w, h)
    ao = _resize(cv.ao, w, h)
    a = np.clip(a, 0, 1)
    # bleed colours / normals into the transparent area so mips and cutout edges stay clean
    solid = a > 0.35
    if solid.any():
        _, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
        rgb = rgb[iy, ix]
        n = n[iy, ix]
        rough = rough[iy, ix]
        ao = np.where(solid, ao, 1.0)
    n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-5)
    n[..., 2] = np.abs(n[..., 2])
    a = np.clip((a - 0.5) * 1.6 + 0.5, 0, 1)  # crisper mask for alpha test at 0.5
    os.makedirs(out_dir, exist_ok=True)
    _save_rgba(np.clip(rgb, 0, 1), a, os.path.join(out_dir, name + "_diff.png"))
    _save_rgb(n * 0.5 + 0.5, os.path.join(out_dir, name + "_nor_gl.jpg"))
    _save_rgb(np.dstack([ao, np.clip(rough * 0.6 + 0.4, 0.5, 1), np.zeros_like(ao)]), os.path.join(out_dir, name + "_arm.jpg"))
    with open(os.path.join(out_dir, name + ".json"), "w") as f:
        json.dump({"name": name, "size": [w, h], "cells": meta}, f, indent=1)
    print("atlas", name, len(meta), "cells")


# ------------------------------------------------------------------ tiling textures
def prep_tiling(cache, out_dir, asset, part="", size=1024, name=None, fn=None):
    name = name or asset
    p = os.path.join(cache, asset, asset + ("_" + part if part else ""))
    os.makedirs(out_dir, exist_ok=True)
    for m in ("diff", "nor_gl", "arm"):
        src = p + f"_{m}.jpg"
        im = Image.open(src).convert("RGB").resize((size, size), Image.LANCZOS)
        if fn and m in fn:
            arr = fn[m](np.asarray(im, np.float32) / 255.0)
            _save_rgb(arr, os.path.join(out_dir, f"{name}_{m}.jpg"))
        else:
            im.save(os.path.join(out_dir, f"{name}_{m}.jpg"), quality=92)
    print("tex", name)


def birch_diff(rgb):
    """White birch bark from japanese_sycamore: bleached, horizontal dark lenticels and black patches."""
    h, w, _ = rgb.shape
    rng = np.random.default_rng(7)
    l = lum(rgb)
    base = 0.78 + (l - l.mean()) * 0.9
    out = np.dstack([base * 0.97, base * 0.95, base * 0.9])
    mark = np.zeros((h, w), np.float32)
    for _ in range(260):  # lenticels: thin horizontal dashes
        y, x = rng.integers(0, h), rng.integers(0, w)
        ln, th = rng.integers(w // 40, w // 7), rng.integers(1, 4)
        xs = (np.arange(x, x + ln) % w)
        mark[y:y + th, xs] = np.maximum(mark[y:y + th, xs], rng.uniform(0.5, 1.0))
    for _ in range(14):  # black fissured patches
        y, x = rng.integers(0, h), rng.integers(0, w)
        yy, xx = np.ogrid[:h, :w]
        dx = np.minimum(np.abs(xx - x), w - np.abs(xx - x))
        r = rng.uniform(w * 0.02, w * 0.07)
        blob = np.exp(-((dx / (r * 2.2)) ** 2 + ((yy - y) / r) ** 2))
        mark = np.maximum(mark, blob * rng.uniform(0.6, 1.0))
    mark = ndimage.gaussian_filter(mark, 0.8)
    noise = ndimage.gaussian_filter(rng.random((h, w)).astype(np.float32), (1, 6))
    mark = np.clip(mark * (0.7 + noise), 0, 1)
    dark = np.array([0.12, 0.11, 0.1], np.float32)
    return out * (1 - mark[..., None]) + dark * mark[..., None]


# ------------------------------------------------------------------ main
def main():
    cache, work = sys.argv[1], sys.argv[2]
    only = sys.argv[3].split(",") if len(sys.argv) > 3 else None
    A = os.path.join(work, "atlas")
    T = os.path.join(work, "tex")

    def want(n):
        return only is None or n in only

    # --- tiling textures
    tiling = [("chinese_hackberry_bark", ""), ("tree_bark_03", ""), ("japanese_hackberry_bark", ""), ("bark_brown_01", ""),
              ("bark_brown_02", ""), ("palm_tree_bark", ""), ("palm_bark", ""), ("fever_tree_bark", ""),
              ("bark_willow_02", ""), ("eucalyptus_bark", ""), ("pine_bark", ""), ("metasequoia_bark", ""),
              ("boulder_01", ""), ("namaqualand_boulder_04", ""), ("namaqualand_boulder_05", ""), ("dead_tree_trunk", "")]
    if want("tex"):
        for a, p in tiling:
            prep_tiling(cache, T, a, p)
        prep_tiling(cache, T, "fir_tree_01", "bark", name="fir_bark")
        prep_tiling(cache, T, "japanese_sycamore", "", name="birch_bark", fn={"diff": birch_diff})
        prep_tiling(cache, T, "palm_tree_bark", "", name="palm_tree_bark_dark", fn={"diff": lambda c: tint(c, mul=(0.74, 0.68, 0.6), sat=1.1)})

    if only is not None and not any(n != "tex" for n in only):
        return
    island = cut(cache, "island_tree_01", "leaves", [(0.58, 0.18), (0.42, 0.2), (0.25, 0.21), (0.75, 0.22), (0.29, 0.79), (0.49, 0.8), (0.1, 0.81)], mode="up")
    pachira = cut(cache, "pachira_aquatica_01", "leaves", [(0.69, 0.25), (0.25, 0.29), (0.7, 0.68), (0.24, 0.72)], mode="leaf")
    anthur = cut(cache, "anthurium_botany_01", [(0.73, 0.13), (0.24, 0.14), (0.79, 0.33), (0.43, 0.4), (0.35, 0.61), (0.86, 0.69), (0.38, 0.85)], mode="leaf")
    calath = cut(cache, "calathea_orbifolia_01", [(0.67, 0.23), (0.23, 0.24), (0.67, 0.72), (0.23, 0.74)], mode="up")
    jacar = cut(cache, "jacaranda_tree", "leaves", [(0.57, 0.25), (0.22, 0.51), (0.62, 0.79)], mode="leaf")
    sprays = cut(cache, "fir_tree_01", "twig", [(0.31, 0.18), (0.80, 0.20), (0.48, 0.59), (0.80, 0.64), (0.56, 0.33)], mode="up", tint_args=dict(mul=(0.78, 0.9, 0.92), sat=0.9, val=0.82))
    small_sprays = cut(cache, "fir_tree_01", "twig", [(0.56, 0.33), (0.56, 0.16), (0.42, 0.34)], mode="up", tint_args=dict(mul=(0.78, 0.9, 0.92), sat=0.9, val=0.82))
    ferns = cut(cache, "fern_02", [(0.6, 0.26), (0.39, 0.39), (0.16, 0.53), (0.73, 0.55), (0.59, 0.74)], mode="up")
    blades = cut(cache, "grass_medium_02", [(0.38, 0.48), (0.92, 0.51), (0.10, 0.49), (0.75, 0.53), (0.22, 0.57), (0.83, 0.86)], mode="up")
    leaflets = cut(cache, "grass_medium_02", [(0.38, 0.48), (0.92, 0.51)], mode="up") + cut(cache, "grass_medium_02", [(0.53, 0.94)], mode="blade")
    dry_blades = cut(cache, "grass_medium_02", [(0.53, 0.41), (0.59, 0.63), (0.82, 0.44)], mode="up")
    heart = cut(cache, "shrub_03", [(0.16, 0.17), (0.72, 0.2), (0.42, 0.25), (0.17, 0.48), (0.52, 0.49), (0.36, 0.71), (0.61, 0.77), (0.15, 0.79)], mode="leaf")
    olive = cut(cache, "shrub_04", [(0.18, 0.21), (0.56, 0.25), (0.65, 0.48), (0.48, 0.52), (0.21, 0.54), (0.28, 0.78), (0.59, 0.78), (0.43, 0.81)], mode="leaf")
    tiny = cut(cache, "searsia_burchellii", [(0.18, 0.1), (0.47, 0.13), (0.25, 0.29), (0.09, 0.28), (0.57, 0.38)], mode="leaf")
    twigs = cut(cache, "searsia_burchellii", [(0.14, 0.68), (0.37, 0.74), (0.53, 0.82)], mode="leaf")
    tufts = cut(cache, "wild_rooibos_bush", [(0.07, 0.08), (0.25, 0.08), (0.4, 0.08), (0.57, 0.09)], mode="leaf")

    BR = (0.33, 0.25, 0.17)
    GRN_STEM = (0.36, 0.42, 0.2)
    q = lambda x, y, w, h, kind, fn, **kw: (x, y, w, h, kind, fn, kw)  # noqa: E731

    # --- broadleaf canopies: 4 clusters (top half) + 4 twigs (bottom half)
    def canopy(name, leaves, stem, seed, cl_kw, tw_kw, size=(1024, 1024)):
        cells = [q(i * 0.5, 0, 0.5, 0.5, "cluster", r_cluster, leaves=leaves, stem_col=stem, **cl_kw) for i in range(2)]
        cells += [q(i * 0.25, 0.5, 0.25, 0.5, "twig", r_twig, leaves=leaves, stem_col=stem, **tw_kw) for i in range(4)]
        build_atlas(name, size, cells, A, seed)

    if want("ceiba"):
        canopy("ceiba", pachira, BR, 11, dict(twigs=4, n=4, size=(0.28, 0.36), spread=(0.5, 0.9)), dict(n=5, size=(0.3, 0.42)))
    if want("jungle_a"):
        canopy("jungle_a", island, BR, 12, dict(twigs=4, n=6, size=(0.2, 0.28)), dict(n=8, size=(0.22, 0.3)))
    if want("jungle_b"):
        canopy("jungle_b", jacar, (0.3, 0.27, 0.18), 13, dict(twigs=4, n=4, size=(0.35, 0.45), spread=(0.4, 0.8)),
               dict(n=5, size=(0.4, 0.5), spread=(0.4, 0.8)))
    if want("acacia"):
        acac = cut(cache, "jacaranda_tree", "leaves", [(0.57, 0.25), (0.22, 0.51), (0.62, 0.79)], mode="leaf", tint_args=dict(mul=(1.02, 0.97, 0.78), sat=0.75, val=0.85))
        canopy("acacia", acac, (0.25, 0.21, 0.17), 14, dict(twigs=5, n=4, size=(0.24, 0.32), spread=(0.6, 1.1), stem_w=0.008),
               dict(n=6, size=(0.25, 0.35), stem_w=0.008))
    if want("birch"):
        canopy("birch", heart, (0.2, 0.17, 0.14), 15, dict(twigs=4, n=7, size=(0.1, 0.14), stem_w=0.006),
               dict(n=10, size=(0.1, 0.15), stem_w=0.006))
    if want("jbush_b"):
        canopy("jbush_b", island + anthur[:3], GRN_STEM, 16, dict(twigs=3, n=5, size=(0.22, 0.3)), dict(n=6, size=(0.25, 0.33)))
    if want("dshrub_a"):
        canopy("dshrub_a", olive, (0.4, 0.36, 0.3), 17, dict(twigs=5, n=9, size=(0.1, 0.15), stem_w=0.007),
               dict(n=12, size=(0.1, 0.16), stem_w=0.007))
    if want("dshrub_b"):
        cells = [q(i * 0.5, 0, 0.5, 0.5, "cluster", r_bare, twigs=twigs, n=3, mul=(0.95, 0.9, 0.85)) for i in range(2)]
        cells += [q(i * 0.25, 0.5, 0.25, 0.5, "twig", r_twig, leaves=tiny + tufts, stem_col=(0.38, 0.32, 0.26), n=16, size=(0.12, 0.18), stem_w=0.006)
                  for i in range(4)]
        build_atlas("dshrub_b", (1024, 1024), cells, A, 18)
    if want("bare"):
        cells = [q(i * 0.25, 0, 0.25, 0.5, "twig", r_bare, twigs=twigs, n=2, mul=(0.85, 0.85, 0.85)) for i in range(4)]
        cells += [q(i * 0.25, 0.5, 0.25, 0.5, "twig", r_bare, twigs=twigs, n=1, mul=(0.8, 0.8, 0.8)) for i in range(4)]
        build_atlas("bare", (1024, 1024), cells, A, 19)
    # --- understory big leaves (one leaf per card)
    if want("jbush_a"):
        cells = [q(i * 0.25, 0, 0.25, 0.5, "leaf", r_single, sprites=calath, idx=i) for i in range(4)]
        cells += [q(i * 0.25, 0.5, 0.25, 0.5, "leaf", r_single, sprites=anthur, idx=i, wscale=1.0) for i in range(4)]
        build_atlas("jbush_a", (1024, 1024), cells, A, 20)
    # --- conifer branches (+ snow layer)
    if want("fir"):
        cells = [q(i * 0.25, 0, 0.25, 0.62, "branch", r_fir_branch, sprays=sprays, stem_col=(0.3, 0.23, 0.17)) for i in range(4)]
        cells += [q(i * 0.25, 0.62, 0.25, 0.38, "twig", r_single, sprites=sprays, idx=i % len(sprays)) for i in range(4)]
        build_atlas("fir", (1024, 1024), cells, A, 21, snow=True)
    # --- palms
    if want("coconut"):
        cells = [q(i * 0.25, 0, 0.25, 1, "frond", r_frond, leaflets=leaflets, rachis_col=(0.55, 0.53, 0.3), pairs=40, max_len=0.42,
                   angle=(0.75, 0.95), width=0.7, mul=m) for i, m in enumerate([(1, 1, 0.95), (1.05, 1.05, 0.85), (0.95, 1, 0.95), (1.25, 1.1, 0.7)])]
        build_atlas("coconut", (1024, 1024), cells, A, 22)
    if want("royal"):
        cells = [q(i * 0.25, 0, 0.25, 1, "frond", r_frond, leaflets=leaflets, rachis_col=(0.45, 0.48, 0.28), pairs=48, max_len=0.40,
                   angle=(0.6, 0.9), width=0.55, droop=0.3, mul=m) for i, m in enumerate([(0.85, 0.95, 0.85), (0.9, 1, 0.9), (0.8, 0.9, 0.8), (1.1, 1.05, 0.75)])]
        build_atlas("royal", (1024, 1024), cells, A, 23)
    if want("date"):
        cells = [q(i * 0.25, 0, 0.25, 1, "frond", r_frond, leaflets=leaflets, rachis_col=(0.6, 0.55, 0.38), pairs=36, max_len=0.30,
                   angle=(0.45, 0.7), width=0.55, droop=0.0, mul=m, bare=0.18) for i, m in enumerate([(0.85, 0.92, 0.95), (0.9, 0.95, 0.95), (0.8, 0.88, 0.92), (1.15, 1.0, 0.7)])]
        build_atlas("date", (1024, 1024), cells, A, 24)
    if want("banana"):
        cells = [q(i * 0.25, 0, 0.25, 1, "leaf", r_banana, leaf=island[[1, 2, 5, 6][i]], tears=[4, 12, 18, 30][i],
                   mul=[(0.95, 1.1, 0.9), (0.9, 1.1, 0.85), (1, 1.1, 0.9), (1.2, 1.05, 0.6)][i]) for i in range(4)]
        build_atlas("banana", (1024, 1024), cells, A, 25)
    # --- ferns
    if want("fern"):
        cells = [q(i * 0.2, 0, 0.2, 1, "frond", r_single, sprites=ferns, idx=i) for i in range(5)]
        build_atlas("fern", (1024, 1024), cells, A, 26)
    if want("treefern"):
        cells = [q(i * 0.25, 0, 0.25, 1, "frond", r_fernfrond, fronds=ferns, rachis_col=(0.32, 0.28, 0.17),
                   mul=[(1, 1, 1), (0.9, 1, 0.9), (1, 1.05, 0.95), (1.15, 1.0, 0.7)][i]) for i in range(4)]
        build_atlas("treefern", (1024, 1024), cells, A, 27)
    # --- grasses
    green = lambda r: (r.uniform(0.62, 0.8), r.uniform(0.82, 0.95), r.uniform(0.45, 0.6))  # noqa: E731  lush, saturated jungle green
    if want("grass_jungle"):
        cells = [q(i * 0.25, 0, 0.25, 0.5, "clump", r_grass, blades=blades, n=24, height=(0.55, 0.98), width=0.5, mul_fn=green) for i in range(4)]
        cells += [q(i * 0.25, 0.5, 0.25, 0.5, "clump", r_grass, blades=blades, n=16, height=(0.4, 0.8), spread=0.5, width=0.55, mul_fn=green) for i in range(4)]
        build_atlas("grass_jungle", (1024, 1024), cells, A, 30)
    if want("tallgrass_jungle"):
        cells = [q(i * 0.25, 0, 0.25, 1, "clump", r_grass, blades=blades, n=44, height=(0.6, 0.99), spread=0.22, width=0.32, heads=(0.55, 0.5, 0.3), n_heads=4,
                   mul_fn=green) for i in range(4)]
        build_atlas("tallgrass_jungle", (1024, 1024), cells, A, 31)
    dry = lambda r: (r.uniform(1.15, 1.35), r.uniform(1.0, 1.15), r.uniform(0.6, 0.75))  # noqa: E731
    drymix = cut(cache, "grass_medium_02", [(0.38, 0.48), (0.92, 0.51), (0.10, 0.49), (0.75, 0.53), (0.22, 0.57)], mode="up", tint_args=dict(mul=(1.25, 1.05, 0.7), sat=0.6, val=1.05)) + dry_blades
    if want("grass_dry"):
        cells = [q(i * 0.25, 0, 0.25, 0.5, "clump", r_grass, blades=drymix, n=24, height=(0.5, 0.95), width=0.5) for i in range(4)]
        cells += [q(i * 0.25, 0.5, 0.25, 0.5, "clump", r_grass, blades=drymix, n=16, height=(0.4, 0.8), spread=0.5, width=0.55) for i in range(4)]
        build_atlas("grass_dry", (1024, 1024), cells, A, 32)
    if want("tallgrass_dry"):
        reed = cut(cache, "grass_medium_02", [(0.38, 0.48), (0.92, 0.51), (0.10, 0.49)], mode="up", tint_args=dict(mul=(1.15, 1.05, 0.75), sat=0.7))
        cells = [q(i * 0.25, 0, 0.25, 1, "clump", r_grass, blades=reed + dry_blades, n=42, height=(0.65, 0.99), spread=0.15, width=0.3,
                   heads=(0.5, 0.4, 0.3), n_heads=6) for i in range(4)]
        build_atlas("tallgrass_dry", (1024, 1024), cells, A, 33)
    frost = lambda r: (r.uniform(0.8, 0.95), r.uniform(0.85, 0.98), r.uniform(0.75, 0.9))  # noqa: E731

    def frost_post(at, rows=2):  # hoar frost: whitens the upper part of every blade + speckles
        H = at.h
        yy = np.linspace(1, 0, H, dtype=np.float32)[:, None]
        cell_t = np.where(yy > 0.5, (yy - 0.5) * 2, yy * 2) if rows == 2 else yy
        sp = ndimage.gaussian_filter(np.random.default_rng(3).random(at.a.shape).astype(np.float32), 1.0)
        f = np.clip(cell_t * 0.42 + (sp - 0.5) * 0.9 - 0.05, 0, 0.55)[..., None]
        at.rgb = at.rgb * (1 - f) + np.array([0.85, 0.88, 0.92], np.float32) * f
        at.rough = np.clip(at.rough + f[..., 0] * 0.2, 0, 1)

    alp = cut(cache, "grass_medium_02", [(0.38, 0.48), (0.92, 0.51), (0.10, 0.49), (0.75, 0.53), (0.22, 0.57)], mode="up", tint_args=dict(mul=(0.95, 1.0, 0.85), sat=0.7, val=0.78)) + dry_blades[:1]
    if want("grass_alpine"):
        cells = [q(i * 0.25, 0, 0.25, 0.5, "clump", r_grass, blades=alp, n=24, height=(0.5, 0.95), width=0.5, mul_fn=frost) for i in range(4)]
        cells += [q(i * 0.25, 0.5, 0.25, 0.5, "clump", r_grass, blades=alp, n=16, height=(0.4, 0.8), spread=0.5, width=0.55, mul_fn=frost) for i in range(4)]
        build_atlas("grass_alpine", (1024, 1024), cells, A, 34, post=frost_post)
    if want("tallgrass_alpine"):
        cells = [q(i * 0.25, 0, 0.25, 1, "clump", r_grass, blades=alp + dry_blades, n=42, height=(0.6, 0.99), spread=0.2, width=0.32, heads=(0.6, 0.58, 0.5), n_heads=4,
                   mul_fn=frost) for i in range(4)]
        build_atlas("tallgrass_alpine", (1024, 1024), cells, A, 35, post=lambda at: frost_post(at, 1))


if __name__ == "__main__":
    main()
