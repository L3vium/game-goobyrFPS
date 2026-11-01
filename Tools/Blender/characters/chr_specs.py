# Author : Baptiste Vu Liem
"""Character definitions (MPFB macro sliders, face targets, MakeHuman CC0/CC-BY assets, recolours).

Colours are sRGB 0..1.
  keys     colour-key recolour [(colour found in the source texture, new colour)], albedo detail kept
  tint     luminance-preserving tint of the whole texture (contrast flattens patterns)
  box_tint [(uv boxes (x0, y0, x1, y1) top-left origin, colour, contrast)] tint only inside the boxes
  camo     procedural camouflage {colours, scale, seed, kind: woodland|digital, boxes (optional)}
  fill     ([boxes], colour) flat fill (hides logos) before the recolour
Asset names are MakeHuman asset folder names (installed into MPFB's user data by fetch_assets.py).
"""

DARK_SUIT = (0.075, 0.08, 0.095)
BLACK_SUIT = (0.035, 0.035, 0.04)
WHITE_SHIRT = (0.86, 0.86, 0.84)

WOODLAND = [(0.22, 0.25, 0.14), (0.33, 0.29, 0.18), (0.10, 0.12, 0.07), (0.40, 0.42, 0.26)]
DESERT = [(0.72, 0.62, 0.45), (0.58, 0.47, 0.33), (0.80, 0.72, 0.57)]
SNOW = [(0.88, 0.89, 0.91), (0.70, 0.72, 0.75), (0.95, 0.95, 0.96), (0.56, 0.58, 0.61)]
TAN = (0.66, 0.56, 0.41)
OLIVE = (0.27, 0.29, 0.18)


def suit_tie_keys(suit, shirt):
    # toigo_male_suit_tie_and_jacket: grey cloth, white collar/cuffs (tie handled by box_tint)
    return [((0.33, 0.33, 0.33), suit), ((0.20, 0.20, 0.20), suit), ((0.95, 0.95, 0.95), shirt)]


TIE_BOX = [(0.74, 0.20, 1.0, 0.57)]

CHARACTERS = {
    # ------------------------------------------------------------------ player
    "Chr_Agent": dict(
        phenotype=dict(gender=1.0, age=0.56, muscle=0.66, weight=0.46, height=0.62, proportions=0.75),
        race=dict(caucasian=0.92, african=0.0, asian=0.08),
        face_seed=11,
        targets=[("chin-prominent-incr", 0.3), ("nose-hump-incr", 0.2)],
        skin="middleage_caucasian_male/middleage_caucasian_male.mhmat",
        eyes="blue", eyebrows="eyebrow006", eyelashes="eyelashes02", hair=None,
        brow_colour=(0.16, 0.12, 0.09),
        clothes=[
            dict(asset="toigo_male_suit_tie_and_jacket", part="Suit", rough=0.72,
                 keys=suit_tie_keys(DARK_SUIT, WHITE_SHIRT), box_tint=[(TIE_BOX, (0.30, 0.025, 0.04), 0.45)]),
            dict(asset="mindfront_shoes_oxford_male", part="Shoes", rough=0.3),
        ],
    ),
    "Chr_Agent_F": dict(
        phenotype=dict(gender=0.0, age=0.48, muscle=0.6, weight=0.4, height=0.58, proportions=0.8,
                       cupsize=0.45, firmness=0.6),
        race=dict(caucasian=0.6, african=0.0, asian=0.4),
        face_seed=23,
        targets=[],
        skin="young_caucasian_female/young_caucasian_female.mhmat",
        eyes="brown", eyebrows="eyebrow010", eyelashes="eyelashes01", hair="ponytail01",
        hair_colour=(0.10, 0.07, 0.05), brow_colour=(0.12, 0.08, 0.06),
        clothes=[
            dict(asset="toigo_female_suit", part="Suit", rough=0.72,
                 keys=[((0.30, 0.30, 0.30), DARK_SUIT), ((0.18, 0.18, 0.18), DARK_SUIT),
                       ((0.95, 0.95, 0.95), WHITE_SHIRT)]),
            dict(asset="mindfront_shoes_oxford_female", part="Shoes", rough=0.3),
        ],
    ),
    # ------------------------------------------------------------------ guards
    "Chr_Guard_Jungle": dict(
        phenotype=dict(gender=1.0, age=0.45, muscle=0.72, weight=0.55, height=0.6, proportions=0.6),
        race=dict(caucasian=0.1, african=0.85, asian=0.05),
        face_seed=31,
        skin="young_african_male/young_african_male.mhmat",
        eyes="brown", eyebrows="eyebrow001", eyelashes="eyelashes01", hair="short02",
        hair_colour=(0.05, 0.04, 0.035), brow_colour=(0.04, 0.03, 0.03),
        clothes=[
            dict(asset="drednicolson_short-tail_camo_tee", part="Shirt", rough=0.85,
                 camo=dict(colours=WOODLAND, scale=60, seed=4)),
            dict(asset="cortu_cargo_pants", part="Pants", rough=0.85,
                 camo=dict(colours=WOODLAND, scale=60, seed=9)),
            dict(asset="mindfront_tactical_vest_male", part="Vest", rough=0.7),
            dict(asset="mindfront_shoes_biker_boots_male", part="Boots", rough=0.5),
            dict(asset="mindfront_fishing_hat_01", part="Hat", rough=0.85,
                 camo=dict(colours=WOODLAND, scale=40, seed=2)),
        ],
    ),
    "Chr_Guard_Desert": dict(
        phenotype=dict(gender=1.0, age=0.5, muscle=0.62, weight=0.5, height=0.55, proportions=0.6),
        race=dict(caucasian=0.55, african=0.15, asian=0.3),
        face_seed=37,
        skin="middleage_caucasian_male/middleage_caucasian_male.mhmat", skin_tint=(0.86, 0.74, 0.62),
        eyes="brownlight", eyebrows="eyebrow003", eyelashes="eyelashes01", hair="short04",
        hair_colour=(0.06, 0.045, 0.035), brow_colour=(0.05, 0.04, 0.03),
        clothes=[
            dict(asset="elvs_emt_uniform_jacket_male", part="Jacket", rough=0.85,
                 fill=([(0.53, 0.0, 1.0, 0.25), (0.53, 0.55, 1.0, 1.0)], (0.9, 0.0, 0.0)),
                 tint=TAN, contrast=0.6, no_src_normal=True),
            dict(asset="cortu_cargo_pants", part="Pants", rough=0.85,
                 camo=dict(colours=DESERT, scale=70, seed=5)),
            dict(asset="mindfront_tactical_vest_male", part="Vest", rough=0.7, tint=(0.55, 0.47, 0.33), contrast=0.9),
            dict(asset="mindfront_shoes_biker_boots_male", part="Boots", rough=0.6, tint=(0.36, 0.28, 0.19)),
            dict(asset="mindfront_patrol_cap", part="Hat", rough=0.85,
                 camo=dict(colours=DESERT, scale=40, seed=6)),
        ],
    ),
    "Chr_Guard_Snow": dict(
        phenotype=dict(gender=1.0, age=0.5, muscle=0.6, weight=0.62, height=0.66, proportions=0.6),
        race=dict(caucasian=0.95, african=0.0, asian=0.05),
        face_seed=41,
        skin="young_caucasian_male2/young_caucasian_male2.mhmat",
        eyes="grey", eyebrows="eyebrow002", eyelashes="eyelashes01", hair=None,
        brow_colour=(0.25, 0.18, 0.12),
        clothes=[
            dict(asset="elvs_hooded_sweat_jacket1", part="Parka", rough=0.9,
                 camo=dict(colours=SNOW, scale=50, seed=8, kind="digital")),
            dict(asset="elvs_emt_uniform_pants_male", part="Pants", rough=0.9,
                 camo=dict(colours=SNOW, scale=50, seed=12, kind="digital")),
            dict(asset="mindfront_tactical_vest_male", part="Vest", rough=0.7, tint=(0.42, 0.44, 0.46), contrast=0.9),
            dict(asset="punkduck_winter_boots", part="Boots", rough=0.7, tint=(0.22, 0.2, 0.18), contrast=0.8),
            dict(asset="mindfront_knitted_hat_01", part="Hat", rough=0.95, tint=(0.36, 0.38, 0.4)),
        ],
    ),
    "Chr_Bodyguard": dict(
        phenotype=dict(gender=1.0, age=0.5, muscle=0.85, weight=0.62, height=0.68, proportions=0.6),
        race=dict(caucasian=0.2, african=0.75, asian=0.05),
        face_seed=53,
        targets=[("chin-width-incr", 0.4)],
        earpiece=True,
        skin="middleage_african_male/middleage_african_male.mhmat",
        eyes="brown", eyebrows="eyebrow004", eyelashes="eyelashes01", hair="short02",
        hair_colour=(0.03, 0.03, 0.03), brow_colour=(0.03, 0.03, 0.03),
        clothes=[
            dict(asset="toigo_male_suit_tie_and_jacket", part="Suit", rough=0.7,
                 keys=suit_tie_keys(BLACK_SUIT, (0.9, 0.9, 0.9)), box_tint=[(TIE_BOX, (0.03, 0.03, 0.035), 0.3)]),
            dict(asset="mindfront_shoes_oxford_male", part="Shoes", rough=0.3),
        ],
    ),
    # ------------------------------------------------------------------ NPCs
    "Chr_Staff": dict(
        phenotype=dict(gender=1.0, age=0.38, muscle=0.5, weight=0.42, height=0.5, proportions=0.6),
        race=dict(caucasian=0.1, african=0.0, asian=0.9),
        face_seed=61,
        skin="young_asian_male/young_asian_male.mhmat",
        eyes="brown", eyebrows="eyebrow005", eyelashes="eyelashes01", hair="short04",
        hair_colour=(0.03, 0.025, 0.02), brow_colour=(0.03, 0.025, 0.02),
        clothes=[
            dict(asset="elvs_male_shirt_tie_tucked1", part="Shirt", rough=0.75, tint=(0.88, 0.88, 0.86), contrast=0.4,
                 box_tint=[([(0.5, 0.0, 1.0, 0.27)], (0.03, 0.03, 0.035), 0.3)]),
            dict(asset="mindfront_male_trousers_2", part="Pants", rough=0.8, tint=(0.04, 0.04, 0.045)),
            dict(asset="mindfront_shoes_oxford_male", part="Shoes", rough=0.3),
        ],
    ),
    "Chr_Civilian_M": dict(
        phenotype=dict(gender=1.0, age=0.42, muscle=0.45, weight=0.6, height=0.48, proportions=0.5),
        race=dict(caucasian=0.7, african=0.1, asian=0.2),
        face_seed=71,
        skin="young_caucasian_male/young_caucasian_male.mhmat",
        eyes="green", eyebrows="eyebrow007", eyelashes="eyelashes01", hair="short04",
        hair_colour=(0.22, 0.14, 0.08), brow_colour=(0.18, 0.11, 0.07),
        clothes=[
            dict(asset="namuhekam_male_polo_shirt", part="Shirt", rough=0.85, tint=(0.12, 0.25, 0.42),
                 fill=([(0.53, 0.40, 0.61, 0.53)], (0.58, 0.58, 0.58))),
            dict(asset="punkduck_male_classic_jeans", part="Pants", rough=0.85, hide_under=("Shirt", 0.05)),
            dict(asset="punkduck_running_shoes_01", part="Shoes", rough=0.6),
        ],
    ),
    "Chr_Civilian_F": dict(
        phenotype=dict(gender=0.0, age=0.4, muscle=0.45, weight=0.5, height=0.45, proportions=0.6,
                       cupsize=0.55, firmness=0.55),
        race=dict(caucasian=0.1, african=0.8, asian=0.1),
        face_seed=83,
        skin="young_african_female/young_african_female.mhmat",
        eyes="brown", eyebrows="eyebrow011", eyelashes="eyelashes03", hair="bob02",
        hair_colour=(0.04, 0.03, 0.025), brow_colour=(0.04, 0.03, 0.025),
        clothes=[
            dict(asset="punkduck_v_neck_top", part="Top", rough=0.85, tint=(0.78, 0.36, 0.30)),
            dict(asset="punkduck_female_tight_jeans", part="Pants", rough=0.85),
            dict(asset="mindfront_shoes_oxford_female", part="Shoes", rough=0.4, tint=(0.35, 0.22, 0.14)),
        ],
    ),
    "Chr_VIP": dict(
        phenotype=dict(gender=1.0, age=0.78, muscle=0.4, weight=0.72, height=0.55, proportions=0.5),
        race=dict(caucasian=0.95, african=0.0, asian=0.05),
        face_seed=97,
        targets=[("head-age-incr", 0.5)],
        skin="old_caucasian_male/old_caucasian_male.mhmat",
        eyes="lightblue", eyebrows="eyebrow009", eyelashes="eyelashes01", hair="short03",
        hair_colour=(0.62, 0.6, 0.58), brow_colour=(0.55, 0.52, 0.5),
        clothes=[
            dict(asset="toigo_suit_with_dinner_jacket", part="Suit", rough=0.65),
            dict(asset="mindfront_shoes_oxford_male", part="Shoes", rough=0.25),
        ],
    ),
}
