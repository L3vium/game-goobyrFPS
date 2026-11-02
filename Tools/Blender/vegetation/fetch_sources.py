# Author : Baptiste Vu Liem
"""Download the CC0 Poly Haven sources of the realistic vegetation into a cache folder OUTSIDE Assets.

    python Tools/Blender/vegetation/fetch_sources.py <cache_dir>

Only the texture maps are fetched (diffuse, OpenGL normal, ARM, alpha), plus the FBX of the scanned
rocks / log that are decimated. Nothing lands under Assets: prep_textures.py and the Blender builders
read this cache and write the final, downscaled game textures next to each vegetation FBX.
"""
import json
import os
import sys
import urllib.request

API = "https://api.polyhaven.com"
UA = {"User-Agent": "GoobyrFPS-asset-tool/1.0"}

# Poly Haven ids whose texture maps are used (models: leaf / twig atlases + bark; textures: tiling bark).
MODEL_TEXTURES = [
    "fir_tree_01", "island_tree_01", "jacaranda_tree", "fern_02", "grass_medium_02", "shrub_03", "shrub_04",
    "pachira_aquatica_01", "calathea_orbifolia_01", "anthurium_botany_01", "searsia_burchellii", "wild_rooibos_bush",
]
TEXTURES = [
    "chinese_hackberry_bark", "tree_bark_03", "japanese_hackberry_bark", "bark_brown_01", "bark_brown_02",
    "palm_tree_bark", "palm_bark", "fever_tree_bark", "bark_willow_02", "eucalyptus_bark", "japanese_sycamore",
    "pine_bark", "metasequoia_bark",
]
# scanned meshes that are decimated into game rocks / log
MODELS_FBX = ["boulder_01", "namaqualand_boulder_04", "namaqualand_boulder_05", "dead_tree_trunk"]

WANTED = ("diff", "nor_gl", "arm", "alpha", "mask")


def _get(url):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=300) as res:
        return res.read()


def _json(url):
    return json.loads(_get(url).decode("utf-8"))


def _save(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = _get(url)
    with open(path, "wb") as f:
        f.write(data)
    print("  +", os.path.basename(path), len(data) // 1024, "KB")


def _map_name(key):
    k = key.lower().replace("diffuse", "diff")
    for w in WANTED:
        if k == w or k.endswith("_" + w):
            return k
    return None


def fetch_maps(asset_id, out_root, res="2k"):
    files = _json(f"{API}/files/{asset_id}")
    out = os.path.join(out_root, asset_id)
    for key, entry in files.items():
        name = _map_name(key)
        if not name or not isinstance(entry, dict):
            continue
        opts = entry.get(res) or entry.get("2k") or entry.get("1k")
        if not opts:
            continue
        ext = "png" if (name.endswith("alpha") or name.endswith("mask")) and "png" in opts else ("jpg" if "jpg" in opts else "png")
        if ext not in opts:
            continue
        _save(opts[ext]["url"], os.path.join(out, f"{asset_id}_{name}.{ext}"))
    return files


def fetch_fbx(asset_id, out_root):
    files = fetch_maps(asset_id, out_root)
    fbx = files["fbx"].get("1k") or files["fbx"]["2k"]
    _save(fbx["fbx"]["url"], os.path.join(out_root, asset_id, f"{asset_id}.fbx"))


HDRI = "kloofendal_48d_partly_cloudy_puresky"  # preview lighting only (not shipped)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    out_root = sys.argv[1]
    files = _json(f"{API}/files/{HDRI}")
    _save(files["hdri"]["1k"]["hdr"]["url"], os.path.join(out_root, "_hdri.hdr"))
    for a in MODEL_TEXTURES + TEXTURES:
        print(a)
        fetch_maps(a, out_root)
    for a in MODELS_FBX:
        print(a)
        fetch_fbx(a, out_root)


if __name__ == "__main__":
    main()
