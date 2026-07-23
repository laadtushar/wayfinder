#!/usr/bin/env python3
"""Soft radial ambient-occlusion decal for grounding props (#33) — a dark
centre fading to transparent, laid flat under the lander so it reads as
sitting on the regolith rather than floating. Procedural, committed.
"""
import numpy as np
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "unity" / "Assets" / "Wayfinder" / "Textures"
N = 256


def main():
    from PIL import Image
    OUT.mkdir(parents=True, exist_ok=True)
    yy, xx = np.mgrid[0:N, 0:N]
    cx = cy = (N - 1) / 2.0
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / (N / 2.0)
    # Dark near centre, soft falloff to 0 at the rim; noise breaks the perfect circle.
    rng = np.random.default_rng(33)
    noise = 1.0 + 0.12 * (rng.random((N, N)) - 0.5)
    alpha = np.clip(1.0 - r * noise, 0.0, 1.0)
    alpha = alpha ** 1.8  # tighter core
    rgba = np.zeros((N, N, 4), np.float32)
    rgba[..., 3] = alpha * 0.7  # peak opacity
    arr = (np.clip(rgba, 0, 1) * 255).astype("uint8")
    Image.fromarray(arr, "RGBA").save(OUT / "ContactAO.png")
    print(f"  wrote ContactAO.png ({N}x{N})")


if __name__ == "__main__":
    main()
