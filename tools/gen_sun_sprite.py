#!/usr/bin/env python3
"""Generate sun sprites for Wayfinder — a sharp disc and a soft glow halo.

Procedural (numpy), no third-party art. Two RGBA PNGs, white RGB so the
material tints them per world:

  sun_disc.png  — a bright hard-edged disc with a thin soft limb. The sun BODY.
                  Moon (airless) uses just this, no bloom. Alpha = 1 out to ~85%
                  radius, then a quick smooth falloff to 0 — crisp but not
                  aliased.
  sun_glow.png  — a wide, faint radial halo (gaussian-ish falloff). Mars only:
                  the dusty forward-scatter glow around the disc. Rendered
                  additively so the black background adds nothing.

Usage:
  python tools/gen_sun_sprite.py
"""
import numpy as np
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "unity" / "Assets" / "Wayfinder" / "Sky"


def radial(size):
    """Normalized radius 0..~1.41 from centre."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float64)
    cy = cx = (size - 1) / 2.0
    r = np.sqrt((x - cx) ** 2 + (y - cy) ** 2) / (size / 2.0)
    return r


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def save_rgba(path, rgb01, alpha01):
    from PIL import Image
    h, w = alpha01.shape
    arr = np.zeros((h, w, 4), np.float64)
    arr[..., 0] = rgb01[0]
    arr[..., 1] = rgb01[1]
    arr[..., 2] = rgb01[2]
    arr[..., 3] = alpha01
    out = np.clip(arr * 255.0 + 0.5, 0, 255).astype(np.uint8)
    OUT.mkdir(parents=True, exist_ok=True)
    Image.fromarray(out, "RGBA").save(path)
    print(f"  wrote {path} ({w}x{h})")


def gen_disc(size=256):
    r = radial(size)
    # solid core to 0.82, smooth limb to 0.98
    alpha = 1.0 - smoothstep(0.82, 0.98, r)
    # a faint 1px-ish brightening at the very centre reads as a hot sun
    save_rgba(OUT / "sun_disc.png", (1.0, 1.0, 1.0), alpha)


def gen_glow(size=512):
    r = radial(size)
    # broad gaussian halo, peak ~0.55 at centre, ~0 by the edge
    alpha = 0.55 * np.exp(-(r ** 2) / (2 * 0.33 ** 2))
    alpha *= 1.0 - smoothstep(0.9, 1.0, r)  # clean fade to 0 at the texture edge
    save_rgba(OUT / "sun_glow.png", (1.0, 1.0, 1.0), alpha)


if __name__ == "__main__":
    gen_disc()
    gen_glow()
    print("done")
