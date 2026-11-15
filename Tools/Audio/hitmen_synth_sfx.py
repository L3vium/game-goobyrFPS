"""Synthesized one-shots the sound packs do not cover (headshot, hamster squeaks), written as 16-bit mono WAVs into
Tools/Audio/Synth. Re-run to regenerate; Tools/Editor/hitmen_patch_0100.py (step "sounds") imports them.

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


def main():
    rng = np.random.default_rng(7)
    write("SW_Headshot", headshot(rng))
    write("SW_HamsterSqueak_01", squeak(rng, 2300, 1300, 0.14))
    write("SW_HamsterSqueak_02", squeak(rng, 2700, 900, 0.10))
    write("SW_HamsterSqueak_03", squeak(rng, 2100, 1700, 0.18))


if __name__ == "__main__":
    main()
