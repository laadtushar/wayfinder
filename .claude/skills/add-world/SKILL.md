---
name: add-world
description: The repeatable flow to add a new explorable world to Wayfinder as a World Package. Use when creating a new landing site (a Mars/Moon location now, an exoplanet later). Reuses the proven pipeline so a new world costs no new travel/locomotion/discovery code.
---

# Add a world (World Package authoring)

A world in Wayfinder is a data package, not new systems code. Adding one follows the same steps every time. Do this only after the current-phase framerate gate has passed for at least one site, so you're replicating a proven setup (see the build plan's sequencing rule).

## Steps

1. **Pick a bounded, iconic site.** A region you can walk in a 2.0 m space with teleport, not a whole planet. Record why it's interesting (that becomes the points of interest).

2. **Build the terrain from real data** using the `dem-to-terrain` skill (real Mars/Moon DEM → 16-bit RAW → Unity terrain at true metric scale + draped imagery). For a v2 exoplanet, swap in the physics-seeded procedural terrain path instead, but keep everything below identical.

3. **Create the scene and the World Package asset.**
   - New scene `Assets/Scenes/Worlds/<WorldName>.unity` holding the terrain, a sky (Blockade Labs HDRI for atmosphere/exoplanet skies), and a directional light at the correct sun angle.
   - New `WorldPackage` asset (menu: Wayfinder/World Package): set `id`, `displayName`, `sceneName`, and the **real `surfaceGravity`** (Mars 3.72, Moon 1.62 m/s²; for an exoplanet, compute it from the real mass/radius).

4. **Author 5–8 points of interest**, each a structured record: `id`, `title`, the **real fact**, a **source citation**, and a world position. These records are the exact data the future Gemini companion will read — author them well now.

5. **Register** the package in the `WorldRegistry` so it appears on the bridge viewscreen.

6. **Reuse, don't rebuild:** locomotion (teleport/world-grab/snap-turn/vignette), the discovery/field-log system, and the airlock return are already in the persistent layer. A correct World Package needs none of them re-authored.

7. **Verify on the headset:** it loads from the bridge behind the warp, holds 72+ fps (android-xr-perf skill), the terrain scale/gravity feel right, and each POI reveals its real fact and logs. Add the data source + attribution to `docs/data-sources.md`.

## Definition of done

Loads from the bridge, renders the real place at framerate, gravity and sky are physically correct, points of interest are real and sourced, and no new travel/locomotion/discovery code was written.
