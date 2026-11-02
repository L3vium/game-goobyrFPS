# Author : Baptiste Vu Liem
"""Body bases of the cast as SDF lumps hung on the skeleton (slim, average, heavy; male or female).

A body is a list of named lumps (prim, blend k, region). The skin Part unions them all; garments
re-use the same lumps (offset outward, re-blended with a softer k so cloth bridges the hollows)
through BodyShape.shell(). The mass sits low (belly, hips, buttocks) and the chest stays narrow at
the armpits so mocap arms swing clear of the torso.
"""
import numpy as np

import glove
import sdf
from skeleton import Build

V = lambda *a: np.array(a, dtype=np.float64)

# Presets: every number is a radius or an offset in metres.
PRESETS = {
    "slim": dict(shoulder_x=0.180, hip_x=0.083, chest=(0.122, 0.096, 0.150), waist=(0.104, 0.084, 0.11),
                 belly=(0.106, 0.086, 0.105, -0.002), pelvis=(0.120, 0.092, 0.106), butt=(0.056, 0.054, 0.070),
                 neck=0.051, delt=0.054, girdle=0.054, arm=(0.046, 0.037), forearm=0.039, thigh=(0.067, 0.047),
                 calf=(0.049, 0.031), calf_m=0.041, pecs=0.8, bust=0.0, trap=0.038),
    "average": dict(shoulder_x=0.190, hip_x=0.085, chest=(0.138, 0.110, 0.158), waist=(0.118, 0.096, 0.115),
                    belly=(0.118, 0.100, 0.108, -0.010), pelvis=(0.122, 0.098, 0.110), butt=(0.057, 0.058, 0.073),
                    neck=0.057, delt=0.062, girdle=0.062, arm=(0.053, 0.041), forearm=0.043, thigh=(0.071, 0.050),
                    calf=(0.054, 0.033), calf_m=0.046, pecs=1.1, bust=0.0, trap=0.045),
    # Rook: a little broader in the shoulders and chest, straight and narrow in the hips
    "hero": dict(shoulder_x=0.196, hip_x=0.084, chest=(0.142, 0.113, 0.160), waist=(0.116, 0.095, 0.114),
                 belly=(0.114, 0.097, 0.106, -0.008), pelvis=(0.118, 0.096, 0.108), butt=(0.055, 0.057, 0.071),
                 neck=0.058, delt=0.066, girdle=0.065, arm=(0.055, 0.042), forearm=0.044, thigh=(0.069, 0.050),
                 calf=(0.054, 0.034), calf_m=0.046, pecs=1.2, bust=0.0, trap=0.048),
    "hero_f": dict(shoulder_x=0.182, hip_x=0.084, chest=(0.122, 0.097, 0.150), waist=(0.098, 0.082, 0.108),
                   belly=(0.104, 0.088, 0.100, -0.006), pelvis=(0.128, 0.102, 0.110), butt=(0.062, 0.061, 0.075),
                   neck=0.049, delt=0.053, girdle=0.054, arm=(0.046, 0.036), forearm=0.038, thigh=(0.069, 0.048),
                   calf=(0.050, 0.031), calf_m=0.042, pecs=0.0, bust=0.95, trap=0.037),
    "heavy": dict(shoulder_x=0.200, hip_x=0.092, chest=(0.150, 0.124, 0.160), waist=(0.146, 0.132, 0.13),
                  belly=(0.156, 0.156, 0.145, -0.056), pelvis=(0.138, 0.112, 0.116), butt=(0.064, 0.062, 0.078),
                  neck=0.066, delt=0.066, girdle=0.068, arm=(0.058, 0.046), forearm=0.047, thigh=(0.082, 0.058),
                  calf=(0.059, 0.036), calf_m=0.051, pecs=1.2, bust=0.0, trap=0.052),
    # bodyguard: broad shoulders, barrel chest, a firm belly, narrow hips
    "bulky": dict(shoulder_x=0.205, hip_x=0.088, chest=(0.166, 0.140, 0.172), waist=(0.146, 0.126, 0.128),
                  belly=(0.142, 0.128, 0.128, -0.030), pelvis=(0.130, 0.106, 0.112), butt=(0.058, 0.058, 0.074),
                  neck=0.072, delt=0.074, girdle=0.076, arm=(0.063, 0.049), forearm=0.051, thigh=(0.080, 0.058),
                  calf=(0.060, 0.037), calf_m=0.052, pecs=1.4, bust=0.0, trap=0.062),
    "female": dict(shoulder_x=0.180, hip_x=0.086, chest=(0.122, 0.097, 0.150), waist=(0.100, 0.084, 0.11),
                   belly=(0.108, 0.090, 0.105, -0.008), pelvis=(0.132, 0.105, 0.114), butt=(0.064, 0.062, 0.077),
                   neck=0.049, delt=0.051, girdle=0.052, arm=(0.045, 0.035), forearm=0.037, thigh=(0.072, 0.049),
                   calf=(0.050, 0.031), calf_m=0.042, pecs=0.0, bust=1.0, trap=0.035),
    "female_curvy": dict(shoulder_x=0.182, hip_x=0.090, chest=(0.126, 0.100, 0.150), waist=(0.112, 0.095, 0.12),
                         belly=(0.120, 0.106, 0.118, -0.018), pelvis=(0.142, 0.112, 0.120), butt=(0.070, 0.066, 0.082),
                         neck=0.052, delt=0.054, girdle=0.055, arm=(0.049, 0.038), forearm=0.040, thigh=(0.078, 0.052),
                         calf=(0.054, 0.033), calf_m=0.045, pecs=0.0, bust=1.2, trap=0.039),
}


class BodyShape:
    def __init__(self, preset="average"):
        self.preset = preset
        self.p = dict(PRESETS[preset])
        self.female = preset.startswith("female") or preset.endswith("_f")
        self.sk = Build(self.p["shoulder_x"], self.p["hip_x"], self.female)
        self.j = self.sk.j
        self.lumps = []          # (name, prim, k, region)
        self._build()

    # ------------------------------------------------------------------ lumps
    def _add(self, name, prim, k, region):
        self.lumps.append((name, prim, k, region))

    def _build(self):
        p, j = self.p, self.j
        cx, cy, cz = p["chest"]
        self._add("chest", sdf.Ellipsoid(V(0, 0.002, 1.255), (cx, cy, cz)), 0.0, "torso")
        self._add("back", sdf.Ellipsoid(V(0, 0.040, 1.285), (cx * 0.95, cy * 0.66, 0.115)), 0.05, "torso")
        wx, wy, wz = p["waist"]
        self._add("waist", sdf.Ellipsoid(V(0, 0.004, 1.085), (wx, wy, wz)), 0.06, "torso")
        bx, by, bz, boy = p["belly"]
        self._add("belly", sdf.Ellipsoid(V(0, boy, 1.020), (bx, by, bz)), 0.06, "torso")
        px, py, pz = p["pelvis"]
        self._add("pelvis", sdf.Ellipsoid(V(0, 0.008, 0.935), (px, py, pz)), 0.05, "hips")
        ux, uy, uz = p["butt"]
        for sx in (1, -1):
            self._add("butt", sdf.Ellipsoid(V(sx * 0.062, 0.058, 0.875), (ux, uy, uz)), 0.04, "hips")
        if p["pecs"] > 0:
            for sx in (1, -1):
                self._add("pec", sdf.Ellipsoid(V(sx * 0.060, -0.062, 1.272), (0.064 * p["pecs"], 0.036 * p["pecs"], 0.048)), 0.045, "torso")
        if p["bust"] > 0:
            b = p["bust"]
            for sx in (1, -1):
                self._add("bust", sdf.Ellipsoid(V(sx * 0.056, -0.066, 1.235), (0.058 * b, 0.050 * b, 0.056 * b),
                                                sdf.rot((0, 0, 1), sx * 12)), 0.035, "torso")
        g = p["girdle"]
        sxw = self.sk.shoulder_x
        self._add("girdle", sdf.Capsule(V(-(sxw - 0.06), 0.006, 1.372), V(sxw - 0.06, 0.006, 1.372), g), 0.05, "torso")
        for s, sx in (("l", 1), ("r", -1)):
            self._add("trap", sdf.RoundCone(V(0, 0.030, 1.455), V(sx * (sxw - 0.045), 0.018, 1.405), p["trap"] + 0.008, p["trap"]), 0.04, "torso")
            self._add("delt_" + s, sdf.Sphere(j["shoulder_" + s] + V(sx * 0.004, 0.0, 0.003), p["delt"]), 0.04, "shoulder_" + s)
        self._add("neck", sdf.RoundCone(V(0, 0.014, 1.380), V(0, 0.006, 1.560), p["neck"] + 0.004, p["neck"]), 0.04, "neck")
        # arms
        a0, a1 = p["arm"]
        for s in ("l", "r"):
            sh, el = j["shoulder_" + s], j["elbow_" + s]
            self._add("upperarm_" + s, sdf.RoundCone(sh, el, a0, a1), 0.03, "upperarm_" + s)
            mid = sh + (el - sh) * 0.45
            R = sdf.frame(el - sh, (0, -1, 0))
            self._add("biceps_" + s, sdf.Ellipsoid(mid + V(0, -0.006, 0), (a0 * 0.86, a0 * 0.80, 0.10), R), 0.02, "upperarm_" + s)
            self._add("elbow_" + s, sdf.Sphere(el, a1 * 0.98), 0.015, "upperarm_" + s)
            fr = glove.Frame(*self.sk.hand_frame(s))
            for i, (prim, k) in enumerate(glove.forearm_prims(el, fr, p["forearm"])):
                self._add("forearm_" + s, prim, 0.02 if i == 0 else 0.018, "forearm_" + s)
        # legs
        t0, t1 = p["thigh"]
        c0, c1 = p["calf"]
        for s, sx in (("l", 1), ("r", -1)):
            hp, kn, an = j["hip_" + s], j["knee_" + s], j["ankle_" + s]
            self._add("thigh_" + s, sdf.RoundCone(hp + V(sx * 0.004, 0, -0.035), kn, t0, t1), 0.05, "thigh_" + s)
            R = sdf.frame(kn - hp, (1, 0, 0))
            self._add("quad_" + s, sdf.Ellipsoid(hp + (kn - hp) * 0.40 + V(sx * 0.010, -0.010, 0), (t0 * 0.92, t0 * 0.92, 0.17), R), 0.04, "thigh_" + s)
            self._add("knee_" + s, sdf.Sphere(kn + V(0, -0.004, 0), t1 * 0.86), 0.03, "knee_" + s)
            self._add("calf_" + s, sdf.RoundCone(kn, an + V(0, 0, 0.02), c0, c1), 0.02, "calf_" + s)
            R = sdf.frame(an - kn, (1, 0, 0))
            self._add("calfm_" + s, sdf.Ellipsoid(kn + (an - kn) * 0.30 + V(0, 0.016, 0), (p["calf_m"] * 0.95, p["calf_m"], 0.11), R), 0.03, "calf_" + s)
            self._add("ankle_" + s, sdf.Sphere(an + V(0, 0.002, 0.004), c1 * 1.02), 0.02, "calf_" + s)
            # simple foot (always inside a shoe)
            self._add("foot_" + s, sdf.RoundCone(an + V(0, 0.020, -0.03), self.j["ball_" + s] + V(0, 0.0, 0.012), 0.034, 0.030), 0.02, "foot_" + s)

    # ------------------------------------------------------------------ access
    def select(self, regions):
        """Lumps whose region starts with one of `regions` ('torso', 'hips', 'upperarm', 'thigh', ...)."""
        return [(n, pr, k) for n, pr, k, r in self.lumps if any(r.startswith(x) for x in regions)]

    def frame(self, side):
        return glove.Frame(*self.sk.hand_frame(side))

    def skin_part(self, voxel=0.003, material="Skin", regions=None):
        part = sdf.Part("Body", voxel, material)
        for n, pr, k, r in self.lumps:
            if regions is None or any(r.startswith(x) for x in regions):
                part.add(pr, k)
        for s in ("l", "r"):
            part.sub(glove.body_cut(self.frame(s)), 0.0)
        return part

    def hand(self, side, style="bare", material="Skin", ease=0.0, name=None):
        fr = self.frame(side)
        return glove.hand_part(name or ("Hand_" + side), fr, style, material,
                               elbow=self.j["elbow_" + side] if style == "bare" else None,
                               forearm_r=self.p["forearm"], ease=ease)

    def shell(self, part, regions, ease=0.01, k_extra=0.02, k_min=0.03):
        """Add the lumps of `regions`, blended softer, to a garment Part (offset by `ease` afterwards)."""
        for n, pr, k in self.select(regions):
            part.add(pr, max(k + k_extra, k_min))
        return part

    def limb_radius(self, kind, t):
        """Approximate body radius along a limb (t = 0 at the proximal joint, 1 at the distal)."""
        p = self.p
        if kind == "upperarm":
            return p["arm"][0] + (p["arm"][1] - p["arm"][0]) * t
        if kind == "forearm":
            return p["forearm"] + (0.0285 - p["forearm"]) * t
        if kind == "thigh":
            return p["thigh"][0] + (p["thigh"][1] - p["thigh"][0]) * t
        if kind == "calf":
            return p["calf"][0] + (p["calf"][1] - p["calf"][0]) * t
        raise KeyError(kind)
