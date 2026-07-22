# Presence & terrain techniques from comparable products

Research pass (July 22, 2026) over Brink Traveler, Google Earth VR / Maps
immersive, Apollo 11 VR, Titans of Space, Red Matter 1/2, Walkabout, plus Meta
performance guidance. Ranked by presence-per-millisecond for the Galaxy XR
(Quest 3-class: 400–600 draw calls, 1.3–1.8M tris per Meta's medium-sim
budget). Full citations inline.

1. **Near-field detail blend over the orbital basemap** (~0.3–0.5 ms) — tiling
   regolith albedo+normal faded in under ~15–30 m over the ortho photo, macro
   noise to break tiling, ≤4 terrain layers (URP renders 4 per pass).
   [Brink impressions — UploadVR; MSFS terrain approach; Unity TerrainLit docs]
2. **Suit-referenced spatial audio** (~free) — breathing, suit hum, boot
   thumps, radio crackle; Mars gets real low-pass wind (InSight recordings).
   Airless silence is authentic — the soundscape lives inside the suit.
   AudioMixer snapshots per state. [Brink reviews credit audio unanimously]
3. **Human-scale anchors at POIs** (few draw calls) — lander/rover props, boot
   prints, hand-height rocks; scale references make the DEM feel its size.
   [Apollo 11 VR ground-truth mosaic — NASA Spinoff; Titans of Space]
4. **Airless lighting: one sun, black sky, earthshine ambient, opposition
   surge** (ALU-cheap) — no sky ambient on the Moon; flat earthshine fill;
   `saturate(dot(V,L))^n` surge term in the terrain shader. Mars sun ~43%
   Earth intensity, butterscotch ambient. [Opposition surge lit.; POLAR-Sim]
5. **Motion-gated helmet visor rest frame** (~0.1–0.2 ms while visible) —
   visor rim appears during programmatic motion, hides at rest.
   [Duke/JMIR rest-frame studies; Titans of Space comfort mode]
6. **Earth VR-style grab-drag** (CPU-trivial) — ONE-hand primary (fatigue),
   cone-from-feet constraint (never drag into terrain), gain ~1.5–2×,
   exponential damping to zero on release, no inertia, no rotation coupling,
   vignette only while dragging. [Adam Glazier GEVR UX; GDC "UX in Google
   Earth VR"]
7. **POI compass instrument** (negligible) — palm/wrist compass pointing at
   nearest undiscovered POI; discovery-by-instrument beats floating markers.
   [Brink Traveler compass + info cards]
8. **Red Matter doctrine** (negative cost) — bake everything, fake lights with
   glows + parallax-corrected cubemaps, atlas hard, per-mesh manual LOD,
   shader prewarm at boot. [UploadVR/Meta interviews]
9. **Instanced rock scatter, hard cap, dithered cross-fade LOD** (~0.5–1 ms) —
   few archetypes, GPU instanced, RenderDoc-verify instancing survives the
   Vulkan build (documented silent-failure pitfall).
10. **Fog only where physically real, per-vertex** (~0.1 ms) — Mars pink haze
    (hides LOD seams, free depth cue); Moon none, contrast does the work.

Cross-product "being there" lessons: audio ≈ half of presence and nearly
free; real scale + human-scale anchors beat texture resolution; comfort
scaffolding must be motion-gated so it never taxes presence at rest.

Skipped as PCVR-only: planet-scale streaming, full-body IK, real-time
shadowed dynamic lights.
