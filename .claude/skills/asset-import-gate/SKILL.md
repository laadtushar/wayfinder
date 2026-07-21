---
name: asset-import-gate
description: Run the android-xr-asset-budget rules automatically whenever a generated mesh, texture, skybox, or splat is about to be imported into Assets/ — catches the "every AI asset violates these by default" problem at import time instead of relying on memory.
---

# Asset import gate

Trigger this whenever a file under `Assets/` is being added/imported that came from Meshy, Tripo, Rodin, TRELLIS, Blockade Labs, Scaniverse, or Polycam — before it's wired into a scene.

## Checklist (full rules live in `android-xr-asset-budget`)

**Meshes:**
- [ ] Retopologized/decimated to mobile budget (not raw generator output)
- [ ] LODs generated if not always up-close
- [ ] Correct metric scale on import (generators often come in wrong-scale)

**Textures:**
- [ ] ASTC compressed
- [ ] URP channel packing (mask map / metallic-smoothness), not a custom layout
- [ ] Resolution matches how close the player gets

**Skyboxes:**
- [ ] 32-bit HDRI exported for IBL + equirect/cubemap for the sky itself

**Splats:**
- [ ] Distant set-dressing/hero backdrop only — never walkable geometry
- [ ] Splat count capped, profiled on real headset
- [ ] Paired decimated collision mesh if player can reach it

## Verdict

Mark the asset **provisional** if it can't be profiled on-device yet — don't call it done because it "looks good in the generator." If any unchecked box is a hard blocker (no ASTC, no decimation), say so and stop before it's wired into a scene.
