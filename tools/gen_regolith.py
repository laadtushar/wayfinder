#!/usr/bin/env python3
"""Generate seamlessly-tiling regolith detail textures for Wayfinder.

Everything is procedural (numpy FFT periodic noise) — no third-party assets.
Seamlessness holds through the WHOLE pipeline: the fBm height field is
periodic by construction (inverse FFT of a synthetic spectrum wraps exactly),
clast stamps use toroidal (modulo) coordinates, and the Sobel normal uses
np.roll — so no operation ever reads across a hard tile edge.

Profiles are real-referenced, not invented:
  mars_basalt       — butterscotch basaltic fines + rounded granules
                      (MER/MSL surface imagery; albedo ~0.14, warm)
  moon_anorthosite  — neutral grey anorthositic breccia, angular fragments +
                      craterlets, harder micro-contrast (airless)
                      (Apollo surface photography; albedo ~0.13)

Albedo obeys Unity's detail-map convention: mean luminance normalized to 0.5
so the shader's x2 multiply preserves the real orbital basemap's color truth.

Usage:
  python tools/gen_regolith.py --profile mars_basalt
  python tools/gen_regolith.py --profile moon_anorthosite
  python tools/gen_regolith.py --macro
"""
import argparse
import zlib
import numpy as np
from pathlib import Path

OUT_DIR = Path(__file__).resolve().parent.parent / "unity" / "Assets" / "Wayfinder" / "Terrain" / "Regolith"

PROFILES = {
    "mars_basalt": dict(
        base_rgb=(0.19, 0.12, 0.08),
        octaves=4, spectral_slope=1.9,
        clast_count=900, clast_rounded=True, clast_radius=(2, 7),
        craterlets=0,
        micro_contrast=0.35, normal_strength=0.8,
    ),
    "moon_anorthosite": dict(
        base_rgb=(0.15, 0.15, 0.16),
        octaves=5, spectral_slope=1.7,
        clast_count=650, clast_rounded=False, clast_radius=(2, 9),
        craterlets=140,
        micro_contrast=0.55, normal_strength=1.0,
    ),
}


def periodic_fbm(size, octaves, slope, rng):
    """fBm on a periodic domain: synthesize a power-law spectrum with random
    phase and inverse-FFT it. Wraps exactly by construction."""
    fy = np.fft.fftfreq(size)[:, None]
    fx = np.fft.fftfreq(size)[None, :]
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1.0  # avoid div0; DC removed below
    height = np.zeros((size, size))
    for o in range(octaves):
        lo, hi = 2.0 ** (o + 1) / size, 2.0 ** (o + 5) / size
        band = ((f >= lo) & (f < hi)).astype(float)
        amp = band / np.power(f, slope)
        phase = rng.uniform(0, 2 * np.pi, (size, size))
        spec = amp * (np.cos(phase) + 1j * np.sin(phase))
        spec[0, 0] = 0.0
        layer = np.real(np.fft.ifft2(spec))
        s = layer.std()
        if s > 1e-12:
            height += layer / s * (0.5 ** o)
    height -= height.min()
    height /= max(height.max(), 1e-12)
    return height


def stamp_clasts(height, profile, rng):
    """Rock chips / craterlets stamped with toroidal coordinates (REQUIRED
    FIX #4: modulo wrap so edge stamps continue on the far side)."""
    size = height.shape[0]
    yy, xx = np.mgrid[0:size, 0:size]

    def toroidal_d2(cy, cx):
        dy = np.minimum(np.abs(yy - cy), size - np.abs(yy - cy))
        dx = np.minimum(np.abs(xx - cx), size - np.abs(xx - cx))
        return dy * dy + dx * dx

    rmin, rmax = profile["clast_radius"]
    for _ in range(profile["clast_count"]):
        cy, cx = rng.integers(0, size, 2)
        r = rng.uniform(rmin, rmax)
        d2 = toroidal_d2(cy, cx)
        mask = d2 < r * r
        if profile["clast_rounded"]:
            bump = np.sqrt(np.clip(1.0 - d2 / (r * r), 0, 1))  # dome
        else:
            bump = np.clip(1.0 - np.sqrt(d2) / r, 0, 1)        # cone (angular)
            bump = np.where(bump > 0.35, bump, bump * 0.4)     # faceted shoulder
        height[mask] += (bump[mask] * rng.uniform(0.25, 0.6) * 0.12)

    for _ in range(profile["craterlets"]):
        cy, cx = rng.integers(0, size, 2)
        r = rng.uniform(3, 10)
        d2 = toroidal_d2(cy, cx)
        d = np.sqrt(d2) / r
        bowl = np.where(d < 1.0, -(np.cos(np.clip(d, 0, 1) * np.pi) * 0.5 + 0.5), 0.0)
        rim = np.where((d >= 0.8) & (d < 1.25), np.exp(-((d - 1.0) ** 2) / 0.02) * 0.4, 0.0)
        height += (bowl * 0.06 + rim * 0.05) * rng.uniform(0.5, 1.0)

    height -= height.min()
    height /= max(height.max(), 1e-12)
    return height


def sobel_normal_wrapped(height, strength):
    """Tangent-space normal from height via Sobel with np.roll (REQUIRED FIX
    #4: gradients wrap, keeping the normal seamless)."""
    def rolled(dy_, dx_):
        return np.roll(np.roll(height, dy_, axis=0), dx_, axis=1)

    gx = (rolled(-1, -1) + 2 * rolled(0, -1) + rolled(1, -1)
          - rolled(-1, 1) - 2 * rolled(0, 1) - rolled(1, 1))
    gy = (rolled(-1, -1) + 2 * rolled(-1, 0) + rolled(-1, 1)
          - rolled(1, -1) - 2 * rolled(1, 0) - rolled(1, 1))
    # Unity tangent space is +Y-up in V; PIL rows run top-down, so array +y
    # IS -V — green must be +gy (a -gy here shades bumps as dents).
    nx = -gx * strength
    ny = gy * strength
    nz = np.ones_like(height)
    norm = np.sqrt(nx * nx + ny * ny + nz * nz)
    return nx / norm, ny / norm, nz / norm


def srgb_encode(linear):
    return np.where(linear <= 0.0031308, 12.92 * linear,
                    1.055 * np.power(np.clip(linear, 0, 1), 1 / 2.4) - 0.055)


def save_png(path, arr_float01):
    from PIL import Image
    arr = np.clip(arr_float01 * 255.0 + 0.5, 0, 255).astype(np.uint8)
    Image.fromarray(arr).save(path)
    print(f"  wrote {path} ({arr.shape})")


def generate_profile(name, size=1024, seed=None):
    profile = PROFILES[name]
    # crc32, not hash(): Python string hashing is per-process randomized,
    # which would silently regenerate different textures every run.
    rng = np.random.default_rng(seed if seed is not None else zlib.crc32(name.encode()))
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    height = periodic_fbm(size, profile["octaves"], profile["spectral_slope"], rng)
    height = stamp_clasts(height, profile, rng)

    # Albedo: base color modulated by high-passed height; mean luminance
    # normalized to 0.5 (Unity detail-map x2 convention).
    lowpass = np.real(np.fft.ifft2(np.fft.fft2(height) *
                      (np.sqrt(np.fft.fftfreq(size)[:, None] ** 2 + np.fft.fftfreq(size)[None, :] ** 2) < 8.0 / size)))
    highpass = height - lowpass
    hp = highpass / max(np.abs(highpass).max(), 1e-12)

    base = np.array(profile["base_rgb"])
    lum = 1.0 + profile["micro_contrast"] * hp
    rgb = base[None, None, :] * lum[:, :, None]
    # Normalize each channel's LINEAR mean to 0.5 so the shader's x2 detail
    # multiply (which runs in linear space after sRGB decode) is chroma- and
    # luminance-NEUTRAL: the real orbital basemap keeps its color truth and
    # detail only adds high-frequency crunch. The file is then sRGB-ENCODED
    # (stored mean ~0.735) because the importer marks albedo sRGB.
    for c in range(3):
        rgb[:, :, c] *= 0.5 / max(rgb[:, :, c].mean(), 1e-12)
    rgba = np.concatenate([srgb_encode(rgb), height[:, :, None]], axis=2)  # A = height (linear)
    save_png(OUT_DIR / f"{name}_albedo.png", rgba)

    nx, ny, nz = sobel_normal_wrapped(height, profile["normal_strength"])
    normal = np.stack([nx * 0.5 + 0.5, ny * 0.5 + 0.5, nz * 0.5 + 0.5], axis=2)
    save_png(OUT_DIR / f"{name}_normal.png", normal)


def generate_macro(size=512, seed=77):
    rng = np.random.default_rng(seed)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    noise = periodic_fbm(size, 2, 1.5, rng)
    save_png(OUT_DIR / "macro_noise.png", noise)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--profile", choices=list(PROFILES))
    ap.add_argument("--macro", action="store_true")
    ap.add_argument("--all", action="store_true")
    args = ap.parse_args()
    if args.all:
        for p in PROFILES:
            print(f"profile {p}:")
            generate_profile(p)
        generate_macro()
    else:
        if args.profile:
            generate_profile(args.profile)
        if args.macro:
            generate_macro()
