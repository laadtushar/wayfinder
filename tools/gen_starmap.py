#!/usr/bin/env python3
"""Generate an equirectangular star skybox for Wayfinder from real star data.

HONESTY (read docs/data-sources.md): the bright stars below are REAL — their
right ascension, declination, apparent magnitude and B-V colour index are the
catalogued values for the ~65 brightest naked-eye stars, so the constellations
(Orion, Ursa Major / the Big Dipper, Cassiopeia, Crux, Scorpius, ...) are
correctly shaped and placed. Network access to the full Yale Bright Star /
Hipparcos catalogue is blocked in this build environment, so this is a curated
bright-star SUBSET, not the whole sky. A faint, low-brightness procedural dust
of dim points is added purely for visual density — it is NOT catalogued and is
flagged as such. To regenerate from the full catalogue, drop a BSC/Hipparcos
CSV (columns: ra_deg, dec_deg, vmag, bv) next to this script and extend load().

Output: unity/Assets/Wayfinder/Sky/starmap_equirect.png (equirect, lat/long),
for a Skybox/Panoramic material.

Usage: python tools/gen_starmap.py
"""
import numpy as np
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "unity" / "Assets" / "Wayfinder" / "Sky"

# name, RA (hours), Dec (deg), apparent V magnitude, B-V colour index. Real values.
STARS = [
    ("Sirius", 6.7525, -16.716, -1.46, 0.00), ("Canopus", 6.399, -52.696, -0.74, 0.15),
    ("RigilKent", 14.660, -60.834, -0.27, 0.71), ("Arcturus", 14.261, 19.182, -0.05, 1.23),
    ("Vega", 18.615, 38.784, 0.03, 0.00), ("Capella", 5.278, 45.998, 0.08, 0.80),
    ("Rigel", 5.242, -8.202, 0.13, -0.03), ("Procyon", 7.655, 5.225, 0.34, 0.42),
    ("Betelgeuse", 5.919, 7.407, 0.42, 1.85), ("Achernar", 1.629, -57.237, 0.46, -0.16),
    ("Hadar", 14.064, -60.373, 0.61, -0.23), ("Altair", 19.846, 8.868, 0.77, 0.22),
    ("Acrux", 12.443, -63.099, 0.77, -0.24), ("Aldebaran", 4.599, 16.509, 0.87, 1.54),
    ("Antares", 16.490, -26.432, 1.06, 1.83), ("Spica", 13.420, -11.161, 0.98, -0.23),
    ("Pollux", 7.755, 28.026, 1.14, 1.00), ("Fomalhaut", 22.961, -29.622, 1.16, 0.09),
    ("Deneb", 20.690, 45.280, 1.25, 0.09), ("Mimosa", 12.795, -59.689, 1.25, -0.23),
    ("Regulus", 10.140, 11.967, 1.35, -0.11), ("Adhara", 6.977, -28.972, 1.50, -0.21),
    ("Castor", 7.577, 31.888, 1.58, 0.03), ("Shaula", 17.560, -37.104, 1.62, -0.23),
    ("Gacrux", 12.519, -57.113, 1.63, 1.60), ("Bellatrix", 5.418, 6.350, 1.64, -0.22),
    ("Elnath", 5.438, 28.608, 1.65, -0.13), ("Miaplacidus", 9.220, -69.717, 1.68, 0.07),
    ("Alnilam", 5.604, -1.202, 1.69, -0.18), ("Alnair", 22.137, -46.961, 1.74, -0.07),
    ("Alnitak", 5.679, -1.943, 1.77, -0.20), ("Alioth", 12.900, 55.960, 1.77, -0.02),
    ("Dubhe", 11.062, 61.751, 1.79, 1.07), ("Mirfak", 3.405, 49.861, 1.79, 0.48),
    ("Wezen", 7.140, -26.393, 1.83, 0.67), ("Sargas", 17.622, -42.998, 1.86, 0.40),
    ("KausAust", 18.403, -34.385, 1.85, -0.03), ("Avior", 8.375, -59.510, 1.86, 1.29),
    ("Alkaid", 13.792, 49.313, 1.85, -0.10), ("Menkalinan", 5.992, 44.947, 1.90, 0.03),
    ("Atria", 16.811, -69.028, 1.91, 1.44), ("Alhena", 6.629, 16.399, 1.93, 0.00),
    ("Peacock", 20.427, -56.735, 1.94, -0.12), ("Polaris", 2.530, 89.264, 1.98, 0.60),
    ("Mirzam", 6.378, -17.956, 1.98, -0.24), ("Alphard", 9.460, -8.659, 1.98, 1.44),
    ("Hamal", 2.119, 23.462, 2.00, 1.15), ("Diphda", 0.726, -17.987, 2.04, 1.02),
    ("Nunki", 18.921, -26.297, 2.05, -0.13), ("Menkent", 14.111, -36.370, 2.06, 1.01),
    ("Mizar", 13.399, 54.925, 2.04, 0.06), ("Saiph", 5.796, -9.670, 2.06, -0.17),
    ("Kochab", 14.845, 74.156, 2.08, 1.47), ("Rasalhague", 17.582, 12.560, 2.08, 0.16),
    ("Algieba", 10.333, 19.842, 2.08, 1.13), ("Denebola", 11.818, 14.572, 2.11, 0.09),
    ("Naos", 8.060, -40.003, 2.21, -0.27), ("Schedar", 0.675, 56.537, 2.24, 1.17),
    ("Caph", 0.153, 59.150, 2.28, 0.34), ("GammaCas", 0.945, 60.717, 2.15, -0.15),
    ("Ruchbah", 1.430, 60.235, 2.68, 0.16), ("Merak", 11.031, 56.383, 2.37, 0.03),
    ("Phecda", 11.897, 53.695, 2.44, 0.04), ("Megrez", 12.257, 57.033, 3.31, 0.08),
    ("Mintaka", 5.533, -0.299, 2.23, -0.18), ("Alphecca", 15.578, 26.715, 2.22, 0.03),
]

W, H = 4096, 2048


def bv_to_rgb(bv):
    """Approximate a star's RGB tint from its B-V colour index."""
    bv = float(np.clip(bv, -0.4, 2.0))
    if bv < 0.0:   return (0.62, 0.74, 1.00)   # hot blue
    if bv < 0.3:   return (0.82, 0.88, 1.00)   # blue-white
    if bv < 0.6:   return (1.00, 0.98, 0.94)   # white
    if bv < 1.0:   return (1.00, 0.94, 0.78)   # yellow-white
    if bv < 1.5:   return (1.00, 0.82, 0.60)   # orange
    return (1.00, 0.68, 0.50)                  # red


def stamp(img, cx, cy, radius, rgb, peak):
    """Additive gaussian dot, wrapping in x (longitude)."""
    r = int(np.ceil(radius * 3)) + 1
    for dy in range(-r, r + 1):
        y = cy + dy
        if y < 0 or y >= H:
            continue
        for dx in range(-r, r + 1):
            x = (cx + dx) % W
            d2 = dx * dx + dy * dy
            a = peak * np.exp(-d2 / (2.0 * radius * radius))
            if a <= 0.002:
                continue
            img[y, x, 0] = min(1.0, img[y, x, 0] + a * rgb[0])
            img[y, x, 1] = min(1.0, img[y, x, 1] + a * rgb[1])
            img[y, x, 2] = min(1.0, img[y, x, 2] + a * rgb[2])


def radec_to_xy(ra_h, dec_deg):
    u = (ra_h / 24.0) % 1.0
    v = (90.0 - dec_deg) / 180.0        # 0 at north pole (top)
    return int(u * W) % W, int(np.clip(v, 0, 1) * (H - 1))


def add_milky_way(img):
    """Faint diffuse galactic band, correctly oriented. For each equirect pixel
    (RA, Dec) compute galactic latitude b (equatorial->galactic rotation, J2000)
    and the angular distance to the galactic centre in Sagittarius; brightness is
    a narrow gaussian in |b| with a broad bulge lobe toward the centre, mottled by
    smooth value-noise so it clumps and has a dark rift. Faint and warm-white so
    it never washes out the catalogued stars."""
    from PIL import Image
    ys, xs = np.mgrid[0:H, 0:W]
    ra = (xs / W) * 2.0 * np.pi
    dec = (np.pi / 2.0) - (ys / (H - 1)) * np.pi
    a_ngp, d_ngp = np.radians(192.8595), np.radians(27.1283)   # north galactic pole (J2000)
    sinb = np.clip(np.sin(dec) * np.sin(d_ngp)
                   + np.cos(dec) * np.cos(d_ngp) * np.cos(ra - a_ngp), -1, 1)
    b = np.arcsin(sinb)
    band = np.exp(-(b * b) / (2.0 * np.radians(10.0) ** 2))
    # bulge toward the galactic centre (RA 266.405, Dec -28.936)
    ac, dc = np.radians(266.405), np.radians(-28.936)
    cosang = np.clip(np.sin(dec) * np.sin(dc)
                     + np.cos(dec) * np.cos(dc) * np.cos(ra - ac), -1, 1)
    bulge = np.exp(-(np.arccos(cosang) ** 2) / (2.0 * np.radians(26.0) ** 2))
    # smooth value-noise mottle (coarse random grids upsampled + summed)
    rng = np.random.default_rng(7)
    mottle = np.zeros((H, W))
    for gh, gw, amp in [(12, 24, 0.6), (28, 56, 0.3), (64, 128, 0.16), (160, 320, 0.09)]:
        g = (rng.uniform(0, 1, (gh, gw)) * 255).astype(np.uint8)
        mottle += np.asarray(Image.fromarray(g).resize((W, H), Image.BICUBIC)) / 255.0 * amp
    mottle /= mottle.max()
    intensity = band * (0.45 + 0.9 * bulge) * (0.45 + 0.55 * mottle) * 0.13
    # fine dither breaks 8-bit contour banding in the very faint gradient
    intensity += rng.uniform(-0.004, 0.004, (H, W)) * (band > 0.015)
    intensity = np.clip(intensity, 0.0, 1.0)
    tint = (1.0, 0.98, 0.93)
    for c in range(3):
        img[..., c] += intensity * tint[c]


def main():
    img = np.zeros((H, W, 3), np.float64)
    add_milky_way(img)

    # Faint, NON-catalogued procedural dust for density (flagged; deterministic).
    rng = np.random.default_rng(42)
    n_dust = 4000
    for _ in range(n_dust):
        x = rng.integers(0, W)
        # sin(dec) uniform so dust is even over the sphere, not pole-bunched
        dec = np.degrees(np.arcsin(rng.uniform(-1, 1)))
        y = int((90.0 - dec) / 180.0 * (H - 1))
        b = rng.uniform(0.04, 0.16)
        stamp(img, x, y, 0.7, (0.9, 0.92, 1.0), b)

    # Real bright stars: size + brightness from magnitude, tint from B-V.
    for name, ra, dec, mag, bv in STARS:
        x, y = radec_to_xy(ra, dec)
        # brighter (lower mag) -> larger + more intense
        t = np.clip((3.4 - mag) / 4.9, 0.0, 1.0)          # 0 faint .. 1 Sirius
        radius = 1.1 + t * 3.2
        peak = 0.55 + t * 0.85
        stamp(img, x, y, radius, bv_to_rgb(bv), peak)

    from PIL import Image
    out = np.clip(img * 255.0 + 0.5, 0, 255).astype(np.uint8)
    OUT.mkdir(parents=True, exist_ok=True)
    p = OUT / "starmap_equirect.png"
    Image.fromarray(out, "RGB").save(p)
    print(f"wrote {p} ({W}x{H}) — {len(STARS)} real stars + {n_dust} procedural dust")


if __name__ == "__main__":
    main()
