# Author : Baptiste Vu Liem
"""Every sound of the game, synthesised (python 3, numpy, scipy).

    python Tools/Audio/sfx_gen.py            # all of them, into Tools/Blender/out/audio
    python Tools/Audio/sfx_gen.py shot punch # only the names starting with these

Impacts are modal (a few decaying resonances per material), guns layer a crack, a body and a room tail
by convolution, voices are wordless formant gibberish, ambiences are seamless loops. A name ending in
_N is a variant of its base, drawn with another seed; Audio/Sfx.cs picks one at random. Copy the result
over Assets/_Game/Audio/Resources/Sfx under the editor lock.
"""
import os
import sys
import zlib

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, fftconvolve, sosfilt

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Blender", "out", "audio")


# ---------------------------------------------------------------- blocks

def T(seconds):
    return np.arange(int(seconds * SR)) / SR


def lp(x, hz, order=2):
    return sosfilt(butter(order, min(hz, SR * 0.45), "low", fs=SR, output="sos"), x)


def hp(x, hz, order=2):
    return sosfilt(butter(order, hz, "high", fs=SR, output="sos"), x)


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, min(hi, SR * 0.45)], "band", fs=SR, output="sos"), x)


def noise(r, seconds):
    return r.standard_normal(int(seconds * SR))


def brown(r, seconds):
    x = np.cumsum(noise(r, seconds))
    return hp(x / (np.abs(x).max() + 1e-9), 20)


def decay(seconds, rate, attack=0.001):
    t = T(seconds)
    return np.exp(-t * rate) * np.minimum(1.0, t / max(attack, 1e-5))


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def at(x, seconds):
    return np.concatenate([np.zeros(int(seconds * SR)), x])


def seq(*items):
    """(start seconds, sound), ... on one timeline."""
    return mix(*[at(s, t) for t, s in items])


def sweep(seconds, f0, f1, curve=1.0):
    t = T(seconds)
    f = f0 + (f1 - f0) * (t / seconds) ** curve
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def modal(r, freqs, rates, amps=None, seconds=0.6, detune=0.03):
    """Struck material: decaying partials, slightly different at every strike."""
    t = T(seconds)
    amps = amps if amps is not None else [1.0 / (i + 1) for i in range(len(freqs))]
    out = np.zeros(len(t))
    for f, d, a in zip(freqs, rates, amps):
        out += a * np.sin(2 * np.pi * f * (1 + r.uniform(-detune, detune)) * t + r.uniform(0, 6.28)) * np.exp(-t * d)
    return out * np.minimum(1.0, t / 0.0006)


def tick(r, seconds=0.012, lo=1500, hi=9000):
    return bp(noise(r, seconds), lo, hi) * decay(seconds, 6 / seconds, 0.0003)


def thump(f0, f1, seconds, rate):
    return sweep(seconds, f0, f1, 0.5) * decay(seconds, rate, 0.001)


def swish(r, seconds, lo, hi, up=True):
    """A shape moving through the air: filtered noise whose band sweeps across, loudest in the middle."""
    n = int(seconds * SR)
    x = noise(r, seconds)
    parts = 16
    out = np.zeros(n)
    win = np.hanning(2 * n // parts + 1)
    for i in range(parts - 1):
        k = (i + 1) / parts
        f = (lo + (hi - lo) * k) if up else (hi + (lo - hi) * k)
        a = i * n // parts
        seg = x[a:a + len(win)]
        out[a:a + len(seg)] += bp(seg, f * 0.7, f * 1.5) * win[:len(seg)]
    return out * np.sin(np.pi * np.arange(n) / n) ** 1.2


def room(r, seconds, rate, lo=200, hi=6000):
    ir = bp(noise(r, seconds), lo, hi) * np.exp(-T(seconds) * rate)
    ir[0] = 1.0
    return ir / np.sqrt((ir ** 2).sum())


def tail(x, r, seconds, rate, wet, lo=250, hi=5000):
    w = fftconvolve(x, room(r, seconds, rate, lo, hi))
    out = np.zeros(len(w))
    out[:len(x)] += x
    return out + w * wet


def loop(x, fade=1.0):
    """Seamless: the end fades into the start."""
    n = int(fade * SR)
    a = np.linspace(0, 1, n)
    body = x[:-n].copy()
    body[:n] = body[:n] * np.sqrt(a) + x[-n:] * np.sqrt(1 - a)
    return body


def level(x, peak_db):
    return x / (np.abs(x).max() + 1e-9) * 10 ** (peak_db / 20)


def clip(x, drive=1.5):
    return np.tanh(x * drive) / np.tanh(drive)


# ---------------------------------------------------------------- guns

def shot(r, crack, body_hz, body_len, room_s, wet, noise_hz=2600, grit=0.4):
    c = hp(noise(r, 0.012), 2800) * decay(0.012, 420, 0.0002) * crack
    b = thump(body_hz * 2.2, body_hz, body_len, 5 / body_len)
    g = bp(noise(r, body_len), 300, noise_hz) * decay(body_len, 7 / body_len) * grit
    return clip(tail(mix(c, b, g), r, room_s, 5.0 / room_s, wet), 1.8)


def shot_pistol(r):
    return shot(r, 0.9, 150, 0.09, 0.8, 0.35)


def shot_smg(r):
    return shot(r, 0.75, 180, 0.06, 0.5, 0.25, grit=0.5)


def shot_shotgun(r):
    return shot(r, 1.0, 85, 0.22, 1.2, 0.45, noise_hz=1700, grit=0.9)


def shot_sniper(r):
    return shot(r, 1.3, 95, 0.18, 2.2, 0.75, noise_hz=2200, grit=0.7)


def shot_silenced(r):
    puff = bp(noise(r, 0.09), 500, 2400) * decay(0.09, 55, 0.002)
    return mix(puff, tick(r, 0.01, 2500, 8000) * 0.5, at(modal(r, [2100, 3400], [160, 200], seconds=0.06) * 0.25, 0.02))


def dryfire(r):
    return mix(modal(r, [3100, 5200], [260, 340], seconds=0.05), tick(r) * 0.6)


def reload_(r):
    a = modal(r, [1700, 2900, 4400], [90, 130, 200], seconds=0.15)
    b = modal(r, [1200, 2300], [70, 120], seconds=0.2)
    return seq((0, swish(r, 0.2, 500, 1800) * 0.2), (0.16, a), (0.42, tick(r) * 0.8), (0.5, b))


def explosion(r):
    low = thump(95, 28, 1.6, 2.6)
    body = lp(noise(r, 1.8), 900) * decay(1.8, 3.2, 0.004)
    crack = hp(noise(r, 0.03), 1500) * decay(0.03, 140, 0.0005)
    debris = sum(at(modal(r, [r.uniform(500, 2400)], [40], seconds=0.1) * r.uniform(0.05, 0.14), r.uniform(0.3, 1.5)) for _ in range(1)) if False else np.zeros(1)
    return clip(tail(mix(low * 1.3, body, crack * 0.7, debris), r, 2.2, 2.4, 0.7, 120, 2500), 2.2)


def rocket_launch(r):
    return clip(mix(thump(120, 45, 0.3, 11), swish(r, 0.9, 500, 2400) * 1.0, lp(noise(r, 0.9), 700) * decay(0.9, 3.5, 0.01) * 0.6), 1.5)


def rocket_fly(r):
    x = bp(noise(r, 2.2), 300, 1900) * 0.7 + 0.25 * sweep(2.2, 180, 210)
    return loop(x, 0.3)


def whizz(r):
    return swish(r, 0.22, 1400, 3800) * 0.8


# ---------------------------------------------------------------- bodies, punches, things thrown

def punch(r):
    return clip(mix(thump(150, 60, 0.13, 26) * 1.2, hp(noise(r, 0.018), 1200) * decay(0.018, 180, 0.0003) * 0.7, bp(noise(r, 0.07), 300, 1100) * decay(0.07, 60) * 0.5), 1.7)


def hit(r):
    return clip(mix(thump(190, 80, 0.09, 36), bp(noise(r, 0.05), 500, 2200) * decay(0.05, 70) * 0.7), 1.5)


def hit_head(r):
    return mix(hit(r), modal(r, [840, 1370], [60, 90], seconds=0.16) * 0.5)


def combat_hit(r):
    return seq((0, thump(70, 40, 0.5, 7) * 1.0), (0, hp(noise(r, 0.05), 1400) * decay(0.05, 50) * 0.3))


def thud(r):
    return mix(thump(100, 48, 0.24, 16) * 1.2, lp(noise(r, 0.12), 500) * decay(0.12, 34) * 0.6)


def death(r):
    return seq((0, thud(r)), (0.13, thump(120, 55, 0.14, 26) * 0.6), (0.22, lp(noise(r, 0.2), 700) * decay(0.2, 20) * 0.3))


def takedown(r):
    return seq((0, swish(r, 0.1, 400, 1400) * 0.4), (0.04, punch(r)), (0.08, modal(r, [420, 760], [70, 110], seconds=0.1) * 0.35))


def swing(r):
    return swish(r, 0.14, 500, 2100)


def throw(r):
    return swish(r, 0.2, 400, 1700)


def brick(r):
    return mix(modal(r, [520, 980, 1650], [50, 70, 110], seconds=0.25), lp(noise(r, 0.05), 3000) * decay(0.05, 60) * 0.5)


def coin(r):
    return seq((0, modal(r, [4200, 6300, 9100], [24, 34, 48], seconds=0.5)), (0.14, modal(r, [4300, 6500], [40, 50], seconds=0.28) * 0.5), (0.24, modal(r, [4400], [70], seconds=0.15) * 0.25))


def glass(r):
    shards = [(r.uniform(0, 0.16), modal(r, [r.uniform(2500, 7800), r.uniform(5000, 11000)], [40, 70], seconds=0.25) * r.uniform(0.25, 0.8)) for _ in range(14)]
    return seq((0, hp(noise(r, 0.05), 1800) * decay(0.05, 50)), *shards)


def metal_clang(r):
    return tail(modal(r, [420, 1130, 1870, 2760], [9, 14, 20, 30], seconds=0.9), r, 0.6, 7, 0.2)


def crowbar(r):
    return seq((0, modal(r, [620, 1540, 2510], [20, 30, 40], seconds=0.4)), (0.14, lp(noise(r, 0.3), 1200) * decay(0.3, 10) * 0.5))


def chain_snap(r):
    return seq((0, hp(noise(r, 0.02), 2000) * decay(0.02, 180)), *[(0.03 + i * 0.05, modal(r, [2300 + i * 200, 3600], [60, 90], seconds=0.1) * (0.7 - i * 0.1)) for i in range(5)])


def crash(r):
    return seq((0, thump(90, 40, 0.5, 8)), (0, hp(noise(r, 0.3), 700) * decay(0.3, 9) * 0.8), (0.05, glass(r) * 0.5), (0.1, metal_clang(r) * 0.7))


# ---------------------------------------------------------------- feet and movement

def step(r, body_hz, body, grit_lo, grit_hi, grit, length=0.1, extra=None):
    x = mix(thump(body_hz * 1.5, body_hz, length, 6 / length) * body, bp(noise(r, length), grit_lo, grit_hi) * decay(length, 5 / length, 0.003) * grit)
    return mix(x, extra) if extra is not None else x


def step_grass(r):
    return mix(step(r, 90, 0.3, 700, 3800, 0.8, 0.14), swish(r, 0.14, 1200, 3000) * 0.3)


def step_dirt(r):
    return step(r, 95, 0.8, 300, 2400, 0.7)


def step_sand(r):
    return step(r, 80, 0.4, 1200, 6500, 0.8, 0.16)


def step_stone(r):
    return step(r, 120, 0.7, 1500, 6000, 0.45, 0.08, tick(r, 0.008) * 0.45)


def step_wood(r):
    return step(r, 110, 0.6, 400, 2500, 0.3, 0.1, modal(r, [230, 440, 730], [40, 60, 90], seconds=0.13) * 0.55)


def step_metal(r):
    return step(r, 120, 0.5, 900, 3500, 0.3, 0.08, modal(r, [560, 1240, 2130], [24, 34, 46], seconds=0.25) * 0.5)


def step_water(r):
    return mix(step(r, 90, 0.3, 500, 2600, 0.6, 0.18), at(bp(noise(r, 0.14), 900, 4200) * decay(0.14, 18, 0.01) * 0.6, 0.04))


def footstep(r):
    return step_stone(r)


def jump(r):
    return mix(swish(r, 0.16, 500, 1600) * 0.6, thump(110, 70, 0.06, 40) * 0.4)


def land(r):
    return mix(thump(90, 45, 0.18, 20), bp(noise(r, 0.1), 400, 2400) * decay(0.1, 36) * 0.5)


def mantle(r):
    return seq((0, swish(r, 0.3, 400, 1400) * 0.7), (0.22, thump(120, 70, 0.08, 34) * 0.5))


def vault(r):
    return seq((0, swish(r, 0.24, 500, 1900) * 0.8), (0.18, thump(110, 65, 0.1, 30) * 0.5))


def slide(r):
    return bp(noise(r, 0.6), 500, 3200) * np.sin(np.pi * np.linspace(0, 1, int(0.6 * SR))) ** 0.5 * 0.8


def ladder_drop(r):
    return seq(*[(i * 0.07, modal(r, [720 + i * 40, 1650], [34, 50], seconds=0.2) * (0.9 - i * 0.1)) for i in range(6)])


def splash(r):
    return mix(lp(noise(r, 0.8), 3200) * decay(0.8, 5, 0.01), thump(120, 50, 0.2, 18) * 0.7, *[at(modal(r, [r.uniform(500, 1500)], [50], seconds=0.1) * 0.15, r.uniform(0.05, 0.6)) for _ in range(8)])


def splash_small(r):
    return mix(bp(noise(r, 0.25), 500, 4000) * decay(0.25, 14, 0.005), *[at(modal(r, [r.uniform(700, 1800)], [70], seconds=0.06) * 0.2, r.uniform(0, 0.15)) for _ in range(4)])


def duck(r):
    """A rubber duck squeezed: a nasal rising squeak, then its sigh."""
    def squeak(seconds, f0, f1):
        phase = 2 * np.pi * np.cumsum(np.linspace(f0, f1, int(SR * seconds))) / SR
        tone = np.sin(phase) + 0.5 * np.sin(2 * phase) + 0.3 * np.sin(3 * phase)
        return bp(tone, 500, 5000) * np.sin(np.pi * np.linspace(0, 1, len(phase))) ** 0.5
    return seq((0.0, squeak(0.11, 1200, 1950)), (0.12, squeak(0.16, 1700, 1050) * 0.8))


def swim(r):
    return bp(noise(r, 0.5), 300, 1800) * np.sin(np.pi * np.linspace(0, 1, int(0.5 * SR))) ** 1.5 * 0.7


def underwater(r):
    x = lp(brown(r, 9), 320) + 0.08 * lp(noise(r, 9), 800)
    for _ in range(26):
        x = mix(x, at(sweep(0.06, r.uniform(500, 900), r.uniform(1100, 1700)) * decay(0.06, 40, 0.005) * 0.06, r.uniform(0, 8.7)))
    return loop(x[:int(9 * SR)], 1.0)


# ---------------------------------------------------------------- interface and game events

def note(hz, seconds=0.4, rate=7, harm=0.25):
    t = T(seconds)
    return (np.sin(2 * np.pi * hz * t) + harm * np.sin(4 * np.pi * hz * t)) * np.exp(-t * rate) * np.minimum(1, t / 0.004)


def click(r):
    return mix(tick(r, 0.006, 1800, 7000) * 0.7, modal(r, [1650], [260], seconds=0.03) * 0.7)


def hover(r):
    return modal(r, [2300], [420], seconds=0.02) * 0.6


def map_(r):
    return swish(r, 0.26, 900, 3800) * 0.7


def pickup(r):
    return seq((0, note(740, 0.16, 26)), (0.07, note(1110, 0.22, 20)))


def ping(r):
    return note(1320, 0.5, 9, 0.1)


def announce(r):
    return seq((0, note(392, 0.7, 5)), (0, note(587, 0.7, 5) * 0.7), (0.12, note(784, 0.8, 5) * 0.5))


def spotted(r):
    return mix(note(233, 0.5, 8, 0.5), note(247, 0.5, 8, 0.5), at(note(932, 0.3, 14) * 0.5, 0.01))


def target_down(r):
    return seq((0, note(440, 0.5, 6)), (0.14, note(659, 0.7, 5)), (0.28, note(880, 0.9, 4)))


def win(r):
    return seq((0, note(523, 0.6, 5)), (0.16, note(659, 0.6, 5)), (0.32, note(784, 0.7, 4)), (0.48, note(1047, 1.2, 3)), (0.48, note(784, 1.2, 3) * 0.5))


def lose(r):
    return seq((0, note(392, 0.8, 4)), (0.24, note(370, 0.8, 4)), (0.48, note(311, 1.3, 2.5)), (0.48, note(233, 1.3, 2.5) * 0.6))


def tension_loop(r):
    t = T(12)
    x = sum(a * np.sin(2 * np.pi * f * t) for f, a in ((55, 1), (55.5, 0.8), (82.5, 0.3), (110.3, 0.2)))
    return loop(x * (0.8 + 0.2 * np.sin(2 * np.pi * t / 4)) + 0.05 * lp(noise(r, 12), 400), 1.0)


def radio(r):
    squelch = bp(noise(r, 0.05), 1400, 5200) * decay(0.05, 40)
    hiss = bp(noise(r, 0.34), 1200, 3600) * 0.28
    beep = note(1560, 0.08, 40, 0)
    return seq((0, squelch), (0.03, beep * 0.5), (0.05, hiss), (0.38, squelch * 0.8))


def turret_warn(r):
    return seq(*[(i * 0.18, note(1700, 0.1, 30, 0.4)) for i in range(4)])


# ---------------------------------------------------------------- machines and places

def door_unlock(r):
    return seq((0, modal(r, [1500, 2800], [90, 130], seconds=0.1)), (0.08, thump(160, 90, 0.07, 40) * 0.6), (0.14, modal(r, [1100, 2300], [70, 110], seconds=0.1)))


def switch(r):
    return seq((0, modal(r, [2400, 3700], [260, 340], seconds=0.04)), (0.03, thump(180, 110, 0.03, 90) * 0.4))


def c4_stick(r):
    return seq((0, thump(170, 90, 0.05, 50)), (0.05, bp(noise(r, 0.06), 1500, 5000) * decay(0.06, 50) * 0.3), (0.2, note(2100, 0.06, 40, 0) * 0.5))


def smoke_pop(r):
    return mix(thump(220, 90, 0.07, 40), bp(noise(r, 1.2), 1500, 7000) * decay(1.2, 2.6, 0.01) * 0.4)


def flame_start(r):
    return lp(noise(r, 0.6), 1600) * np.minimum(1, T(0.6) / 0.08) * (0.7 + 0.3 * np.exp(-T(0.6) * 5))


def fire_start(r):
    x = lp(noise(r, 1.1), 1100) * np.minimum(1, T(1.1) / 0.25) * 0.6
    return mix(x, *[at(tick(r, 0.006, 900, 5000) * r.uniform(0.2, 0.7), r.uniform(0, 1.0)) for _ in range(16)])


def vent_hiss(r):
    return bp(noise(r, 1.4), 2000, 9000) * np.minimum(1, T(1.4) / 0.06) * np.exp(-T(1.4) * 1.2) * 0.6


def vent_open(r):
    return seq((0, modal(r, [760, 1450], [38, 60], seconds=0.2)), (0.08, bp(noise(r, 0.25), 400, 1500) * decay(0.25, 14) * 0.5))


def power_off(r):
    return mix(sweep(0.8, 140, 34) * decay(0.8, 3.6, 0.002) * 0.7, thump(120, 50, 0.1, 30))


def power_on(r):
    return mix(sweep(0.8, 34, 130) * np.minimum(1, T(0.8) / 0.4) * np.exp(-T(0.8) * 2) * 0.7, thump(150, 70, 0.08, 40) * 0.7)


def generator(r):
    t = T(2.0)
    x = sum(np.sign(np.sin(2 * np.pi * f * t)) * a for f, a in ((48, 1), (96, 0.3)))
    return loop(lp(x, 500) * 0.5 + 0.2 * lp(noise(r, 2.0), 1200), 0.2)


def fan(r):
    t = T(2.0)
    return loop(bp(noise(r, 2.0), 300, 2000) * (0.7 + 0.3 * np.sin(2 * np.pi * 24 * t)), 0.2)


def computer(r):
    return seq(*[(i * 0.11, note(r.choice([880, 1175, 1568]), 0.08, 30, 0) * 0.6) for i in range(4)])


def keyboard(r):
    return seq(*[(i * 0.09 + r.uniform(-0.02, 0.02) + 0.02, modal(r, [1800, 3100], [220, 300], seconds=0.04)) for i in range(6)])


def tv(r):
    x = bp(noise(r, 1.2), 1200, 6000) * 0.25
    return mix(x, note(15700 / 4, 1.2, 0.3, 0) * 0.03, *[at(formant(r, 150, 0.2, r.choice(3)) * 0.5, 0.1 + i * 0.24) for i in range(3)])


def phone(r):
    t = T(1.4)
    ring = np.sin(2 * np.pi * 950 * t) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 20 * t)))
    return ring * np.where(t < 1.0, 1.0, 0.0) * np.minimum(1, t / 0.005) * 0.6


def caralarm(r):
    t = T(1.8)
    return np.sign(np.sin(2 * np.pi * np.cumsum(1000 + 700 * np.sign(np.sin(2 * np.pi * 3 * t))) / SR)) * 0.5


def siren(r):
    t = T(4.0)
    f = 620 + 340 * np.sin(2 * np.pi * t / 4 - np.pi / 2)
    x = lp(np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)), 2600)
    return tail(x, r, 1.0, 4, 0.2)[:len(x)]


def gas_alarm(r):
    return seq(*[(i * 0.4, note(1040, 0.25, 4, 0.5)) for i in range(5)])


def brakes(r):
    return mix(sweep(0.9, 2600, 1800) * np.minimum(1, T(0.9) / 0.1) * np.exp(-T(0.9) * 1.6) * 0.5, bp(noise(r, 0.9), 800, 4000) * decay(0.9, 2.5) * 0.4)


def truck(r):
    t = T(3.0)
    x = lp(np.sign(np.sin(2 * np.pi * np.cumsum(38 + 9 * t / 3) / SR)), 340) + 0.3 * lp(noise(r, 3.0), 700)
    return x * np.sin(np.pi * t / 3) ** 0.7


def hose(r):
    return loop(bp(noise(r, 1.6), 900, 7000) * 0.6, 0.2)


def pour(r):
    x = bp(noise(r, 1.2), 500, 2600)
    return mix(x * 0.5, *[at(modal(r, [r.uniform(600, 1300)], [80], seconds=0.05) * 0.2, r.uniform(0, 1.1)) for _ in range(30)])


def puke(r):
    return seq((0, formant(r, 90, 0.4, 2)), (0.3, bp(noise(r, 0.5), 300, 1500) * decay(0.5, 5, 0.03) * 0.8))


def sprinkler(r):
    return mix(bp(noise(r, 1.6), 2500, 9000) * 0.4, *[at(tick(r, 0.012, 2500, 7000) * 0.7, i * 0.12) for i in range(13)])


def jumppad(r):
    return mix(sweep(0.28, 180, 780, 0.8) * decay(0.28, 8, 0.002), thump(110, 60, 0.1, 30) * 0.7)


# ---------------------------------------------------------------- voices: formant gibberish

VOWELS = [(730, 1090, 2440), (530, 1840, 2480), (270, 2290, 3010), (570, 840, 2410), (300, 870, 2240), (660, 1720, 2410)]


def formant(r, pitch, seconds, vowel=None, glide=0.0, scale=1.0, breath=0.05):
    """One syllable: a glottal buzz through three vowel resonances."""
    t = T(seconds)
    v = VOWELS[r.integers(len(VOWELS)) if vowel is None else vowel]
    f = pitch * (1 + glide * t / seconds) * (1 + 0.012 * np.sin(2 * np.pi * 5.5 * t))
    src = 2 * ((np.cumsum(f) / SR) % 1.0) - 1
    src = src + r.standard_normal(len(t)) * breath
    out = np.zeros(len(t))
    for hz, gain in zip(v, (1.0, 0.6, 0.25)):
        out += bp(src, hz * scale * 0.88, hz * scale * 1.12) * gain
    return out * np.sin(np.pi * np.minimum(1, t / seconds)) ** 0.6


def babble(r, pitch, scale, count, length, glide_end, gap=0.02, loud=1.0):
    items, cursor = [], 0.0
    for i in range(count):
        k = i / max(1, count - 1)
        p = pitch * r.uniform(0.92, 1.1) * (1 + glide_end * k)
        ln = length * r.uniform(0.7, 1.3)
        items.append((cursor, formant(r, p, ln, None, glide_end * 0.3, scale) * loud))
        cursor += ln + gap * r.uniform(0.5, 2)
    return seq(*items)


VOICES = {"low": (95, 0.88), "mid": (135, 1.0), "high": (205, 1.14), "squeak": (310, 1.3)}


def bark(voice, kind, r):
    pitch, scale = VOICES[voice]
    if kind == "chatter":
        return babble(r, pitch, scale, r.integers(5, 9), 0.13, r.uniform(-0.2, 0.15))
    if kind == "hey":
        return babble(r, pitch * 1.35, scale, 1, 0.3, 0.35, loud=1.2)
    if kind == "huh":
        return babble(r, pitch * 1.05, scale, 1, 0.26, 0.4)
    if kind == "clear":
        return babble(r, pitch, scale, 3, 0.16, -0.25)
    if kind == "help":
        return babble(r, pitch * 1.5, scale, 3, 0.2, 0.3, loud=1.2)
    if kind == "overthere":
        return babble(r, pitch * 1.25, scale, 4, 0.16, 0.25, loud=1.1)
    if kind == "radio":
        x = babble(r, pitch, scale, 5, 0.12, -0.1)
        return seq((0, bp(noise(r, 0.04), 1500, 5000) * 0.4), (0.04, bp(x, 500, 2800) * 1.6))
    if kind == "scream":
        return formant(r, pitch * 2.6, 0.7, 0, 0.3, scale * 1.15, 0.2)
    raise KeyError(kind)


# ---------------------------------------------------------------- ambiences

def wind(r, seconds, lo, hi, gust):
    t = T(seconds)
    swell = 0.55 + gust * (0.5 * np.sin(2 * np.pi * t / seconds * 3 + 1) + 0.5 * np.sin(2 * np.pi * t / seconds * 5))
    return bp(noise(r, seconds), lo, hi) * swell


def bird(r):
    f = r.uniform(2400, 4600)
    n = r.integers(2, 6)
    items = [(i * r.uniform(0.09, 0.16), sweep(0.07, f * r.uniform(0.9, 1.1), f * r.uniform(1.1, 1.4)) * decay(0.07, 30, 0.01)) for i in range(n)]
    return seq(*items)


def amb_forest(r):
    n = 24
    x = wind(r, n, 300, 2400, 0.25) * 0.14
    x = x + bp(noise(r, n), 5200, 8200) * 0.012 * (1 + np.sin(2 * np.pi * 18 * T(n)))
    x = mix(x, *[at(bird(r) * r.uniform(0.05, 0.16), r.uniform(0, n - 2)) for _ in range(46)])
    return loop(x[:n * SR], 1.5)


def amb_desert(r):
    n = 24
    x = wind(r, n, 150, 1400, 0.45) * 0.28 + wind(r, n, 1800, 5200, 0.5) * 0.04
    return loop(x, 1.5)


def amb_night(r):
    n = 24
    x = wind(r, n, 120, 900, 0.5) * 0.2 + wind(r, n, 2200, 6000, 0.45) * 0.015
    far = [at(formant(r, 110, 0.5, 4, 0.05, 0.7, 0.3) * r.uniform(0.008, 0.02), r.uniform(0, n - 2)) for _ in range(6)]
    return loop(mix(x, *far)[:n * SR], 1.5)


# ---------------------------------------------------------------- the list

GROUPS = {
    -2.0: {"shot_pistol": shot_pistol, "shot_smg": shot_smg, "shot_shotgun": shot_shotgun, "shot_sniper": shot_sniper, "explosion": explosion, "rocket_launch": rocket_launch, "crash": crash, "siren": siren},
    -5.0: {"shot_silenced": shot_silenced, "punch": punch, "hit": hit, "hit_head": hit_head, "combat_hit": combat_hit, "thud": thud, "death": death, "takedown": takedown, "glass": glass, "metal_clang": metal_clang, "crowbar": crowbar, "chain_snap": chain_snap, "splash": splash, "caralarm": caralarm, "gas_alarm": gas_alarm, "phone": phone, "truck": truck, "brakes": brakes, "power_on": power_on, "power_off": power_off, "jumppad": jumppad, "smoke_pop": smoke_pop, "flame_start": flame_start, "fire_start": fire_start},
    -8.0: {"duck": duck, "swing": swing, "throw": throw, "brick": brick, "coin": coin, "dryfire": dryfire, "reload": reload_, "whizz": whizz, "rocket_fly": rocket_fly, "land": land, "jump": jump, "mantle": mantle, "vault": vault, "slide": slide, "ladder_drop": ladder_drop, "splash_small": splash_small, "swim": swim, "door_unlock": door_unlock, "switch": switch, "c4_stick": c4_stick, "vent_hiss": vent_hiss, "vent_open": vent_open, "generator": generator, "fan": fan, "computer": computer, "keyboard": keyboard, "tv": tv, "hose": hose, "pour": pour, "puke": puke, "sprinkler": sprinkler, "turret_warn": turret_warn, "radio": radio, "spotted": spotted, "target_down": target_down, "win": win, "lose": lose, "announce": announce},
    -11.0: {"step_grass": step_grass, "step_dirt": step_dirt, "step_sand": step_sand, "step_stone": step_stone, "step_wood": step_wood, "step_metal": step_metal, "step_water": step_water, "footstep": footstep, "click": click, "hover": hover, "map": map_, "pickup": pickup, "ping": ping},
    -14.0: {"amb_forest": amb_forest, "amb_desert": amb_desert, "amb_night": amb_night, "underwater": underwater, "tension_loop": tension_loop},
}


def write(name, x, peak):
    x = np.nan_to_num(x)
    x = x * np.minimum(1, (len(x) - np.arange(len(x))) / (0.004 * SR))  # no click at the end
    x = level(x, peak)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))
    rms = 20 * np.log10(np.sqrt((x ** 2).mean()) + 1e-9)
    print("%-26s %5.2fs  rms %6.1f dB" % (name, len(x) / SR, rms))


def variants(base):
    """The names of a sound and its variants, as they were in the game."""
    if base.startswith("step_"):
        return [base] + [base + "_%d" % i for i in (1, 2, 3)]
    if base.startswith("shot_"):
        return [base, base + "_1", base + "_2"]
    if base == "swim":
        return [base, base + "_1", base + "_2"]
    if base == "footstep":
        return ["footstep_1", "footstep_2", "footstep_3"]
    return [base]


def main():
    os.makedirs(OUT, exist_ok=True)
    only = sys.argv[1:]
    wanted = lambda n: not only or any(n.startswith(o) for o in only)
    for peak, group in GROUPS.items():
        for base, fn in group.items():
            for name in variants(base):
                if wanted(name):
                    write(name, fn(np.random.default_rng(zlib.crc32(name.encode()))), peak)
    for voice in VOICES:
        for kind, count in (("chatter", 3), ("clear", 2), ("help", 2), ("hey", 2), ("huh", 2), ("overthere", 2), ("radio", 2), ("scream", 2)):
            for i in range(count):
                name = "bark_%s_%s%s" % (voice, kind, "_%d" % i if i else "")
                if wanted(name):
                    write(name, bark(voice, kind, np.random.default_rng(zlib.crc32(name.encode()))), -7.0)


if __name__ == "__main__":
    main()
