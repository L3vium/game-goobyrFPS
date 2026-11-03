# Author : Baptiste Vu Liem
"""Copy exported cast characters (Tools/Blender/out/cast/export/<Chr>) into Assets/_Game/Art/Characters/<Chr>.

    python Tools/Blender/characters/cast/stage.py Chr_Rook [Chr_Guard_Jungle ...] | all

Run under the editor lock. A folder that held an older body of the same name (the MPFB guards and
bodyguard) is emptied first, except the FBX's own .meta (its GUID keeps every reference alive).
"""
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
SRC = os.path.join(PROJECT, "Tools", "Blender", "out", "cast", "export")
DST = os.path.join(PROJECT, "Assets", "_Game", "Art", "Characters")


def stage(name):
    src = os.path.join(SRC, name)
    dst = os.path.join(DST, name)
    if not os.path.isfile(os.path.join(src, name + ".fbx")):
        print("missing export", name)
        return
    os.makedirs(dst, exist_ok=True)
    fresh = {f for f in os.listdir(src) if f.endswith((".fbx", "_diff.jpg", "_nor_gl.png", "_arm.jpg"))}
    slots = {f.rsplit("_diff", 1)[0] for f in fresh if "_diff" in f}
    for f in os.listdir(dst):
        base = f[:-5] if f.endswith(".meta") else f
        if base == name + ".fbx" or base in fresh:
            continue
        stem = os.path.splitext(base)[0]
        stem = stem[2:] if stem.startswith("M_") else stem
        for suffix in ("_diff", "_nor_gl", "_arm", "_ms", "_diffa"):
            if stem.endswith(suffix):
                stem = stem[: -len(suffix)]
        if stem in slots and (base.endswith(".mat") or base.endswith("_ms.png")):
            continue
        # an older body's leftovers (textures, generated maps, materials of slots it no longer has) go
        os.remove(os.path.join(dst, f))
    for f in fresh:
        shutil.copy2(os.path.join(src, f), os.path.join(dst, f))
    print("staged", name, len(fresh), "files")


def main():
    names = sys.argv[1:]
    if names == ["all"]:
        names = sorted(d for d in os.listdir(SRC) if os.path.isdir(os.path.join(SRC, d)))
    for n in names:
        stage(n)


if __name__ == "__main__":
    main()
