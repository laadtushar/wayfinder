#!/usr/bin/env python3
"""Synthesize a physically-characterized DEM for Jezero Crater, Mars (#45) —
the ancient crater-lake floor where Perseverance landed (Octavia E. Butler
Landing, 18.4447N 77.4508E) and Ingenuity flew.

PROVENANCE: PROCEDURALLY SYNTHESIZED, pending a real HiRISE/CTX Jezero DTM
(network downloads blocked in this environment). Faithful in CHARACTER, not
exact topography: a gently-undulating ancient lakebed crater floor, the western
sedimentary DELTA FRONT rising above the floor behind a stepped scarp (the
fossil river delta that fed the lake ~3.5+ Gya), the isolated flat-topped
KODIAK butte at the delta's southwestern toe (its inclined foreset beds proved
standing water), a rougher olivine-rich SEITAH unit of wind-sculpted ridges,
and a sparse Mars crater field (fewer/softer than the Moon — wind + water
erosion). Directions are real (delta to the NW, Kodiak SW of it, Seitah between
landing and delta); distances are compressed to a walkable ~4 km window.

Output: 16-bit big-endian ("Mac byte order") RAW at 2049 + meta.json, matching
the dem-to-terrain contract (2^n+1 resolution, big-endian).

Usage: python tools/gen_jezero_dem.py
"""
import json
import numpy as np
from pathlib import Path

SITE = "mars-jezero"
OUT = Path(__file__).resolve().parent.parent / "assets" / "terrain" / SITE
RES = 2049
WIDTH_M = 4000.0          # 4 x 4 km window, landing site at centre
SEED = 2020               # Mars 2020

# Relative offsets from Octavia E. Butler Landing (metres, +x East, +z North).
KODIAK = (-1150.0, -250.0)     # isolated butte, SW toward the delta toe
SEITAH_CENTRE = (-450.0, 500.0)  # rough olivine unit, NW-ish between landing + delta


def periodic_fbm(n, octaves, slope, rng):
    fy = np.fft.fftfreq(n)[:, None]; fx = np.fft.fftfreq(n)[None, :]
    f = np.sqrt(fx * fx + fy * fy); f[0, 0] = 1.0
    h = np.zeros((n, n))
    for o in range(octaves):
        lo, hi = 2.0 ** (o + 1) / n, 2.0 ** (o + 5) / n
        band = ((f >= lo) & (f < hi)).astype(float)
        amp = band / np.power(f, slope)
        ph = rng.uniform(0, 2 * np.pi, (n, n))
        layer = np.real(np.fft.ifft2(amp * (np.cos(ph) + 1j * np.sin(ph))))
        s = layer.std()
        if s > 1e-12:
            h += layer / s * (0.5 ** o)
    return h


def stamp_crater(H, cx, cy, diam_px, depth_m):
    n = H.shape[0]; r = diam_px / 2.0
    reach = int(r * 2.0) + 2
    x0, x1 = max(0, cx - reach), min(n, cx + reach)
    y0, y1 = max(0, cy - reach), min(n, cy + reach)
    if x1 <= x0 or y1 <= y0:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1]
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / max(r, 1e-6)
    bowl = np.where(d < 1.0, -depth_m * (1.0 - d * d), 0.0)
    rim = np.where((d >= 0.8) & (d < 1.15), depth_m * 0.15 * np.exp(-((d - 1.0) ** 2) / 0.004), 0.0)
    H[y0:y1, x0:x1] += bowl + rim


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(SEED)
    mpp = WIDTH_M / (RES - 1)
    cxc = (RES - 1) / 2.0

    def px(mx, mz):
        return int(cxc + mx / mpp), int(cxc + mz / mpp)

    # world-metre coordinate grids (x East, z North) for feature masks
    ax = (np.arange(RES) - cxc) * mpp
    X = ax[None, :]                      # east, columns
    Z = ax[:, None]                      # north, rows

    # 1) Gentle ancient-lakebed floor (~ +-12 m over 4 km).
    H = periodic_fbm(RES, 3, 2.3, rng) * 12.0

    # 2) Western delta front: a raised sedimentary platform (NW) behind a scarp.
    #    ramp up toward the west/northwest, ~55 m above the floor, with a fairly
    #    sharp toe (the delta front scarp) and subtle layered terracing.
    delta_dir = (-X * 0.85 - Z * 0.35)                 # high toward NW
    toe = 700.0                                        # scarp toe distance
    delta = 55.0 * np.clip((delta_dir - toe) / 500.0, 0.0, 1.0)
    # foreset-bed terracing on the delta face
    face = (delta_dir - toe)
    delta += np.where((face > 0) & (face < 500.0),
                      3.0 * np.sin(face / 45.0), 0.0)
    H += delta

    # 3) Seitah: a patch of rougher wind-sculpted ridges (higher-freq roughness).
    rough = periodic_fbm(RES, 5, 1.7, rng) * 6.0
    sx, sz = SEITAH_CENTRE
    seitah_mask = np.exp(-(((X - sx) ** 2 + (Z - sz) ** 2) / (2 * 420.0 ** 2)))
    H += rough * seitah_mask

    # 4) Kodiak butte: isolated flat-topped mesa (~28 m tall, ~180 m across).
    kx, kz = KODIAK
    kd = np.sqrt((X - kx) ** 2 + (Z - kz) ** 2)
    butte = 28.0 * np.clip((110.0 - kd) / 40.0, 0.0, 1.0)   # flat top + steep sides
    H += butte

    # 5) Sparse, softened Mars crater field (erosion -> fewer, shallower).
    d_min, d_max = 30.0, 900.0
    for _ in range(160):
        u = rng.uniform((d_min / d_max) ** 2, 1.0)
        diam_m = d_min / np.sqrt(u)
        depth_m = diam_m * rng.uniform(0.06, 0.12)         # shallower than fresh lunar
        cx = rng.integers(0, RES); cy = rng.integers(0, RES)
        stamp_crater(H, cx, cy, diam_m / mpp, depth_m)

    # Normalize to a real elevation band (Jezero floor ~ -2600 m below datum).
    H -= H.min()
    rng_m = float(H.max())
    base_elev = -2600.0

    norm = (H / max(rng_m, 1e-6) * 65535.0).astype(np.uint16)
    norm.astype(">u2").tofile(OUT / f"heightmap_{RES}.raw")
    print(f"  wrote heightmap_{RES}.raw ({RES}x{RES}, big-endian, range {rng_m:.0f} m)")

    meta = {
        "siteId": SITE,
        "displayName": "Mars " + chr(0x2014) + " Jezero Crater (Perseverance)",
        "heightmapResolution": RES,
        "widthMeters": WIDTH_M,
        "lengthMeters": WIDTH_M,
        "minElevationMeters": round(base_elev),
        "maxElevationMeters": round(base_elev + rng_m),
        "heightRangeMeters": round(rng_m),
        "surfaceGravity": 3.72,
        "sourceProduct": "PROCEDURALLY SYNTHESIZED (pending a real HiRISE/CTX Jezero DTM) — ancient lakebed crater floor + western delta-front scarp with foreset terracing + isolated Kodiak butte + rough olivine-rich Seitah unit + sparse eroded Mars crater field. Faithful in CHARACTER, not exact topography; feature DIRECTIONS are real (delta NW, Kodiak SW of it, Seitah between).",
        "sourceResolutionMPerPx": round(mpp, 2),
        "byteOrder": "big-endian",
        "baseColor": "#8B5A3C",
        "credit": "Synthesized for Wayfinder (tools/gen_jezero_dem.py). Feature layout per NASA Mars 2020 / USGS Jezero maps; replace with a real HiRISE/CTX DTM when a tile is available.",
        "notes": "4x4 km window centred on the Octavia E. Butler Landing site (18.4447N, 77.4508E), Jezero Crater. Real distances (delta ~2 km NW) compressed to a walkable scene; directions preserved. Elevation band ~ -2600 m below the Mars areoid.",
        "regolithProfile": "mars_basalt",
        "detailTileMeters": 0.75,
        "macroTileMeters": 11.0,
        "detailFadeStart": 6.0,
        "detailFadeEnd": 22.0,
        "detailStrength": 1.0,
    }
    with open(OUT / "meta.json", "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
    print(f"  wrote meta.json ({SITE})")


if __name__ == "__main__":
    main()
