# Frame Budget Report — 2026-07-22 — Site One (mars-olympus)

Checkpoint 2 gate evidence (build-plan Task 2.8). Captured unattended via the
development-build PerfProbe (auto-warp to the site 8 s after boot) on the real
Galaxy XR over adb.

```
Device: Samsung Galaxy XR (R3GL60B2DDP), development build, unattended soak
Scene:  Site_mars-olympus — real HRSC terrain (2049², 20×20 km, 2.07 km relief),
        8 POI beacon markers, hands rig + teleport stack, return UI
CPU/GPU clocks locked: no (unattended overnight run — see residuals)

Measured (compositor heartbeat, 4 samples over 4 min on-site):
  t+1min  FrameRate 72.09 / expected 72.0   XR frame drops/sec 0.0   thermal 0
  t+2min  FrameRate 72.08 / expected 72.0   XR frame drops/sec 0.0   thermal 0
  t+3min  FrameRate 72.08 / expected 72.0   XR frame drops/sec 0.0   thermal 0
  t+4min  FrameRate 72.08 / expected 72.0   XR frame drops/sec 0.0   thermal 0

PASS/FAIL @ 72 fps: PASS — locked at refresh, zero XR frame drops across the soak
PASS/FAIL @ 90 fps (stretch): not attempted this pass

Foveated rendering: VK_LAYER_ANDROID_foveation confirmed loaded in the game's
Vulkan instance (device log); FoveatedRenderingFeature enabled in OpenXR
settings since Phase 0. Eye-tracked quality/behaviour not visually verified.

Render setup at gate time: URP/Vulkan, single-layer terrain (solid-color splat,
basemapDistance 20 km, pixel error 8, drawInstanced, no shadows anywhere),
single directional light, flat ambient, solid-color camera clear.
```

## Residuals (human pass)

- Soak was head-static (headset on a desk): no head-motion load, no eye
  tracking driving the foveation. Wear it, walk the rim, re-read the drops
  metric — the margin (empty greybox aside from terrain) predicts headroom,
  but the rule is the device is the only truth.
- Clocks unlocked; numbers are steady-state ambient, not worst-case.
- Re-run this gate after the imagery drape and any second terrain layer land —
  the always-splat/basemap equivalence that makes 20 km basemapDistance cheap
  dies at that point (rationale comment in TerrainImporter).

## Verdict

Site One holds 72 fps on the real headset with the full travel + POI stack.
CHECKPOINT 2 cleared per the plan's sequencing rule — world-building beyond
Site One is unblocked.

---

## Addendum — Sites Two & Three (same night, ticket #11)

Same unattended probe method, ~100 s soak each:

```
mars-valles     FrameRate 72.05 / 72.0   XR drops/sec 0.0   thermal 0
moon-shackleton FrameRate 71.98 / 72.0   XR drops/sec 0.0   thermal 0
```

All three v1 worlds hold refresh with zero XR frame drops. Same residuals
apply (head-static, clocks unlocked, pre-imagery-drape).
