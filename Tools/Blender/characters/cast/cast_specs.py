# Author : Baptiste Vu Liem
"""The cast: body base, face, hair, outfit (garment builders + parameters) and looks per slot.

Colours are sRGB 0..1. Look kinds: fabric (Poly Haven scan recoloured), plain, skin, hair, glass,
camo (fabric with a camouflage pattern), metal. Slot "Team" and "Lining" carry the team colour on
players (Unity tints them); the sheet shows the default orange.
"""

ORANGE = (0.96, 0.42, 0.07)
TEAM_BLUE = (0.10, 0.40, 0.88)

# -------------------------------------------------------------------------------------- palettes
SKIN = dict(fair=(0.90, 0.70, 0.58), light=(0.86, 0.64, 0.50), tan=(0.76, 0.53, 0.38), olive=(0.70, 0.50, 0.34),
            brown=(0.52, 0.33, 0.22), dark=(0.34, 0.21, 0.14), deep=(0.24, 0.15, 0.10))
HAIR = dict(copper=(0.60, 0.235, 0.085), black=(0.06, 0.05, 0.05), brown=(0.24, 0.14, 0.08), dark_brown=(0.13, 0.08, 0.05),
            blond=(0.78, 0.62, 0.36), grey=(0.62, 0.60, 0.58), auburn=(0.42, 0.14, 0.07), white=(0.85, 0.84, 0.82))


def L_fabric(colour, ph="cotton_jersey", scale=3.0, rough=0.85, sheen=0.35, bump=0.25, contrast=0.6, **kw):
    d = dict(kind="fabric", colour=colour, ph=ph, scale=scale, rough=rough, sheen=sheen, bump=bump, contrast=contrast)
    d.update(kw)
    return d


def L_plain(colour, rough=0.5, metal=0.0, coat=0.0, noise=0.0, bump=0.0, scale=40.0, sheen=0.0):
    return dict(kind="plain", colour=colour, rough=rough, metal=metal, coat=coat, noise=noise, bump=bump, scale=scale, sheen=sheen)


def L_leather(colour, ph="brown_leather", scale=4.0, rough=0.55, contrast=0.5, coat=0.15):
    return dict(kind="fabric", colour=colour, ph=ph, scale=scale, rough=rough, sheen=0.05, bump=0.35, contrast=contrast, coat=coat)


def L_camo(colours, ph="denim_fabric_05", kind="woodland", scale=5.0, seed=0.0, rough=0.88):
    return dict(kind="camo", colours=colours, ph=ph, camo=kind, cscale=scale, seed=seed, rough=rough)


METAL = dict(brass=L_plain((0.78, 0.60, 0.30), 0.32, 1.0), gunmetal=L_plain((0.30, 0.31, 0.33), 0.35, 1.0),
             silver=L_plain((0.85, 0.85, 0.86), 0.22, 1.0), gold=L_plain((0.95, 0.74, 0.32), 0.25, 1.0))
RUBBER = L_plain((0.075, 0.07, 0.068), 0.75, noise=0.25, bump=0.15, scale=120.0)

# -------------------------------------------------------------------------------------- hero
ROOK_LOOKS = {
    "Jacket": L_fabric((0.25, 0.28, 0.15), "bi_stretch", 5.0, 0.58, 0.12, 0.25, 0.45, coat=0.05),
    "Lining": L_fabric(ORANGE, "bi_stretch", 6.0, 0.55, 0.25, 0.15, 0.3),
    "Rib": L_fabric((0.17, 0.19, 0.11), "knitted_fleece", 9.0, 0.9, 0.3, 0.4, 0.5),
    "Team": L_fabric(ORANGE, "bi_stretch", 6.0, 0.5, 0.2, 0.15, 0.3),
    "Zip": METAL["brass"],
    "Tee": L_fabric((0.93, 0.92, 0.89), "cotton_jersey", 4.0, 0.9, 0.45, 0.2, 0.35),
    "Scarf": L_fabric((0.72, 0.48, 0.09), "knitted_fleece", 5.0, 0.95, 0.35, 0.45, 0.5),
    "Trousers": L_fabric((0.19, 0.21, 0.23), "denim_fabric_05", 3.0, 0.85, 0.3, 0.3, 0.45),
    "Belt": L_leather((0.16, 0.10, 0.07)),
    "Buckle": METAL["gunmetal"],
    "Boots": L_leather((0.46, 0.28, 0.14), "brown_leather", 4.0, 0.6, 0.55),
    "Sole": RUBBER,
    "Laces": L_plain((0.12, 0.08, 0.06), 0.8),
    "Glove": L_leather((0.20, 0.12, 0.08), "leather_red_03", 6.0, 0.45, 0.45, 0.25),
    "Frame": L_plain((0.16, 0.08, 0.04), 0.25, coat=0.6, noise=0.5, scale=60.0),
    "Lens": dict(kind="glass", colour=(0.98, 0.58, 0.10)),
}

ROOK_OUTFIT = [
    ("tee", dict(hem=0.950)),
    ("bomber", dict()),
    ("scarf", dict()),
    ("trousers", dict(top=0.972, bottom=0.150)),
    ("boots", dict()),
    ("gloves", dict()),
    ("shades_on_head", dict(up=True)),
]

CAST = {
    "Chr_Rook": dict(
        title="Rook", role="Hero (player), male", body="average",
        face=dict(iris=(0.30, 0.44, 0.22), brow=(1.30, 1.05), jaw_square=0.75, nose="button", nose_scale=1.04,
                  smile=1.3, skin=SKIN["light"], freckles=0.6, blush=0.4),
        hair="quiff", hair_colour=HAIR["copper"], brow_colour=(0.46, 0.18, 0.07),
        outfit=ROOK_OUTFIT, looks=ROOK_LOOKS,
    ),
    "Chr_Rook_F": dict(
        title="Rook", role="Hero (player), female", body="female",
        face=dict(iris=(0.30, 0.44, 0.22), brow=(0.95, 1.0), jaw_square=0.0, chin=0.92, cheek=1.05, nose="button",
                  nose_scale=0.92, lips=1.12, smile=1.3, female=True, lashes=True, eye_r=0.0199, skin=SKIN["light"],
                  freckles=0.6, blush=0.45, jaw=(0.080, 0.077, 0.068)),
        hair="bun", hair_colour=HAIR["copper"], brow_colour=(0.46, 0.18, 0.07),
        outfit=ROOK_OUTFIT, looks=ROOK_LOOKS,
    ),
}
