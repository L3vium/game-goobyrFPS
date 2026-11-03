# Author : Baptiste Vu Liem
"""Assemble the cast design sheets from the turnaround renders (system Python + Pillow, no Blender).

    python Tools/Blender/characters/cast/sheet.py [out_dir]

Reads Tools/Blender/out/cast/renders/<Chr>_{front,side,back}.png (render.PX_PER_M px per metre,
feet FOOT_MARGIN above the bottom edge), plus <Chr>_beauty/_face/_expr.png for the hero, and writes
three sheets: cast_sheet_1_hero.png, cast_sheet_2_bases_guards.png, cast_sheet_3_staff_guests_vips.png.
Every view is shown at the same scale next to a grey 1.80 m silhouette, with the 1.80 (top of skull),
1.60 (eyes, Core/BodyMetrics) and 0.88 (minimum hip joint) guides.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
RENDERS = os.path.join(PROJECT, "Tools", "Blender", "out", "cast", "renders")
FONTS = os.path.join(PROJECT, "Assets", "_Game", "Art", "Fonts", "Barlow")
SRC_PPM = 400.0
FOOT = 0.06
VIEW_H = 2.20

BG = (234, 232, 228)
CELL = (245, 244, 241)
INK = (38, 40, 44)
SOFT = (120, 122, 128)
RULE = (206, 204, 199)
GUIDE = (196, 120, 60)
ORANGE = (240, 110, 20)

SHEETS = [
    ("cast_sheet_1_hero.png", "Rook, the hero", "Player character, male and female. Team colour on the jacket lining, facings and armband (orange shown).",
     [["Chr_Rook"], ["Chr_Rook_F"]], "hero"),
    ("cast_sheet_2_bases_guards.png", "Body bases and guards", "Three body bases (slim, average, heavy) in the neutral base outfit, the biome guards and the bodyguard.",
     [["Chr_Base_Slim", "Chr_Base_Average", "Chr_Base_Heavy"], ["Chr_Guard_Jungle", "Chr_Guard_Desert", "Chr_Guard_Snow"],
      ["Chr_Bodyguard"]], "grid"),
    ("cast_sheet_3_staff_guests_vips.png", "Staff, guests and targets", "Staff (waiter, cook, maid, mechanic), two guest looks and the two VIP targets.",
     [["Chr_Waiter", "Chr_Cook", "Chr_Maid"], ["Chr_Mechanic", "Chr_Guest_M", "Chr_Guest_F"], ["Chr_VIP_M", "Chr_VIP_F"]], "grid"),
]


def font(weight, size):
    name = {"bold": "Barlow-Bold.ttf", "semi": "Barlow-SemiBold.ttf", "med": "Barlow-Medium.ttf", "reg": "Barlow-Regular.ttf",
            "cond": "BarlowCondensed-Bold.ttf"}[weight]
    path = os.path.join(FONTS, name)
    return ImageFont.truetype(path, size) if os.path.exists(path) else ImageFont.load_default()


def load_view(name, view):
    p = os.path.join(RENDERS, "%s_%s.png" % (name, view))
    return Image.open(p).convert("RGBA") if os.path.exists(p) else None


def crop_x(img, margin=8):
    a = img.split()[3]
    bb = a.getbbox()
    if bb is None:
        return img
    x0, x1 = max(bb[0] - margin, 0), min(bb[2] + margin, img.width)
    return img.crop((x0, 0, x1, img.height))


def scaled(img, ppm):
    s = ppm / SRC_PPM
    return img.resize((max(1, int(img.width * s)), max(1, int(img.height * s))), Image.LANCZOS)


def shadow(canvas, cx, ground_y, w, ppm):
    sh = Image.new("L", canvas.size, 0)
    d = ImageDraw.Draw(sh)
    rw, rh = w * 0.42, ppm * 0.035
    d.ellipse((cx - rw, ground_y - rh, cx + rw, ground_y + rh), fill=70)
    sh = sh.filter(ImageFilter.GaussianBlur(ppm * 0.025))
    dark = Image.new("RGBA", canvas.size, (60, 58, 55, 255))
    canvas.paste(dark, (0, 0), sh)


def silhouette(ppm):
    s = load_view("Silhouette", "front")
    if s is None:
        return None
    s = crop_x(s, 2)
    s = scaled(s, ppm)
    alpha = s.split()[3]
    flat = Image.new("RGBA", s.size, (176, 178, 182, 255))
    flat.putalpha(alpha.point(lambda v: 255 if v > 90 else int(v * 2.8)))
    return flat


def draw_guides(d, x0, x1, ground_y, ppm, labels=True):
    f = font("med", max(11, int(ppm * 0.055)))
    for z, txt in ((1.80, "1.80"), (1.60, "1.60 eye"), (0.88, "0.88 hip")):
        y = ground_y - z * ppm
        for x in range(int(x0), int(x1), 10):
            d.line((x, y, min(x + 5, x1), y), fill=GUIDE if z == 1.60 else RULE, width=1)
        if labels:
            d.text((x0 + 4, y - f.size - 2), txt, font=f, fill=GUIDE if z == 1.60 else SOFT)
    d.line((x0, ground_y, x1, ground_y), fill=RULE, width=2)


def character_cell(name, ppm, extra=()):
    """RGBA strip: silhouette, front, side, back (+ extra images scaled to the same height)."""
    views = []
    sil = silhouette(ppm)
    for v in ("front", "side", "back"):
        im = load_view(name, v)
        if im is not None:
            views.append(scaled(crop_x(im), ppm))
    if not views:
        return None, 0
    h = views[0].height
    gap = int(ppm * 0.10)
    parts = ([sil] if sil is not None else []) + views
    w = sum(p.width for p in parts) + gap * (len(parts) + 1)
    extra_imgs = []
    for e in extra:
        p = os.path.join(RENDERS, e)
        if os.path.exists(p):
            im = Image.open(p).convert("RGBA")
            s = (h * 0.98) / im.height
            extra_imgs.append(im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS))
    w += sum(e.width + gap for e in extra_imgs)
    cell = Image.new("RGBA", (w, h), CELL + (255,))
    d = ImageDraw.Draw(cell)
    ground_y = h - FOOT * ppm
    views_w = sum(p.width for p in parts) + gap * (len(parts) + 1)
    draw_guides(d, 0, views_w, ground_y, ppm)
    x = gap
    for i, p in enumerate(parts):
        if i > 0 or sil is None:
            shadow(cell, x + p.width / 2, ground_y, p.width if i != 2 else p.width * 1.6, ppm)
        cell.alpha_composite(p, (int(x), 0))
        x += p.width + gap
    for e in extra_imgs:
        bgc = Image.new("RGBA", e.size, (226, 224, 220, 255))
        bgc.alpha_composite(e)
        cell.alpha_composite(bgc, (int(x), int((h - e.height) / 2)))
        x += e.width + gap
    return cell, views_w


EXPR = ["Neutral", "Blink", "Smile", "Surprised", "Angry", "Scared", "Pain", "Dead"]


def expression_strip(name, width, ppm):
    ims = []
    for k in EXPR:
        p = os.path.join(RENDERS, "%s_expr_%s.png" % (name, k))
        if os.path.exists(p):
            ims.append((k, Image.open(p).convert("RGBA")))
    if not ims:
        return None
    gap = int(ppm * 0.05)
    side = int((width - gap * (len(ims) + 1)) / len(ims))
    fl = font("semi", int(ppm * 0.07))
    h = side + int(ppm * 0.16)
    strip = Image.new("RGBA", (width, h + gap), CELL + (255,))
    d = ImageDraw.Draw(strip)
    x = gap
    for k, im in ims:
        tile = Image.new("RGBA", im.size, (226, 224, 220, 255))
        tile.alpha_composite(im)
        tile = tile.resize((side, side), Image.LANCZOS)
        strip.alpha_composite(tile, (x, 0))
        tw = d.textlength(k, font=fl)
        d.text((x + (side - tw) / 2, side + int(ppm * 0.03)), k, font=fl, fill=INK if k != "Dead" else ORANGE)
        x += side + gap
    return strip


def label_block(spec, width, ppm):
    f1, f2, f3 = font("bold", int(ppm * 0.12)), font("semi", int(ppm * 0.075)), font("reg", int(ppm * 0.066))
    h = int(ppm * 0.42)
    im = Image.new("RGBA", (width, h), CELL + (255,))
    d = ImageDraw.Draw(im)
    d.text((int(ppm * 0.08), int(ppm * 0.03)), spec["title"], font=f1, fill=INK)
    d.text((int(ppm * 0.08), int(ppm * 0.19)), "%s  |  %s" % (spec["name"], spec["role"]), font=f2, fill=SOFT)
    d.text((int(ppm * 0.08), int(ppm * 0.29)), spec.get("note", ""), font=f3, fill=SOFT)
    return im


def build_sheet(fname, title, subtitle, rows, mode, out_dir, specs):
    ppm = 330 if mode == "hero" else 215
    pad = int(ppm * 0.18)
    built_rows = []
    for row in rows:
        cells = []
        for n in row:
            extra = ("%s_beauty.png" % n, "%s_face.png" % n) if mode == "hero" else ()
            cell, vw = character_cell(n, ppm, extra)
            if cell is None:
                continue
            spec = dict(specs.get(n, {}))
            spec["name"] = n
            lab = label_block(spec, cell.width, ppm)
            strip = expression_strip(n, cell.width, ppm) if mode == "hero" else None
            sh = strip.height if strip is not None else 0
            full = Image.new("RGBA", (cell.width, cell.height + lab.height + sh), CELL + (255,))
            full.alpha_composite(lab, (0, 0))
            full.alpha_composite(cell, (0, lab.height))
            if strip is not None:
                full.alpha_composite(strip, (0, lab.height + cell.height))
            cells.append(full)
        if cells:
            built_rows.append(cells)
    if not built_rows:
        return None
    width = max(sum(c.width for c in r) + pad * (len(r) + 1) for r in built_rows)
    head_h = int(ppm * 0.62)
    height = head_h + sum(max(c.height for c in r) + pad for r in built_rows) + pad
    sheet = Image.new("RGBA", (width, height), BG + (255,))
    d = ImageDraw.Draw(sheet)
    d.text((pad, int(ppm * 0.10)), "GOOBYR FPS  /  CAST  /  " + title.upper(), font=font("cond", int(ppm * 0.20)), fill=INK)
    d.text((pad, int(ppm * 0.36)), subtitle, font=font("reg", int(ppm * 0.085)), fill=SOFT)
    d.rectangle((pad, head_h - 6, pad + int(ppm * 0.6), head_h - 3), fill=ORANGE)
    y = head_h
    for r in built_rows:
        x = pad
        for c in r:
            sheet.alpha_composite(c, (x, y))
            d.rectangle((x, y, x + c.width - 1, y + c.height - 1), outline=RULE)
            x += c.width + pad
        y += max(c.height for c in r) + pad
    path = os.path.join(out_dir, fname)
    sheet.convert("RGB").save(path, quality=92)
    return path


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(PROJECT, "Tools", "Blender", "out", "cast", "sheets")
    os.makedirs(out_dir, exist_ok=True)
    sys.path.insert(0, HERE)
    specs = {}
    try:
        import importlib.util
        for mod in ("cast_specs", "cast_specs_cast"):
            spec = importlib.util.spec_from_file_location(mod, os.path.join(HERE, mod + ".py"))
            m = importlib.util.module_from_spec(spec)
            sys.modules[mod] = m
            spec.loader.exec_module(m)
        from cast_specs import CAST
        for k, v in CAST.items():
            note = "Body: %s.  Hair: %s." % (v["body"], v.get("hair") or "none")
            specs[k] = dict(title=v["title"], role=v["role"], note=note)
    except Exception as e:
        print("specs unavailable:", e)
    for fname, title, subtitle, rows, mode in SHEETS:
        p = build_sheet(fname, title, subtitle, rows, mode, out_dir, specs)
        print("sheet", p)


if __name__ == "__main__":
    main()
