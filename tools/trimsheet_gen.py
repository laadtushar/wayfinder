#!/usr/bin/env python3
"""Generate the Wayfinder bridge hull trim sheet (#22) — one 2048² sheet, three
maps, six horizontal trim bands. Every hull/console/bezel/cove face UVs into a
V-band, so the whole interior shares one material (~1 SetPass).

Fully procedural + deterministic (seeded), numpy+Pillow — the same heightfield→
normal logic the DEM→terrain pipeline already trusts. No paid assets.

Bands (V):
  A 0.00-0.25  large hull panel: recessed panel-line grid, bevels, corner rivets
  B 0.25-0.44  sub-panels / access hatches, screw rows
  C 0.44-0.60  greeble strip: vents, boxes, conduit clamps (+ tiny indicator dots)
  D 0.60-0.72  horizontal pipe/conduit run (cylindrical profile)
  E 0.72-0.84  light-cove channel — hot emissive mask -> accent glow
  F 0.84-1.00  deck-edge kick / grating

Maps:
  Hull_Albedo (sRGB)  desaturated Starfleet grey-blue, AO-darkened cavities
  Hull_Normal (linear, +Y-up)  Sobel of the heightfield
  Hull_Mask   (linear)  R=metallic  G=micro-AO  B=emissive  A=smoothness

Usage: python tools/trimsheet_gen.py [--size 2048]
"""
import argparse
import numpy as np
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "unity" / "Assets" / "Wayfinder" / "Textures" / "Bridge"
SEED = 22


def band(v0, v1, n):
    return slice(int(v0 * n), int(v1 * n))


def stamp_rect(H, y0, y1, x0, x1, height):
    H[y0:y1, x0:x1] += height


def stamp_gauss(H, cy, cx, r, amp):
    n = H.shape[0]
    y0, y1 = max(0, cy - r), min(n, cy + r)
    x0, x1 = max(0, cx - r), min(n, cx + r)
    yy, xx = np.mgrid[y0:y1, x0:x1]
    d2 = (yy - cy) ** 2 + (xx - cx) ** 2
    H[y0:y1, x0:x1] += amp * np.exp(-d2 / (2 * (r / 2.5) ** 2))


def build_height(n, rng):
    H = np.zeros((n, n), np.float32)

    # --- Band A: 8-column panel grid + bevel + rivets ---
    yA = band(0.00, 0.25, n)
    cols = 8
    gx = np.abs(((np.arange(n) / n * cols) % 1) - 0.5)      # dist to column centre
    groove = np.clip(1 - gx / 0.02, 0, 1)                    # thin recessed line
    H[yA] -= 0.15 * groove[None, :]
    # horizontal panel seam mid-band
    row_seam = np.zeros(n, np.float32)
    seam_y = int((0.00 + 0.25) / 2 * n)
    H[seam_y - 2:seam_y + 2, :] -= 0.15
    # bevel: raise panel faces away from grooves
    bevel = np.clip((gx - 0.02) / 0.05, 0, 1)
    bevel = bevel * bevel * (3 - 2 * bevel)
    H[yA] += 0.04 * (1 - bevel)[None, :]
    # corner rivets: gaussian bumps near each panel corner
    for c in range(cols):
        cx = int((c + 0.5) / cols * n)
        for vy in (0.02, 0.23):
            stamp_gauss(H, int(vy * n), cx - int(0.05 * n), 6, 0.10)
            stamp_gauss(H, int(vy * n), cx + int(0.05 * n), 6, 0.10)

    # --- Band B: sub-panels / hatches + screw rows ---
    yB = band(0.25, 0.44, n)
    for _ in range(24):
        hx0 = rng.integers(0, n - int(0.12 * n))
        hy0 = int(0.27 * n) + rng.integers(0, int(0.13 * n))
        w = int(rng.uniform(0.06, 0.12) * n)
        h = int(rng.uniform(0.03, 0.08) * n)
        stamp_rect(H, hy0, hy0 + h, hx0, hx0 + w, 0.03)
        stamp_rect(H, hy0, hy0 + 3, hx0, hx0 + w, -0.08)     # top seam
        for sx in range(hx0 + 6, hx0 + w - 6, 24):           # screw row
            stamp_gauss(H, hy0 + 6, sx, 3, -0.05)

    # --- Band C: greebles (vents, boxes, clamps) ---
    yC0, yC1 = int(0.44 * n), int(0.60 * n)
    for _ in range(120):
        gx0 = rng.integers(0, n - 40)
        gy0 = yC0 + rng.integers(0, (yC1 - yC0) - 40)
        w = int(rng.uniform(0.01, 0.05) * n)
        h = int(rng.uniform(0.02, 0.10) * n)
        stamp_rect(H, gy0, min(gy0 + h, yC1), gx0, min(gx0 + w, n), rng.uniform(0.05, 0.30))
    # vent slats
    for vx in range(0, n, 6):
        H[yC0 + 4:yC1 - 4, vx:vx + 2] -= 0.04

    # --- Band D: pipe (cylinder profile across the band) ---
    yD = band(0.60, 0.72, n)
    t = np.linspace(-1, 1, yD.stop - yD.start)
    H[yD] += (np.sqrt(np.clip(1 - t * t, 0, 1)) * 0.5)[:, None]
    # clamp rings on the pipe
    for cx in range(int(0.08 * n), n, int(0.2 * n)):
        H[yD, cx:cx + 6] += 0.08

    # --- Band E: cove channel (recessed slot; emissive added in mask) ---
    yE = band(0.72, 0.84, n)
    H[yE] -= 0.10
    slot = band(0.75, 0.81, n)
    H[slot] -= 0.06

    # --- Band F: deck-edge kick / grating ---
    yF = band(0.84, 1.00, n)
    for gx0 in range(0, n, 20):
        H[yF, gx0:gx0 + 10] += 0.06
    return H


def derive_maps(H, n, rng):
    # Normal from Sobel gradient (same trick as DEM->terrain normals), +Y-up.
    gy, gx = np.gradient(H.astype(np.float32))
    scale = 4.0
    nx, ny, nz = -gx * scale, gy * scale, np.ones_like(H)   # +gy for Unity +Y-up
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack([nx * inv, ny * inv, nz * inv], -1) * 0.5 + 0.5

    # Albedo: desaturated Starfleet grey-blue, AO-darkened cavities, per-panel jitter.
    ao = np.clip(0.6 + H * 1.2, 0.25, 1.0)
    base = np.array([0.34, 0.37, 0.42])
    albedo = np.clip(base[None, None, :] * ao[..., None], 0, 1)
    # per-panel value jitter (8 columns × band rows) so panels aren't uniform
    cols = 8
    for c in range(cols):
        x0, x1 = int(c / cols * n), int((c + 1) / cols * n)
        jit = 1.0 + (rng.random() - 0.5) * 0.10
        albedo[:, x0:x1, :] *= jit
    albedo = np.clip(albedo, 0, 1)

    # Mask: R=metallic  G=micro-AO  B=emissive  A=smoothness
    metallic = np.full((n, n), 0.9, np.float32)             # metal everywhere
    metallic[band(0.72, 0.84, n)] = 0.0                     # cove strip is emissive, not metal
    smooth = np.clip(0.35 + 0.4 * (H > 0), 0, 1).astype(np.float32)
    emissive = np.zeros((n, n), np.float32)
    emissive[band(0.75, 0.81, n)] = 1.0                     # cove slot glows
    # tiny indicator dots in band C
    for _ in range(40):
        cy = int(0.44 * n) + rng.integers(0, int(0.16 * n))
        cx = rng.integers(0, n)
        stamp_gauss(emissive, cy, cx, 3, 1.0)
    emissive = np.clip(emissive, 0, 1)
    mask = np.stack([metallic, ao.astype(np.float32), emissive, smooth], -1)
    return albedo, normal, mask


def save(path, arr, mode):
    from PIL import Image
    Image.fromarray((np.clip(arr, 0, 1) * 255).astype("uint8"), mode).save(path)
    print(f"  wrote {path.name} {arr.shape}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--size", type=int, default=2048)
    args = ap.parse_args()
    n = args.size
    rng = np.random.default_rng(SEED)
    OUT.mkdir(parents=True, exist_ok=True)

    H = build_height(n, rng)
    albedo, normal, mask = derive_maps(H, n, rng)
    save(OUT / "Hull_Albedo.png", albedo, "RGB")
    save(OUT / "Hull_Normal.png", normal, "RGB")
    save(OUT / "Hull_Mask.png", mask, "RGBA")


if __name__ == "__main__":
    main()
