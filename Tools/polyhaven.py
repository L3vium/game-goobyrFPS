# Author : Baptiste Vu Liem
"""Download CC0 assets from Poly Haven (api.polyhaven.com) into the project.

    python Tools/polyhaven.py texture <id> [res]      -> Assets/_Game/Art/PBR/Textures/<id>/
    python Tools/polyhaven.py model <id> [res]        -> Assets/_Game/Art/PBR/Models/<id>/
    python Tools/polyhaven.py hdri <id> [res]         -> Assets/_Game/Art/PBR/HDRI/<id>.hdr
    python Tools/polyhaven.py search <type> <words>   (type: textures | models | hdris)

Textures keep Poly Haven's names (<id>_diff_2k.jpg, <id>_nor_gl_2k.jpg, <id>_arm_2k.jpg); the
Unity side (Goobyr/Build/PBR Materials, Editor/Pipeline/PbrLibrary.cs) turns them into URP Lit
materials. Every download is appended to docs/Credits.md (all Poly Haven assets are CC0).
"""
import json
import os
import sys
import urllib.request

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
PBR = os.path.join(ROOT, "Assets", "_Game", "Art", "PBR")
CREDITS = os.path.join(ROOT, "docs", "Credits.md")
API = "https://api.polyhaven.com"
UA = {"User-Agent": "GoobyrFPS-asset-tool/1.0"}
TEXTURE_MAPS = ("diff", "nor_gl", "arm")


def _get(url):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=120) as res:
        return res.read()


def _json(url):
    return json.loads(_get(url).decode("utf-8"))


def _save(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return False
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = _get(url)
    with open(path, "wb") as f:
        f.write(data)
    return True


def _credit(kind, asset_id):
    line = f"- Poly Haven {kind} `{asset_id}` (CC0) - https://polyhaven.com/a/{asset_id}\n"
    os.makedirs(os.path.dirname(CREDITS), exist_ok=True)
    existing = open(CREDITS, encoding="utf-8").read() if os.path.exists(CREDITS) else ""
    if line in existing:
        return
    if "## Poly Haven" not in existing:
        existing = existing.rstrip() + ("\n\n" if existing else "# Credits\n\n") + "## Poly Haven (CC0)\n"
    existing = existing.rstrip("\n") + "\n" + line
    with open(CREDITS, "w", encoding="utf-8") as f:
        f.write(existing)


def texture(asset_id, res="2k"):
    files = _json(f"{API}/files/{asset_id}")
    out = os.path.join(PBR, "Textures", asset_id)
    got = []
    for name in TEXTURE_MAPS:
        # the API calls the base colour "Diffuse"
        key = "Diffuse" if name == "diff" and "Diffuse" in files else name
        if key not in files:
            continue
        options = files[key].get(res) or files[key].get("2k") or files[key].get("1k")
        fmt = options.get("jpg") or options.get("png")
        if not fmt:
            continue
        ext = "jpg" if "jpg" in options else "png"
        path = os.path.join(out, f"{asset_id}_{name}_{res}.{ext}")
        _save(fmt["url"], path)
        got.append(os.path.basename(path))
    _credit("texture", asset_id)
    print(asset_id, "->", got)


def model(asset_id, res="2k"):
    files = _json(f"{API}/files/{asset_id}")
    fbx = files.get("fbx", {})
    options = fbx.get(res) or fbx.get("2k") or fbx.get("1k")
    if not options:
        raise SystemExit(f"{asset_id}: no fbx download")
    entry = options["fbx"]
    out = os.path.join(PBR, "Models", asset_id)
    _save(entry["url"], os.path.join(out, f"{asset_id}.fbx"))
    for rel, inc in entry.get("include", {}).items():
        name = os.path.basename(rel)
        # keep only what URP uses: base colour, OpenGL normal, AO/rough/metal (+ alpha masks)
        if any(t in name for t in ("_nor_dx", "_disp", "_bump", "_spec")):
            continue
        _save(inc["url"], os.path.join(out, name))
    _credit("model", asset_id)
    print(asset_id, "->", sorted(os.listdir(out)))


def hdri(asset_id, res="2k"):
    files = _json(f"{API}/files/{asset_id}")
    entry = files["hdri"].get(res) or files["hdri"]["2k"]
    path = os.path.join(PBR, "HDRI", f"{asset_id}.hdr")
    _save(entry["hdr"]["url"], path)
    _credit("HDRI", asset_id)
    print(asset_id, "->", path)


def search(kind, words):
    assets = _json(f"{API}/assets?t={kind}")
    words = [w.lower() for w in words]
    for key, info in sorted(assets.items()):
        hay = " ".join([key, info.get("name", "")] + info.get("tags", []) + info.get("categories", [])).lower()
        if all(w in hay for w in words):
            print(key, "|", info.get("name"), "|", ",".join(info.get("categories", [])))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 3:
        print(__doc__)
        return
    cmd, arg = sys.argv[1], sys.argv[2]
    res = sys.argv[3] if len(sys.argv) > 3 else "2k"
    if cmd == "texture":
        for asset_id in arg.split(","):
            texture(asset_id, res)
    elif cmd == "model":
        for asset_id in arg.split(","):
            model(asset_id, "1k" if len(sys.argv) <= 3 else res)
    elif cmd == "hdri":
        hdri(arg, res)
    elif cmd == "search":
        search(arg, sys.argv[3:])


if __name__ == "__main__":
    main()
