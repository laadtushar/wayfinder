# Terrain data sources and processing audit

> **Imagery drape (added 2026-07-22, Windows/GDAL 3.12.2):** each terrain now
> wears real co-registered orbital imagery, clipped to the exact windows below
> and delivered as `unity/Assets/Wayfinder/Terrain/<site>_albedo.png`
> (2048², 2–98% stretch, vertically flipped to match Unity's south-origin
> heights, Mars panchromatic sources colorized with the site `baseColor`):
>
> - **mars-olympus:** HRSC nadir channel `h0037_0000_nd4.img` (12.5 m/px, same
>   orbit/dataset/credit as the DTM), warped to the identical aeqd grid.
> - **mars-valles:** HiRISE orthoimage `ESP_046764_1660_RED_A_01_ORTHO.JP2`
>   (25 cm/px, same product page/credit as the DTM), warped to the identical
>   omerc strip. Source SRS passed explicitly (`+proj=eqc +lat_ts=-10
>   +lon_0=300.46 +R=3395582.027`) — the JP2's named Mars datum breaks GDAL's
>   transform silently.
> - **moon-shackleton:** the official LOLA hillshade product
>   `ldem_87s_5mpp_hillshade.tif` (same grid/credit/citation as the DEM) —
>   on an airless world surface brightness is geometry, so the mission's own
>   hillshade is the honest albedo until real polar imagery is added.
>
> Pipeline: `scratchpad clip_imagery.py` pattern — `gdal.Warp` from `/vsicurl`
> with the windows/projections below, 2–98% histogram stretch to byte PNG.
> Windows note: GDAL wheels' bundled curl could not reach DNS; hosts are
> system-resolved and passed as IP + `Host:` header (`GDAL_HTTP_UNSAFESSL`).

Every heightmap in `assets/terrain/` derives from real planetary elevation data. This file records the exact source product, the commands run, and the honesty notes per site. Processing ran on macOS with GDAL 3.13.1 on 2026-07-21. Raw source rasters are NOT in the repo (multi-hundred-MB); everything here is reproducible from the URLs + commands.

Output contract (all three sites): 2049x2049, 16-bit unsigned, **big-endian** RAW, exactly 8,396,802 bytes, elevation scaled min→0, max→65535. Unity import: Terrain > Import Raw, bit depth 16, byte order Mac, then set Width/Length/Height from the site's `meta.json`.

---

## mars-olympus — Olympus Mons caldera rim

- **Product:** Mars Express HRSC level-4 DTM, orbit 0037 (the dedicated caldera pass, 21 Jan 2004). `H0037_0000_DT4.IMG`, dataset `MEX-M-HRSC-5-REFDR-DTM-V1.0`, NASA PDS Geosciences Node. 50 m/px, 16-bit signed big-endian, NoData -32768, sinusoidal projection (center 227E, Mars sphere R=3,396,000 m), heights in metres above the Mars areoid.
- **URL:** https://pds-geosciences.wustl.edu/mex/mex-m-hrsc-5-refdr-dtm-v1/mexhrs_2001/data/0037/h0037_0000_dt4.img (317,943,528 bytes; supports HTTP range requests, so the window was clipped over the network without downloading the strip)
- **Credit:** ESA/DLR/FU Berlin (G. Neukum), CC BY-SA 3.0 IGO
- **Window:** 20x20 km centered 18.745N 227.176E — frames the ~19 km nested collapse crater on the caldera's northeast rim.
- **Commands:**
```bash
gdal_translate -projwin -145 1121048 19855 1101048 \
  /vsicurl/https://pds-geosciences.wustl.edu/mex/.../h0037_0000_dt4.img olympus_clip.tif
gdalwarp -s_srs '+proj=sinu +lon_0=227 +R=3396000 +no_defs' \
  -t_srs '+proj=aeqd +lat_0=18.745 +lon_0=227.176 +R=3396000 +no_defs' \
  -te -10000 -10000 10000 10000 -ts 2049 2049 -r cubicspline olympus_clip.tif olympus_2049.tif
gdal_translate -ot UInt16 -scale 17515 19585 0 65535 -of ENVI olympus_2049.tif olympus_le.raw
dd if=olympus_le.raw of=heightmap_2049.raw conv=swab   # ENVI wrote little-endian; swab = 16-bit byteswap
```
- **Verified:** clip stats 17,513–19,585 m (2.07 km rim-to-floor cliff) match the product; zero NoData pixels in-window; final RAW exactly 8,396,802 bytes.
- **Honesty:** 50 m/px source upsampled to ~9.76 m cells with cubic-spline; real information content is 50 m. The sanity landmark: the rim-to-floor drop in-window is 2.07 km, consistent with ESA's "about 3 km" for the deepest caldera walls elsewhere on the rim.

## mars-valles — Coprates Chasma canyon wall (Valles Marineris)

- **Product:** HiRISE stereo DTM `DTEEC_046764_1660_050812_1660_A01` ("East Coprates Chasma Wall Slopes", stereo pair ESP_046764_1660 + ESP_050812_1660). 1.01 m/px, 32-bit float, equirectangular (center 300.46E, std parallel -10, sphere R=3,395,582.027 m), heights relative to the Mars areoid. Product page: https://www.uahirise.org/dtm/ESP_046764_1660
- **URL:** https://www.uahirise.org/PDS/DTM/ESP/ORB_046700_046799/ESP_046764_1660_ESP_050812_1660/DTEEC_046764_1660_050812_1660_A01.IMG (561,223,996 bytes, downloaded in full)
- **Credit:** NASA/JPL-Caltech/UArizona
- **Window:** the HiRISE swath is tilted ~5° from north and its true data width is ~4.1 km (narrower than the lon/lat footprint). A north-aligned box wasted 31% of the window on NoData wedges, so the target grid was **rotated to follow the swath** with an oblique-mercator projection: 3.6 x 19.5 km, centered 13.893S 300.458E, alignment alpha = -5.0°, shifted 100 m west of the swath's bounding center to clear an edge sliver.
- **Commands:**
```bash
curl -C - -o valles_dtm.img "https://www.uahirise.org/PDS/DTM/.../DTEEC_046764_1660_050812_1660_A01.IMG"
gdalwarp -t_srs '+proj=omerc +lat_0=-13.893 +lonc=300.458 +alpha=-5.0 +gamma=0 +k=1 \
  +x_0=0 +y_0=0 +R=3395582.027 +no_defs' \
  -te -1900 -9750 1700 9750 -ts 2049 2049 -r average valles_dtm.img valles_2049_raw.tif
gdal_fillnodata.py -md 50 valles_2049_raw.tif valles_2049.tif
gdal_translate -ot UInt16 -scale -2204.885 2498.504 0 65535 -of ENVI valles_2049.tif valles_le.raw
dd if=valles_le.raw of=heightmap_2049.raw conv=swab
```
- **Verified:** 99.8% of the final grid is real measured pixels; the remaining **0.2% were small interior stereo-correlation pinholes filled by interpolation** (gdal_fillnodata). Post-fill 100% valid. Relief in-window: -2,205 to +2,499 m = 4.70 km continuous rim-to-floor. Final RAW exactly 8,396,802 bytes.
- **Honesty:** grid-up is along-track (~5° west of north), not true north; cells are anisotropic (~1.76 m across-track, ~9.52 m along-track — the heightmap is square, the physical extent is not). The strip's absolute extremes (-2,892/+3,031 m from the PDS label) lie in the ragged strip ends excluded from the window; the 4.70 km captured is what is walkable and clean.

## moon-shackleton — Shackleton crater rim (lunar south pole)

- **Product:** LOLA South Pole DEM Mosaic 5 m/px (`ldem_87s_5mpp`), NASA GSFC Planetary Geodesy Data Archive product 81 (https://pgda.gsfc.nasa.gov/products/81). 40000x40000 px, Float32, lunar south polar stereographic on the 1,737,400 m sphere; value = radius − 1,737,400 m. Laser altimetry: the permanently shadowed floor is real measured data.
- **URL:** https://pgda.gsfc.nasa.gov/data/LOLA_5mpp/87S/ldem_87s_5mpp.tif (3,465,285,714 bytes; window read over the network — see gotcha)
- **Credit:** NASA LRO LOLA / NASA GSFC PGDA. Required citation: Barker, M.K., et al. (2021), "Improved LOLA Elevation Maps for South Pole Landing Sites", Planetary and Space Science 203, 105119, doi:10.1016/j.pss.2020.105119
- **Window:** 20x20 km in projected metres, X -2000..18000, Y -16500..3500 — contains the lunar south pole (on Shackleton's western rim crest) and the entire ~21 km crater bowl.
- **Commands:**
```bash
export GDAL_DISABLE_READDIR_ON_OPEN=EMPTY_DIR CPL_VSIL_CURL_ALLOWED_EXTENSIONS=.tif \
       GDAL_HTTP_MULTIRANGE=SINGLE GDAL_HTTP_MERGE_CONSECUTIVE_RANGES=YES \
       CPL_VSIL_CURL_CHUNK_SIZE=10485760 GDAL_HTTP_MAX_RETRY=5 GDAL_HTTP_RETRY_DELAY=2
gdal_translate -projwin -2000 3500 18000 -16500 \
  /vsicurl/https://pgda.gsfc.nasa.gov/data/LOLA_5mpp/87S/ldem_87s_5mpp.tif shackleton_clip.tif
gdalwarp -ts 2049 2049 -r average shackleton_clip.tif shackleton_2049.tif
gdal_translate -ot UInt16 -scale -2872.436 1565.606 0 65535 -of ENVI shackleton_2049.tif shackleton_le.raw
dd if=shackleton_le.raw of=heightmap_2049.raw conv=swab
```
- **Verified:** 100% valid pixels, zero interpolation. Relief -2,872 to +1,566 m = 4.44 km rim-to-floor, matching the researched values to the metre. Final RAW exactly 8,396,802 bytes.
- **Gotchas hit and solved:** the PGDA server chokes on scattered TIFF range reads (the tile index lives at the end of the 3.46 GB file) — the retry/merge/chunk env vars above are required, or the first attempt fails with strile errors. The server also soft-404s (wrong URLs return HTTP 200 HTML), so always check Content-Type. The embedded GMT metadata (`actual_range` in km) is stale — trust pixels and geotransform, which are metres.

## moon-tranquillity (Apollo 11 / Statio Tranquillitatis) � #31

Terrain: PROCEDURALLY SYNTHESIZED (tools/gen_mare_dem.py), pending a real LOLA/SLDEM2015 tile (network downloads blocked in the build environment). Faithful in character � flat high-Ti mare basalt plain + impact-crater field with the standard production function N(>D) proportional to D^-2 and fresh bowl/rim/ejecta profiles; Little West (~33 m) and West (~180 m) craters at their real Apollo 11 EVA relative positions. 20x20 km window on the LM Eagle descent stage (0.674N, 23.473E). POI facts sourced (LROC, NASA SP-214 Apollo 11 Preliminary Science Report, JPL, Planetary Society, PSRD Hawaii, Staid et al. 1996 JGR) � see unity/Assets/Wayfinder/POI/moon-tranquillity.json. Regolith: moon_mare_basalt profile (dark high-Ti, distinct from the bright highlands anorthosite). Replace the DEM with real LOLA data when a tile is available.

**Star field (#41):** `Assets/Wayfinder/Sky/starmap_equirect.png` (4096x2048
equirect, lat/long), driving the shared `StarSky` skybox via `Skybox/Panoramic`.
Generated by `tools/gen_starmap.py` from **real** star data: the ~65 brightest
naked-eye stars, each with its catalogued right ascension, declination, apparent
V magnitude, and B-V colour index — so the constellations (Orion, Ursa Major /
the Big Dipper, Cassiopeia, Crux, Scorpius, ...) are correctly shaped and placed,
magnitudes drive size + brightness, and B-V drives the star tint (blue-white to
red). The same sky is seen from every world (interstellar parallax is negligible
at solar-system scale); on Mars it's hidden behind the daytime atmosphere.
**Honesty:** network access to the full Yale Bright Star / Hipparcos catalogue is
blocked in this build environment, so this is a curated bright-star SUBSET, not
the whole sky. A faint, low-brightness procedural dust of ~4000 dim points is
added purely for visual density and is **NOT catalogued** — flagged in the
generator. To regenerate from the full catalogue, drop a BSC/Hipparcos CSV next
to the script and extend its `load()` (see the header). The old procedural-noise
6-sided star textures are superseded. **Milky Way (#44):** a faint diffuse
galactic band is baked into the same equirect, computed per-pixel from the real
equatorial->galactic transform (J2000 north galactic pole 192.86/27.13) as a
narrow gaussian in galactic latitude with a broad bulge lobe toward the galactic
centre in Sagittarius (RA 266.4, Dec -28.9), mottled by smooth value-noise for
clumps + dark rifts. It is procedural (not a photographic panorama) but
correctly oriented relative to the constellations, and kept faint so it never
washes out the catalogued stars.

**Earth in the sky (#37):** `EarthSky` object in `Site_moon-tranquillity.unity`, textured with `Assets/Wayfinder/Sky/earth_epic.png` (a full-disc sunlit Earth image, NASA DSCOVR/EPIC style, public domain; pre-existing repo asset — the exact EPIC frame/date is `[unverified]`). Placed at its **real fixed position** from Tranquility Base: the Moon is tidally locked, so from (0.674N, 23.473E) the sub-Earth point (0,0) sits ~23.5deg from zenith => **elevation ~66.5deg, azimuth ~268deg** (nearly due west, a hair south), **angular size ~2deg** (about 4x the Moon as seen from Earth). A double-sided unlit disc at 850 m (inside the 1000 m runtime far clip), oriented to face the player; no billboard script (parallax across the 2 m play space is ~0.1deg). Honesty: shown ~**full** (EPIC full-disc texture) rather than the exact gibbous phase Earth showed at the Apollo 11 epoch. Deliberately **NOT** added to moon-shackleton: from the lunar south pole Earth sits on/below the horizon (invisible from the crater floor), so placing it there would be physically wrong.
