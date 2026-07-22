#!/usr/bin/env python3
"""Generate Wayfinder's suit/world audio beds (#20) procedurally.

Deterministic, seeded, numpy + stdlib wave — no downloads, no licensing risk,
matches the project's procedural-asset ethos. Mono 44.1 kHz 16-bit WAV loops.

Beds:
  mars_wind    — low, mournful filtered-noise wind, tuned to the InSight
                 character (real InSight marsquake/wind is ~10-40 Hz rumble;
                 this evokes it in the audible band with slow gusts).
  suit_breath  — slow inhale/exhale envelope over band-limited breath noise.
  boots        — short regolith-crunch one-shot (not a loop).
  radio_static — band-limited hiss with occasional pops/squelch.
  bridge_hum   — low machinery hum (sine stack + faint noise), seamless loop.

Real NASA InSight wind (public domain) is the intended fidelity upgrade —
drop mars_wind.wav in and re-import; the SuitAudio system doesn't care about
the source. Credit would go in CREDITS.md.

Usage: python tools/gen_audio.py
"""
import numpy as np
import wave
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "unity" / "Assets" / "Wayfinder" / "Audio"
SR = 44100


def save_wav(path, samples):
    s = np.clip(samples, -1.0, 1.0)
    pcm = (s * 32767.0).astype("<i2")
    with wave.open(str(path), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"  wrote {path.name}  {len(samples)/SR:.1f}s")


def loopable_fade(x, fade=0.05):
    """Crossfade the tail into the head so the clip loops seamlessly."""
    n = int(SR * fade)
    if n * 2 >= len(x):
        return x
    head, tail = x[:n].copy(), x[-n:].copy()
    ramp = np.linspace(0, 1, n)
    x[:n] = head * ramp + tail * (1 - ramp)
    return x[:-n]


def brown_noise(n, rng):
    white = rng.standard_normal(n)
    b = np.cumsum(white)
    b -= b.mean()
    b /= (np.abs(b).max() + 1e-9)
    return b


def onepole_lp(x, cutoff_hz):
    dt = 1.0 / SR
    rc = 1.0 / (2 * np.pi * cutoff_hz)
    a = dt / (rc + dt)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a * (x[i] - acc)
        y[i] = acc
    return y


def gen_mars_wind(seconds=12.0, seed=1):
    rng = np.random.default_rng(seed)
    n = int(SR * seconds)
    base = brown_noise(n, rng)
    base = onepole_lp(base, 320.0)          # low, airy
    # slow gusts: sum of a few low-freq LFOs
    t = np.arange(n) / SR
    gust = (0.5 + 0.3 * np.sin(2 * np.pi * 0.06 * t)
            + 0.2 * np.sin(2 * np.pi * 0.017 * t + 1.3))
    gust = np.clip(gust, 0.05, 1.0)
    # a faint low whistle that swells with gusts
    whistle = 0.06 * np.sin(2 * np.pi * 92 * t) * gust
    x = base * gust * 0.9 + whistle
    x /= (np.abs(x).max() + 1e-9)
    return loopable_fade(x * 0.8)


def gen_suit_breath(seconds=8.0, seed=2):
    rng = np.random.default_rng(seed)
    n = int(SR * seconds)
    t = np.arange(n) / SR
    breath_hz = 0.28                        # ~17 breaths/min, calm
    env = 0.5 + 0.5 * np.sin(2 * np.pi * breath_hz * t - np.pi / 2)
    env = env ** 1.6                        # sharper attack on inhale
    noise = onepole_lp(rng.standard_normal(n), 1400.0)
    noise = noise - onepole_lp(noise, 200.0)  # band-pass-ish (remove rumble)
    x = noise * env
    x /= (np.abs(x).max() + 1e-9)
    return loopable_fade(x * 0.5)


def gen_boots(seed=3):
    """One-shot regolith crunch (~0.28s), not a loop."""
    rng = np.random.default_rng(seed)
    n = int(SR * 0.28)
    t = np.arange(n) / SR
    env = np.exp(-t * 22.0)
    grains = rng.standard_normal(n)
    grains = onepole_lp(grains, 2600.0)
    # a couple of grit transients
    for g in (0.02, 0.06, 0.11):
        i = int(g * SR)
        if i < n:
            grains[i:i + 200] += rng.standard_normal(min(200, n - i)) * 1.5
    thud = 0.4 * np.sin(2 * np.pi * 70 * t) * np.exp(-t * 30)
    x = grains * env + thud
    x /= (np.abs(x).max() + 1e-9)
    return x * 0.7


def gen_radio_static(seconds=6.0, seed=4):
    rng = np.random.default_rng(seed)
    n = int(SR * seconds)
    hiss = rng.standard_normal(n)
    hiss = hiss - onepole_lp(hiss, 800.0)   # high-pass hiss
    hiss = onepole_lp(hiss, 6000.0)
    x = hiss * 0.25
    # occasional squelch pops
    for _ in range(int(seconds * 1.5)):
        i = rng.integers(0, n - 500)
        x[i:i + 300] += rng.standard_normal(300) * 0.6
    x /= (np.abs(x).max() + 1e-9)
    return loopable_fade(x * 0.4)


def gen_bridge_hum(seconds=6.0, seed=5):
    rng = np.random.default_rng(seed)
    n = int(SR * seconds)
    t = np.arange(n) / SR
    # low machinery hum: fundamental + harmonics, slight detune shimmer
    x = (0.6 * np.sin(2 * np.pi * 55 * t)
         + 0.25 * np.sin(2 * np.pi * 110 * t + 0.4)
         + 0.12 * np.sin(2 * np.pi * 165 * t + 1.1))
    shimmer = 1.0 + 0.03 * np.sin(2 * np.pi * 0.2 * t)
    x *= shimmer
    x += 0.04 * onepole_lp(rng.standard_normal(n), 400.0)  # faint airflow
    x /= (np.abs(x).max() + 1e-9)
    return loopable_fade(x * 0.5)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    save_wav(OUT / "mars_wind.wav", gen_mars_wind())
    save_wav(OUT / "suit_breath.wav", gen_suit_breath())
    save_wav(OUT / "boots.wav", gen_boots())
    save_wav(OUT / "radio_static.wav", gen_radio_static())
    save_wav(OUT / "bridge_hum.wav", gen_bridge_hum())


if __name__ == "__main__":
    main()
