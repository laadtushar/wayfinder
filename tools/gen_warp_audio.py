#!/usr/bin/env python3
"""Generate the warp SFX for Wayfinder (#47) — procedural, numpy -> 16-bit WAV.

Matched to the WarpFade curve (flash up ~0.30 s, hold ~0.16 s, resolve ~0.75 s):
a low SUB-BASS swell that rises with the flash-up (power building), then a
bright, clean ARRIVAL CHIME that shimmers up as the destination world resolves
out of the light. Kept low and comfortable — a swell, not a jump-scare.

Output: unity/Assets/Wayfinder/Audio/warp.wav (mono, 44.1 kHz).

Usage: python tools/gen_warp_audio.py
"""
import wave
import numpy as np
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "unity" / "Assets" / "Wayfinder" / "Audio"
SR = 44100
DUR = 1.25


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def main():
    n = int(SR * DUR)
    t = np.arange(n) / SR

    # --- Sub-bass swell: a low pitch gliding up, amplitude swelling in then out.
    freq = 40.0 + 26.0 * smoothstep(0.0, 0.5, t)          # 40 -> 66 Hz glide
    phase = 2 * np.pi * np.cumsum(freq) / SR
    sub = np.sin(phase) + 0.55 * np.sin(0.5 * phase)      # + a sub-octave rumble
    swell_env = np.minimum(t / 0.30, 1.0) * np.exp(-np.maximum(0.0, t - 0.55) / 0.45)
    swell = sub * swell_env * 0.55

    # --- Arrival chime: a bright bell that blooms up as the world resolves.
    t0 = 0.48
    chime = np.zeros(n)
    for f, a in [(784.0, 1.0), (1568.0, 0.5), (2352.0, 0.28), (3293.0, 0.14)]:
        vib = 1.0 + 0.004 * np.sin(2 * np.pi * 5.5 * t)   # faint shimmer
        chime += a * np.sin(2 * np.pi * f * t * vib)
    decay = np.where(t >= t0, np.exp(-(t - t0) / 0.40), 0.0)
    attack = smoothstep(t0, t0 + 0.04, t)                 # soft onset, no click
    chime *= decay * attack * 0.30

    mix = np.tanh(swell + chime)                          # gentle soft-clip
    mix /= max(np.abs(mix).max(), 1e-9)
    mix *= 0.85
    # 6 ms edge fades to kill boundary clicks
    e = int(SR * 0.006)
    mix[:e] *= np.linspace(0, 1, e)
    mix[-e:] *= np.linspace(1, 0, e)

    OUT.mkdir(parents=True, exist_ok=True)
    ints = (np.clip(mix, -1, 1) * 32767).astype("<i2")
    p = OUT / "warp.wav"
    with wave.open(str(p), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(ints.tobytes())
    print(f"wrote {p} ({DUR:.2f}s, {SR} Hz mono)")


if __name__ == "__main__":
    main()
