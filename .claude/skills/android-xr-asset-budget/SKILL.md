---
name: android-xr-asset-budget
description: The rules every AI-generated 3D asset, texture, and Gaussian splat must pass before it goes into Wayfinder on the Galaxy XR. Use when importing a Meshy/Tripo/Rodin mesh, a generated texture, a skybox, or a scanned splat. Every AI asset violates these by default.
---

# Android XR asset budget and import rules

AI asset generators (Meshy, Tripo, Rodin, TRELLIS) and scanners (Scaniverse, Polycam) produce beautiful output that will tank framerate on a standalone mobile GPU if imported raw. Every generated asset needs this treatment before it is safe. There is no MCP that does this for you — it's a discipline, and it's the single most valuable asset-side habit for this project.

## Meshes (from Meshy / Tripo / Rodin / TRELLIS)

- **Always retopologize/decimate.** Their default output is dense high-poly. Use the generator's remesh option AND decimate in Blender to a mobile budget: aim low (props in the low thousands of triangles; a hero asset higher but still lean). Total scene triangle and draw-call budget is set by the frame budget, not by any single asset.
- Generate **LODs** for anything not always up close.
- Import as glTF/FBX. Verify scale (generators often come in at the wrong metric scale).

## Textures / materials

- **ASTC compression** for URP/Vulkan on Android XR. Never ship uncompressed or desktop-format textures.
- Respect Unity's URP **channel packing** (mask map / metallic-smoothness convention) rather than inventing your own layout.
- Right-size resolution to how close the player gets; 4K on a distant rock is wasted bandwidth.

## Skyboxes (Blockade Labs Skybox AI)

- Export the **32-bit HDRI** for image-based lighting so terrain reflections/ambient read as a real place, plus the equirectangular/cubemap for the sky itself. This is the sanctioned path for alien atmospheres and exoplanet skies.

## Gaussian splats (Scaniverse/Polycam → aras-p UnityGaussianSplatting)

- Splats are **distant set-dressing or hero backdrops only** — never geometry the player walks through, and never in the near field. The importer runs on Unity 6 + URP + Vulkan, but splat rendering is heavy on a Snapdragon headset and the author himself calls it a toy-grade visualizer.
- **Cap splat count hard** and profile on the real Galaxy XR from day one (see the android-xr-perf skill). A splat has no collision — pair it with a decimated collision mesh (KIRI Mesh-Inclusive 3DGS or a Poisson mesh) if the player can reach it.
- Prefer a decimated **mesh** (Polycam GLB) over a splat when the player interacts with the surface.

## The rule

An asset is not "done" when it looks good in the generator; it's done when it looks good **and holds framerate on the headset.** If you can't profile it on-device yet, mark it provisional. Batch-generatable tools with REST APIs (Meshy, Tripo, ElevenLabs SFX, Blockade Labs) can be scripted; everything else (Substance, Scaniverse, Unity's in-editor AI) is a human-in-the-GUI step — don't pretend to automate it.
