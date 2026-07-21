---
name: terrain-verify
description: Verify a dem-to-terrain RAW output before Unity import — checks byte order, NoData holes, and metric scale so the two gotchas that "bite every time" get caught before the Terrain Import Raw step, not after.
---

# Terrain RAW verification

Run immediately after the `dem-to-terrain` skill produces `out.raw`, before importing into Unity.

## Checks

1. **NoData holes filled.** Re-run `gdalinfo -stats` on the pre-RAW GeoTIFF (`filled.tif` in the dem-to-terrain steps) and confirm no `NoData Value` pixels remain in the stats — if `gdal_fillnodata.py` missed any, they become spikes/craters in Unity.
2. **Resolution is `2^n + 1`.** Confirm the sized raster is 513, 1025, or 2049 on both axes (`gdalinfo sized.tif`) — Unity's Terrain Import Raw rejects/mishandles anything else.
3. **Scale sanity.** Recall the `MIN`/`MAX` elevation (metres) recorded during `gdal_translate -scale`. Confirm Terrain Height will be set to `MAX - MIN` and Width/Length to the real `-te` extent — flag if these weren't recorded.
4. **Byte order reminder.** State explicitly: try **Mac (big-endian)** first on import; if inverted/garbled, switch to Windows byte order. This is not auto-detectable from the file — it's a manual toggle in Unity's import dialog.

## After import

Tell the user to check: the known landmark reads correctly, slopes feel right, no visible spikes, 1:1 vertical exaggeration. This skill only verifies the pre-import data — on-device/in-editor visual confirmation is still a human step.
