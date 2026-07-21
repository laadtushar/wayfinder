# Spec: v1 terrain data + POI content (Mac-side pre-production)

Scope: the two workstreams that can complete before the Unity project exists — (A) real-terrain heightmaps for the three v1 sites, (B) the points-of-interest content those sites reveal. Everything produced here is consumed unchanged by build-plan Tasks 2.1–2.3, 3.1–3.3 on Windows.

## A. Terrain heightmaps

**Sites (from DESIGN.md):**
1. `mars-olympus` — Olympus Mons caldera rim region
2. `mars-valles` — a stretch of Valles Marineris
3. `moon-shackleton` — Shackleton crater rim (lunar south pole)

**Scale strategy.** Each site is a bounded walkable diorama roughly 5–20 km across, not a whole planet. That sets the data bar: source elevation at ≤ ~100 m/pixel (prefer 20–75 m/px where products exist). The global Mars MOLA 463 m/px mosaic alone is too coarse for a walking site; higher-resolution regional DTMs (HRSC-class for Mars, LOLA polar for the Moon) are required. Exact product selection is research-driven per site, with the chosen product's real ID, resolution, projection, and URL recorded — no guessed filenames.

**Processing recipe (per site, from the dem-to-terrain skill):** inspect (`gdalinfo -stats`) → reproject to a local metric projection → clip to the site bounds → fill NoData → resample to **2049×2049** → `gdal_translate -ot UInt16 -scale <min> <max> 0 65535` → RAW. Record which byte order the file is written in.

**Output contract (committed to the repo):**
```
assets/terrain/<site-id>/
  heightmap_2049.raw     # 16-bit unsigned, 2049x2049 => exactly 8,396,802 bytes
  meta.json              # the numbers Unity import needs (schema below)
docs/data-sources.md     # per-site: product name/ID, resolution, projection,
                         # URL, license/credit line, and the exact commands run
```
`meta.json` schema (all metric, all from the real data):
```json
{
  "siteId": "mars-olympus",
  "displayName": "Mars — Olympus Mons caldera rim",
  "heightmapResolution": 2049,
  "widthMeters": 0.0,          "lengthMeters": 0.0,
  "minElevationMeters": 0.0,   "maxElevationMeters": 0.0,
  "heightRangeMeters": 0.0,
  "surfaceGravity": 3.72,
  "sourceProduct": "",         "sourceResolutionMPerPx": 0.0,
  "byteOrder": "big-endian",
  "credit": ""
}
```
Raw source rasters stay OUT of git (multi-GB); the derived 8 MB RAWs are committed (LFS-tracked `*.raw`).

**Acceptance per site:** RAW is exactly 8,396,802 bytes; `gdalinfo -stats` of the pre-RAW stage shows no NoData remaining; min/max elevation in meta.json matches the clipped raster's statistics; a landmark sanity check is stated in data-sources.md (e.g. "caldera rim drop ≈ Xm, matches product stats"). Texture imagery is explicitly OUT of scope here (a per-site imagery clip is a Windows-side nice-to-have; the terrain must not block on it).

## B. POI content

**Contract:** per site, 5–8 points of interest as structured data the Unity POI system (and later the Gemini companion) reads directly.
```
assets/poi/<site-id>.json
{
  "siteId": "mars-olympus",
  "pois": [{
    "id": "mars-olympus/<slug>",
    "title": "",
    "fact": "",            // 2-4 sentences, plain language, true, specific to THIS spot
    "source": "",          // URL of a NASA/USGS/ESA/peer-reviewed source
    "placementHint": ""    // where on the terrain it belongs (e.g. "on the rim scarp edge")
  }]
}
```
**Quality bar:** every fact verified against its cited source (no folklore numbers); facts are about what the player is standing on/looking at, not generic planet trivia; plain language (a visitor with no astronomy background understands every sentence); ids are stable slugs (they end up in players' field logs).

**Acceptance:** 3 files, 5–8 POIs each, every `source` URL real and supporting its fact; JSON validates against the shape above.

## Execution order

1. Research pass (parallel): per-site DEM product selection (real IDs/URLs/resolutions) + per-site POI fact-finding, then an adversarial verification of both.
2. Download chosen products, run the GDAL recipe, emit RAW + meta.json + data-sources.md.
3. Author the three POI JSON files from the verified facts.
4. Verify acceptance criteria, commit, push.

Non-goals: no Unity assets, no imagery textures, no exoplanets, no more than three sites.
