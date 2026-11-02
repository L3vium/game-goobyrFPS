# Author : Baptiste Vu Liem
"""Builds every realistic vegetation asset of Goobyr FPS (run headless, after prep_textures.py).

    blender -b --factory-startup --python Tools/Blender/vegetation/build_all.py -- \
        --src <poly haven cache> --work <prep work dir> [--only Veg_A,Veg_B] [--preview <png dir>] [--no-bake]

Per asset: Assets/_Game/Art/PBR/Models/<Asset>/<Asset>.fbx with <Asset>_LOD0/1/2 meshes, material slots
<Asset>_<part> and textures <Asset>_<part>_diff/_nor_gl/_arm next to it; LOD2 is a baked impostor
(<Asset>_card). Updates Assets/_Game/Art/PBR/Models/vegetation_manifest.json and docs/Credits.md.
"""
import json
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import gen_plants as gp  # noqa: E402
import veg_lib as vl  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Game", "Art", "PBR", "Models")
CREDITS = os.path.join(ROOT, "docs", "Credits.md")


def _parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args, i = {}, 0
    while i < len(argv):
        if argv[i] == "--no-bake":
            args["no-bake"] = True
            i += 1
        else:
            args[argv[i].lstrip("-")] = argv[i + 1]
            i += 2
    vl.Ctx.src = args.get("src", "")
    vl.Ctx.work = args.get("work", "")
    vl.Ctx.out = OUT
    vl.Ctx.preview = args.get("preview", "")
    vl.Ctx.bake = "no-bake" not in args
    return args


ARGS = _parse_args()


def T(i):
    return os.path.join(vl.Ctx.work, "tex", i)


def A(i):
    return os.path.join(vl.Ctx.work, "atlas", i)


# ------------------------------------------------------------------ asset table
def L(**k):
    return k


ASSETS = []


def asset(name, biome, kind, wind, gen, P, parts, credits):
    ASSETS.append(dict(name=name, biome=biome, kind=kind, wind=wind, gen=gen, P=P, parts=parts, credits=credits))


def tree_gen(atlas_name):
    return lambda P, lod: gp.broadleaf(P, lod, vl.atlas(atlas_name))


def fir_gen(snow):
    return lambda P, lod: gp.conifer(P, lod, vl.atlas("fir"), vl.atlas("fir_snow") if snow else None)


# --- JUNGLE ------------------------------------------------------------------------------------------
asset("Veg_Ceiba", "jungle", "tree", "trunk", tree_gen("ceiba"), dict(
    seed=3, height=34, trunk_r=1.35, trunk_frac=0.72, trunk_tip=0.5, trunk_sides=28, trunk_segs=28, trunk_bias=1.8,
    flare=0.45, flare_h=0.05, buttress=7, buttress_h=0.2, buttress_s=2.6, roots=7, root_len=(3, 6), gnarl=0.05,
    crown_start=0.8, bark_tile=1.6, normal_k=0.55,
    levels=[L(),
            L(count=7, angle=74, angle_var=8, len=0.5, r=0.4, r_max=0.7, shape="tip", gravity=0.06, up=0.04, wobble=0.12, sides=10, seg=2.0, tip=0.3, end=1.0, zclamp=0.4),
            L(count=7, angle=50, len=0.42, r=0.45, start=0.25, gravity=0.02, up=0.06, wobble=0.18, sides=6, seg=1.2, tip=0.25, planar=True, zclamp=0.3),
            L(count=6, angle=55, len=0.5, r=0.5, start=0.2, gravity=0.12, up=0.0, wobble=0.2, sides=4, seg=0.8, tip=0.2, tube=False, planar=True,
              zclamp=0.25, leaves=7, leaf_size=(2.6, 3.6), leaf_start=0.0, leaf_flat=0.75, leaf_up=0.05, leaf_zclamp=0.35, tip_clusters=3)]),
    [("bark", "bark", T("chinese_hackberry_bark"), False), ("leaf", "leaf", A("ceiba"), True)],
    ["chinese_hackberry_bark", "pachira_aquatica_01"])

asset("Veg_JungleTree_A", "jungle", "tree", "trunk", tree_gen("jungle_a"), dict(
    seed=11, height=21, trunk_r=0.45, trunk_frac=0.8, trunk_tip=0.3, trunk_sides=16, flare=0.6, flare_h=0.05, buttress=5, buttress_h=0.1,
    buttress_s=1.2, roots=5, root_len=(1, 2.2), lean=0.05, trunk_wobble=0.03, crown_start=0.55, bark_tile=1.0,
    levels=[L(),
            L(count=10, angle=42, angle_var=12, len=0.32, r=0.45, shape="conical", gravity=0.12, up=0.2, wobble=0.2, sides=8, seg=1.2, tip=0.25),
            L(count=6, angle=45, len=0.5, r=0.5, start=0.25, gravity=0.15, up=0.15, wobble=0.25, sides=5, seg=0.7, tip=0.2,
              leaves=3, leaf_size=(1.4, 2.0), leaf_start=0.3, leaf_flat=0.4, tip_clusters=0),
            L(count=5, angle=45, len=0.5, r=0.5, start=0.25, gravity=0.25, up=0.1, wobble=0.25, sides=3, seg=0.6, tip=0.25, tube=False,
              leaves=7, leaf_size=(1.7, 2.5), leaf_start=0.0, leaf_flat=0.45, tip_clusters=3)]),
    [("bark", "bark", T("tree_bark_03"), False), ("leaf", "leaf", A("jungle_a"), True)],
    ["tree_bark_03", "island_tree_01"])

asset("Veg_JungleTree_B", "jungle", "tree", "trunk", tree_gen("jungle_b"), dict(
    seed=23, height=16, trunk_r=0.4, trunk_frac=0.4, trunk_tip=0.6, trunk_sides=14, flare=0.5, flare_h=0.04, lean=0.1,
    trunk_wobble=0.05, crown_start=0.55, bark_tile=0.9, normal_k=0.55, buttress=3, buttress_h=0.06, buttress_s=0.8,
    levels=[L(),
            L(count=5, angle=55, angle_var=12, len=0.62, r=0.6, r_max=0.8, shape="even", gravity=0.0, up=0.18, wobble=0.25, sides=9, seg=1.0, tip=0.3),
            L(count=7, angle=55, len=0.45, r=0.5, start=0.25, gravity=0.05, up=0.12, wobble=0.25, sides=5, seg=0.7, tip=0.2, planar=True, zclamp=0.45),
            L(count=5, angle=50, len=0.5, r=0.5, start=0.25, gravity=0.3, up=0.05, wobble=0.2, sides=3, seg=0.6, tip=0.25, tube=False, planar=True,
              zclamp=0.35, leaves=6, leaf_size=(1.9, 2.7), leaf_start=0.0, leaf_flat=0.75, leaf_zclamp=0.45, tip_clusters=3)]),
    [("bark", "bark", T("japanese_hackberry_bark"), False), ("leaf", "leaf", A("jungle_b"), True)],
    ["japanese_hackberry_bark", "jacaranda_tree"])

asset("Veg_RoyalPalm", "jungle", "palm", "trunk", lambda P, lod: gp.palm(P, lod, vl.atlas("royal")), dict(
    seed=5, height=14, trunk_r=0.26, base_swell=0.35, taper=0.15, bulge=0.18, bend=0.02, shaft=2.4, fronds=22, frond_len=4.8,
    elev_young=78, elev_old=-5, droop=1.5, fold=-0.4, rows=11, sides=16, bark_tile=1.0, crown_drop=0.5, frond_width=1.0),
    [("bark", "bark", T("palm_tree_bark"), False), ("shaft", "shaft", T("fever_tree_bark"), False), ("leaf", "frond", A("royal"), True)],
    ["palm_tree_bark", "fever_tree_bark", "grass_medium_02"])

asset("Veg_CoconutPalm", "jungle", "palm", "trunk", lambda P, lod: gp.palm(P, lod, vl.atlas("coconut")), dict(
    seed=8, height=13, trunk_r=0.19, base_swell=0.6, taper=0.25, bend=0.28, s_curve=0.06, lean_az=0.9, fronds=30, frond_len=4.8,
    elev_young=65, elev_old=-35, droop=1.6, fold=-0.55, rows=11, sides=14, bark_tile=0.9, dead=True, nuts=9, nut_r=0.14, crown_drop=0.3),
    [("bark", "bark", T("palm_tree_bark_dark"), False), ("shaft", "nut", T("fever_tree_bark"), False), ("leaf", "frond", A("coconut"), True)],
    ["palm_tree_bark", "fever_tree_bark", "grass_medium_02"])

asset("Veg_Banana", "jungle", "bush", "leaves", lambda P, lod: gp.rosette(P, lod, vl.atlas("banana")), dict(
    seed=4, stems=[(Vector((0, 0, 0)), 2.2, 0.12), (Vector((0.45, 0.2, 0)), 1.3, 0.08), (Vector((-0.3, 0.38, 0)), 0.8, 0.06)],
    fronds=12, frond_len=2.6, elev_young=62, elev_old=5, droop=1.7, fold=0.22, rows=8, frond_width=1.75, bark_tile=0.7, dead=0.14, last_is_dead=True,
    sucker_ratio=0.6, sucker_scale=0.6, stem_taper=0.35, crown_r=0.05),
    [("bark", "stem", T("fever_tree_bark"), False), ("leaf", "leaf", A("banana"), True)],
    ["fever_tree_bark", "island_tree_01"])

asset("Veg_TreeFern", "jungle", "palm", "leaves", lambda P, lod: gp.rosette(P, lod, vl.atlas("treefern")), dict(
    seed=9, stems=[(Vector((0, 0, 0)), 3.6, 0.16)], stem_lean=0.12, stem_swell=0.5, stem_taper=0.15, stem_sides=12, fronds=24,
    frond_len=2.9, elev_young=48, elev_old=-18, droop=1.1, fold=0.12, rows=9, frond_width=1.25, bark_tile=0.6, dead=0.2, last_is_dead=True, crown_r=0.12, curl=0.0),
    [("bark", "bark", T("metasequoia_bark"), False), ("leaf", "frond", A("treefern"), True)],
    ["metasequoia_bark", "fern_02"])

asset("Veg_JungleBush_A", "jungle", "bush", "leaves", lambda P, lod: gp.leafy_plant(P, lod, vl.atlas("jbush_a")), dict(
    seed=6, leaves=28, petiole=(0.3, 0.95), leaf_size=(0.5, 0.75), spread=0.18),
    [("bark", "stem", T("fever_tree_bark"), False), ("leaf", "leaf", A("jbush_a"), True)],
    ["fever_tree_bark", "calathea_orbifolia_01", "anthurium_botany_01"])

asset("Veg_JungleBush_B", "jungle", "bush", "leaves", tree_gen("jbush_b"), dict(
    seed=14, height=2.4, bark_tile=0.4, tube_min_r=0.004, normal_k=0.7,
    stems=dict(count=6, angle=(10, 40), len=(0.45, 0.6), r=0.03, spread=0.15),
    levels=[L(), L(sides=5, seg=0.35, gravity=0.15, up=0.1, wobble=0.25, tip=0.3),
            L(count=5, angle=50, len=0.5, r=0.5, start=0.3, gravity=0.3, up=0.05, wobble=0.25, sides=3, seg=0.3, tip=0.3, lod1_skip=True,
              leaves=4, leaf_size=(0.55, 0.8), leaf_start=0.1, leaf_flat=0.35, tip_clusters=1)]),
    [("bark", "bark", T("bark_brown_02"), False), ("leaf", "leaf", A("jbush_b"), True)],
    ["bark_brown_02", "island_tree_01", "anthurium_botany_01"])

asset("Veg_Fern", "jungle", "fern", "leaves", lambda P, lod: gp.rosette(P, lod, vl.atlas("fern")), dict(
    seed=2, fronds=16, frond_len=1.0, elev_young=75, elev_old=20, droop=1.0, fold=0.15, rows=6, crown_r=0.04, lod1_keep=0.5),
    [("leaf", "fern", A("fern"), True)], ["fern_02"])

asset("Veg_Grass_Jungle", "jungle", "grass", "grass", lambda P, lod: gp.grass(P, lod, vl.atlas("grass_jungle")), dict(
    seed=1, cards=40, radius=0.3, height=(0.35, 0.6), segs=2, droop=0.25, width=0.8),
    [("leaf", "grass", A("grass_jungle"), True)], ["grass_medium_02"])

asset("Veg_TallGrass_Jungle", "jungle", "tallgrass", "grass", lambda P, lod: gp.grass(P, lod, vl.atlas("tallgrass_jungle")), dict(
    seed=7, cards=80, radius=0.7, height=(1.3, 1.8), segs=3, droop=0.25, width=0.75, tilt=(2, 14), sink=0.16),
    [("leaf", "grass", A("tallgrass_jungle"), True)], ["grass_medium_02"])

# --- DESERT ------------------------------------------------------------------------------------------
asset("Veg_DatePalm", "desert", "palm", "trunk", lambda P, lod: gp.palm(P, lod, vl.atlas("date")), dict(
    seed=12, height=11, trunk_r=0.3, base_swell=0.3, taper=0.1, bend=0.05, fronds=34, frond_len=4.0, elev_young=80, elev_old=-25,
    droop=0.8, fold=0.45, rows=10, sides=16, bark_tile=0.9, dead=True, crown_drop=0.6),
    [("bark", "bark", T("palm_bark"), False), ("leaf", "frond", A("date"), True)],
    ["palm_bark", "grass_medium_02"])

asset("Veg_Acacia", "desert", "tree", "trunk", tree_gen("acacia"), dict(
    seed=17, height=8.5, trunk_r=0.26, trunk_frac=0.3, trunk_tip=0.7, trunk_sides=12, flare=0.4, flare_h=0.05, lean=0.15,
    trunk_wobble=0.06, crown_start=0.85, bark_tile=0.7, normal_k=0.5,
    levels=[L(),
            L(count=4, angle=42, angle_var=10, len=0.62, r=0.65, r_max=0.8, gravity=0.0, up=0.12, wobble=0.3, sides=8, seg=0.6, tip=0.3, end=1.0),
            L(count=7, angle=60, len=0.55, r=0.5, start=0.3, gravity=0.0, up=0.1, wobble=0.3, sides=5, seg=0.45, tip=0.25, planar=True, zclamp=0.3),
            L(count=5, angle=60, len=0.45, r=0.5, start=0.3, gravity=0.05, up=0.05, wobble=0.25, sides=3, seg=0.4, tip=0.3, planar=True, zclamp=0.2,
              lod1_skip=True, leaves=6, leaf_size=(1.1, 1.6), leaf_start=0.2, leaf_flat=0.9, leaf_up=0.0, leaf_follow=0.8, leaf_zclamp=0.2, tip_clusters=2)]),
    [("bark", "bark", T("eucalyptus_bark"), False), ("leaf", "leaf", A("acacia"), True)],
    ["eucalyptus_bark", "jacaranda_tree"])

asset("Veg_DesertShrub_A", "desert", "bush", "leaves", tree_gen("dshrub_a"), dict(
    seed=21, height=1.7, bark_tile=0.3, tube_min_r=0.004, normal_k=0.75,
    stems=dict(count=8, angle=(15, 55), len=(0.5, 0.7), r=0.025, spread=0.12),
    levels=[L(), L(sides=4, seg=0.25, gravity=0.1, up=0.15, wobble=0.3, tip=0.3),
            L(count=5, angle=45, len=0.5, r=0.5, start=0.3, gravity=0.2, up=0.1, wobble=0.3, sides=3, seg=0.2, tip=0.3, lod1_skip=True,
              leaves=4, leaf_size=(0.4, 0.6), leaf_start=0.1, leaf_flat=0.3, tip_clusters=1)]),
    [("bark", "bark", T("bark_brown_01"), False), ("leaf", "leaf", A("dshrub_a"), True)],
    ["bark_brown_01", "shrub_04"])

asset("Veg_DesertShrub_B", "desert", "bush", "leaves", tree_gen("dshrub_b"), dict(
    seed=22, height=1.1, bark_tile=0.3, tube_min_r=0.003, normal_k=0.7,
    stems=dict(count=9, angle=(20, 65), len=(0.55, 0.8), r=0.018, spread=0.1),
    levels=[L(), L(sides=4, seg=0.2, gravity=0.15, up=0.1, wobble=0.35, tip=0.3),
            L(count=5, angle=50, len=0.5, r=0.5, start=0.25, gravity=0.2, up=0.05, wobble=0.35, sides=3, seg=0.15, tip=0.3, lod1_skip=True,
              leaves=3, leaf_size=(0.3, 0.45), leaf_start=0.1, leaf_flat=0.2, tip_clusters=1)]),
    [("bark", "bark", T("bark_willow_02"), False), ("leaf", "twig", A("dshrub_b"), True)],
    ["bark_willow_02", "searsia_burchellii", "wild_rooibos_bush"])

asset("Veg_DeadTree", "desert", "deadwood", "trunk", tree_gen("bare"), dict(
    seed=31, height=9, trunk_r=0.28, trunk_frac=0.55, trunk_tip=0.3, trunk_sides=12, flare=0.25, flare_h=0.05, lean=0.12,
    trunk_wobble=0.08, crown_start=0.35, bark_tile=0.8, gnarl=0.12,
    levels=[L(),
            L(count=6, angle=45, angle_var=15, len=0.45, r=0.5, shape="conical", gravity=0.05, up=0.25, wobble=0.35, sides=7, seg=0.5, tip=0.2),
            L(count=4, angle=45, len=0.5, r=0.5, start=0.3, gravity=0.05, up=0.2, wobble=0.35, sides=4, seg=0.35, tip=0.2),
            L(count=3, angle=40, len=0.5, r=0.5, start=0.4, gravity=0.0, up=0.1, wobble=0.3, sides=3, seg=0.3, tip=0.25, lod1_skip=True,
              leaves=2, leaf_size=(0.7, 1.1), leaf_start=0.4, leaf_flat=0.0, leaf_kind="twig", tip_clusters=0)]),
    [("bark", "bark", T("bark_willow_02"), False), ("leaf", "twig", A("bare"), True)],
    ["bark_willow_02", "searsia_burchellii"])

asset("Veg_Grass_Dry", "desert", "grass", "grass", lambda P, lod: gp.grass(P, lod, vl.atlas("grass_dry")), dict(
    seed=3, cards=36, radius=0.28, height=(0.3, 0.55), segs=2, droop=0.3, width=0.8),
    [("leaf", "grass", A("grass_dry"), True)], ["grass_medium_02"])

asset("Veg_TallGrass_Dry", "desert", "tallgrass", "grass", lambda P, lod: gp.grass(P, lod, vl.atlas("tallgrass_dry")), dict(
    seed=13, cards=80, radius=0.65, height=(1.35, 1.85), segs=3, droop=0.15, width=0.75, tilt=(1, 10), sink=0.16),
    [("leaf", "grass", A("tallgrass_dry"), True)], ["grass_medium_02"])

# --- SNOW --------------------------------------------------------------------------------------------
FIR = dict(bark_tile=0.9, cone_exp=0.95, droop_low=30, rise_top=35, sag=0.25, card_droop=0.3)
for nm, snow, h, r, ml, wh, seed in (("Veg_Fir_Large", False, 25, 0.42, 5.2, 0.55, 41), ("Veg_Fir_Medium", False, 14, 0.25, 3.3, 0.45, 42),
                                     ("Veg_Fir_Small", False, 5, 0.1, 1.7, 0.24, 43), ("Veg_Fir_Snowy_Large", True, 24, 0.4, 5.0, 0.55, 44),
                                     ("Veg_Fir_Snowy_Medium", True, 13, 0.24, 3.1, 0.45, 45)):
    parts = [("bark", "bark", T("fir_bark"), False), ("leaf", "needle", A("fir"), True)]
    if snow:
        parts.append(("snow", "snowtwig", A("fir_snow"), True))
    asset(nm, "snow", "tree", "trunk", fir_gen(snow), dict(FIR, seed=seed, height=h, trunk_r=r, max_len=ml, whorl=wh, per_whorl=5,
                                                          clear=0.04 if h < 8 else 0.08), parts, ["fir_tree_01"])

asset("Veg_Birch", "snow", "tree", "trunk", tree_gen("birch"), dict(
    seed=51, height=14, trunk_r=0.17, trunk_frac=0.85, trunk_tip=0.2, trunk_sides=12, flare=0.3, flare_h=0.04, lean=0.06,
    trunk_wobble=0.03, crown_start=0.35, bark_tile=0.6, normal_k=0.6,
    levels=[L(),
            L(count=14, angle=40, angle_var=10, len=0.3, r=0.35, shape="conical", gravity=0.2, up=0.1, wobble=0.25, sides=6, seg=0.6, tip=0.2),
            L(count=6, angle=40, len=0.55, r=0.5, start=0.2, gravity=0.45, up=0.0, wobble=0.25, sides=3, seg=0.4, tip=0.25, lod1_skip=True,
              leaves=8, leaf_size=(0.9, 1.3), leaf_start=0.15, leaf_flat=0.25, leaf_up=-0.2, tip_clusters=1)]),
    [("bark", "bark", T("birch_bark"), False), ("leaf", "leaf", A("birch"), True)],
    ["japanese_sycamore", "shrub_03"])

asset("Veg_SnowBush", "snow", "bush", "leaves", lambda P, lod: gp.broadleaf(P, lod, vl.atlas("fir")), dict(
    seed=52, height=1.3, bark_tile=0.3, tube_min_r=0.006, normal_k=0.65, snow_off=0.03, card_droop=0.25, lod1_keep=0.45,
    stems=dict(count=12, angle=(15, 70), len=(0.55, 0.85), r=0.03, spread=0.2),
    levels=[L(), L(sides=4, seg=0.25, gravity=0.12, up=0.12, wobble=0.25, tip=0.35, leaves=5, leaf_size=(0.45, 0.7), leaf_start=0.2,
                   leaf_flat=0.7, leaf_kind="branch", tip_clusters=0),
            L(count=3, angle=45, len=0.5, r=0.5, start=0.3, gravity=0.1, up=0.1, wobble=0.25, sides=3, seg=0.2, tip=0.3, tube=False,
              leaves=3, leaf_size=(0.35, 0.55), leaf_start=0.0, leaf_flat=0.7, leaf_kind="twig", tip_clusters=0)]),
    [("bark", "bark", T("pine_bark"), False), ("leaf", "needle", A("fir"), True), ("snow", "snowtwig", A("fir_snow"), True)],
    ["pine_bark", "fir_tree_01"])

asset("Veg_Grass_Alpine", "snow", "grass", "grass", lambda P, lod: gp.grass(P, lod, vl.atlas("grass_alpine")), dict(
    seed=5, cards=34, radius=0.26, height=(0.25, 0.45), segs=2, droop=0.25, width=0.8),
    [("leaf", "grass", A("grass_alpine"), True)], ["grass_medium_02"])

asset("Veg_TallGrass_Alpine", "snow", "tallgrass", "grass", lambda P, lod: gp.grass(P, lod, vl.atlas("tallgrass_alpine")), dict(
    seed=15, cards=80, radius=0.62, height=(1.3, 1.7), segs=3, droop=0.2, width=0.75, tilt=(2, 12), sink=0.16),
    [("leaf", "grass", A("tallgrass_alpine"), True)], ["grass_medium_02"])

# --- ANY: scanned rocks / log ------------------------------------------------------------------------
SCANS = {
    "Veg_Rock_A": dict(src="boulder_01", part="rock", lod=(4000, 1200, 180), kind="rock"),
    "Veg_Rock_B": dict(src="namaqualand_boulder_04", part="rock", lod=(4000, 1200, 180), kind="rock"),
    "Veg_Rock_C": dict(src="namaqualand_boulder_05", part="rock", lod=(3500, 1000, 160), kind="rock"),
    "Veg_Log": dict(src="dead_tree_trunk", part="bark", lod=(3500, 1000, 150), kind="deadwood"),
}


# ------------------------------------------------------------------ pipeline
def credit(asset_id, kind):
    line = f"- Poly Haven {kind} `{asset_id}` (CC0) - https://polyhaven.com/a/{asset_id}\n"
    existing = open(CREDITS, encoding="utf-8").read() if os.path.exists(CREDITS) else ""
    if line in existing:
        return
    if "## Poly Haven" not in existing:
        existing = existing.rstrip() + ("\n\n" if existing else "# Credits\n\n") + "## Poly Haven (CC0)\n"
    existing = existing.rstrip("\n") + "\n" + line
    os.makedirs(os.path.dirname(CREDITS), exist_ok=True)
    with open(CREDITS, "w", encoding="utf-8") as f:
        f.write(existing)


TEXTURE_IDS = {"chinese_hackberry_bark", "tree_bark_03", "japanese_hackberry_bark", "bark_brown_01", "bark_brown_02", "palm_tree_bark",
               "palm_bark", "fever_tree_bark", "bark_willow_02", "eucalyptus_bark", "japanese_sycamore", "pine_bark",
               "metasequoia_bark"}


def manifest_update(entry):
    path = os.path.join(OUT, "vegetation_manifest.json")
    data = {"assets": []}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
    data["assets"] = [a for a in data["assets"] if a["name"] != entry["name"]] + [entry]
    order = [a["name"] for a in ASSETS] + list(SCANS)
    data["assets"].sort(key=lambda a: order.index(a["name"]) if a["name"] in order else 999)
    data["generator"] = "Tools/Blender/vegetation/build_all.py"
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)


def finish(name, objs, folder, biome, kind, wind, cutout, credits_):
    for o in objs:
        o.data.name = o.name
    export_path = os.path.join(folder, name + ".fbx")
    vl.export_fbx(objs, export_path)
    lo, hi = vl.bounds(objs[0])
    radius = max(abs(float(lo[0])), abs(float(hi[0])), abs(float(lo[1])), abs(float(hi[1])))
    tris = [vl.tri_count(o) for o in objs]
    manifest_update(dict(name=name, biome=biome, kind=kind, height_m=round(float(hi[2]), 2), radius_m=round(radius, 2),
                         tris_lod0=tris[0], tris_lod1=tris[1], tris_lod2=tris[2], cutout=cutout, wind=wind,
                         fbx=f"Assets/_Game/Art/PBR/Models/{name}/{name}.fbx"))
    for c in credits_:
        credit(c, "texture" if c in TEXTURE_IDS else "model")
    print(f"BUILT {name}: tris {tris} h={hi[2]:.1f} r={radius:.1f}")
    if vl.Ctx.preview:
        os.makedirs(vl.Ctx.preview, exist_ok=True)
        hdri = os.path.join(vl.Ctx.src, "_hdri.hdr")
        vl.preview(name, objs, os.path.join(vl.Ctx.preview, name + ".png"), hdri)


def build_procedural(spec):
    vl.reset_scene()
    name = spec["name"]
    folder = os.path.join(OUT, name)
    mats, idx = [], {}
    cutout = False
    for i, (role, part, src, cut) in enumerate(spec["parts"]):
        slot = vl.install_textures(name, part, src, folder)
        mats.append(vl.make_material(slot, folder, cut))
        idx[role] = i
        cutout |= cut
    P = dict(spec["P"])
    P["mat"] = idx
    objs = []
    for lod in (0, 1):
        mb = spec["gen"](P, lod)
        objs.append(mb.to_object(f"{name}_LOD{lod}", mats))
    tmp = os.path.join(vl.Ctx.work, "_bake")
    os.makedirs(tmp, exist_ok=True)
    slot, layout = vl.bake_billboard(name, objs[0], folder, tmp)
    cmat = vl.make_material(slot, folder, True)
    objs.append(vl.billboard_mesh(f"{name}_LOD2", cmat, layout))
    finish(name, objs, folder, spec["biome"], spec["kind"], spec["wind"], cutout, spec["credits"])


def build_scan(name, S):
    vl.reset_scene()
    folder = os.path.join(OUT, name)
    src = S["src"]
    slot = vl.install_textures(name, S["part"], T(src), folder)
    bpy.ops.import_scene.fbx(filepath=os.path.join(vl.Ctx.src, src, src + ".fbx"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    hi = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o is not hi:
            bpy.data.objects.remove(o)
    # pivot: bottom centre
    lo_, hi_ = vl.bounds(hi)
    off = Vector((-(lo_[0] + hi_[0]) / 2, -(lo_[1] + hi_[1]) / 2, -lo_[2]))
    hi.data.transform(__import__("mathutils").Matrix.Translation(off))
    hi.data.update()
    hi.data.materials.clear()
    # high-poly material carrying the scan normal map (bake source)
    hm = vl.make_material(f"{name}_{S['part']}", folder, False)
    hi.data.materials.append(hm)
    hi.name = "high"
    objs = []
    for i, target in enumerate(S["lod"]):
        o = hi.copy()
        o.data = hi.data.copy()
        bpy.context.scene.collection.objects.link(o)
        o.name = f"{name}_LOD{i}"
        ratio = min(1.0, target / max(1, vl.tri_count(hi)))
        mod = o.modifiers.new("dec", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.ops.object.modifier_apply(modifier="dec")
        bpy.ops.object.shade_smooth()
        objs.append(o)
    if vl.Ctx.bake:
        bake_normals(hi, objs[0], folder, slot)
    hm = vl.make_material(slot, folder, False)  # reload with the baked normal map
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(hm)
    bpy.data.objects.remove(hi)
    finish(name, objs, folder, "any", S["kind"], "none", False, [src])


def bake_normals(high, low, folder, slot):
    """Cycles: bake the scan (geometry + its normal map) into a tangent normal map of the decimated LOD0."""
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 4
    sc.cycles.device = "CPU"
    img = bpy.data.images.new(slot + "_bake", 1024, 1024, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"
    bake_mat = bpy.data.materials.new("bake_target")
    bake_mat.use_nodes = True
    node = bake_mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = img
    bake_mat.node_tree.nodes.active = node
    saved = list(low.data.materials)
    low.data.materials.clear()
    low.data.materials.append(bake_mat)
    bpy.ops.object.select_all(action="DESELECT")
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    lo_, hi_ = vl.bounds(low)
    size = float(max(hi_ - lo_))
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = size * 0.02
    sc.render.bake.max_ray_distance = size * 0.05
    sc.render.bake.margin = 8
    sc.render.bake.normal_space = "TANGENT"
    bpy.ops.object.bake(type="NORMAL")
    path = os.path.join(folder, slot + "_nor_gl.jpg")
    img.filepath_raw = path
    img.file_format = "JPEG"
    img.save()
    low.data.materials.clear()
    for m in saved:
        low.data.materials.append(m)
    bpy.data.images.remove(img)
    bpy.data.materials.remove(bake_mat)
    # drop the cached old normal image so the material reload picks up the baked one
    for im in list(bpy.data.images):
        if im.filepath and os.path.abspath(bpy.path.abspath(im.filepath)) == os.path.abspath(path):
            bpy.data.images.remove(im)
    sc.render.engine = "BLENDER_EEVEE"


def main():
    args = ARGS
    only = set(args["only"].split(",")) if "only" in args else None
    t0 = time.time()
    for spec in ASSETS:
        if only and spec["name"] not in only:
            continue
        build_procedural(spec)
    for name, S in SCANS.items():
        if only and name not in only:
            continue
        build_scan(name, S)
    print(f"DONE in {time.time() - t0:.0f}s")


if __name__ == "__main__":
    main()
