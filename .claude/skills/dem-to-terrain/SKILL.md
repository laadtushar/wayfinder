---
name: dem-to-terrain
description: Convert a real NASA/USGS planetary elevation model (Mars MOLA/HiRISE, Moon LOLA) into a Unity-ready 16-bit RAW heightmap at true metric scale, using GDAL. Use whenever building or updating a world's terrain from real data. Bakes in the byte-order and NoData gotchas that bite every time.
---

# Real DEM → Unity terrain

Turn a real digital elevation model (a height map of a real place) into a Unity terrain that is the genuine shape and true metric scale of that site. This is the fiddliest part of the whole project; follow the recipe exactly.

## Prerequisites

`brew install gdal` (and optionally `brew install --cask qgis` to eyeball tiles first). Source data from USGS Astrogeology (https://astrogeology.usgs.gov/search) or NASA PDS. Record the product name, resolution, projection, vertical units, and required attribution in `docs/data-sources.md`.

## Steps

**1. Inspect** — know your projection, elevation range, and NoData value before touching it:
```bash
gdalinfo -stats input_dem.tif
```
Note: the coordinate system, `Min`/`Max` elevation (metres), the `NoData Value`, and the pixel size.

**2. Reproject + clip to your bounded site.** Pick a small, iconic area (a caldera rim, a canyon stretch), not a whole planet. Use an equal-area/local projection so metres are true:
```bash
gdalwarp -t_srs <local_projection> -te <xmin> <ymin> <xmax> <ymax> \
  -r bilinear input_dem.tif clipped.tif
```

**3. Fill NoData holes** — unfilled holes become spikes/craters in Unity:
```bash
gdal_fillnodata.py clipped.tif filled.tif
```

**4. Resample to a Unity heightmap resolution** — must be `2^n + 1` (513, 1025, 2049):
```bash
gdalwarp -ts 2049 2049 -r bilinear filled.tif sized.tif
```

**5. Convert to 16-bit unsigned RAW**, scaling the real elevation range to the full 16-bit range for maximum vertical precision (record MIN and MAX from step 1):
```bash
gdal_translate -ot UInt16 -scale <MIN> <MAX> 0 65535 -of ENVI sized.tif out.raw
```

**6. Import into Unity.** Terrain > Import Raw: pick the `out.raw`, bit depth 16, resolution 2049. **Byte order is the gotcha** — try **Mac (big-endian) first**; if the terrain looks inverted/garbage, switch to Windows. Then set the terrain's real dimensions so slopes are true-to-life:
- Terrain **Width/Length** = the real metric extent of your clip (from the `-te` bounds).
- Terrain **Height** = `MAX - MIN` in metres (the real vertical range you scaled).

**7. Drape the imagery.** Apply the co-registered orbital photo as the terrain base texture (same clip bounds).

## Verify

The known landmark reads correctly (the caldera rim, the canyon wall), slopes feel right when walked in VR, and there are no NoData spikes. Cross-check the vertical exaggeration is 1:1, not stretched. Commit the processing commands (not the multi-GB source rasters) to `docs/data-sources.md`.
