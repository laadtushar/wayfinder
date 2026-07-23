#!/usr/bin/env python3
"""Synthesize a physically-characterized lunar-mare DEM for the Apollo 11 site
(Statio Tranquillitatis, Mare Tranquillitatis) — #31.

PROVENANCE: this is PROCEDURALLY SYNTHESIZED, pending a real LOLA/SLDEM2015
tile (network downloads were blocked in this environment). It is faithful in
CHARACTER, not in exact topography: a nearly-flat high-titanium mare basalt
plain with gentle regional undulation, overlaid with an impact-crater field
whose size-frequency follows the standard lunar production function
(cumulative N(>D) proportional to D^-2) and whose individual craters use the
observed fresh-crater bowl + raised-rim + ejecta profile. The two named real
craters of the Apollo 11 EVA — Little West (~33 m, ~60 m W of the LM) and
West (~180 m, ~400 m E) — are placed at their real relative positions.

Output: 16-bit big-endian ("Mac byte order") RAW at 2049, + meta.json, matching
the dem-to-terrain contract (byteOrder big-endian, 2^n+1 resolution).

Usage: python tools/gen_mare_dem.py
"""
import json
import numpy as np
from pathlib import Path

SITE = "moon-tranquillity"
OUT = Path(__file__).resolve().parent.parent / "assets" / "terrain" / SITE
RES = 2049
WIDTH_M = 20000.0          # 20 x 20 km window
SEED = 111

# Apollo 11: LM Eagle descent stage is the window centre (origin).
# Real relative offsets of the two EVA craters (metres, +x East, +z North):
LITTLE_WEST = (-60.0, 0.0, 33.0)    # ~60 m west, ~33 m diameter
WEST_CRATER = (400.0, -40.0, 180.0) # ~400 m east, ~180 m diameter (the one the autopilot aimed at)


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


def stamp_crater(H, cx_px, cy_px, diam_px, depth_m, mpp):
    """Fresh simple crater: parabolic bowl + raised rim + faint ejecta."""
    n = H.shape[0]
    r_px = diam_px / 2.0
    reach = int(r_px * 2.2) + 2
    x0, x1 = max(0, cx_px - reach), min(n, cx_px + reach)
    y0, y1 = max(0, cy_px - reach), min(n, cy_px + reach)
    if x1 <= x0 or y1 <= y0:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1]
    d = np.sqrt((xx - cx_px) ** 2 + (yy - cy_px) ** 2) / max(r_px, 1e-6)  # normalized radius
    bowl = np.where(d < 1.0, -depth_m * (1.0 - d * d), 0.0)               # parabolic bowl
    rim = np.where((d >= 0.8) & (d < 1.15),
                   depth_m * 0.18 * np.exp(-((d - 1.0) ** 2) / 0.004), 0.0)  # raised rim
    ejecta = np.where((d >= 1.0) & (d < 2.0),
                      depth_m * 0.05 * (2.0 - d), 0.0)                    # thin ejecta apron
    H[y0:y1, x0:x1] += bowl + rim + ejecta


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(SEED)
    mpp = WIDTH_M / (RES - 1)   # ~9.77 m per grid cell

    # 1) Flat mare plain: very gentle regional undulation (~±25 m over 20 km).
    H = periodic_fbm(RES, 3, 2.2, rng) * 25.0

    # 2) Crater field — cumulative production function N(>D) ~ D^-2.
    #    Sample diameters from 20 m up to 2 km, many small / few large.
    d_min, d_max = 20.0, 2000.0
    # number of craters >= d_min in a 400 km^2 window (order-of-magnitude realistic)
    n_craters = 900
    for _ in range(n_craters):
        # inverse-CDF for N(>D) ∝ D^-2  ->  D = d_min / sqrt(u)
        u = rng.uniform((d_min / d_max) ** 2, 1.0)
        diam_m = d_min / np.sqrt(u)
        depth_m = diam_m * rng.uniform(0.12, 0.20)   # fresh simple-crater depth/diam ~0.15-0.2
        cx = rng.integers(0, RES); cy = rng.integers(0, RES)
        stamp_crater(H, cx, cy, diam_m / mpp, depth_m, mpp)

    # 3) The two real named EVA craters at their relative positions.
    cxc = (RES - 1) / 2.0
    for (ex, ez, diam) in (LITTLE_WEST, WEST_CRATER):
        cx = int(cxc + ex / mpp)
        cy = int(cxc + ez / mpp)   # +z North -> +row (flipped to south-origin at import)
        stamp_crater(H, cx, cy, diam / mpp, diam * 0.16, mpp)

    # Normalize to a real elevation band (mare is ~ -2000 m below datum, ~200 m relief).
    H -= H.min()
    rng_m = float(H.max())
    base_elev = -2000.0
    min_elev = base_elev
    max_elev = base_elev + rng_m

    # 16-bit big-endian RAW.
    norm = (H / max(rng_m, 1e-6) * 65535.0).astype(np.uint16)
    be = norm.astype(">u2")   # big-endian
    raw_path = OUT / f"heightmap_{RES}.raw"
    be.tofile(raw_path)
    print(f"  wrote {raw_path.name} ({RES}x{RES}, big-endian, range {rng_m:.0f} m)")

    meta = {
        "siteId": SITE,
        "displayName": "Moon — Tranquility Base (Apollo 11, Mare Tranquillitatis)",
        "heightmapResolution": RES,
        "widthMeters": WIDTH_M,
        "lengthMeters": WIDTH_M,
        "minElevationMeters": round(min_elev),
        "maxElevationMeters": round(max_elev),
        "heightRangeMeters": round(rng_m),
        "surfaceGravity": 1.62,
        "sourceProduct": "PROCEDURALLY SYNTHESIZED (pending real LOLA/SLDEM2015 tile) — flat high-Ti mare basalt plain + impact-crater field with cumulative production function N(>D) proportional to D^-2 and fresh simple-crater bowl/rim/ejecta profiles; Little West (~33 m) and West (~180 m) craters at their real Apollo 11 EVA relative positions",
        "sourceResolutionMPerPx": round(mpp, 2),
        "byteOrder": "big-endian",
        "baseColor": "#3B3A38",
        "credit": "Synthesized for Wayfinder (tools/gen_mare_dem.py). Crater positions/character per Apollo 11 Preliminary Science Report (NASA SP-214) and LROC observations; replace with LOLA/SLDEM2015 when a tile is available.",
        "notes": "20x20 km window centred on the Apollo 11 LM Eagle descent stage (0.674N, 23.473E), Mare Tranquillitatis. Mare basalt is dark, high-titanium, low-relief. Elevation band ~ -2000 m below the lunar datum. This DEM is faithful in CHARACTER (flat plain + realistic crater size-frequency), not exact topography.",
        "regolithProfile": "moon_anorthosite",
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
