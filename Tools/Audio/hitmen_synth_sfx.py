"""Synthesized sounds the sound packs do not cover (headshot, hamster squeaks, zipline clamp and cable loop), written as
16-bit mono WAVs into Tools/Audio/Synth. Re-run to regenerate; Tools/Editor/hitmen_patch_0100.py (step "sounds") and
hitmen_patch_0110.py (step "zipline") import them.

    python Tools/Audio/hitmen_synth_sfx.py
"""
import os
import wave

import numpy as np

RATE = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Synth")


def write(name, samples):
    os.makedirs(OUT, exist_ok=True)
    peak = np.max(np.abs(samples)) or 1.0
    data = np.int16(np.clip(samples / peak * 0.92, -1.0, 1.0) * 32767)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())
    print("wrote", name)


def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def bandpass_noise(seconds, low, high, rng):
    n = len(t(seconds))
    spectrum = np.fft.rfft(rng.standard_normal(n))
    freqs = np.fft.rfftfreq(n, 1.0 / RATE)
    spectrum[(freqs < low) | (freqs > high)] = 0
    return np.fft.irfft(spectrum, n)


def headshot(rng):
    """A dry crack (bandpassed noise, 35 ms) under a bright metallic tink (inharmonic partials, 0.35 s)."""
    time = t(0.4)
    crack = np.zeros_like(time)
    burst = bandpass_noise(0.035, 900, 5000, rng) * np.exp(-t(0.035) / 0.008)
    crack[:len(burst)] = burst
    tink = np.zeros_like(time)
    for freq, amp, decay in ((2650, 1.0, 0.09), (4180, 0.55, 0.06), (6900, 0.3, 0.035), (1320, 0.35, 0.12)):
        tink += amp * np.sin(2 * np.pi * freq * time) * np.exp(-time / decay)
    thump = 0.6 * np.sin(2 * np.pi * 140 * time) * np.exp(-time / 0.03)
    return crack * 1.4 + tink * 0.8 + thump


def squeak(rng, base, rise, length):
    """A short rising chirp with vibrato (a hamster's squeak)."""
    time = t(length)
    freq = base + rise * (time / length) ** 0.6 + 120 * np.sin(2 * np.pi * 38 * time)
    phase = 2 * np.pi * np.cumsum(freq) / RATE
    env = np.sin(np.pi * np.clip(time / length, 0, 1)) ** 0.7
    tone = np.sin(phase) + 0.25 * np.sin(2 * phase) + 0.08 * np.sin(3 * phase)
    return tone * env


def zipline_enter(rng):
    """The trolley clamping onto the cable: a metal clack (two struck partial sets) and a short cable twang."""
    time = t(0.55)
    clack = np.zeros_like(time)
    for offset, gain in ((0.0, 1.0), (0.045, 0.6)):
        start = int(offset * RATE)
        hit_time = time[: len(time) - start]
        strike = bandpass_noise(len(hit_time) / RATE, 1500, 7000, rng) * np.exp(-hit_time / 0.012)
        ring = sum(a * np.sin(2 * np.pi * f * hit_time) * np.exp(-hit_time / d)
                   for f, a, d in ((1870, 0.7, 0.05), (3120, 0.45, 0.035), (5230, 0.25, 0.02)))
        clack[start:] += gain * (strike * 1.2 + ring)
    twang_freq = 110 + 25 * np.exp(-time / 0.08)
    twang = 0.55 * np.sin(2 * np.pi * np.cumsum(twang_freq) / RATE) * np.exp(-time / 0.18)
    thump = 0.5 * np.sin(2 * np.pi * 70 * time) * np.exp(-time / 0.05)
    return clack + twang + thump


def zipline_loop(rng):
    """The steady cable sound of a ride, 2 s, seamless: a rolling pulley whir (band noise), a cable hum and a light
    rattle, every component periodic over the clip so the loop has no seam."""
    seconds = 2.0
    time = t(seconds)
    n = len(time)
    whir = bandpass_noise(seconds, 600, 3200, rng)
    hiss = bandpass_noise(seconds, 3000, 9000, rng) * 0.25
    # Periodic modulations: whole cycles over the clip (the FFT noise above is periodic too).
    roll = 1.0 + 0.25 * np.sin(2 * np.pi * 9.0 * time) + 0.1 * np.sin(2 * np.pi * 23.0 * time)
    hum = sum(a * np.sin(2 * np.pi * f * time) for f, a in ((96.0, 0.5), (192.0, 0.3), (288.0, 0.12), (1150.0, 0.08)))
    rattle = bandpass_noise(seconds, 1800, 6000, rng) * (0.5 + 0.5 * np.sin(2 * np.pi * 31.0 * time)) ** 8 * 0.6
    loop = whir * roll + hiss + hum * 0.6 + rattle
    assert len(loop) == n
    return loop


def main():
    rng = np.random.default_rng(7)
    write("SW_Headshot", headshot(rng))
    write("SW_HamsterSqueak_01", squeak(rng, 2300, 1300, 0.14))
    write("SW_HamsterSqueak_02", squeak(rng, 2700, 900, 0.10))
    write("SW_HamsterSqueak_03", squeak(rng, 2100, 1700, 0.18))
    write("SW_ZiplineEnter", zipline_enter(rng))
    write("SW_ZiplineLoop", zipline_loop(rng))


if __name__ == "__main__":
    main()
