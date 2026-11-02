# Author : Baptiste Vu Liem
"""G2 gallery pages from the Unity strips (Logs/cast_g2/strips/<Body>__<Clip>.png, Goobyr/Cast/G2 Gallery).

    python Tools/Blender/characters/cast/gallery_sheet.py [out_dir]

One page per body and per 17 clips, each row a clip (6 frames) with its name and the report's numbers
(hand penetration raw / with the runtime push-out, stance-foot slide).
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
G2 = os.path.join(PROJECT, "Logs", "cast_g2")
FONT = os.path.join(PROJECT, "Assets", "_Game", "Art", "Fonts", "Barlow", "Barlow-SemiBold.ttf")
PER_PAGE = 17


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(G2, "pages")
    os.makedirs(out, exist_ok=True)
    report = json.load(open(os.path.join(G2, "report.json"), encoding="utf-8"))
    f1, f2 = ImageFont.truetype(FONT, 22), ImageFont.truetype(FONT, 15)
    for body in report["bodies"]:
        clips = body["clips"]
        for page in range(0, len(clips), PER_PAGE):
            rows = clips[page:page + PER_PAGE]
            strips = []
            for c in rows:
                p = os.path.join(G2, "strips", "%s__%s.png" % (body["name"], c["clip"]))
                strips.append(Image.open(p).convert("RGB") if os.path.exists(p) else None)
            w = max(s.width for s in strips if s is not None) + 260
            h = sum((s.height if s is not None else 40) for s in strips) + 60
            img = Image.new("RGB", (w, h), (236, 234, 230))
            d = ImageDraw.Draw(img)
            d.text((12, 14), "%s  -  clips %d-%d of %d  (humanScale %s)" % (body["name"], page + 1, page + len(rows), len(clips), body["humanScale"]),
                   font=f1, fill=(30, 32, 36))
            y = 52
            for c, s in zip(rows, strips):
                d.text((12, y + 8), c["clip"], font=f1, fill=(30, 32, 36))
                pen = "hands %.1f / %.1f cm" % (c["handPenetrationRaw"] * 100, c["handPenetrationIK"] * 100)
                d.text((12, y + 40), pen, font=f2, fill=(200, 80, 30) if c["handPenetrationIK"] > 0.03 else (110, 112, 118))
                if c["footSlide"] is not None:
                    d.text((12, y + 60), "foot slide %.2f m/s" % c["footSlide"], font=f2,
                           fill=(200, 80, 30) if c["footSlide"] > 0.1 else (110, 112, 118))
                if s is not None:
                    img.paste(s, (250, y))
                y += s.height if s is not None else 40
            img.save(os.path.join(out, "%s_clips_%02d.jpg" % (body["name"], page // PER_PAGE + 1)), quality=88)
    print("pages in", out)


if __name__ == "__main__":
    main()
