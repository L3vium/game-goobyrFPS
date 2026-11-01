# Author : Baptiste Vu Liem
"""Texture processing for the character pipeline (numpy, runs inside Blender's Python).

Outputs per material slot (Unity convention of the project):
  <Slot>_diff.png|jpg   sRGB albedo (png when the part needs alpha)
  <Slot>_nor_gl.jpg     OpenGL tangent-space normal map
  <Slot>_arm.jpg        R = ambient occlusion, G = roughness, B = metallic
"""
import os

import bpy
import numpy as np


# ------------------------------------------------------------------------------------- io

def load_rgba(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    # Blender stores rows bottom-up: flip so row 0 is the top (like every image editor)
    return px.reshape(h, w, 4)[::-1].copy()


def save_rgba(arr, path, quality=92):
    h, w = arr.shape[:2]
    if arr.shape[2] == 3:
        arr = np.concatenate([arr, np.ones((h, w, 1), np.float32)], axis=2)
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=True, float_buffer=False)
    img.colorspace_settings.name = "Non-Color" if not path.endswith("_diff.png") and not path.endswith("_diff.jpg") else "sRGB"
    img.pixels.foreach_set(np.ascontiguousarray(np.clip(arr[::-1], 0, 1)).ravel())
    ext = os.path.splitext(path)[1].lower()
    img.file_format = "PNG" if ext == ".png" else "JPEG"
    img.filepath_raw = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    try:
        img.save(filepath=path, quality=quality)
    except TypeError:
        img.save()
    bpy.data.images.remove(img)


def resize(arr, size):
    """Box/bilinear resize with numpy (size = (w, h))."""
    h, w = arr.shape[:2]
    tw, th = size
    if (tw, th) == (w, h):
        return arr
    if w % tw == 0 and h % th == 0:
        fx, fy = w // tw, h // th
        return arr.reshape(th, fy, tw, fx, arr.shape[2]).mean(axis=(1, 3))
    ys = np.linspace(0, h - 1, th)
    xs = np.linspace(0, w - 1, tw)
    y0 = np.floor(ys).astype(int); y1 = np.minimum(y0 + 1, h - 1); fy = (ys - y0)[:, None, None]
    x0 = np.floor(xs).astype(int); x1 = np.minimum(x0 + 1, w - 1); fx = (xs - x0)[None, :, None]
    a = arr[y0][:, x0] * (1 - fx) + arr[y0][:, x1] * fx
    b = arr[y1][:, x0] * (1 - fx) + arr[y1][:, x1] * fx
    return a * (1 - fy) + b * fy


def blur(a, radius):
    """Separable box blur repeated 3x (~gaussian). a: HxW or HxWxC."""
    if radius < 1:
        return a
    r = int(radius)
    out = a.astype(np.float32)
    for _ in range(3):
        for axis in (0, 1):
            c = np.cumsum(np.pad(out, [(r + 1, r) if i == axis else (0, 0) for i in range(out.ndim)], mode="wrap"), axis=axis)
            hi = np.take(c, np.arange(2 * r + 1, c.shape[axis]), axis=axis)
            lo = np.take(c, np.arange(0, c.shape[axis] - 2 * r - 1), axis=axis)
            out = (hi - lo) / (2 * r + 1)
    return out


def lum(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


# ------------------------------------------------------------------------------------- normals

def height_to_normal(height, strength):
    """height: HxW 0..1 -> OpenGL normal map (Y up = green up)."""
    gx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    gy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    nx = -gx * strength
    ny = gy * strength  # row 0 is the top: +v is up, so a height increasing upwards tilts +Y
    nz = np.ones_like(height)
    n = np.stack([nx, ny, nz], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def detail_normal_from_albedo(rgb, strength=2.0, radius=6):
    l = lum(rgb)
    hp = l - blur(l, radius)
    return height_to_normal(hp, strength * (rgb.shape[1] / 1024.0))


# ------------------------------------------------------------------------------------- recolour

def srgb(c):
    return np.array(c[:3], dtype=np.float32)


def recolour_keys(rgb, keys, sigma=0.16, keep_unmatched=True):
    """Soft colour-key transfer.
    keys: list of (source_rgb, target_rgb) in sRGB 0..1. Each pixel is assigned to the source
    colours by a gaussian weight on RGB distance; the albedo detail (luminance ratio) is kept.
    """
    out_acc = np.zeros_like(rgb)
    wsum = np.zeros(rgb.shape[:2], np.float32)
    l = lum(rgb) + 1e-4
    for s, t in keys:
        s = srgb(s)
        t = srgb(t)
        d2 = ((rgb - s) ** 2).sum(-1)
        w = np.exp(-d2 / (2 * sigma * sigma))
        ratio = np.clip(l / max(float(lum(s[None, None, :])[0, 0]), 0.02), 0.0, 3.0)[..., None]
        out_acc += w[..., None] * np.clip(t * ratio, 0, 1)
        wsum += w
    if keep_unmatched:
        w0 = np.full_like(wsum, 0.05)
        out_acc += w0[..., None] * rgb
        wsum += w0
    return out_acc / wsum[..., None]


def tint(rgb, colour, contrast=1.0):
    l = lum(rgb)
    m = max(float(np.median(l[l > 0.02])) if np.any(l > 0.02) else 0.5, 0.02)
    detail = np.clip((l / m) ** contrast, 0, 3)[..., None]
    return np.clip(srgb(colour) * detail, 0, 1)


def _noise(h, w, scale, seed):
    rng = np.random.default_rng(seed)
    n = rng.random((max(2, h // scale), max(2, w // scale))).astype(np.float32)
    n = resize(n[..., None], (w, h))[..., 0]
    return blur(n, max(1, scale // 3))


def camo_pattern(h, w, colours, scale=48, seed=1, kind="woodland"):
    """Return HxWx3 camouflage pattern (sRGB) made of len(colours) flat blobs."""
    f = _noise(h, w, scale, seed) * 0.65 + _noise(h, w, max(4, scale // 3), seed + 7) * 0.35
    if kind == "digital":
        # pixelated blocks
        b = max(4, scale // 6)
        f = resize(resize(f[..., None], (w // b, h // b)), (w, h))[..., 0]
        f = np.repeat(np.repeat(f[::b, ::b], b, 0), b, 1)[:h, :w]
    qs = np.quantile(f, np.linspace(0, 1, len(colours) + 1)[1:-1])
    idx = np.digitize(f, qs)
    pal = np.array([srgb(c) for c in colours], np.float32)
    return pal[idx]


def apply_camo(rgb, colours, scale, seed, kind="woodland", mask=None):
    h, w = rgb.shape[:2]
    pat = camo_pattern(h, w, colours, scale=scale, seed=seed, kind=kind)
    l = lum(rgb)
    m = max(float(np.median(l[l > 0.02])) if np.any(l > 0.02) else 0.5, 0.02)
    detail = np.clip(l / m, 0.25, 2.0)[..., None]
    out = np.clip(pat * detail, 0, 1)
    if mask is not None:
        out = out * mask[..., None] + rgb * (1 - mask[..., None])
    return out


def fill_boxes(rgb, boxes, colour):
    h, w = rgb.shape[:2]
    for (x0, y0, x1, y1) in boxes:
        rgb[int(y0 * h):int(y1 * h), int(x0 * w):int(x1 * w)] = srgb(colour)
    return rgb


def box_mask(h, w, boxes):
    m = np.zeros((h, w), np.float32)
    for (x0, y0, x1, y1) in boxes:
        m[int(y0 * h):int(y1 * h), int(x0 * w):int(x1 * w)] = 1.0
    return m
