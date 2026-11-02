# Author : Baptiste Vu Liem
"""One-time setup for the character pipeline (re-runnable, skips what is already there):
  1. installs + enables the MPFB extension (extensions.blender.org, id "mpfb", GPL code, CC0 base assets)
  2. downloads the MakeHuman asset packs used by chr_specs.py and unzips them into MPFB's user data.

Usage: blender -b --online-mode --python Tools/Blender/characters/fetch_assets.py
Licences: makehuman_system_assets / *_cc0 packs = CC0; *_ccby / *_cc-by packs = CC-BY (see docs/Credits.md).
"""
import os
import tempfile
import urllib.request
import zipfile

import bpy

PACKS = [
    "makehuman_system_assets/makehuman_system_assets_cc0.zip",
    "suits01/suits01_cc0.zip", "equipment03/equipment03_cc-by.zip", "hats01/hats01_cc0.zip",
    "hats03/hats03_cc-by.zip", "pants01/pants01_cc0.zip", "pants02/pants02_ccby.zip",
    "shirts01/shirts01_cc0.zip", "shirts02/shirts02_ccby.zip", "shirts03/shirts03_ccby.zip",
    "shoes02/shoes02_ccby.zip", "shoes03/shoes03_ccby.zip", "gloves01/gloves01_cc0.zip",
    "skins01/skins01_cc0.zip", "skins02/skins02_cc0.zip", "suits03/suits03_cc-by.zip",
]
BASE = "https://files.makehumancommunity.org/asset_packs/"


def ensure_mpfb():
    import addon_utils
    if any("mpfb" in m.__name__ for m in addon_utils.modules()):
        print("[fetch] MPFB already installed")
        return
    bpy.context.preferences.system.use_online_access = True
    bpy.ops.extensions.repo_sync_all()
    idx = next(i for i, r in enumerate(bpy.context.preferences.extensions.repos) if "extensions.blender.org" in r.remote_url)
    bpy.ops.extensions.package_install(repo_index=idx, pkg_id="mpfb", enable_on_install=True)
    bpy.ops.wm.save_userpref()


def main():
    ensure_mpfb()
    from bl_ext.blender_org.mpfb.services.locationservice import LocationService
    data = LocationService.get_user_data()
    cache = os.path.join(tempfile.gettempdir(), "goobyr_mh_packs")
    os.makedirs(cache, exist_ok=True)
    for rel in PACKS:
        name = os.path.basename(rel)
        z = os.path.join(cache, name)
        if not os.path.isfile(z):
            print("[fetch] download", BASE + rel, flush=True)
            urllib.request.urlretrieve(BASE + rel, z)
        with zipfile.ZipFile(z) as zf:
            for m in zf.namelist():
                dest = os.path.normpath(os.path.join(data, m))
                if not dest.startswith(os.path.normpath(data)):
                    raise RuntimeError("unsafe path in zip: " + m)
            zf.extractall(data)
        print("[fetch] installed", name, flush=True)


main()
