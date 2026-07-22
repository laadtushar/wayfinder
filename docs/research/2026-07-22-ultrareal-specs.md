# Ultra-realism implementation specs (design workflow wf_7587e2c1-340)

Four tracks, each spec + adversarial verification. Synthesis: build order = lighting contract -> regolith shader -> rock scatter -> starship bridge -> (cuttable) volumetrics. Mars worst-case ledger ~9.0ms content after discipline.


---

## agent

I have enough grounding in the actual project (URP Lit greybox, baked lightmapping already enabled, the `Viewscreen`/`Console`/`BridgeFloor` object-name contracts in `BridgeSceneTests`, the `Wayfinder.Unity` / `Wayfinder.Unity.Editor` asmdef split, and the Python-generator ethos used for terrain/sky). Here is the complete spec.

---

# Wayfinder #22 â€” Star-Trek-grade Bridge Interior: buildable implementation spec

**Stack lock:** Unity 6000.5.4f1, URP 17.5, Vulkan, single-pass instanced stereo, Android XR (XR2+ Gen 2). Doctrine: bake everything, fake lights, atlas hard, procedural/free assets only.

**Governing facts pulled from the repo (do not break these):**
- Scene: `unity/Assets/Scenes/Bridge.unity`. It already has `m_EnableBakedLightmaps: 1`, `m_BakeBackend: 2` (Progressive GPU). Greybox = `HullSegment_0..7` (octagon), `Viewscreen`, `Console`, `BridgeCeiling`, `BridgeFloor`, `KeyLight`, all parented under `BridgeVisuals`.
- `BridgeSceneTests.cs` hard-requires GameObjects named exactly **`Viewscreen`**, **`Console`**, **`BridgeFloor`**, the console+viewscreen centres **within 2.0 m horizontal** of the XR Origin, and `BridgeFloor`'s collider flat at yâ‰ˆ0 (non-capsule). The new kit must keep those names/anchors â€” add detail as children, don't rename.
- Current materials (`Hull.mat`, `Console.mat`, `Viewscreen.mat`) are stock **URP/Lit** (shader guid `933532a4fcc9baf4fa0491de14d08ed7`), flat colors, `MOTIONVECTORS` pass disabled + `_XRMotionVectorsPass:1` (ASW-ready). We replace the look, keep that motion-vector posture.
- Runtime scripts â†’ `Wayfinder.Unity` asmdef. **Generators/importers â†’ `Wayfinder.Unity.Editor` asmdef** (mandatory: an Editor-only mesh/trim baker must not land in the runtime assembly).
- The room is tiny by comfort law (~3â€“4 m interior, everything â‰¤2.0 m). Consequence that drives every cost estimate below: **the hull fills the FOV, so fragment cost dominates and triangle count is a non-issue.**
- **Budget context:** on the Bridge, no World Package terrain is loaded, so the entire 400â€“600 DC / 1.3â€“1.8M-tri / 13.8 ms envelope is available to the bridge alone. Gate `BridgeVisuals.SetActive(false)` (and its reflection probe) on `TravelState == OnSurface` so the bridge costs **zero** while a world is drawn. This is the single most important perf move and it's free.

---

## 0. Asset inventory (all procedural or free+credited)

| Asset | Source | License |
|---|---|---|
| Hull trim sheet (albedo/normal/mask) | `tools/trimsheet_gen.py` (numpy+Pillow), committed & re-runnable | self-generated |
| Kit meshes | `BridgeKitGenerator.cs` (Editor) **or** ProBuilder (free Unity pkg) | self / Unity |
| Emissive display look | procedural shader, **no texture** | self-generated |
| Viewscreen nebula backdrop (optional) | NASA/ESA public-domain image, downsampled | PD, credit in `CREDITS.md` |
| Lightmaps / reflection cube | baked in-engine | self |

No Substance, no Meshy/Tripo/Rodin, no paid accounts. Matches the existing GDAL/Python terrain + star-texture pipeline.

---

## 1. Trim sheet â€” procedural generation (`tools/trimsheet_gen.py`)

A **single 2048Â² trim sheet**, three maps, six horizontal trim bands. Every hull/console/bezel/cove face UVs into one of these V-bands, so the whole interior shares **one material** â†’ collapses to ~1 SetPass under the SRP Batcher.

**Band layout (V ranges):**

| Band | V range | Content | Emissive mask |
|---|---|---|---|
| A | 0.00â€“0.25 | Large hull panel: recessed panel-line grid, bevels, corner rivets | 0 |
| B | 0.25â€“0.44 | Sub-panels / access hatches, screw rows | 0 |
| C | 0.44â€“0.60 | Greeble strip: vents, boxes, conduit clamps (the "detail" band) | tiny (indicator dots) |
| D | 0.60â€“0.72 | Horizontal pipe/conduit run (cylindrical profile) | 0 |
| E | 0.72â€“0.84 | **Light-cove channel** â€” this band's emissive mask is hot; it becomes the accent glow | 1 (strip) |
| F | 0.84â€“1.00 | Deck-edge kick / grating | 0 |

**Generation method (fully deterministic, seeded):** build a float **heightfield** `H[2048,2048]`, then derive every map from it â€” exactly the DEMâ†’normal logic the project already trusts.

```python
# tools/trimsheet_gen.py  (numpy + Pillow, ~200 lines total)
import numpy as np
from PIL import Image
N, SEED = 2048, 22
rng = np.random.default_rng(SEED)
H = np.zeros((N, N), np.float32)

def band(v0, v1): return slice(int(v0*N), int(v1*N))

# --- Band A: panel grid + bevel + rivets ---
yA = band(0.00, 0.25)
# grooves: distance to nearest grid line -> negative valley
gx = (np.abs(((np.arange(N)/N*8) % 1) - 0.5))            # 8 columns of panels
groove = np.clip(1 - gx/0.02, 0, 1)                       # thin recess
H[yA] -= 0.15 * groove[None, :]
# bevel: raise panel edges slightly (smoothstep near groove)
bevel = np.clip((gx-0.02)/0.05, 0, 1); bevel = bevel*bevel*(3-2*bevel)
H[yA] += 0.04 * (1-bevel)[None, :]
# rivets: radial bumps every 256px along the band edges  (loop or vectorized stamp)
# ... stamp gaussian bumps at seeded positions ...

# --- Band D: pipe (cylinder profile across the band) ---
yD = band(0.60, 0.72); t = np.linspace(-1,1,yD.stop-yD.start)
H[yD] += (np.sqrt(np.clip(1-t*t,0,1))*0.5)[:,None]

# --- Band C: greebles = seeded stamped rectangles of varied height ---
# for _ in range(120): rng box in band C, add flat height 0.05..0.3

# ===== derive maps =====
# NORMAL from Sobel gradient of H (same trick as DEM->terrain normals)
gy, gx2 = np.gradient(H.astype(np.float32))
scale = 4.0
nx, ny, nz = -gx2*scale, -gy*scale, np.ones_like(H)
inv = 1.0/np.sqrt(nx*nx+ny*ny+nz*nz)
normal = np.stack([nx*inv, ny*inv, nz*inv], -1)*0.5+0.5   # +Y-up (OpenGL/Unity)
# ALBEDO: desaturated Starfleet grey-blue, darkened in cavities (AO from -H), edge-worn on bevels
ao  = np.clip(0.6 + H*1.2, 0.25, 1.0)
base = np.array([0.34,0.37,0.42])                         # linear-ish grey-blue
albedo = np.clip(base[None,None,:]*ao[...,None], 0, 1)
# per-panel hue jitter via cell id (voronoi/floor of grid) so panels aren't uniform
# MASK: R=metallic, G=micro-AO, B=emissive, A=smoothness
metallic  = np.full((N,N), 0.9, np.float32)               # metal everywhere except painted trims
smooth    = np.clip(0.35 + 0.4*(H>0), 0, 1)               # panel faces glossier, grooves rougher
emissive  = np.zeros((N,N), np.float32); emissive[band(0.72,0.84)] = 1.0   # cove strip
mask = np.stack([metallic, ao, emissive, smooth], -1)

Image.fromarray((albedo*255).astype('uint8')).save('Hull_Albedo.png')
Image.fromarray((normal*255).astype('uint8')).save('Hull_Normal.png')
Image.fromarray((mask*255).astype('uint8')).save('Hull_Mask.png')
```

**Optional fidelity upgrade (still free):** model the greeble/pipe strip high-poly in **Blender** and bake its normal, compositing over the numpy normal for Band C/D. Do this only if the procedural normal reads flat on-device.

**Unity import steps:**
1. Drop the three PNGs in `unity/Assets/Wayfinder/Textures/Bridge/`.
2. `Hull_Albedo`: sRGB **on**, ASTC **6Ã—6**, max 2048, mipmaps on, aniso 2.
3. `Hull_Normal`: import type **Normal map**, sRGB **off**, ASTC **6Ã—6** (drop to 5Ã—5 if panel lines block up), mipmaps on. **On-device check: if raking light looks inverted, flip green** (regenerate with `ny â†’ -ny`; Unity expects +Y-up).
4. `Hull_Mask`: sRGB **off** (linear data), ASTC **6Ã—6**, mipmaps on.

**Memory:** 3 Ã— 2048Â² ASTC 6Ã—6 â‰ˆ **3 Ã— 1.86 MB â‰ˆ 5.6 MB** for the entire hull look.

**Degrades gracefully:** regenerate at 1024Â² (`N=1024`) â†’ 1.4 MB total; or ASTC 6Ã—6â†’8Ã—8 halves again. Panel lines soften but silhouette/lighting hold.

**Per-frame cost of the trim sheet itself:** it's just 3 texture samples inside the hull material â€” folded into the hull fragment cost in Â§5, not a separate line item.

---

## 2. Modular mesh kit

**Recommended path: procedural Editor generator** `unity/Assets/Wayfinder/Editor/BridgeKitGenerator.cs` (in `Wayfinder.Unity.Editor` asmdef), writing `.mesh` assets + a `BridgeKit` prefab with trim UVs baked in. This matches the repo's "committable, deterministic, re-runnable" pattern and guarantees UVâ†’trim-band mapping is exact. **ProBuilder (free) is the manual fallback** for artist iteration on the console curve.

**Kit pieces, tri budgets, and trim-band mapping:**

| Piece | Count | Tris ea | UV band(s) | Notes |
|---|---|---|---|---|
| `WallPanel` (2 m seg) | 8 | ~70 | A/B upper, C lower third | beveled edges so normal-mapped lines catch key light |
| `CornerPillar` | 8 | ~60 | D (pipe) | octagon joints, vertical conduit |
| `CeilingCove` (ring seg) | 8 | ~24 | E (emissive) | angled soffit **hiding the emissive strip** â†’ indirect glow |
| `DeckFloor` | 1 | ~200 | F edge + A field | inset grid; **keep name `BridgeFloor`, flat box collider at yâ‰ˆ0** |
| `ConsoleShell` (hero) | 1 | ~4â€“8k | A/B + screen cutouts | swept curve, **centre â‰¤1.5 m reach**, keep name `Console` |
| `ViewscreenBezel` | 1 | ~150 | A + chamfer | frame; keep display quad named `Viewscreen` |
| `Greeble` archetypes | 3â€“4 | ~150 | C | GPU-instanced, **hard cap 20** |

**Total unique interior â‰ˆ 20â€“40k tris** incl. a generous hero console. Against 1.3â€“1.8M that is ~2% â€” tris are irrelevant here; the console can be lavish.

**Build steps:**
1. Run `Tools â–¸ Wayfinder â–¸ Generate Bridge Kit` (menu item on the generator). It emits meshes to `Assets/Wayfinder/Meshes/Bridge/` and a `BridgeKit.prefab`.
2. Parent all pieces under existing `BridgeVisuals`; **mark every piece `Static` (Contribute GI + Batching Static + Occluder/Occludee).**
3. Assign one shared `Hull_Trim.mat` (the TrimLit material, Â§3a) to hull/pillar/cove/floor/bezel. Console shell = same material (its screens are separate quads, Â§3b). One material across the shell â†’ SRP-batched.
4. Delete the greybox `HullSegment_*` after the kit reads correct in Direct Preview.

**Degrades gracefully:** greebles are the least load-bearing â€” cap 20â†’8â†’0. Console tri budget 8kâ†’3k by dropping button relief (relief moves into the normal map instead).

**Cost:** geometry submission is SRP-batched; see Â§5.

---

## 3. Shaders

### 3a. `TrimLit` â€” the hull material (URP **Lit** Shader Graph)

Must be **Lit** (not Unlit) so it receives the baked lightmap + reflection probe (that's the whole point). Master stack (Lit, Metallic workflow):

- **Base Color** = `SAMPLE(Hull_Albedo).rgb Ã— _Tint`
- **Normal (Tangent)** = `NormalUnpack(SAMPLE(Hull_Normal)) Ã— _NormalStrength`
- **Metallic** = `Mask.r`
- **Smoothness** = `Mask.a`
- **Ambient Occlusion** = `Mask.g`
- **Emission** = `Mask.b Ã— _CoveColor(HDR) Ã— (0.85 + 0.15Â·sin(_Time.yÂ·_CovePulse))` â€” a slow cove breathe, ALU-trivial, zero CPU.

Three samplers, SRP-batcher compatible, no realtime lights sampled. Set **Emission GI = Baked** on the material so the cove wash is picked up by the lightmap.

### 3b. `EmissiveDisplay` â€” animated console screens (URP **Unlit**, procedural, **no texture**)

Red-Matter-cheap: additive-emissive, unlit, procedural LCARS/telemetry/star-chart. Hand-written URP Unlit is preferred over Shader Graph here for prewarm control and to avoid variant bloat. Core fragment sketch:

```hlsl
// EmissiveDisplay.shader  (URP Unlit, HLSLPROGRAM fragment core)
// _Base(dark), _Glow(HDR cyan/amber), _Speed, _Mode(0 telemetry / 1 starchart)
half hash(half2 p){ return frac(sin(dot(p,half2(127.1,311.7)))*43758.5); }

half4 frag(Varyings i):SV_Target {
    float2 uv = i.uv; float t = _Time.y*_Speed;
    // --- grid + scanlines ---
    float grid = step(0.96, frac(uv.x*24)) + step(0.98, frac(uv.y*16));
    float scan = 0.5 + 0.5*sin((uv.y*220) - t*6);            // rolling CRT lines
    // --- animated bar graph / waveform ---
    float wave = 0.5 + 0.35*sin(uv.x*18 + t*3) * sin(uv.x*5 - t);
    float bars = step(uv.y, wave) * step(frac(uv.x*20),0.7);
    // --- star chart: hashed blinking points ---
    float2 c = floor(uv*20); float star = step(0.92, hash(c)) *
               (0.5+0.5*sin(t*2 + hash(c)*6.28));            // twinkle
    float pat = lerp(saturate(grid*0.6 + bars*0.8 + scan*0.15),
                     star, _Mode);
    float flick = 0.9 + 0.1*hash(float2(floor(t*12),0));     // CRT life
    half3 col = _Base.rgb + _Glow.rgb * pat * flick;         // HDR for bloom pickup
    return half4(col, 1);
}
```

ShaderGraph equivalent (if artist-editable is preferred): `Time â†’ Multiply(_Speed)`; `UV â†’ Tiling(24,16) â†’ Fraction â†’ Step` for grid; `Sine(UV.xÂ·18 + tÂ·3) â†’ Step(UV.y)` for bars; `Voronoi/Hash cell â†’ Step(0.92) â†’ Ã—Sine(t)` for stars; `Lerp(telemetry, stars, _Mode)`; feed **Emission** only (Unlit master, no Base lighting). Set **Surface = Opaque**, `_XRMotionVectorsPass` handling to match existing materials (static geometry â†’ zero motion vectors â†’ safe under ASW; only the scrolling *content* half-rates, which is imperceptible).

**Screen variety without breaking SRP batching:** drive per-screen `_Mode`, `_Speed`, `_Glow` via **UV2 / vertex color** authored into the mesh, **not** MaterialPropertyBlock (MPB breaks the SRP Batcher). One material, N screens, one SetPass.

### 3c. Viewscreen + bezel glow

- `Viewscreen` quad: `EmissiveDisplay` in a **"warp starfield/nebula"** variant (radial streaks = `pat` driven by `atan2/length` of centered UV), **composited behind the existing world-space `DestinationCanvas`** (the destination list already lives there). Optionally multiply in one downsampled **NASA/ESA public-domain nebula** (credit in `CREDITS.md`) for richness â€” one 512Â² sample, cheap.
- **Bezel glow = fake bloom, not post-process.** Add a soft-edged **additive "glow card"** quad (transparent, radial-falloff alpha, HDR color) floating ~1 cm in front of the bezel inner edge and behind each cove strip. This is the Red Matter glow trick: it costs bounded transparent overdraw instead of a full-screen Bloom pass.

**Why not URP Bloom post-process:** full-screen bloom on mobile XR adds multiple half-res passes (~1â€“2 ms) and interacts badly with foveation/ASW. Fake additive cards give the halo for ~0.2â€“0.4 ms of bounded overdraw. Keep real Bloom as an *optional* upgrade, off by default, measured on-device before ever enabling.

**Degrades gracefully:** `EmissiveDisplay` drops layers in order â€” starfield twinkle â†’ waveform â†’ scanlines; final fallback bakes the animated look into a small scrolling flipbook (trades ALU for a texture sample). Glow cards: reduce count, then remove.

---

## 4. Lighting bake plan

**All baked. Zero realtime lights, zero realtime shadows at runtime.** The scene's Progressive GPU lightmapper is already enabled.

**Lights (all `Baked` mode):**
- **`KeyLight`** (exists): area/spot from the ceiling-front ring, warm-neutral (~5200 K), defines form and rakes the trim normals.
- **`FillLight`** (add): soft area light opposite the key, cool tint (~7000 K), low intensity â€” lifts shadow side so the octagon doesn't read as a cave.
- **Accent = emissive-GI**, not lights: the Band-E cove strips and console/viewscreen glow are **emissive materials with GI = Baked**, so the blue/cyan wash bakes onto adjacent hull for free. Add 2â€“3 tiny baked point lights only where you want a pooled hotspot (under-console, viewscreen base).

**Directional vs non-directional (real decision for this feature):** bake **Directional lightmaps**. The entire value of a normal-mapped trim sheet is that baked light rakes across panel lines â€” with **non-directional** lightmaps, the normal map contributes **nothing to diffuse** (only to reflection-probe specular), and the hull goes flat. Directional costs one extra lightmap sampler + ~2Ã— lightmap memory. That's the price of the look; it's the **first** degradation lever if the frame is short (Â§6).

**Reflection â€” the load-bearing fake:** **one Baked reflection probe** centred in the bridge with **Box Projection ON (parallax-corrected)** so reflections align to the octagon walls. This is what makes cheap baked metal read as "expensive."
- URP Asset â–¸ Lighting â–¸ Reflection Probes â–¸ enable **Box Projection** (and Probe Blending off â€” one probe).
- Probe: **Type = Baked**, resolution **256** (128 as fallback), **Box Size = room bounds**, HDR on.
- URP Lit already does one cubemap sample; box projection reprojects it â€” near-zero added runtime cost, one bake.

**Light Probes:** small `LightProbeGroup` (â‰ˆ12â€“20 probes) so **dynamic elements** â€” the player's gloved hands (`SuitWardrobe`), grabbables, uninstanced greebles â€” sit in the baked lighting.

**Bake settings (concrete):** Progressive GPU; Lightmap Resolution ~30 texels/unit (small room â†’ crisp); Lightmap Size 1024 (bump to 2048 only if seams); Direct/Indirect samples default-high; **AO on**, max distance ~0.6 m; Filtering: Auto/Gaussian; Compress on. Static-flag everything; mark cove/console/viewscreen emission **Baked**.

**Shader prewarm (doctrine #8):** collect all bridge shader variants into a `ShaderVariantCollection`, `Warmup()` at boot, so first look at the viewscreen doesn't hitch.

**Per-frame cost:** the bake means the **shadow pass and realtime light loop are ~0 ms**. Runtime lighting = 1 lightmap sample (2 if directional) + 1 reflection-probe sample per hull pixel, folded into Â§5.

**Degrades gracefully:** Directionalâ†’Non-Directional lightmaps (halve sampler+memory, lose normal-in-diffuse); reflection 256â†’128; boxâ†’infinite projection (cheaper, slightly wrong parallax); drop the 2â€“3 accent point lights (coves still carry the accent via emissive-GI).

---

## 5. Estimated cost â€” per-frame ms, draw calls, tris (with justification)

Target device XR2+ Gen 2, ~13.8 ms stereo budget, eye-tracked foveation **on** (mandated). **On the Bridge, no world terrain is loaded**, so this is the whole scene.

**Draw calls / SetPass (SRP Batcher on):**

| Group | Materials | ~SetPass |
|---|---|---|
| Hull + pillars + coves + floor + bezel + console shell | 1 (`Hull_Trim`) | 1â€“2 |
| Emissive console screens | 1 (`EmissiveDisplay`, UV2-varied) | 1 |
| Viewscreen backdrop | 1 | 1 |
| Fake-bloom glow cards (transparent) | 1 | 1 |
| DestinationCanvas world-space UI | 1â€“2 | 2 |
| Hands (2) + wardrobe gloves | 1â€“2 | 2 |
| **Total** | | **~8â€“10 SetPass, ~60â€“120 raw draws â†’ far under 400â€“600** |

**Tris:** ~20â€“40k static + hands â‰ˆ **well under 2% of the 1.3â€“1.8M budget.**

**Per-frame ms (honest breakdown):**

| Cost | ms (est.) | Why |
|---|---|---|
| Opaque hull/console/floor fragment (fills FOV, 3 samplers + directional lightmap + box reflection, no realtime lights) | **2.5â€“4.0** | dominant cost; small room = high pixel coverage; foveation cuts periphery |
| Emissive console screens (unlit procedural, small coverage) | 0.2â€“0.5 | ALU-only, tiny pixel area |
| Viewscreen backdrop | 0.3â€“0.6 | one wall's worth of unlit procedural + optional 1 texture |
| Fake-bloom glow cards (bounded transparent overdraw) | 0.2â€“0.4 | few soft additive quads |
| Realtime shadow pass | **0** | fully baked |
| Realtime light loop | ~0 | baked; probes are cheap |
| World-space UI | ~0.2 | |
| Hands | ~0.3 | |
| **Bridge total** | **â‰ˆ 3.5â€“6.0 ms** | leaves **7â€“10 ms** headroom under 13.8 |

**Justification for the headroom claim:** the two things that normally eat a mobile-XR frame â€” realtime shadows and realtime lights â€” are both zero here (baked). Everything is SRP-batched into ~8â€“10 SetPass. The remaining spend is hull fragment cost, which foveated rendering attacks directly, plus a bounded amount of transparent overdraw we control by counting glow cards. This is precisely the Red Matter cost profile.

---

## 6. Graceful-degradation ladder (apply in order, only as the on-device profiler demands)

1. **Directional â†’ Non-Directional lightmaps.** Biggest single lightmap-sampler + memory win. Cost: hull normals stop reading in diffuse (still read in reflection). Re-check the trim still reads before accepting.
2. **Trim sheet 2048â†’1024**, ASTC 6Ã—6â†’8Ã—8. ~4Ã— texture-memory + bandwidth cut.
3. **Fake-bloom cards: reduce count / merge.** If real URP Bloom was ever enabled, disable it first (biggest single reclaim).
4. **Reflection probe 256â†’128**, then **boxâ†’infinite projection** (parallax slightly wrong on the console curve, but stereo-safe and cheaper).
5. **`EmissiveDisplay`: shed procedural layers** (twinkle â†’ waveform â†’ scanlines); final fallback = scrolling flipbook texture.
6. **Merge `Console` material into `Hull_Trim`** â€” one fewer SetPass; screens become emissive regions of the hull mask.
7. **Greeble instances 20â†’8â†’0** (least load-bearing detail).
8. **Nuclear:** static-batch/mesh-combine the whole hull into one draw; gate emissive animation to 30 Hz by quantizing `_Time`; freeze the viewscreen to a static emissive image. The bridge is 100% static, so this always converges.

Every lever above is reversible and none touches the comfort/locomotion contract or the `Viewscreen`/`Console`/`BridgeFloor` test anchors.

---

## 7. The single riskiest unknown

**Whether URP 17.5's box-projected (parallax-corrected) reflection probe is stereo-correct under Vulkan single-pass instanced rendering on Android XR.** `[unverified â€” must be checked on-device]`

Why this one: the box-projected probe is the load-bearing "fake expensive reflections" trick â€” it's what sells baked metal as a Star-Trek hull. Box projection reprojects the reflection ray using the **camera position**, and single-pass instanced stereo has a documented class of bugs where per-eye view/position isn't threaded correctly into reflection math, producing an **eye-divergent reflection** (the two eyes see the reflection at different parallax â†’ it swims or ghosts when you move your head, which is both ugly and a comfort hazard). It cannot be caught in the editor or Direct Preview (the PC GPU renders those and isn't the real stereo path) â€” **only an on-device build reveals it.**

**How to test it early (do this in the first device build, before building the console):** put a curved reflective surface (the console shell) with a recognizable bright cove reflected in it, then **translate your head laterally ~20 cm** and watch the reflection. Correct = reflection tracks smoothly and identically in both eyes. Broken = reflection judders, doubles, or the two eyes disagree (close one eye then the other â€” the reflected hotspot should sit in the same world spot).

**Fallback if broken:** drop to **infinite-projection** baked probe (loses wall-aligned parallax but is stereo-robust and still gives the metal its environment reflection), and lean harder on the directional-lightmap specular + emissive cove wash to carry the "expensive" read. This keeps the look 80% intact with zero stereo risk.

---

**Files this feature touches/creates:**
- `tools/trimsheet_gen.py` (new, committed generator)
- `unity/Assets/Wayfinder/Textures/Bridge/Hull_{Albedo,Normal,Mask}.png` (+ `.meta`)
- `unity/Assets/Wayfinder/Editor/BridgeKitGenerator.cs` (new, `Wayfinder.Unity.Editor` asmdef)
- `unity/Assets/Wayfinder/Meshes/Bridge/*.mesh`, `BridgeKit.prefab`
- `unity/Assets/Wayfinder/Shaders/TrimLit.shadergraph`, `EmissiveDisplay.shader` (new `Shaders/` folder)
- `unity/Assets/Wayfinder/Materials/Hull.mat`, `Console.mat`, `Viewscreen.mat` (retarget to new shaders; keep GameObject names)
- `unity/Assets/Scenes/Bridge.unity` (kit under `BridgeVisuals`; add `FillLight`, `LightProbeGroup`, one Baked box reflection probe; keep `Viewscreen`/`Console`/`BridgeFloor`)
- URP Asset: enable Reflection Probe **Box Projection**
- `CREDITS.md` if any NASA/ESA nebula image is used

**Verification gates:** re-run `BridgeSceneTests` (object-name + comfort-radius contracts must still pass), then the **on-device 72 fps gate is the only real proof** â€” Direct Preview and the editor are never framerate evidence for this feature, and the reflection-probe stereo check in Â§7 must be cleared on the first device build.

---

## agent

I have everything needed â€” the exact URP terrain source layout, the confirmed fragment injection point (`positionWS` is already interpolated at `TEXCOORD7`; inject between `half3 albedo = mixedDiffuse.rgb;` and `InitializeInputData`), the current 1-layer material/terrainlayer setup, and the importer wiring. Here is the complete spec.

---

# Near-field regolith detail shader (#18) â€” buildable implementation spec

## 0. Design decision that drives everything: detail is NOT a terrain layer

The single most important architectural choice, and the answer to the "â‰¤4 layers" constraint: **the regolith detail is a pair of plain material textures sampled inside a forked URP TerrainLit fragment, blended by camera distance â€” it consumes zero terrain-layer slots.** Each site keeps its one basemap layer (`<site>_base.terrainlayer`, the orbital ortho photo tiled once over the 20 km clip). We stay at **1 of 4 layers used**, URP still renders the terrain in a **single pass** (â‰¤4 layers = single pass), and 3 slots remain free for future splat variety. Adding detail is a fragment-stage change: **+0 draw calls, +0 triangles.**

Rejected alternative: adding a 2nd terrain layer for detail. That would (a) burn a layer slot, (b) double the splatmap `_Control` + splat sampling machinery, (c) give no clean per-pixel distance fade (terrain layers fade only via `basemapDistance`, all-or-nothing), and (d) tile at the layer's `tileSize` with no macro-noise break. It's strictly worse.

**Shader Graph is not viable for the terrain surface here.** URP 17.5 has no Shader Graph master stack that emits a terrain-system-compatible shader with the `"TerrainCompatible"="True"` tag, the `_Control`/`_Splat0..3` convention, the `TerrainLitAdd` >4-layer pass, and `_TERRAIN_INSTANCED_PERPIXEL_NORMAL` (which their material has ON: `_EnableInstancedPerPixelNormal: 1`). We fork the HLSL. I give the "node list" below as the conceptual graph of the injected function, but it ships as HLSL.

---

## 1. Exact build steps (Unity engineer follows in order)

**Source of truth (confirmed present):**
`unity/Library/PackageCache/com.unity.render-pipelines.universal@e38be786c41e/Shaders/Terrain/` â€” contains `TerrainLit.shader`, `TerrainLitInput.hlsl`, `TerrainLitPasses.hlsl` (+ `TerrainLitAdd/Base/BasemapGen`, `TerrainLitDepthNormalsPass.hlsl`, `TerrainLitMetaPass.hlsl`). This is URP 17.5 (Unity 6000.5.4f1).

1. **Create fork folder** `unity/Assets/Wayfinder/Shaders/Terrain/`. Copy in **three** files only:
   - `TerrainLit.shader` â†’ `WayfinderRegolithLit.shader`
   - `TerrainLitInput.hlsl` â†’ `WayfinderRegolithLitInput.hlsl`
   - `TerrainLitPasses.hlsl` â†’ `WayfinderRegolithLitPasses.hlsl`
   Leave `TerrainLitAdd/Base/BasemapGen`, `DepthNormalsPass`, `MetaPass` referencing the package copies via absolute `Packages/com.unity.render-pipelines.universal/Shaders/Terrain/...` includes. We only touch the Forward path; basemap-gen, add-pass, depth-normals, meta (lightmap) are untouched, so detail never perturbs the baked basemap or shadow/depth.

2. **In `WayfinderRegolithLit.shader`:**
   - Rename: `Shader "Wayfinder/Terrain/RegolithLit"`.
   - Fix the two forked `#include` paths to the local copies; keep the `TerrainLitAdd`/`Base`/`BasemapGen` sub-shader references pointing at the package files (or delete the add/base passes entirely â€” with 1 layer we never need the >4-layer add pass; keep them for safety).
   - Add Properties (see Â§2A).
   - In the **ForwardLit `Pass` only**, add: `#pragma shader_feature_local_fragment _ _REGOLITH_DETAIL`. Do **not** add it to GBuffer/DepthOnly/DepthNormals/Meta passes â€” mobile XR runs Forward+, so this keeps the variant count at +1 and leaves depth/shadow untouched.

3. **In `WayfinderRegolithLitInput.hlsl`:** append the texture declarations + `ApplyRegolithDetail()` function (see Â§2B). Guard the whole block in `#if defined(_REGOLITH_DETAIL)` â€¦ `#endif` so the off-variant is byte-for-byte the stock terrain shader.

4. **In `WayfinderRegolithLitPasses.hlsl`, `SplatmapFragment` (forward variant, confirmed at line ~441â€“462):** inject one call between `half3 albedo = mixedDiffuse.rgb;` and `InitializeInputData(IN, normalTS, inputData);`:
   ```hlsl
   #if defined(_REGOLITH_DETAIL)
       ApplyRegolithDetail(IN.positionWS, /*inout*/ albedo, /*inout*/ normalTS);
   #endif
   ```
   `IN.positionWS` already exists (`TEXCOORD7`) and `normalTS` is `(0,0,1)` here because no layer normal map is assigned â€” so our detail normal cleanly *becomes* the terrain normal within the fade band. **No new interpolator, no vertex change.**

5. **Generate detail textures** (Â§3): run `tools/gen_regolith.py --profile mars_basalt` and `--profile moon_anorthosite`, plus `--macro`. Outputs land in `unity/Assets/Wayfinder/Terrain/Regolith/`. Set import settings via the AssetPostprocessor in Â§3.

6. **Point the material at the fork.** For each `unity/Assets/Wayfinder/Materials/Terrain_<site>.mat`: swap `m_Shader` from the URP Terrain/Lit guid (`69c1f799e772cb6438f56c23efccb782`) to `Wayfinder/Terrain/RegolithLit`, enable keyword `_REGOLITH_DETAIL`, assign the profile's `_DetailAlbedo`/`_DetailNormal` + shared `_MacroNoise`, and set the fade/tiling floats from meta.json. Do this through `TerrainImporter.cs` (Â§5) â€” never hand-edit the `.mat`, keep it re-runnable and data-driven.

7. **Prewarm** (Red Matter doctrine): add the `_REGOLITH_DETAIL` ForwardLit variant to the boot `ShaderVariantCollection` so the first warp-in doesn't hitch. If a `Resources/WayfinderShaderVariants.shadervariants` exists, add it there; else add `WayfinderRegolithLit` to Project Settings â†’ Graphics â†’ Preloaded Shaders and call `ShaderVariantCollection.WarmUp()` at boot behind the warp fade.

8. **Verify tiers** per CLAUDE.md: XR Sim (wiring, keyword compiles) â†’ Direct Preview (visual fade band, tiling break) â†’ **on-device adb build for the ms number** (only-on-device is framerate evidence).

---

## 2. Shader code, concrete

### 2A. Properties to add to `WayfinderRegolithLit.shader`
```
[Toggle(_REGOLITH_DETAIL)] _RegolithDetail ("Near-field regolith detail", Float) = 0
[NoScaleOffset] _DetailAlbedo   ("Detail Albedo (RGB, A=height)", 2D) = "grey" {}
[NoScaleOffset] [Normal] _DetailNormal ("Detail Normal (BC5)", 2D) = "bump" {}
[NoScaleOffset] _MacroNoise     ("Macro Noise (R)", 2D) = "grey" {}
_DetailTileMeters   ("Detail tile size (m)", Float) = 0.75
_MacroTileMeters    ("Macro tile size (m)", Float) = 11.0
_FadeStart          ("Fade start (m, full detail)", Float) = 6.0
_FadeEnd            ("Fade end (m, zero detail)",  Float) = 22.0
_DetailStrength     ("Detail strength", Range(0,1)) = 1.0
_DetailNormalScale  ("Detail normal scale", Range(0,2)) = 0.9
_MacroContrast      ("Macro contrast", Range(0,0.5)) = 0.18
```

### 2B. `ApplyRegolithDetail` â€” HLSL (append to forked Input.hlsl)
```hlsl
#if defined(_REGOLITH_DETAIL)
TEXTURE2D(_DetailAlbedo);  SAMPLER(sampler_DetailAlbedo);   // shared sampler for all three
TEXTURE2D(_DetailNormal);
TEXTURE2D(_MacroNoise);
float _DetailTileMeters, _MacroTileMeters, _FadeStart, _FadeEnd;
float _DetailStrength, _DetailNormalScale, _MacroContrast;

void ApplyRegolithDetail(float3 positionWS, inout half3 albedo, inout half3 normalTS)
{
    // Distance fade: 1 near -> 0 far. GetCameraPositionWS() is per-eye correct under XR.
    float camDist = distance(positionWS, GetCameraPositionWS());
    half fade = (half)saturate(1.0 - smoothstep(_FadeStart, _FadeEnd, camDist)) * _DetailStrength;

    // World-XZ planar UVs (the basemap layer is tiled ONCE over 20 km, so its UV is
    // useless for near-field frequency; derive detail from world position instead).
    float2 duv = positionWS.xz * rcp(_DetailTileMeters);
    float2 muv = positionWS.xz * rcp(_MacroTileMeters);
    // Compute derivatives BEFORE the branch so the coherent-branch skip is defined.
    float2 dx = ddx(duv), dy = ddy(duv);

    UNITY_BRANCH
    if (fade <= (half)0.002) return;   // far pixels skip 3 fetches; quad-coherent, cheap on Adreno

    // Macro noise breaks tiling: remap to ~[1-c, 1+c] and modulate detail luminance.
    half macro = SAMPLE_TEXTURE2D(_MacroNoise, sampler_DetailAlbedo, muv).r;
    macro = lerp((half)(1.0 - _MacroContrast), (half)(1.0 + _MacroContrast), macro);

    // Detail albedo uses Unity's detail-map convention (centered on 0.5, *2), so the
    // real orbital photo keeps ALL large-scale color truth; detail only adds high-freq crunch.
    half4 dA = SAMPLE_TEXTURE2D_GRAD(_DetailAlbedo, sampler_DetailAlbedo, duv, dx, dy);
    half3 detail = dA.rgb * ((half)2.0 * macro);
    albedo *= lerp((half3)1.0, detail, fade);

    // Detail normal: with no layer normal, normalTS=(0,0,1) -> detail becomes the surface normal.
    half3 dN = UnpackNormalScale(
        SAMPLE_TEXTURE2D_GRAD(_DetailNormal, sampler_DetailAlbedo, duv, dx, dy),
        _DetailNormalScale);
    normalTS = normalize(lerp(normalTS, BlendNormalRNM(normalTS, dN), fade));
}
#endif
```

**Conceptual node graph** (if anyone insists on visualizing it): `PositionWS â†’ (Distance to CameraPos) â†’ Smoothstep(FadeStart,FadeEnd) â†’ OneMinus â†’ Ã—DetailStrength = fade`; `PositionWS.xz â†’ Ã—(1/TileM) â†’ SampleTex(DetailAlbedo) & SampleTex(DetailNormal)`; `PositionWS.xz â†’ Ã—(1/MacroM) â†’ SampleTex(MacroNoise) â†’ Remap[1Â±c]`; `albedo Ã— Lerp(1, detailRGBÃ—2Ã—macro, fade)`; `NormalBlend(normalTS, UnpackNormal(detail)Ã—scale) â†’ Lerp by fade`. Output into the existing `albedo`/`normalTS` before `InitializeInputData`.

**Two correctness notes baked in above:** (1) UVs/derivatives are computed *before* the `UNITY_BRANCH` and samples use `_GRAD`, so the coherent far-pixel skip never hits undefined-derivative behavior. (2) The branch is the key cost lever â€” it confines all 3 fetches to the â‰¤22 m disc.

---

## 3. Asset generation â€” procedural, per-site, zero paid accounts

Committed tool `tools/gen_regolith.py` (numpy + Pillow; already have Python for the DEM pipeline). Produces **seamlessly tiling** albedo (RGB + height in A), a BC5-ready object-space-flat tangent normal derived from height, and one shared macro-noise. Seamlessness comes from generating noise on a periodic domain via `numpy.fft` (build spectrum, inverse-FFT â†’ wraps exactly), so no visible tile seam.

**Per-profile parameters (real-referenced, data not magic numbers):**

| param | `mars_basalt` | `moon_anorthosite` |
|---|---|---|
| base albedo (linear) | 0.14, warm â€” RGB â‰ˆ (0.19,0.12,0.08) butterscotch-brown | 0.13, neutral grey â‰ˆ (0.15,0.15,0.16) |
| clast (rock chip) density / size | moderate, rounded, small basalt granules | sparse, **angular** fragments + tiny craterlets |
| micro-contrast | softer (fines, mild aeolian sorting) | **higher** (airless: hard shadow terminators, no atmospheric wash) |
| normal strength baked | 0.8 | 1.0 (sharper relief) |
| height detail octaves | 4 (dunelet + granule) | 5 (breccia + craterlet pits) |

**Pipeline inside the script:**
1. FFT periodic fBm height field `H` (1024Â², 4â€“5 octaves, profile spectral slope).
2. Add clasts: Poisson-disk points â†’ stamp rounded (Mars) or faceted (Moon) height bumps; add small negative craterlets for Moon.
3. Albedo = base color Ã— (1 + kÂ·(H_highpass)) with per-clast slight darkening; **normalize mean luminance to 0.5** so it obeys the detail-map Ã—2 convention (this is why the real photo's color survives). Chroma kept subtle (Â±) so it tints, never overrides.
4. Normal from `H` via Sobel â†’ tangent-space RG (Z reconstructed in shader) â†’ pack for BC5.
5. Height â†’ A channel of albedo (reserved for optional future height-blend/parallax; free to carry).
6. `--macro`: separate 512Â² periodic 2-octave value noise, R8, tiled at ~11 m.

**Outputs** â†’ `unity/Assets/Wayfinder/Terrain/Regolith/`:
`mars_basalt_albedo.png` (1024Â² RGBA), `mars_basalt_normal.png` (1024Â² RG), `moon_anorthosite_albedo.png`, `moon_anorthosite_normal.png`, `macro_noise.png` (512Â² R). Credit line in the tool header + repo CREDITS: "Procedurally generated (Wayfinder `gen_regolith.py`), no third-party assets." (Fallback if procedural quality disappoints: CC0 regolith from ambientCG/Poly Haven â€” free + credited â€” dropped into the same filenames; the shader/importer don't care about source.)

**Import settings** (add an `AssetPostprocessor` keyed on the `Terrain/Regolith/` path):
- `*_albedo.png`: sRGB **on**, mipmaps **on**, wrap Repeat, aniso 1, ASTC 6Ã—6 (Android) â€” ~1.4 MB + mips.
- `*_normal.png`: TextureType **Normal**, sRGB off, mipmaps on, ASTC 6Ã—6 / BC5-equivalent â€” ~1.4 MB.
- `macro_noise.png`: sRGB off, single-channel R, mipmaps on, ASTC 8Ã—8 â€” ~0.35 MB.
- All: **Streaming Mipmaps on**, so the top mip loads with the world package and periphery uses cheap high mips.

---

## 4. Estimated per-frame cost + draw/tri, with justification

**Draw calls: +0. Triangles: +0.** It is a `shader_feature` variant of the *same* terrain material on the *same* terrain draw in the *same* Forward pass. Nothing new is submitted. This is the feature's biggest budget virtue and why it fits the 400â€“600 call / 1.3â€“1.8 M tri envelope with no movement.

**GPU fragment cost (the only cost):** within the fade disc, +3 compressed bilinear fetches (albedo RGBA, normal RG, macro R) + ~20 ALU (distance, smoothstep, RNM blend, 2 lerps, unpack). Outside the disc: **0** (coherent `UNITY_BRANCH` skip).

Justification of the ms figure on Snapdragon XR2+ Gen 2 (Adreno 740), per-eye 1856Ã—2160Ã—2 = 8.0 MP raw, ETFR saving ~30% â†’ ~5.6 MP effective shaded:
- At standing eye height the â‰¤22 m regolith band typically fills ~35â€“45% of shaded pixels â†’ ~2.2â€“2.5 M fragments run the detail path.
- 3 ASTC bilinear fetches over that area, cache-friendly (world-XZ UVs are spatially coherent, mips resolve minification) â‰ˆ **0.35â€“0.5 ms typical** â€” consistent with the research doc's 0.3â€“0.5 ms budget for this exact technique.
- **Worst case â€” player pitches to look straight down at the regolith to inspect it** (precisely the moment the feature earns its keep): the disc fills ~80% of the FOV â†’ ~0.8â€“1.1 ms, and that frame likely also carries nearby POI props.
- VRAM: ~3.2 MB (one profile's albedo+normal) + 0.35 MB shared macro â‰ˆ **~3.5 MB resident** for the loaded world (world packages are additive, so only Mars *or* Moon is in memory). Negligible.
- Shader variants: **+1** ForwardLit variant (prewarmed). No variant explosion because the feature is `shader_feature_local_fragment` and lives only in the Forward pass.

All ms numbers are desk estimates â€” flag `[unverified until on-device]`; per CLAUDE.md only the adb build is framerate evidence.

---

## 5. `TerrainImporter.cs` changes + meta.json (worlds-as-data)

Add to each `assets/terrain/<site>/meta.json`:
```json
"regolithProfile": "mars_basalt",       // or "moon_anorthosite"
"detailTileMeters": 0.75,
"macroTileMeters": 11.0,
"detailFadeStart": 6.0,
"detailFadeEnd": 22.0,
"detailStrength": 1.0
```
In `WireIntoScene` (currently at `unity/Assets/Wayfinder/Editor/TerrainImporter.cs:113â€“126`): replace `Shader.Find("Universal Render Pipeline/Terrain/Lit")` with `Shader.Find("Wayfinder/Terrain/RegolithLit")`; then read the fields above and:
```csharp
terrainMat.EnableKeyword("_REGOLITH_DETAIL");
terrainMat.SetTexture("_DetailAlbedo", LoadRegolith(profile, "albedo"));
terrainMat.SetTexture("_DetailNormal", LoadRegolith(profile, "normal"));
terrainMat.SetTexture("_MacroNoise",  LoadMacro());
terrainMat.SetFloat("_DetailTileMeters", detailTileMeters);
terrainMat.SetFloat("_MacroTileMeters",  macroTileMeters);
terrainMat.SetFloat("_FadeStart", detailFadeStart);
terrainMat.SetFloat("_FadeEnd",   detailFadeEnd);
terrainMat.SetFloat("_DetailStrength", detailStrength);
```
The single basemap `terrainLayers` array is untouched (`GetOrCreateBaseLayer`, line 157) â€” still 1 layer. This keeps the whole feature re-runnable and data-driven; a new world only adds meta.json fields. Add an EditMode contract test alongside `TerrainImportContractTests.cs` asserting the material ends with keyword on, `terrainLayers.Length == 1`, and fade fields matching meta.json.

Add a runtime `MonoBehaviour` hook on the perf/quality controller exposing `_DetailStrength` and `_FadeEnd` via `Material.SetFloat` (MaterialPropertyBlock not needed â€” one shared terrain material), so the thermal governor can dial detail down without a rebuild.

---

## 6. Graceful degradation (ordered, each cheaper; all runtime except the last)

1. **Pull `_FadeEnd` inward** (22 â†’ 14 â†’ 10 m). Cheapest, biggest lever: directly shrinks the fragment count running the detail path (the disc). Runtime `SetFloat`.
2. **Drop the macro sample** (set `_MacroContrast`=0 / a `_NO_MACRO` static branch): âˆ’1 fetch, minor tiling return; or substitute a 6-ALU procedural 2-sin macro (trades bandwidth for ALU when the device is bandwidth-bound).
3. **Drop the detail normal** (albedo-only): âˆ’1 fetch and the RNM/unpack ALU. Still adds visible crunch under the single hard sun via albedo; loses micro-relief. Runtime keyword `_DETAIL_ALBEDO_ONLY`.
4. **Global `_DetailStrength` ramp** by thermal state (governor lowers it as the device heats), fading the effect out continuously instead of popping.
5. **Kill switch:** disable `_REGOLITH_DETAIL` â†’ the material is byte-identical to today's stock URP Terrain/Lit single-layer render, **zero cost, zero restructuring** (because detail is not a layer, there is nothing to unwire). This is the safety net if it fails the fps gate outright.

Wire tiers 1/3/4 into the existing `android-xr-perf` quality buckets so the frame-budget report can trip them automatically.

---

## 7. The single riskiest unknown

**Whether the on-device fragment cost holds â‰¤0.5 ms in the exact framing the feature exists to serve â€” player pitched down, inspecting regolith, disc filling ~80% of the FOV â€” under sustained thermal throttle.** Wayfinder is unique among the budgeted techniques in that the terrain is a floor-to-horizon surface: when the user kneels to look at the ground (the whole point of near-field detail), the 3-fetch detail path stops being "40% of screen" and approaches a full-screen 3-tap cost (~1 ms), landing on the *same* frame that carries nearby human-scale POI props â€” right when headroom is thinnest and the Adreno is bandwidth-bound and hot. Every other risk here has a known, cheap fallback (fork drift â†’ 3-file diff; tiling â†’ macro noise; layer budget â†’ solved by design). This one is only answerable on the headset with the Profiler, and it is precisely the fps-gate scenario. Mitigation is pre-built into degradation tiers 1â€“4, but the *measurement* â€” capture a device trace with the camera pitched fully down over each site's regolith, not just the standing-horizon view â€” is the gating unknown to resolve first.

---

**Files touched:** new `unity/Assets/Wayfinder/Shaders/Terrain/WayfinderRegolithLit.shader` + `WayfinderRegolithLitInput.hlsl` + `WayfinderRegolithLitPasses.hlsl` (forked from `unity/Library/PackageCache/com.unity.render-pipelines.universal@e38be786c41e/Shaders/Terrain/`); new `tools/gen_regolith.py`; new `unity/Assets/Wayfinder/Terrain/Regolith/*.png`; edit `unity/Assets/Wayfinder/Editor/TerrainImporter.cs`; edit each `unity/Assets/Wayfinder/Materials/Terrain_<site>.mat` (via importer only); add fields to `assets/terrain/<site>/meta.json`; new EditMode test beside `unity/Assets/Wayfinder/Tests/Editor/TerrainImportContractTests.cs`.

---

## agent

I have everything I need. Here is the complete implementation spec.

---

# Wayfinder #18 â€” Instanced Rock/Boulder Scatter System â€” Buildable Spec

**Stack confirmed against the repo:** Unity 6000.5.4f1 / URP 17.5 / Vulkan / Android XR. Runtime asmdef `Wayfinder.Unity` (namespace `Wayfinder.Unity`, refs `Wayfinder.Core`, XRI, XR.CoreUtils). Editor asmdef `Wayfinder.Unity.Editor` (namespace `Wayfinder.Unity.EditorTools`, Editor-only). EditMode tests in `Wayfinder.Unity.EditorTests`. Sites: `mars-olympus` (20Ã—20 km, basalt blocks), `mars-valles` (3.6Ã—19.5 km HiRISE strip, Coprates wall-base talus â€” grid is anisotropic ~1.76 m across / ~9.52 m along-track, rotated ~5Â° W of N), `moon-shackleton` (20Ã—20 km, rim ejecta, spawn on rim via `spawnOffset`). Site scenes `Assets/Scenes/Site_<id>.unity`; terrain wired by `TerrainImporter` as root object `SiteTerrain` (static, shadows off, `drawInstanced=true`). All three currently hold 72 fps / 0 XR drops â€” scatter is *additive presence*, it must never be able to fail the gate.

This feature is research technique #9. Its budget line is "~0.5â€“1 ms" and its named risk is "RenderDoc-verify instancing survives the Vulkan build (documented silent-failure pitfall)." The spec below treats that verification as the gating first step, not a final check.

---

## 0. Architecture: bake-everything, render-instanced (Red Matter doctrine)

Three pieces, mirroring the terrain pipeline's split (offline authoring â†’ baked asset â†’ thin runtime):

```
Authoring (Editor only)                     Baked assets                         Runtime (world scene)
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€                    â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€                       â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
RockMeshGenerator  â”€â”                        ScatterArchetypeSet_<site>.asset  â”€â”
(procedural meshes) â”‚â”€â”€â–¶ archetype meshes â”€â”€â–¶  (6 archetypes Ã— 3 LOD meshes,    â”‚
                    â”‚                            1 shared material)             â”‚â”€â”€â–¶ ScatterRenderer
ScatterBaker       â”€â”˜                        ScatterFieldData_<site>.asset     â”€â”˜   (MonoBehaviour in
(reads TerrainData   â”€â”€â–¶ placement bake  â”€â”€â–¶   (cell grid + per-instance            Site_<id> scene)
 slope/height)                                  xform/archetype/LOD/tint)            â””â”€ Graphics.RenderMeshInstanced
```

**Why fully-baked instanced draw over the three alternatives:**
- **Not Unity terrain trees** â€” their draw path is opaque (can't cleanly attribute one instanced call in RenderDoc, which is this ticket's explicit acceptance test), and their LODâ†’billboard crossfade is *alpha* based, which violates the ticket's "dithered cross-fade, no alpha overdraw."
- **Not baked GameObject prefabs + LODGroup + GPU-Instancing checkbox** â€” on URP the **SRP Batcher takes priority and silently suppresses `DrawMeshInstanced`-style instancing** unless you disable SRP batching for that shader; hundreds of transforms/LODGroups also add culling and per-renderer overhead. This is the classic "instancing silently didn't happen" trap â€” exactly the pitfall the ticket calls out.
- **`Graphics.RenderMeshInstanced`** gives one instanced draw per (archetype, LOD), zero GameObjects, a guaranteed instancing code path we control, and trivial hard-cap enforcement. It is the modern (Unity 2022+/6) replacement for `DrawMeshInstanced`.

**Files to add** (each needs a committed `.meta`; none touch existing `.unity`/`.meta` by hand):

| Path | Asmdef | Role |
|---|---|---|
| `Assets/Wayfinder/Scripts/Scatter/ScatterArchetype.cs` | Wayfinder.Unity | `[Serializable]` archetype: `Mesh[] lods`, placement rules, size distribution |
| `Assets/Wayfinder/Scripts/Scatter/ScatterArchetypeSet.cs` | Wayfinder.Unity | `ScriptableObject`: per-site archetype list + shared `Material` + LOD distances |
| `Assets/Wayfinder/Scripts/Scatter/ScatterFieldData.cs` | Wayfinder.Unity | `ScriptableObject`: baked cell grid + flat instance arrays (blittable) |
| `Assets/Wayfinder/Scripts/Scatter/ScatterRenderer.cs` | Wayfinder.Unity | MonoBehaviour: cull + LOD + `RenderMeshInstanced` each frame |
| `Assets/Wayfinder/Scripts/Scatter/ScatterPlacement.cs` | Wayfinder.Unity | **Pure** slope/height/cap math (no UnityEditor) â€” so it is EditMode-testable |
| `Assets/Wayfinder/Editor/RockMeshGenerator.cs` | Wayfinder.Unity.Editor | Procedural rock mesh + LOD builder |
| `Assets/Wayfinder/Editor/ScatterBaker.cs` | Wayfinder.Unity.Editor | Menu tool: read TerrainData, run `ScatterPlacement`, write `ScatterFieldData` |
| `Assets/Wayfinder/Shaders/RockInstanced.shader` | â€” | URP HLSL, instanced, dithered crossfade |
| `Assets/Wayfinder/Tests/Editor/ScatterPlacementTests.cs` | Wayfinder.Unity.EditorTests | slope filter, cap enforcement, determinism |

Put the placement *math* in the runtime asmdef (`ScatterPlacement.cs`, no `UnityEditor` references) so EditMode tests can call it headless â€” same reason `TravelStateMachine` lives in `Wayfinder.Core`. The `ScatterBaker` editor tool is only glue that feeds it `TerrainData`.

---

## 1. Exact build steps

### A. Generate rock archetype meshes (procedural, no paid accounts)

`RockMeshGenerator` builds each LOD **by re-evaluating one deterministic displacement field on progressively coarser base meshes** â€” this gives true "same rock, fewer tris" LODs with **no mesh-decimation dependency at all** (avoids needing UnityMeshSimplifier/meshopt). Algorithm:

1. **Base solid per geology** (icosphere via subdivided icosahedron, or a cube for blocks):
   - *Basalt blocks (Olympus):* start from a **cube**, 2 subdivisions; displacement = low-octave value noise + 3â€“4 random **planar chamfer cuts** â†’ angular, columnar-jointing feel.
   - *Talus shards (Valles):* **flattened icosphere** (scale Y Ã—0.55), sharp mid-frequency noise â†’ broken angular plates.
   - *Ejecta boulders (Shackleton):* **icosphere**, rounded low-frequency noise, one variant with a clipped flat base (half-buried).
2. **Displace**: `v += n * (amp * fbm(v*freq, seed))` where `fbm` is 3 octaves of gradient noise seeded by `hash(siteId, archetypeIndex, variant)`. Deterministic.
3. **LODs from base subdivision level, same seed/field**: LOD0 = subdiv 3 (~380 tris), LOD1 = subdiv 2 (~130 tris), LOD2 = subdiv 1 (~46 tris). Because the displacement function is identical, silhouettes match across LODs â†’ minimal pop.
4. **Flat (faceted) normals** at every LOD â€” angular rock reads correctly faceted, halves nothing but lets us skip a normal-map sample. `RecalculateNormals` after splitting shared verts, or generate per-triangle.
5. **Bake AO into vertex color** (`Color.a` or `.rgb` grey): cheap sky-occlusion approximation from the displacement (crevices darker). Replaces an AO texture sample.
6. Author **6 archetypes per site** Ã— 3 LODs = 18 tiny meshes, saved with `AssetDatabase.CreateAsset` into `Assets/Wayfinder/Scatter/Meshes/<site>/`. Menu: `Wayfinder/Scatter/Generate Rock Meshes/<site>`.

Store bounds + a `colliderRadius` on each archetype for placement spacing and optional proximity colliders.

### B. Placement bake (editor tool reading terrain slope)

Menu `Wayfinder/Scatter/Bake Scatter/<site>` â†’ `ScatterBaker.Bake(siteId)`:

1. Load `TerrainData` from `Assets/Wayfinder/Terrain/<site>.asset`; load terrain world offset from `Site_<id>` scene (same `-width/2, -length/2, -centerSurface` transform the importer applied â€” read it off the `SiteTerrain` object, don't recompute).
2. Load POI positions from `Assets/Wayfinder/POI/<site>.json` and the `WorldPackage.spawnOffset` â†’ **exclusion discs** (player must not spawn or land inside rocks; keep a 3 m clear radius around spawn and each POI).
3. **Candidate generation** â€” jittered-grid (or Bridson Poisson-disk) over terrain UV; per candidate compute, in **world space** (critical for `mars-valles` because its grid cells are anisotropic â€” never use grid-space slope):
   - `steepnessDeg = terrainData.GetSteepness(u, v)` (0â€“90, already world-corrected by Unity),
   - `worldHeight = terrainData.GetInterpolatedHeight(u, v) + terrainOffset.y`,
   - `normal = terrainData.GetInterpolatedNormal(u, v)` (for tilt-align + fall-line),
   - **wall-base detection** (talus): sample steepness at 4 uphill offsets within Râ‰ˆ15 m; flag candidate if any neighbor `> 32Â°` while the candidate itself `< 20Â°` (i.e. base of a cliff). This is the Coprates apron rule.
4. **Per-archetype placement rules** (data on `ScatterArchetype`, evaluated in `ScatterPlacement.Accept()`):

   | Site | Archetypes | Slope accept | Height band | Density rule |
   |---|---|---|---|---|
   | Olympus | blocky 0â€“3 | 0â€“25Â° | broad | medium, uniform on rim benches; larger near channel lows |
   | Valles | shards 0â€“3 | 0â€“30Â°, **prefer wall-base flag** | apron below rim | high at apron, falls off with distance-from-wall; long axis along fall-line |
   | Shackleton | boulders 0â€“3 | 0â€“28Â° (avoid slide slopes >35Â°) | rim + floor, sparse | low; cluster weakly (ejecta rays); some half-buried |

5. **Transform per accepted instance**: yaw random 0â€“360Â°; **tilt = align up to a blend of world-up and terrain normal** (70/30, so rocks sit but don't perfectly conform); uniform `scale` from a **log-normal** distribution per archetype (few big, many small â€” natural size-frequency); embed depth (push down 15â€“30 % of radius so it sits *in* the regolith, not on it â€” also hides the LOD2 base).
6. **Assign LOD-independent data** and a **coarse cell id** (uniform grid, cell = 16 m XZ). Sort instances by cell for cache-coherent runtime iteration.
7. **Hard cap enforcement** (`ScatterPlacement.EnforceCap`): if accepted > site cap, drop lowest-priority first where `priority = scale Ã— nearestFeatureBias` (keep big rocks and rocks near POIs/spawn; cull small far ones). Cap is *guaranteed* regardless of terrain noise. Per-site caps: Olympus 1200, Valles 2500, Shackleton 800 (baked totals; runtime visible-cull keeps far fewer on screen).
8. **Write `ScatterFieldData_<site>.asset`**: flat blittable arrays (`Matrix4x4[] transforms` or SoA `float3[] pos, quaternion[] rot, float[] scale`), `byte[] archetypeIndex`, `byte[] tintIndex`, plus `int[] cellStart/cellCount` + grid metadata. Seeded RNG (`hash(siteId)`) â†’ re-bakes are stable (referencers never churn).

**Determinism + the anisotropy trap are the two EditMode tests that matter** (`ScatterPlacementTests`): (a) same seed â†’ identical output; (b) cap is never exceeded on adversarial all-flat terrain; (c) a synthetic cliff produces talus only at its base, proving world-space slope not grid-space.

### C. Runtime wire-up

1. Add a `ScatterRenderer` component to the `Site_<id>` scene root (sibling of `SiteTerrain`), referencing the site's `ScatterFieldData` + `ScatterArchetypeSet`. Because it lives in the additively-loaded world scene, it loads/unloads with the world â€” **no lifecycle code**, consistent with the World-Package doctrine. On scene unload the component dies and `RenderMeshInstanced` stops â†’ rocks vanish under the warp fade.
2. `Awake`: null-check both serialized refs and **fail loudly** (repo convention). Cache: per-(archetype,LOD) preallocated `Matrix4x4[]` scratch buckets sized to the runtime visible cap, one shared `RenderParams` per archetype with `shadowCastingMode=Off`, `receiveShadows=false`, `renderingLayerMask` for the surface, `worldBounds` = terrain bounds, `motionVectorMode=Camera` off.
3. `LateUpdate` (after XR camera pose resolves): 
   - Compute camera XZ; **frustum-cull at cell granularity** (a few hundred cells, cheap) plus a hard **scatter radius** (boulders ~110 m, talus ~45 m â€” far rocks are sub-pixel over interpolated DEM);
   - **LOD band per instance** from camera distance (LOD0 <25 m, LOD1 25â€“70 m, LOD2 70 mâ€“radius; tunable on the archetype set);
   - append visible instance matrices into the matching `_bucket[archetype,LOD]`, and its per-instance fade+tint;
   - one `Graphics.RenderMeshInstanced` per non-empty (archetype, LOD).

Sketch (allocation-free hot path â€” buckets preallocated, no LINQ, no `GetComponent`):

```csharp
void LateUpdate() {
    Vector3 cam = _camTransform.position;
    GeometryUtility.CalculateFrustumPlanes(_cam, _planes);          // _planes cached float array
    for (int a = 0; a < _archCount; a++) for (int l = 0; l < 3; l++) _count[a,l] = 0;

    for (int c = 0; c < _cellCount; c++) {
        if (!CellVisible(c, _planes, cam)) continue;                // frustum + radius
        int end = _cellStart[c] + _cellLen[c];
        for (int i = _cellStart[c]; i < end; i++) {
            int a = _arch[i];
            float d = FastDistXZ(cam, _pos[i]);
            if (d > _radius[a]) continue;
            int lod = d < _lod0[a] ? 0 : d < _lod1[a] ? 1 : 2;
            int n = _count[a,lod]++;
            _mtx[a,lod][n] = _xform[i];                              // prebaked TRS, just copy
            _inst[a,lod][n] = new InstData { fade = FadeAt(d, a, lod), tint = _tint[i] };
        }
    }
    for (int a = 0; a < _archCount; a++)
      for (int l = 0; l < 3; l++)
        if (_count[a,l] > 0)
            Graphics.RenderMeshInstanced(_rp[a,l], _mesh[a,l], 0, _inst[a,l], _count[a,l]);
}
```

Per-instance `InstData{ float fade; uint tint; }` rides the generic `RenderMeshInstanced` overload; the shader reads them as instanced properties (see Â§2). **Flag [verify]:** the exact instanced-struct field ordering for the generic overload in 6000.5 must be confirmed via Context7 (`Graphics.RenderMeshInstanced` + `unity_ObjectToWorld` instanced buffer) â€” this is bundled into the RenderDoc verify step (Â§1E, Â§6). If per-instance data misbehaves on Vulkan, fall back to the **matrices-only** overload + **quantized per-batch fade** (4 fade buckets as extra draw calls only in the transition band) â€” same visual, no per-instance struct.

4. **Ambient/lighting**: rocks receive the single site sun (Main Light) + one global SH ambient matching the sky (Mars butterscotch fill, Moon earthshine, per research #4). No lightmaps (instanced meshes can't be uniquely lightmapped) â€” vertex-AO carries occlusion.
5. **Colliders**: none per instance (would nuke the budget). Terrain collider remains the walkable surface; teleport rays already target only the terrain interaction layer (bit 31), so rocks never intercept teleport. *Optional polish:* a pool of ~8 `SphereCollider`s snapped to the nearest large boulders on teleport (not per frame). Ship without.

### D. Project / URP / build settings

1. **Material**: create `Assets/Wayfinder/Scatter/RockInstanced.mat` from `RockInstanced.shader`. Confirm **Enable GPU Instancing** semantics (the shader declares `#pragma multi_compile_instancing`).
2. **SRP Batcher interaction**: `RenderMeshInstanced` bypasses SRP Batcher and instances correctly â€” but confirm the rock shader is *not* silently SRP-batched instead (it won't be, via the instanced draw path). Do **not** rely on the material-inspector "GPU Instancing" checkbox path (that one loses to SRP Batcher).
3. **Foveated rendering**: already required project-wide (Project Settings â†’ XR Plug-in Management â†’ OpenXR â†’ Foveated Rendering). Rocks in the periphery get the fragment discount for free â€” no per-feature setup.
4. **Shader prewarm** (Red Matter doctrine, avoids first-warp hitch): add `RockInstanced` variants (`_ + LOD_FADE_CROSSFADE`, instancing on, single-pass instanced stereo) to the boot `ShaderVariantCollection` and warm it at app start alongside the existing prewarm.
5. **Single-Pass Instanced stereo**: confirm OpenXR render mode is Single Pass Instanced (project default). Our instancing must coexist â€” verified in E.

### E. RenderDoc verification on the Vulkan device build (the acceptance gate)

This is not optional and not editor/Direct-Preview (PC GPU renders both â€” never instancing evidence for the device). On a **development Vulkan APK on the Galaxy XR**:

1. Capture a frame while standing in the Valles talus apron (densest scatter).
2. In the frame's draw list, find the rock draws. **Pass = one `vkCmdDrawIndexed`/`vkCmdDrawIndexedIndirect` per (archetype, LOD)** with **`instanceCount = visibleCount Ã— 2`** (the Ã—2 is the stereo eye baked in by Single-Pass Instanced). **Fail = one draw per rock** (silent fallback â€” the documented pitfall) or instanceCount == visibleCount (stereo instancing lost).
3. Confirm the pipeline bound is the instanced variant and that `unity_ObjectToWorld` + our `_Fade`/`_Tint` arrive as an instanced constant/SSBO, not per-draw uniforms.
4. Record the draw count and GPU time for the rock passes into `docs/perf/` next to the Site-One gate report.

---

## 2. Shader â€” `RockInstanced.shader` (URP HLSL, concrete)

Hand-written (not Shader Graph) because per-instance `_Fade`/`_Tint` via `RenderMeshInstanced` need a real instancing cbuffer, which Shader Graph won't expose cleanly. Minimal ForwardLit + DepthOnly + DepthNormals passes. Fragment cost: one directional light, no additional lights, no shadow sampling, flat normals, optional single tiling detail sample.

Key fragments:

```hlsl
Shader "Wayfinder/RockInstanced" {
Properties { _BaseMap("Detail", 2D)="grey"{} _BaseColor("Tint",Color)=(0.5,0.4,0.35,1) }
SubShader {
  Tags{ "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
  Pass {
    Name "ForwardLit"  Tags{ "LightMode"="UniversalForward" }
    Cull Back  ZWrite On
    HLSLPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #pragma multi_compile_instancing                 // â† instancing
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_OFF
    #pragma multi_compile _ LOD_FADE_CROSSFADE       // dithered crossfade keyword
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    UNITY_INSTANCING_BUFFER_START(Props)
      UNITY_DEFINE_INSTANCED_PROP(float,  _Fade)     // 0..1 crossfade, +/- selects in/out
      UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)     // per-instance geology tint
    UNITY_INSTANCING_BUFFER_END(Props)

    struct A { float4 pos:POSITION; float3 n:NORMAL; float4 col:COLOR;
               UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct V { float4 pos:SV_POSITION; float3 nWS:TEXCOORD0; float3 ao:TEXCOORD1;
               float4 scr:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

    V vert(A i){ V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i,o);
      float3 wp = TransformObjectToWorld(i.pos.xyz);
      o.pos = TransformWorldToHClip(wp);
      o.nWS = TransformObjectToWorldNormal(i.n);
      o.ao  = i.col.rgb;                 // baked vertex AO
      o.scr = ComputeScreenPos(o.pos);  return o; }

    // Bayer 4x4 dither replicating URP LOD crossfade without unity_LODFade
    void DitherClip(float2 sc, float fade){
      const float m[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
      int idx = (int(sc.x)&3)*4 + (int(sc.y)&3);
      float th = (m[idx]+0.5)/16.0;
      clip(fade>=0 ? (fade-th) : (th+fade));   // sign = fade in vs out
    }

    half4 frag(V i):SV_Target{ UNITY_SETUP_INSTANCE_ID(i);
      float fade = UNITY_ACCESS_INSTANCED_PROP(Props,_Fade);
      DitherClip(i.pos.xy, fade);              // no alpha, just clip â†’ zero overdraw beyond dither
      float3 tint = UNITY_ACCESS_INSTANCED_PROP(Props,_Tint).rgb;
      Light L = GetMainLight();
      float ndl = saturate(dot(normalize(i.nWS), L.direction));
      float3 amb = SampleSH(i.nWS);            // site SH (butterscotch / earthshine)
      float3 col = tint * i.ao * (L.color*ndl + amb);
      return half4(col,1);
    }
    ENDHLSL
  }
  // + DepthOnly and DepthNormals passes (same instancing pragmas + DitherClip) so depth prepass matches.
}}
```

Notes: flat normals come from the mesh (verts split at bake), so no normal-map sample at any LOD â€” cheapest fragment. Detail `_BaseMap` sample is optional and can be dropped in degradation. The `DitherClip` replaces URP's `LODFadeCrossFade(positionCS)` because `unity_LODFade` isn't set by `RenderMeshInstanced` â€” we drive the threshold from our own `_Fade`. **Shader-Graph alternative** (if the team prefers, for the per-batch-fade fallback path): URP Lit graph, Base Color = `_Tint(MPB) Ã— VertexColor Ã— SampleTexture2D(_BaseMap)`, Normal = object normal, a **Custom Function** node running the same `DitherClip` on `ScreenPosition` + a global `_Fade` float, output to Alpha Clip Threshold â€” but only the per-batch-fade variant (no per-instance data).

---

## 3. Asset generation (procedural / free+credited)

- **Meshes: 100 % procedural**, generated in-editor by `RockMeshGenerator` (Â§1A). No marketplace, no AI-generator, no account. Deterministic, regenerable, versionable by seed. This also dodges the `android-xr-asset-budget` import gate entirely â€” nothing is *imported*, it's *generated* under our own tri budget.
- **Detail albedo (optional):** procedurally bake one 256Â² tiling greyscale value-noise texture in-editor (zero attribution), or use a **CC0** rock albedo from **ambientCG** / **Poly Haven** (free, public-domain â€” credit in `docs/` as courtesy though CC0 requires none). Prefer procedural. One shared 256Â² for all archetypes/sites; can be dropped in degradation.
- **No textures at all** is a valid ship state: flat-shaded + vertex-AO + per-instance tint reads as convincing distant/mid rock on a mobile XR display. Recommended default for LOD1/LOD2.
- Tint palettes come from each site's `meta.json baseColor` family (Olympus `#975C3D`, Valles `#8F5A3B`, Shackleton `#6B6B73`) with small per-instance hue/value jitter â€” so rocks belong to their real geology by construction, sourced from data not magic numbers.

---

## 4. Budget: per-frame ms, draw calls, tris (with justification)

**LOD tri counts:** LOD0 380, LOD1 130, LOD2 46.

**Worst case = Valles talus apron**, runtime visible cap **1300** (global, tunable). LOD distribution there:

| LOD | Visible | Tris/eye | Justification |
|---|---|---|---|
| LOD0 (<25 m) | 150 | 57k | near hero rocks, hand-scale detail |
| LOD1 (25â€“70 m) | 550 | 71.5k | mid apron |
| LOD2 (70â€“110 m) | 600 | 27.6k | far apron, sub-pixel-approaching |
| **Total** | **1300** | **â‰ˆ156k / eye** | **â‰ˆ312k stereo** |

- **Tri cost:** ~156k/eye, ~312k stereo. Against the 1.3â€“1.8M medium-sim envelope with terrain at ~200â€“350k (heightmapPixelError 8 on 2049) + sky + ship UI, this fits with margin. Hard tri ceiling set to **â‰¤160k/eye** â€” the runtime cap is derived from it, not the other way around.
- **Draw calls:** 6 archetypes Ã— up to 3 active LODs = **â‰¤18**, typically **10â€“14** (only ~2 LOD bands active in a given view) + â‰¤~12 transition-band sub-batches â†’ **~12â€“26 draws**. Against the 400â€“600 envelope this feature spends **~20 draws**. RenderDoc pass criterion: each is one instanced `vkCmdDrawIndexed`, instanceCount = visibleÃ—2.
- **CPU (main thread):** cell frustum-cull (~few hundred cells) + append â‰¤1300 prebaked matrices into buckets = memcpy-dominated, **~0.2â€“0.35 ms** on Snapdragon XR2+ Gen2. No allocations, no `GetComponent`, no per-instance TRS build (baked).
- **GPU:** vertex ~0.2â€“0.35 ms (156k simple verts, flat normals); fragment ~0.3â€“0.55 ms (opaque, one light, no shadow sample, dither-clip; foveation discounts periphery; near-camera overdraw is the main variable). **GPU total ~0.6â€“0.9 ms.**
- **Feature total â‰ˆ 0.8â€“1.2 ms/frame** â€” squarely on the research's "~0.5â€“1 ms" line, with CPU broken out so it's visible in the frame-budget report.

Sparse sites (Olympus visible ~800, Shackleton ~500) land at ~0.4â€“0.7 ms.

---

## 5. Graceful degradation (ordered levers, cheapest first â€” each is a live scalar, no rebuild)

The whole system is gated by scalars on `ScatterArchetypeSet` / a per-site `scatterBudgetScale` on `WorldPackage`, so a heavy site runs leaner than a sparse one and the whole feature can be dialed to zero:

1. **Lower runtime visible cap** 1300 â†’ 1000 â†’ 600 (single field). Linear ms saving, no pop (culls farthest-smallest first).
2. **Shrink scatter radius** (110 m â†’ 70 m boulders; 45 â†’ 30 talus). Removes far LOD2 mass â€” biggest tri saving for least visual loss.
3. **Push LOD distances nearer** (more LOD2, fewer LOD0). Cuts tris and near-camera overdraw.
4. **Drop LOD0** on Mars sites (start at LOD1) if fragment-bound â€” hand-scale detail is only felt within a couple metres anyway.
5. **Disable the detail `_BaseMap` sample** â†’ flat-shaded + vertex-AO only. Removes a texture fetch per fragment.
6. **Kill dithered crossfade â†’ hard LOD switch** if CPU/overdraw-bound. Removes the transition-band double-draw and the `DitherClip`. Popping on small rocks is barely perceptible.
7. **Per-site `scatterBudgetScale`** â€” Valles apron can run at 0.6 while Olympus runs at 1.0.
8. **Last resort: disable `ScatterRenderer` entirely** via a quality flag. The site still ships â€” terrain, POIs, sky, audio are untouched. **Scatter is additive presence; losing it can never fail the 72 fps gate or break a world.** This is the ultimate guarantee that #18 cannot regress the shipped baseline.

---

## 6. The single riskiest unknown

**Does GPU instancing survive the Vulkan device build on top of Single-Pass Instanced stereo â€” rendering as true instanced draws (`instanceCount = visible Ã— 2`) rather than silently falling back to one draw per rock or losing the stereo multiplier?**

Single-Pass Instanced stereo already consumes the instancing mechanism to pack both eyes into instance IDs. Layering `RenderMeshInstanced` *plus* a per-instance data struct (`_Fade`/`_Tint`) on top of that is precisely the combination the research flagged as a documented silent-failure pitfall â€” and it fails *invisibly*: the scene looks identical in the editor and in Direct Preview (both rendered by the PC's RTX 5070 Ti, which brute-forces the fallback), and only tanks on the mobile Snapdragon GPU where the per-draw overhead of 1300 individual draws is catastrophic.

**Mitigation â€” do this before building the placement/archetype pipeline:** stand up a throwaway 20-line prototype that `RenderMeshInstanced`s ~1000 identical cubes with per-instance `_Tint`, build the Vulkan dev APK, and RenderDoc-capture on the Galaxy XR to confirm one instanced `vkCmdDrawIndexed` with `instanceCount = 2000`. Only if that passes do we build the real system on this draw path. If it fails, the pre-decided fallback is matrices-only `RenderMeshInstanced` (no per-instance struct) with per-batch quantized fade â€” proven simpler and closer to the vanilla instancing path â€” before ever considering abandoning instancing for terrain-tree rendering.

Everything else in this spec is conventional Unity work; this one interaction is the only thing that can quietly cost the frame budget, and it is cheap to de-risk first.

---

## agent

I have everything I need: the terrain is **stock URP TerrainLit (guid `69c1f799â€¦`, 1 layer, `_EnableInstancedPerPixelNormal:1`)**, the skies are **baked 6-sided cubemaps** (`MarsSky.mat`/`StarSky.mat` on builtin `Skybox/6 Sided`, gradient frozen into the face textures), `EarthDisc` is the only custom-shader material, and there are **zero custom shaders in `Assets/Wayfinder/`**. URP is `com.unity.render-pipelines.universal@e38be786c41e` (17.5). I've read the exact `TerrainLitPasses.hlsl` injection points. Here is the spec.

---

# Wayfinder #18 â€” Per-Site Atmospherics: Mars height/distance haze + Moon opposition surge

## 0. Design decision that drives everything

Your terrain is **stock URP `TerrainLit`** and your skies are **baked 6-sided cubemaps** â€” the Mars butterscotch gradient is *already frozen into `MarsSky`'s six face textures* (#15). So "fog in terrain+sky shaders" splits cleanly:

- **Sky side:** nothing runs at draw time. The cubemap **is** the far-field fog color at infinity. URP's skybox pass receives no `MixFog` (correct â€” you never want to fog the sky). Our job is only to make the terrain **dissolve into the exact color the cubemap already shows at the horizon.**
- **Terrain side:** a per-vertex analytic height+distance fog + a per-pixel opposition surge, injected into a **thin fork of `TerrainLit`**. Stock `TerrainLit` gives you distance-only linear/exp fog for free (`ComputeFogFactor`â†’`MixFog`, `TerrainLitPasses.hlsl:353,266`) but **no height term and no surge**, which is exactly the two things the feature is about. Hence the fork â€” 3 edits, no logic rewrite.

Everything below is **0 draw calls, 0 tris, ~0.1 ms, no depth read, no extra pass.** That is the whole reason to do it in-shader per-vertex instead of a fullscreen/volumetric fog Renderer Feature (which would fog the sky wrong, add a pass, and read depth).

---

## 1. Exact build steps (a Unity engineer follows in order)

**Step A â€” data on the World Package.** Add these fields to `unity/Assets/Wayfinder/Scripts/WorldPackage.cs` (all `[SerializeField] private`, per CLAUDE.md), with a public getter block:

```csharp
[Header("Atmospherics (#18) â€” real per-site, data not magic numbers")]
[Tooltip("Master: Mars=1 (haze on), Moon=0 (vacuum, fog off).")]
[SerializeField] private bool hazeEnabled = false;
[Tooltip("Linear-space horizon color the terrain dissolves into. MUST equal the baked sky cubemap's horizon band. Mars ~ (0.68,0.49,0.36).")]
[SerializeField] private Color hazeColor = new Color(0.68f, 0.49f, 0.36f, 1f);
[Tooltip("Beerâ€“Lambert distance density, 1/m. Mars dust ~1/2500 = 0.0004.")]
[SerializeField] private float hazeDistanceDensity = 0.0004f;
[Tooltip("Height falloff, 1/m. Larger = haze hugs the ground harder. ~0.0015.")]
[SerializeField] private float hazeHeightFalloff = 0.0015f;
[Tooltip("World-space Y (m) of the haze datum (valley floor). From the DEM meta.json.")]
[SerializeField] private float hazeGroundY = 0f;
[Tooltip("0=pure distance fog, 1=fully height-stratified. Mars ~0.85.")]
[Range(0,1)][SerializeField] private float hazeHeightStrength = 0.85f;

[Header("Regolith opposition surge (#18) â€” Moon has it, Mars ~0")]
[Tooltip("Peak extra brightness at the anti-solar point. Moon ~0.6, Mars ~0.1.")]
[SerializeField] private float surgeStrength = 0f;
[Tooltip("Angular tightness of the hotspot. n=64 â†’ ~8Â° HWHM. Moon ~48.")]
[SerializeField] private float surgeSharpness = 48f;
[Tooltip("Optional warm tint of the surge (regolith goes slightly brighter-warm). Default white = pure brightness.")]
[SerializeField] private Color surgeTint = Color.white;
```
Author values on the three existing assets (`Sites/mars-olympus.asset`, `mars-valles.asset`, `moon-shackleton.asset`). `hazeGroundY` comes straight from each DEM's `meta.json` min-elevation Ã— your metric scale â€” the same number the `dem-to-terrain` skill already produces.

**Step B â€” the pusher component.** Create `unity/Assets/Wayfinder/Scripts/WorldAtmospherics.cs` (a `MonoBehaviour`, in the `Wayfinder.Unity` asmdef). It reads the active `WorldPackage` and pushes **global** shader uniforms once per world-load (never per-frame). Hook it to the existing world-loaded moment â€” call `Apply(worldPackage)` from `TravelManager` right after the additive scene finishes loading, inside the warp flash (so no visible pop). Cache the `Shader.PropertyToID`s in a `static readonly int` block; the method is allocation-free:

```csharp
public void Apply(WorldPackage w) {
    Shader.SetGlobalFloat(ID_Enable,        w.HazeEnabled ? 1f : 0f);
    Shader.SetGlobalColor(ID_FogColor,      w.HazeColor);          // linear
    Shader.SetGlobalFloat(ID_Density,       w.HazeDistanceDensity);
    Shader.SetGlobalFloat(ID_HeightFalloff, w.HazeHeightFalloff);
    Shader.SetGlobalFloat(ID_GroundY,       w.HazeGroundY);
    Shader.SetGlobalFloat(ID_HeightStr,     w.HazeHeightStrength);
    Shader.SetGlobalFloat(ID_SurgeStr,      w.SurgeStrength);
    Shader.SetGlobalFloat(ID_SurgeSharp,    w.SurgeSharpness);
    Shader.SetGlobalColor(ID_SurgeTint,     w.SurgeTint);
}
```
These are `SetGlobal*`, not per-material â€” so they live **outside** `UnityPerMaterial` in HLSL (SRP-Batcher-safe) and cost nothing at draw time.

**Step C â€” the shader fork.** Copy two files out of the URP package (`unity/Library/PackageCache/com.unity.render-pipelines.universal@e38be786c41e/Shaders/Terrain/`) into `unity/Assets/Wayfinder/Shaders/Terrain/`:
- `TerrainLit.shader` â†’ rename its shader path to `"Wayfinder/TerrainLit"`.
- `TerrainLitPasses.hlsl` â†’ local copy (edited in Step D).

In `TerrainLit.shader`, repoint the one include from the package path to the local copy:
`#include ".../Terrain/TerrainLitPasses.hlsl"` â†’ `#include "TerrainLitPasses.hlsl"`, and add above it `#include "WayfinderAtmos.hlsl"`. Leave `TerrainLitInput.hlsl` pointing at the package copy â€” **do not fork it**, so the material property block is byte-identical and the material re-binds with no data loss. (Leave `TerrainLitAdd/Base/BasemapGen` on the package versions; you have 1 layer so the Add pass never runs â€” see Â§5 risk.)

**Step D â€” the three injection edits** (Â§2 gives the exact HLSL). Then create `WayfinderAtmos.hlsl` next to the fork (Â§2).

**Step E â€” rebind + scene config.**
1. Reassign each `Materials/Terrain_*.mat` shader from stock TerrainLit to `Wayfinder/TerrainLit` (Inspector, or a 2-line editor script; the GUID swap is the only material change).
2. In each world's Lighting settings, **turn scene Fog OFF** (Window â–¸ Rendering â–¸ Lighting â–¸ Environment). Our fork replaces the stock `MixFog` path; leaving scene fog on would double-fog.
3. Skybox stays exactly as-is. Moon (`StarSky`) just gets `hazeEnabled=false`, so it pays **zero** fog cost and only the surge.

**Step F â€” prewarm (Red Matter doctrine).** Add `Wayfinder/TerrainLit` to a Shader Variant Collection and `ShaderVariantCollection.WarmUp()` at boot, so the first warp to Mars doesn't hitch compiling the Vulkan variant. Add the shader to Always-Included Shaders as a belt-and-suspenders against variant stripping (Â§5).

**Step G â€” verify on device, not in editor.** Direct Preview/editor are never framerate evidence (CLAUDE.md). Build over adb, capture with the `android-xr-perf` + `frame-budget-report` skills, and RenderDoc the terrain draw to confirm the fork's GPU instancing + fog constants survived the Vulkan build.

---

## 2. Shader code (concrete)

### `WayfinderAtmos.hlsl` (new, ~40 lines)

```hlsl
#ifndef WAYFINDER_ATMOS_INCLUDED
#define WAYFINDER_ATMOS_INCLUDED
// Globals set by WorldAtmospherics.cs via Shader.SetGlobal* â€” declared OUTSIDE
// UnityPerMaterial so the SRP Batcher stays happy.
float4 _WFFogColor;
float  _WFFogEnable;
float  _WFFogDensity;        // 1/m, distance
float  _WFFogHeightFalloff;  // 1/m, altitude
float  _WFFogGroundY;        // world-space datum (m)
float  _WFFogHeightStrength; // 0..1
float4 _WFSurgeTint;
float  _WFSurgeStrength;
float  _WFSurgeSharpness;

// ---- Per-VERTEX: analytic height + distance haze factor in [0,1] ----
half WayfinderFogFactor(float3 posWS)
{
    float3 d    = posWS - _WorldSpaceCameraPos;
    float  dist = length(d);
    // Beerâ€“Lambert over distance
    float distFog = 1.0 - exp(-_WFFogDensity * dist);
    // density decays with altitude above the valley-floor datum
    float h        = max(posWS.y - _WFFogGroundY, 0.0);
    float heightAt = exp(-_WFFogHeightFalloff * h);
    float fog = distFog * lerp(1.0, heightAt, _WFFogHeightStrength);
    return (half)saturate(fog * _WFFogEnable);
}

// ---- Fragment: dissolve lit color into the horizon color ----
half3 WayfinderApplyFog(half3 col, half fog)
{
    return lerp(col, _WFFogColor.rgb, fog);
}

// ---- Per-PIXEL: opposition surge (regolith backscatter hotspot) ----
half3 WayfinderSurge(half3 col, float3 posWS)
{
    float3 V = normalize(_WorldSpaceCameraPos - posWS); // surface->camera
    float3 L = _MainLightPosition.xyz;                  // surface->sun (URP, unit for dir light)
    float  c = saturate(dot(V, L));                     // 1 at zero phase angle (anti-solar)
    float  surge = _WFSurgeStrength * pow(c, _WFSurgeSharpness);
    return col * (1.0h + (half)surge * _WFSurgeTint.rgb);
}
#endif
```

### Three edits to the forked `TerrainLitPasses.hlsl`

1. **Interpolator** â€” in `struct Varyings` (after line 40, `positionWS`), add:
   ```hlsl
   half wfFog : TEXCOORD11;   // Wayfinder analytic haze factor
   ```
   (TEXCOORD10 is APV occlusion; 11 is free. One `half` â€” trivial against Adreno's interpolator budget.)

2. **Vertex** â€” in `SplatmapVert`, right after `o.positionWS = Attributes.positionWS;` (line 363):
   ```hlsl
   o.wfFog = WayfinderFogFactor(Attributes.positionWS);
   ```

3. **Fragment** â€” in `SplatmapFragment`, replace the forward-path tail (lines 505â€“509). After `UniversalFragmentPBR(...)`, apply surge **before** fog (fog attenuates the hotspot, physically correct), then our fog instead of stock `MixFog`:
   ```hlsl
   half4 color = UniversalFragmentPBR(inputData, albedo, metallic, half3(0,0,0), smoothness, occlusion, half3(0,0,0), alpha);
   color.rgb = WayfinderSurge(color.rgb, IN.positionWS);   // Moon hotspot / Mars faint
   color.rgb *= color.a;                                    // keep TerrainLit's premultiply
   color.rgb = WayfinderApplyFog(color.rgb, IN.wfFog);      // replaces SplatmapFinalColor's MixFog
   outColor = half4(color.rgb, 1.0h);
   ```
   (i.e. drop the `SplatmapFinalColor(color, inputData.fogCoord);` call on the forward path; keep it only under `TERRAIN_GBUFFER` â€” but you're Forward on mobile XR, so GBuffer path is dead. `_ADDITIONAL_LIGHTS_VERTEX`/fog interpolators can stay untouched; they're just unused now.)

### Fog math, stated plainly

Two independent factors multiplied, both physically motivated, both stable (no divide):

- **Distance (Beerâ€“Lambert):** `distFog = 1 âˆ’ e^(âˆ’Î²Â·d)`, Î² = `_WFFogDensity`. Î² â‰ˆ 1/visibility. Mars dust visibility ~2â€“3 km â†’ Î² â‰ˆ 3.3â€“4Ã—10â»â´.
- **Height (exponential atmosphere):** density at a vertex's altitude `= e^(âˆ’kÂ·(yâˆ’yâ‚€))`, k = `_WFFogHeightFalloff`. Haze is thick on the valley floor, thin on a ridge â€” this is the "butterscotch **height** haze" and the LOD-seam hider, because distant *low* geometry (where basemap LODs live) fogs hardest.
- `_WFFogHeightStrength` cross-fades between pure distance fog (0) and fully stratified (1).

The exact closed-form line-integral through an exponential medium (Wenzel-style, with the `1/rayDir.y` term) is a valid upgrade but adds an instability near horizontal rays for no visible gain here â€” your terrain vertices are dense (2049 heightmap), so sampling density **at each vertex's own height** is already smooth. Ship the product form; note the integral as a future option.

### Surge term, stated plainly

`surge = AÂ·cos(Î±)â¿`, Î± = phase angle (Sunâ€“surfaceâ€“observer). `cos Î± = dot(V,L)` peaks at the **anti-solar point** â€” the ground around your own shadow, sun behind you â€” which is exactly where the Moon's coherent-backscatter + shadow-hiding brightening appears. `pow(cosÎ±, n)` is a cheap narrow lobe; angular width maps as **HWHM â‰ˆ âˆš(2Â·ln2 / n)**:

| n | HWHM | look |
|---|---|---|
| 16 | ~17Â° | broad, gentle glow |
| 48 | ~9.7Â° | Moon default (shadow-hiding scale) |
| 64 | ~8.4Â° | tight |
| 200 | ~4.7Â° | coherent-backscatter-tight |

Moon: `strength 0.6, sharpness 48`. Mars: `strength ~0.1` (dusty regolith has a weak surge) or 0. Do surge **per-pixel** (not per-vertex): the hotspot is only a few metres across near your feet, and per-vertex would smear it blockily across large terrain triangles. One `dot` + one `pow` â‰ˆ 6 ALU/pixel.

---

## 3. Asset-generation approach (procedural / free+credited)

There is essentially **no binary asset** â€” the feature is math plus ~9 tuned constants per world. Sources:

- **Mars horizon color** (`hazeColor`): must equal the baked `MarsSky` cubemap's horizon band, or the terrain melts into the wrong color. Two free ways: **(a)** sample it â€” a 15-line Editor script reads the six `MarsSky` face textures, averages the equatorial (horizon) pixel ring, converts to linear, writes the RGB into the `WorldPackage`. **(b)** derive it procedurally: Mars sky is Mie/dust-dominated (not Rayleigh), so a 2-stop vertical gradient â€” pale butterscotch horizon â†’ dustier pink-brown zenith â€” reproduces it. Curiosity Mastcam white-balanced panoramas give horizon linear RGB â‰ˆ **(0.68, 0.49, 0.36)**; that's the default above. **Credit:** NASA/JPL-Caltech/MSSS Curiosity Mastcam (public domain).
- **Moon:** `hazeEnabled=false`; surge is pure math. Surge strength/sharpness from the Hapke opposition parameters fit to **LRO/Apollo** photometry (public domain). **Credit:** NASA LROC / Apollo surface photometry.
- **Height datum / density:** numbers, straight from each DEM's `meta.json` (already generated by `dem-to-terrain`). No asset.

**Optional "perfect-match" upgrade (resolves the Â§6 risk):** bake a **64Ã—64Ã—6 cubemap** from the existing `MarsSky` faces (Editor script, one-time, ~48 KB, credited to the same source), bind it globally as `_WFSkyCube`, and make `WayfinderApplyFog` use `SAMPLE_TEXTURECUBE_LOD(_WFSkyCube, sampler, viewDirWS, 0).rgb` as the fog color instead of the constant. Then terrain dissolves into **exactly** what the sky shows along that pixel's view ray, azimuth-correct by construction. Cost: +1 tiny cubemap fetch on fogged pixels (~+0.03â€“0.05 ms; the 64Â² cube stays resident in texture cache). This is the recommended path for Mars once the constant-color version is validated.

No Meshy/Tripo/Rodin, no paid generators, nothing to import through the `asset-import-gate`.

---

## 4. Per-frame cost + draw-call/tri budget (justified)

**Draw calls: +0. Triangles: +0. Passes: +0. Depth reads: 0. New textures: 0** (or one 64Â² cube on the matched path). The effect rides entirely inside the terrain's existing forward draws.

**Vertex ALU** (added): 2Ã— `exp`, 1Ã— `length`, ~6 mul/add â‰ˆ **~12 ALU/vertex**. Unity terrain at pixel-error ~5 on a 2049 heightmap within the ~2 km playable view â‰ˆ 150kâ€“350k verts, Ã—2 for single-pass-instanced stereo â‰ˆ up to ~700k vertex invocations Ã— 12 ALU â‰ˆ ~8 MFLOP/frame â€” **microseconds** of ALU on the XR2+ Gen 2 (Adreno 740-class, >1 TFLOP). Call it **<0.03 ms**.

**Fragment ALU** (added): fog lerp = 3 mad; surge = 1 dot + 1 `pow`(~4 ALU) + 1 mad â‰ˆ **~10 ALU/pixel** over terrain coverage. Terrain â‰ˆ 50â€“70% of 1856Ã—2160Ã—2 â‰ˆ 8.0 Mpx â†’ ~5.3 Mpx Ã— 10 ALU â‰ˆ 53 MFLOP/frame. The `pow` dominates. Empirically this class of per-pixel add lands at **~0.05â€“0.15 ms** on this GPU.

**Total â‰ˆ 0.1 ms** of the 13.8 ms budget (~0.7%), matching the research doc's estimate (item #10, ~0.1 ms). The matched-cubemap path adds ~0.03â€“0.05 ms for the fetch. Moon pays only the surge (~half of fragment cost, fog branch is `_WFFogEnable=0`).

Why this is honest and not optimistic: no new geometry, no second pass, no G-buffer, no depth prepass dependency, and the two `exp`s are per-vertex (amortized), not per-pixel. The only per-pixel transcendental is one `pow` for surge, which Â§5 can drop.

---

## 5. Graceful degradation (if it blows budget)

A ladder, each rung strictly cheaper, all runtime-switchable via the globals (no rebuild):

- **L0 â€” full:** per-vertex height+distance fog + per-pixel surge + matched cube. (~0.13 ms)
- **L1 â€” drop the cube:** constant `_WFFogColor` instead of `SAMPLE_TEXTURECUBE`. (âˆ’0.05 ms) Already the default path.
- **L2 â€” surge per-vertex:** move `WayfinderSurge` into `SplatmapVert`, interpolate; kills the per-pixel `pow`. Hotspot gets slightly blocky but far cheaper. (âˆ’~0.05 ms)
- **L3 â€” drop the fork entirely, use stock URP fog:** re-enable scene Fog = Exp2, set fog color = horizon, revert terrain material to stock `TerrainLit`. Distance-only (no height stratification, no surge), but **free** and zero-maintenance â€” URP already computes `ComputeFogFactor` per-vertex + `MixFog` per-pixel. Loses the "height" half of butterscotch and the seam-hiding-where-it-matters, keeps the depth cue.
- **L4 â€” bake the haze into the drape:** at authoring time, darken/tint the orbital-imagery drape toward `hazeColor` in a radial ring near the DEM edge (GDAL/Photoshop step in `dem-to-terrain`). Literally **0 runtime cost**, pure Red Matter doctrine. Static (no view-dependence, no surge), but the look survives.
- **Moon is already the cheapest case** â€” fog branch is off; if even the surge is too much, `surgeStrength=0` reverts to flat airless lighting (which the research doc says "contrast does the work" anyway). So the Moon's degradation is free by construction.

Because every knob is a global uniform, you can also bind L-levels to a runtime quality tier without touching shaders.

---

## 6. The single riskiest unknown

**Whether a single constant `hazeColor` matches the baked 6-sided Mars cubemap's horizon well enough across azimuth that terrain dissolves seamlessly â€” because the horizon color the cubemap shows is *not* constant around the compass** (it's brightest and yellowest toward the sun, dustier away from it), while the analytic fog color is one RGB. The fog is specifically supposed to *hide* the LOD/basemap seam at the horizon; a hue/value mismatch there produces a visible band **exactly where the trick is supposed to work**, and it's specific to your baked-cubemap sky decision (a procedural gradient sky wouldn't have this â€” one source of truth would drive both). 

Mitigation is the Â§3 matched-cubemap upgrade (sample `_WFSkyCube` along the view ray â†’ azimuth-correct by construction); the open question is whether the constant-color L1 path is *good enough* to ship without it, which is a look call only answerable **on the device**, not in editor. Prototype the constant path first, stand on the Olympus rim, pan the compass, and look for the band before committing to the cube.

*(Second-order engineering risk worth a RenderDoc check, not the headline: the fork must survive Vulkan variant stripping + terrain GPU instancing on-device â€” the same "instancing silently fails in the Vulkan build" pitfall the research doc flags for rock scatter (#9). If a variant is stripped and falls back, the fog constants can read zero and the horizon seam reappears. Always-Included + a warmed Variant Collection (Step F) is the guard; verify with RenderDoc on the real build. With 1 terrain layer the `TerrainLitAdd` multi-pass never runs and the basemap-crossover seam mostly doesn't arise â€” but set Base Map Distance beyond the DEM so distant terrain never swaps to the un-forked basemap path.)*

---

**Files touched:** `unity/Assets/Wayfinder/Scripts/WorldPackage.cs` (fields), new `unity/Assets/Wayfinder/Scripts/WorldAtmospherics.cs`, new `unity/Assets/Wayfinder/Shaders/Terrain/{TerrainLit.shader, TerrainLitPasses.hlsl, WayfinderAtmos.hlsl}`, rebind `unity/Assets/Wayfinder/Materials/Terrain_*.mat`, author `unity/Assets/Wayfinder/Sites/*.asset`. One thin EditMode test fits your doctrine: assert `WorldAtmospherics.Apply()` sets `Shader.GetGlobalColor(ID_FogColor)` and that Moon's package yields `_WFFogEnable=0` â€” the shader itself is device/RenderDoc-verified, not unit-tested.

---

## agent

I have verified every repo fact the spec cites. All check out. I have enough to render a grounded verdict without further digging.

---

# VERDICT: BUILDABLE-WITH-FIXES

The spec is technically literate and its repo grounding is **accurate on every checkable point** â€” I verified them against the actual files, not the spec's assertions:

| Claim | Verified |
|---|---|
| Unity 6000.5.4f1 | âœ“ `ProjectVersion.txt` line 1 (above the 6000.3.5f2 floor, no drift) |
| `Bridge.unity`: `m_EnableBakedLightmaps:1`, `m_BakeBackend:2` | âœ“ lines 53, 71 |
| Greybox `HullSegment_0..7`, `Viewscreen`, `Console`, `KeyLight`, `BridgeFloor` under `BridgeVisuals` | âœ“ present (BridgeVisuals at line 1676) |
| Materials = URP/Lit guid `933532a4fcc9baf4fa0491de14d08ed7`, MOTIONVECTORS disabled, `_XRMotionVectorsPass:1` | âœ“ `Hull.mat` |
| asmdef split `Wayfinder.Unity` / `Wayfinder.Unity.Editor` | âœ“ both exist |
| `BridgeSceneTests` name/anchor/comfort/flat-floor contracts | âœ“ exactly as described |
| `TravelState == OnSurface` is a real state | âœ“ `TravelStateMachine.cs` enum |
| SRP Batcher on, HDR on | âœ“ `URP-Pipeline.asset` `m_UseSRPBatcher:1`, `m_SupportsHDR:1` |
| Box Projection currently OFF (spec's "enable it" step is real) | âœ“ `m_ReflectionProbeBoxProjection:0` |
| Foveated rendering + SpaceWarp features present | âœ“ `m_foveatedRenderingApi:1`; `SpaceWarpFeature Android` wired |

Nothing in the spec is NOT-VIABLE. But several claims are optimistic, one project-config gap is unaddressed, and two "facts" are actually on-device unknowns dressed as safe. Fixes below.

---

## What the spec gets genuinely right (so the fixes are calibrated)
- **Directional lightmaps are mandatory for the normal map to touch diffuse** â€” correct and non-obvious. Non-directional would make the whole trim sheet cosmetic. This is the single best call in the doc.
- **MPB breaks the SRP Batcher; drive per-screen variation via UV2/vertex color** â€” correct, and the right fix.
- **Fake additive glow cards over full-screen URP Bloom** â€” correct for a tile GPU; full-screen bloom is a bandwidth tax that fights foveation.
- **Gate the whole `BridgeVisuals` off on `OnSurface`** â€” correct, the state exists, it's a few lines, and it's the highest-leverage move.
- **Tris irrelevant, fragment dominates in a â‰¤4 m room** â€” correct framing.

---

## Frame-budget attack

**The 3.5â€“6.0 ms total is a plausible-but-unproven guess with false precision, and its dominant line is the softest.**

1. **"Opaque hull fragment 2.5â€“4.0 ms" is the entire bet, and it's hand-waved.** In a room where the hull fills the FOV, this Lit pass runs on ~every pixel of both eyes (1856Ã—2160Ã—2 â‰ˆ 8M px pre-foveation). Per pixel it does: 3 texture samples + normal unpack + **two** lightmap samples (directional adds the second) + **box-projected** cubemap sample (which is a per-pixel ray/box intersection, *not* "near-zero" â€” it's ALU on top of the most expensive pass) + full metallic BRDF. On an XR2+ Gen 2 (Adreno 740-class) that can land north of 4 ms. The estimate isn't crazy, but it is a guess, and the spec's own Â§7 + gate correctly concede only on-device proves it. Treat 2.5â€“4.0 as "unverified, likely the number that decides pass/fail."

2. **Box projection is double-counted as free.** Â§4 calls it "near-zero added runtime cost" and Â§5 folds it into the hull line â€” but it runs on the dominant, FOV-filling pass, so it is precisely *not* free. It's cheap ALU, but it lands on your most expensive pixels.

3. **Foveation is load-bearing for the margin, and it protects the wrong pixels.** Foveation cuts the *periphery*; your console screens, viewscreen, and cove detail are dead-center where the eye fixates and foveation does nothing. The "7â€“10 ms headroom" leans on a saving that's smallest exactly where the detail is. Foveation is genuinely wired (verified), so it's real â€” but don't bank the periphery savings against center-of-view cost.

**Net:** the budget is directionally sound (baked = zero shadow/light loop is the real win) but the headroom is softer than "7â€“10 ms" implies. It survives only because the gate and degradation ladder exist.

---

## Stack-fit attack (URP 17.5 / Vulkan / Android XR)

1. **MSAA is OFF (`m_MSAA:1`) and the spec never mentions it â€” this fights the entire look.** A high-frequency normal-mapped metal interior with fine panel lines, viewed in stereo, will shimmer/crawl on edges and specular without MSAA. In VR, MSAA (not post-AA) is the standard and on tilers it's comparatively cheap. **Fix: enable MSAA 4x** â€” but note it *raises* the dominant hull-fragment/bandwidth line the budget is already optimistic about, so it must be measured, not assumed free. This is the biggest omission in the spec.

2. **The "`_XRMotionVectorsPass` / ASW-ready" posture the spec carefully preserves may be a no-op on Galaxy XR.** Unity's `SpaceWarpFeature` implements the **`XR_FB_space_warp`** extension â€” historically a Meta/Quest extension. Whether Samsung's Android XR runtime exposes it is `[unverified]`. If it doesn't, all the motion-vector-pass care is harmless dead weight, and the spec's reassurance that "animated screen content half-rating under ASW is imperceptible" is moot (there's no ASW). Don't treat "ASW-ready" as a delivered safety property â€” flag it `[unverified â€” confirm XR_FB_space_warp on Galaxy XR runtime]`.

3. **The from-scratch `EmissiveDisplay` Unlit shader is not automatically SRP-Batcher-compatible.** The spec asserts SRP-batch compatibility, but a hand-written HLSL shader only batches if every property lives in a correctly-laid-out `CBUFFER_START(UnityPerMaterial) â€¦ CBUFFER_END`. It also won't inherit a MOTIONVECTORS pass to disable â€” so "match the existing motion-vector posture" isn't a copy-paste; on a from-scratch Unlit shader the posture is simply *absent* (acceptable for static geometry, but the spec implies parity it won't have). Buildable, but the "SRP-batcher compatible" claim is conditional on the author getting the CBUFFER right.

4. **Â§7's stereo risk is appropriately flagged but somewhat overstated.** Box-projected probes have generally been stereo-correct in URP single-pass instanced for several versions (per-eye `unity_StereoWorldSpaceCameraPos` feeds the reflection math). The "documented class of bugs" is `[unverified]` and the spec admits it. Keep the on-device check â€” it's the right gate â€” but this is a moderate risk, not a probable failure, and the infinite-projection fallback is sound.

5. **Confirm Box Projection on *all* URP quality tiers,** not just the one asset â€” it's a per-asset global toggle and a multi-tier project can silently ship it on one tier only.

---

## Procedural / free-asset attack

Mostly holds â€” numpy+Pillow + ProBuilder + in-engine bake is genuinely dependency-free and matches the repo's terrain/sky ethos. Two real dents:

1. **Memory math undercounts mipmaps.** 2048Â² ASTC 6Ã—6 = 1.87 MB *base*; with mipmaps ON (which the import steps specify) add ~33% â†’ ~2.5 MB each â†’ **~7.5 MB for the three maps, not 5.6 MB.** Trivially within budget, but the number is wrong as written.

2. **`np.gradient`-of-a-heightfield normals will read soft/blobby, not like machined Starfleet bevels.** The spec concedes this ("if it reads flat on-deviceâ€¦ bake in Blender"). Fair, but be honest that the *headline* free path likely produces a mushy hull and the crisp look probably requires the "optional" Blender bake â€” i.e. the optional upgrade is closer to mandatory for the stated aesthetic. Also, ASTC 6Ã—6 on a *normal* map is aggressive; smooth bevels may band (spec's 5Ã—5 fallback acknowledged).

No paid-account or generator-MCP dependency sneaks in. Claim substantially survives.

---

## Comfort-rule attack

Visual-only feature; it touches no locomotion provider, so the `BridgeSceneTests` comfort contracts (no `ContinuousMove/TurnProvider`, teleport+snap present, 2.0 m radius, flat floor) are unaffected. Two things to watch:

1. **The viewscreen "warp starfield / radial streak" idle variant is comfort-adjacent.** A continuously-running radial optical-flow field sitting in the primary forward gaze direction is exactly the "forward-acceleration tunnel" sensation CLAUDE.md warns the warp must *not* be. It's bounded to a screen (not full FOV), so the risk is mild, but a persistent, always-on streaming-starfield viewscreen can induce low-grade vection. **Validate on-device; prefer a slow/near-static nebula for the idle state and reserve streaks for the brief warp transition.**

2. Emissive pulse/flicker/scanline/twinkle are brightness animations on small-coverage surfaces â€” not locomotion, not full-field strobe. No comfort violation. Fine.

---

## Test-contract attack

The `BridgeFloor` test (`unity/Assets/Wayfinder/Tests/Editor/BridgeSceneTests.cs:212`) asserts the collider is **non-capsule** and `bounds.max.y â‰¤ 0.05`. The spec's `DeckFloor` has an **inset grid** â€” if the generator ever gives it a `MeshCollider` off that relief, `bounds.max.y` can exceed 0.05 and the test fails. The spec says "keep flat box collider at yâ‰ˆ0," so it's guarded *if followed*; call it out as a hard generator constraint, not a preference. Same for anchors: the generator must not re-create or re-position the `Viewscreen`/`Console`/`BridgeFloor` transforms (add detail as children only) or the 2.0 m-radius asserts break.

---

## Required fixes (actionable)

1. **Enable MSAA 4x** in the URP asset(s) and re-measure the hull-fragment line â€” it is currently off and the look depends on it. (Highest-impact omission.)
2. **Re-label the frame budget as unverified guesses** and treat 2.5â€“4.0 ms hull-fragment as the gating unknown; stop calling box projection "near-zero." Add MSAA's cost to the estimate.
3. **Flag `[unverified]` on the ASW posture** â€” confirm `XR_FB_space_warp` is exposed by the Galaxy XR runtime before relying on any motion-vector-pass reasoning; if absent, drop that language.
4. **Spell out the `UnityPerMaterial` CBUFFER requirement** for the hand-written `EmissiveDisplay` shader; "SRP-batcher compatible" is conditional, not automatic.
5. **Correct the texture memory to ~7.5 MB** (mip-inclusive) and treat the Blender normal bake as likely-required, not optional, for the stated crispness.
6. **Specify the gating reactivation edge:** `BridgeVisuals.SetActive(true)` must fire on `WarpingToBridge` (behind the warp fade), not only on `OnBridge`, or the bridge pops in on return. The spec only names the disable edge.
7. **Enforce the generator constraints as hard rules:** flat BoxCollider at yâ‰ˆ0 on `BridgeFloor`; never move/re-create the three named anchor transforms.
8. **On-device, validate the idle viewscreen** for vection before shipping animated radial streaks in forward gaze.
9. **Verify Box Projection on every URP quality tier**, not just the primary asset.

---

## Residual on-device unknowns (cannot be cleared in-editor)
- Box-projected probe stereo-correctness under Vulkan single-pass instanced (spec's Â§7 â€” keep the head-translation check; risk moderate, not probable).
- Actual hull-fragment ms with MSAA 4x + directional lightmap + box reflection at full center-of-view rate.
- Whether `XR_FB_space_warp` and eye-tracked foveation deliver their assumed savings on the real Galaxy XR runtime.

**Bottom line:** the architecture, asset pipeline, shader plan, lighting plan, and degradation ladder are all buildable in URP 17.5 / Vulkan / Android XR and respect the test and comfort contracts. It is not shippable "as-is" because MSAA is off and unaddressed, the frame numbers are optimistic guesses, and two safety properties (ASW, box-projection stereo) are unverified assumptions rather than the delivered facts the prose implies. Apply the nine fixes and it clears to buildable, with the on-device 72 fps gate remaining the only real proof.

---

## agent

Repo claims verified. I have what I need. Delivering the adversarial verdict.

---

# VERDICT: BUILDABLE-WITH-FIXES

The doctrine is sound and stack-appropriate â€” bake-offline / render-instanced, 100% procedural meshes, degrade-to-zero. The architecture fits the repo and the platform. **But the two most concrete artifacts in the spec â€” the LateUpdate draw-submission sketch (Â§1C) and the shader (Â§2) â€” are wrong as written and will fail on device in exactly the silent way the ticket warns about.** None of the failures are fatal to the approach; all are fixable. Fix list below, ordered by severity.

## Repo cross-check (what I confirmed, so the fixes land on reality)
- Unity **6000.5.4f1** âœ“, `Wayfinder.Unity` asmdef references `Wayfinder.Core` + XRI + XR.CoreUtils âœ“, EditorTests asmdef âœ“ â€” stack claims are accurate.
- `SiteTerrain` root, `isStatic=true`, `drawInstanced=true`, `shadowCastingMode=Off`, `heightmapPixelError=8`, single solid URP layer âœ“ (matches the budget's terrain assumptions).
- `WorldPackage.spawnOffset` is a `Vector2` âœ“. All three `Site_<id>.unity` scenes exist âœ“.
- **Zero existing `RenderMeshInstanced`/`DrawMeshInstanced` usage anywhere in Assets** â€” this API is unproven in this codebase, which *raises* the weight of the Â§6 de-risk step. Good that the spec front-loads it.
- Locomotion uses XRI `GrabMoveProvider` (moves the **rig**, not the world) and terrain is static â†’ the absolute-world-space baked-matrix approach is **safe** (no world-grab desync). One latent risk retired.
- Minor factual wobble: spec says "TravelStateMachine lives in `Wayfinder.Core`" â€” the travel code (`TravelManager.cs`) actually sits in `Wayfinder.Unity/Scripts`. The *recommendation* (pure math in the runtime asmdef, EditMode tests reference it) is still correct and matches how `WorldCatalogTests`/`PoiContractTests` already work.

---

## BLOCKERS (must fix before it builds/renders correctly)

**F1 â€” The draw sketch never submits the transforms (API misuse).** `Graphics.RenderMeshInstanced` has a matrices-only overload `(RenderParams, mesh, submesh, Matrix4x4[], count)` and a generic `<T>(â€¦, T[], â€¦)`. There is **no overload that takes a `Matrix4x4[]` AND a parallel custom-data array.** The Â§1C sketch builds `_mtx[a,l]` but then calls `RenderMeshInstanced(_rp, _mesh, 0, _inst, _count)` passing `_inst` (`InstData{float fade; uint tint;}` â€” no matrix). The transforms are computed and dropped; rocks render at identity/garbage. **Fix:** commit to ONE path â€” either (a) matrices-only overload + `MaterialPropertyBlock.SetFloatArray/SetVectorArray` on the RenderParams for fade/tint, or (b) the generic overload with a single struct whose **first field is the `Matrix4x4` objectToWorld** followed by fade/tint. Pick (b) if you want >1023/batch (see F4).

**F2 â€” Shader is missing Single-Pass-Instanced stereo plumbing.** Â§2 declares `UNITY_VERTEX_INPUT_INSTANCE_ID` / `SETUP` / `TRANSFER` but has **no `UNITY_VERTEX_OUTPUT_STEREO` in the `V` struct and no `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)` in `vert`**. Under Android XR SPI the eye index is carried on the instance ID and written via these macros; without them you render one eye / broken stereo. This *directly* breaks the Â§1E acceptance test â€” `instanceCount = visibleÃ—2` only materializes if the shader participates in SPI. **Fix:** add `UNITY_VERTEX_OUTPUT_STEREO` to `V`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)` after `UNITY_SETUP_INSTANCE_ID` in vert, and `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)` (via `UNITY_SETUP_INSTANCE_ID`) in frag. Include the `Core.hlsl` first, not just `Lighting.hlsl`.

**F3 â€” Per-instance data path is internally contradictory.** Â§1C routes `_Fade`/`_Tint` through the generic `RenderMeshInstanced<T>` GraphicsBuffer, but Â§2 reads them via `UNITY_ACCESS_INSTANCED_PROP` â€” that macro reads the *MaterialPropertyBlock instancing cbuffer* (the `DrawMeshInstanced` mechanism), **not** the generic-overload struct buffer. Pick one and make the shader match:
  - **MPB path (recommended, well-trodden under SPI):** matrices-only overload + `MaterialPropertyBlock.SetVectorArray`; keep Â§2's `UNITY_DEFINE/ACCESS_INSTANCED_PROP` as-is.
  - **GraphicsBuffer path:** the shader must read a `StructuredBuffer<InstData>` indexed by instance ID â€” a *different* shader than Â§2. The exact struct-fieldâ†’shader binding for the generic overload in 6000.5 is the one genuinely version-sensitive point (the spec's own `[verify]` flag) â€” this is the thing to confirm via Context7 *and* on-device RenderDoc, and it's why F3 exists. Do not present Â§2 as "concrete" until this is pinned.

## HIGH (fix or the frame budget / cross-machine claims don't hold)

**F4 â€” 1300-visible cap collides with the 1023 instancing-cbuffer limit if you take the MPB path.** `DrawMeshInstanced`/MPB instanced arrays cap at ~1023 per batch (and fewer with a matrix + float + float4 per instance). The spec's 1300 visible implies the GraphicsBuffer generic path â€” which is fine, but then F3's MPB shader is wrong. Consistency: 1300 â‡’ generic/GraphicsBuffer â‡’ StructuredBuffer read, not `UNITY_ACCESS_INSTANCED_PROP`. Or keep MPB and cap batches â‰¤1023 (split), adding a few draws.

**F5 â€” `clip()`/dither defeats early-Z on the Adreno tiler; the "zero overdraw" claim is inverted.** On Snapdragon/Adreno, *any* shader containing `clip()`/`discard` forces late-Z and disables the low-res-Z / hidden-surface-removal fast path â€” for **all** fragments of that variant, not just the ones actually discarded. Â§2 calls `DitherClip` unconditionally in frag (and in DepthOnly/DepthNormals), so even fully-opaque steady-state rocks lose early-Z, and near-camera overdraw from the 150 LOD0 hero rocks (large silhouettes, embedded in terrain) becomes full-cost. This is the opposite of "no alpha overdraw." **Fix:** guard `DitherClip` behind `#if LOD_FADE_CROSSFADE` (the keyword already exists in the pragma but the frag ignores it), render only the ~transition-band instances with the crossfade variant, and keep steady-state rocks on a clip-free opaque variant to preserve early-Z. This is material on a HARD-72 tiler.

**F6 â€” Vertex load understated ~3Ã—.** Flat/faceted normals require split verts â†’ ~3 verts per triangle. "156k simple verts" is really ~156k *tris* â‡’ ~468k verts/eye â‡’ ~936k VS invocations stereo, transformed every frame. The 0.2â€“0.35 ms vertex estimate is optimistic by roughly 2â€“3Ã—. Still probably within budget, but the stated margin is thinner than claimed â€” re-derive the â‰¤160k tris/eye ceiling from measured VS cost, not asserted.

## MEDIUM (fix for honesty / determinism / gate integrity)

**F7 â€” Acceptance gate proves the wrong thing.** Â§1E/Â§6 gate on a RenderDoc *draw-call* check ("instancing didn't silently fall back"). That verifies instancing *happens*; it does **not** verify **72 fps holds**. Per this repo's own rule ("only an on-device build is framerate evidence") and the `frame-budget-report` skill, add a co-equal gate: on-device frame-budget-report at **full scatter density in the Valles apron** showing the 72 fps floor held with margin. "Instancing works" â‰  "frame holds."

**F8 â€” Determinism claim is at risk if noise = `Mathf.PerlinNoise`.** The "regenerable/versionable by seed, identical output" claim and EditMode test (a) require a noise function that is bit-stable across Unity versions/platforms. `Mathf.PerlinNoise` is documented as *not* guaranteed identical across platforms/versions. **Fix:** ship a hash-based value/gradient noise in `ScatterPlacement`/`RockMeshGenerator`; do not rely on `Mathf.PerlinNoise`.

## LOW (cleanups, non-blocking)

- **F9** `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_OFF` is not a real URP keyword â€” the shadow keywords are `_MAIN_LIGHT_SHADOWS[_CASCADE|_SCREEN]`. Since you call `GetMainLight()` with no shadow coords, just drop the pragma.
- **F10** `o.scr = ComputeScreenPos(o.pos)` is dead â€” the frag dithers on `i.pos.xy` (already pixel coords under SV_POSITION), which is correct; remove `scr`.
- **F11** No per-instance colliders means the player can teleport to a terrain point *inside* a large LOD0 boulder (visual/physical mismatch, brief inside-geometry view). Not a platform comfort-rule violation, but consider extending the exclusion discs (Â§1B) to large-boulder footprints, or ship the optional 8-SphereCollider pool. The spec already flags this â€” acceptable to ship without.

---

## Attack-axis summary

- **Frame budget:** Plausible but optimistic. Draw calls (~12â€“26) and tri count (~312k stereo) fit the envelope. Real risks the spec under-weights: (F6) 3Ã— vertex undercount and (F5) `clip()` killing early-Z on Adreno, both hitting the fragment/vertex lines that have the least margin. CPU LateUpdate cull (~0.2â€“0.35 ms, single-thread, main) is believable. The "~0.5â€“1 ms" line is achievable **after** F5/F6, not as literally costed.
- **Stack-fit (URP 17.5 / Vulkan / Android XR):** The pieces exist and are supported (RenderMeshInstanced, URP HLSL instancing, foveation, SPI). But the concrete code doesn't currently fit the stack: F1 (no such overload), F2 (missing SPI macros), F3/F4 (wrong per-instance data path for the chosen overload/cap). These are the "instancing silently didn't happen / lost the stereo multiplier" failure the ticket names â€” the spec correctly identifies the risk in prose then reintroduces it in the code.
- **Procedural/free-asset claim:** **Holds, and is the strongest part.** 100% in-editor procedural meshes, no marketplace/AI-gen/account, deterministic, correctly dodges the asset-import gate (nothing imported). Only caveat is F8 (noise determinism). CC0 ambientCG/PolyHaven fallback is genuinely free. No violation.
- **Comfort rules:** **No violations.** No smooth camera rotation, warp untouched, teleport/world-grab preserved (rocks have no colliders; grab-move moves the rig not the world â†’ verified no desync; teleport targets the terrain layer). Additive-presence + degrade-to-zero genuinely protects the 72 fps gate **provided F7's on-device fps measurement is actually run** â€” the design can't regress the baseline, but the spec must *prove* it, not just assert it.

**Bottom line:** BUILDABLE-WITH-FIXES. Do the Â§6 de-risk prototype first â€” but make it representative: same overload + per-instance struct + the F2 stereo macros the real system will use, otherwise it validates a path you won't ship. Fix F1â€“F3 before writing the renderer, F4â€“F6 before costing the budget as final, F7 before calling the gate passed.

---

## agent

I've verified every load-bearing claim against the actual repo and the real URP 17.5.0 source. Here is the adversarial verdict.

---

# VERDICT: BUILDABLE-WITH-FIXES

The spec is unusually honest and its factual claims about the codebase are **true**, not asserted. I confirmed each one:

- **Zero custom shaders in `Assets/Wayfinder/`** â€” confirmed (no `.shader`/`.hlsl`/`.shadergraph` anywhere under it).
- **Terrain = stock URP TerrainLit, guid `69c1f799e772cb6438f56c23efccb782`, 1 layer, `_EnableInstancedPerPixelNormal:1`** â€” confirmed in `Materials/Terrain_mars-olympus.mat` (`_NumLayersCount: 1`).
- **Skies = baked 6-sided cubemaps on builtin `Skybox/6 Sided`** â€” confirmed (`MarsSky.mat` shader guid `â€¦f000000000000000` fileID 104, six face textures).
- **URP `@e38be786c41e` = 17.5.0** â€” confirmed (manifest pins `17.5.0`; PackageCache folder hash matches the spec's exactly).
- **Every injection line number** â€” confirmed against the real `TerrainLitPasses.hlsl`: L353 `ComputeFogFactor`, L266 `MixFog`, L40 `positionWS : TEXCOORD7`, L51 `probeOcclusion : TEXCOORD10` (so TEXCOORD11 is genuinely free), L363 `o.positionWS = Attributes.positionWS;`, L505â€“509 the forward tail (`UniversalFragmentPBR` â†’ `SplatmapFinalColor` â†’ `outColor`). The 1-layer / no-ADD-pass reasoning holds.
- **The world-load hook exists** â€” `TravelManager.WarpToSurface` L122â€“135 runs inside the fade-covered period; `Apply()` drops in cleanly, and `FindPackage(worldId)` already hands you the `WorldPackage`.

It does **not** build exactly as written. Five defects, three of which will visibly bite.

## Attack 1 â€” Frame budget (72 fps HARD): claim SURVIVES

~0.1 ms is defensible and honestly derived. +0 draw calls, +0 tris, +0 passes, no depth read. The only per-pixel transcendental is one `pow` for the surge; fog is a `lerp` (3 mad); the two `exp`s are per-vertex and amortized. On the Adreno 740-class XR2+ Gen 2 this is microseconds of ALU on already-shaded terrain pixels â€” mobile terrain is bandwidth/overdraw-bound, not ALU-bound, and this adds neither. The L0â†’L4 degradation ladder is real and each rung is genuinely cheaper (L3 reverts to stock URP fog for free; L4 bakes into the drape for zero runtime). **I could not break this claim.** It is the correct architecture for the constraint â€” a fullscreen/volumetric Renderer Feature would fog the sky wrong, add a pass, and read depth; per-vertex in-shader avoids all three.

## Attack 2 â€” Stack-fit (URP 17.5 / Vulkan / Android XR): WORKS, with 2 real fixes

The mechanism is sound: copy-fork of `TerrainLit.shader` + `TerrainLitPasses.hlsl`, globals set via `Shader.SetGlobal*` declared **outside** `UnityPerMaterial` (SRP-Batcher-safe), pure HLSL (Vulkan-clean), stereo macros untouched, TEXCOORD11 free, no interpolator overflow. But:

- **FIX A (real, VR-specific, stereo-correctness):** `WayfinderFogFactor`/`WayfinderSurge` use raw `_WorldSpaceCameraPos`. The stock terrain shader deliberately uses `GetWorldSpaceNormalizeViewDir(IN.positionWS)` because in **single-pass instanced stereo** the raw macro is not the per-eye eye position. Use `GetCurrentViewPosition()` (URP, stereo-aware) for the distance term and derive `V` the same way the terrain already does. The narrow `pow(cosÎ±,48)` lobe makes any per-eye mismatch worst exactly at the peak â€” the ground at your feet â€” where retinal rivalry is most noticeable. Low effort, fix it now.

- **FIX B ("the one include" is actually six):** `TerrainLit.shader` includes `TerrainLitPasses.hlsl` in **6 passes** (ForwardLit L114, ShadowCaster L142, GBuffer L205, DepthOnly L228, SceneSelection L272, +Meta region). Only the **ForwardLit** pass (L114) needs the local fork and `#include "WayfinderAtmos.hlsl"`; the others don't call the Wayfinder functions. The step "repoint the one include" will mislead an engineer into a global find-replace or confusion. Also: when you "replace L505â€“509," preserve the `_WRITE_RENDERING_LAYERS` block at L511â€“513 â€” the snippet stops at `outColor` and doesn't mention it.

## Attack 3 â€” "Procedural / free asset" claim: SURVIVES

Genuinely honest. The feature is math + ~9 constants; no mesh, no paid generator, nothing through the asset-import-gate. Mars horizon RGB is credited to Curiosity Mastcam (NASA/JPL-Caltech/MSSS, public domain); surge parameters to LRO/Apollo photometry (public domain). The single optional binary â€” a 64Ã—64Ã—6 cube â€” is **baked from their own existing `MarsSky` faces**, so it introduces no external asset. Claim holds.

- **FIX C (real, will bite â€” color space):** This is the one that undermines the whole feature quietly. `Shader.SetGlobalColor(ID_FogColor, w.HazeColor)` passes the Color's **raw** RGB. Unity's inspector color picker serializes **gamma/sRGB**, but the shader lerps against **linear** lit color. So a human authoring the horizon color in the Inspector gets a gamma value used as linear â†’ washed-out/wrong horizon â€” which is *precisely* the Â§6 "visible band where the trick is supposed to work," introduced by a bug rather than by azimuth. Fix: push `w.HazeColor.linear` in `Apply()`, and make the "sample-the-cubemap-face â†’ linear â†’ write-to-field" Editor script use the matching convention. Without this you will chase the Â§6 seam on-device and blame the constant-color approach when the real cause is a gammaâ†’linear slip.

## Attack 4 â€” Comfort rules: NO VIOLATION

No camera motion, rotation, vection, or tunnel is introduced â€” it's a pure shading change that never touches the warp or locomotion. The opposition surge is world-anchored (ground brightness modulated by view angle, a real heiligenschein/opposition effect), not a full-screen flash or a rotating pattern, so it induces no vestibular conflict. Compatible with eye-tracked foveated rendering (peripheral fragments shade cheaper â€” helps). Clean.

## Two more items the spec under-addresses

- **FIX D (scope gap â€” fog is terrain-only):** The fork only fogs the forked TerrainLit terrain, and Step E.2 turns **scene Fog OFF**. Any distant *non-terrain* geometry renders at full clarity against a hazed terrain+sky and pops out of the butterscotch â€” the ship hull viewed from the surface, POI beacons, and specifically the **rock scatter (#9)** the spec itself cites. Terrain+sky-only sites are fine today; the moment #9 lands the haze is inconsistent. Decide now: either give distant props a matching fog or accept terrain-only and document it. The spec doesn't flag this.

- **FIX E (global-uniform persistence):** `Shader.SetGlobal*` persists across additive unload/return-to-Bridge. The design is safe **only** because `Apply()` is called for *every* world with `hazeEnabled` defaulting false (Moon sets `_WFFogEnable=0`). Make that invariant explicit â€” TravelManager must call `Apply()` unconditionally for every world (Moon included) so a prior Mars `enable=1` can never leak into a world that forgot to author atmospherics. Don't make it an optional per-scene component.

## Correctly identified real risk (not a defect)

The spec's own Â§6 headline (constant color vs azimuth-varying horizon) and its second-order note (Vulkan variant-strip + terrain GPU-instancing must survive on device; Always-Included + warmed SVC + RenderDoc as the guard) are both legitimate and correctly prioritized. These are device look/verification calls, not build blockers.

---

**Bottom line:** architecturally correct, stack-accurate, budget-honest, comfort-clean, asset-honest. Ship it after: **(A)** stereo-correct camera position, **(B)** correct the "6 includes / preserve `_WRITE_RENDERING_LAYERS`" step, **(C)** `.linear` on the fog color â€” the one that will otherwise fake the Â§6 seam and waste a device session, **(D)** decide the terrain-only fog scope before #9 rocks land, **(E)** make the `Apply()`-every-world invariant explicit.

Load-bearing files: `unity\Assets\Wayfinder\Scripts\WorldPackage.cs`, `unity\Assets\Wayfinder\Scripts\TravelManager.cs` (hook L122â€“135), `unity\Library\PackageCache\com.unity.render-pipelines.universal@e38be786c41e\Shaders\Terrain\TerrainLitPasses.hlsl` (L505â€“509 forward tail; L266/L353 fog), and `â€¦\Shaders\Terrain\TerrainLit.shader` (ForwardLit include at L114).

---

## agent

I have enough verified evidence. I cross-checked the spec's core claims against the actual URP 17.5 source and the importer rather than trusting the spec's assertions.

---

# Adversarial verdict: BUILDABLE-WITH-FIXES

The architecture is sound and â€” unusually â€” the spec's hardest technical claims are **verified true against the actual source**, not hand-waved. But four concrete defects will make it either silently not-apply, render UB artifacts, hitch under the thermal governor, or show a tile seam. None require redesign. Fix them and it ships.

## What I verified as CORRECT (credit where due â€” these are the claims most specs get wrong)

- **`positionWS` is unconditional at `TEXCOORD7`.** `TerrainLitPasses.hlsl:40` declares it with **no** `#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)` guard (the general URP Lit shader gates it; the *terrain* shader does not â€” `InitializeInputData` at :62 reads it unconditionally). The single biggest compile-risk I expected to catch does not exist here. Claim holds.
- **Injection point is exact.** `half3 albedo = mixedDiffuse.rgb;` is `:442`, `InitializeInputData(IN, normalTS, inputData);` is `:462`. Spec said "~441â€“462." Correct.
- **`normalTS == (0,0,1)` at injection** is real: `:415` inits it, `:441` `SplatmapMix` only writes a layer normal under `_NORMALMAP`, and the importer's base layer sets **only** `diffuseTexture` (`TerrainImporter.cs:181`) â†’ `_NORMALMAP` off. Detail normal cleanly becomes the surface perturbation. Correct.
- **Ordering is right:** `normalTS` is consumed *inside* `InitializeInputData` (`:68`/`:75` `TransformTangentToWorld`), so modifying it *before* `:462` is mandatory â€” the spec does exactly this. Injecting after would silently drop the normal.
- **1 layer / single pass:** `TerrainImporter.cs:74` assigns a 1-element `terrainLayers`. Confirmed. +0 draws / +0 tris is legitimate (`shader_feature` on the same material/draw).
- **Leaving base/add/depth passes stock is safe** because `basemapDistance = 20000f` (`TerrainImporter.cs:105`) â€” the terrain never switches to the basemap pass in any playable range, so the forked Forward path always runs near-field. Verified, not assumed.
- **"Procedural/free" claim holds.** numpy+Pillow FFT-periodic noise is genuinely free; the CC0 fallback (ambientCG / Poly Haven) is genuinely CC0. No licensing exposure.
- **Comfort: no violation.** It's a fragment-stage effect â€” no locomotion, no camera rotation, no smooth motion, fade is a `smoothstep` (no pop). Passes the platform comfort gate outright.

## REQUIRED FIXES (most severe first)

**1. The importer never applies the shader to the three existing materials (feature silently no-ops).**
`TerrainImporter.cs:115` gates the shader assignment behind `if (terrainMat == null)`. All three `Terrain_*.mat` already exist on disk (confirmed: `Terrain_mars-olympus/valles/moon-shackleton.mat`), so on re-import the `Shader.Find(...)` branch is **skipped** â€” only `terrainMat.color` (`:124`) is set. The spec's Â§5 says "replace `Shader.Find(...Lit)` with `...RegolithLit`" but that line lives inside the null-check. As written, existing materials keep stock `TerrainLit` and never get `_REGOLITH_DETAIL` or the textures. **Fix:** move `terrainMat.shader = Shader.Find("Wayfinder/Terrain/RegolithLit")`, `EnableKeyword`, and all `SetTexture/SetFloat` calls **out** of the `if (terrainMat == null)` block so every import re-asserts them.

**2. The macro sample reintroduces the exact undefined-derivative bug the spec claims to have avoided.**
Â§2B computes `dx/dy` before the branch and uses `SAMPLE_TEXTURE2D_GRAD` for detail albedo/normal â€” good â€” but samples `_MacroNoise` with plain `SAMPLE_TEXTURE2D` (implicit LOD) *after* the `UNITY_BRANCH ... return`. Implicit-derivative sampling inside divergent control flow is UB per HLSL: on the fade-boundary annulus where a quad straddles `fade <= 0.002`, the returned lanes leave the macro's LOD gradient undefined â†’ driver-dependent shimmer/garbage LOD exactly at the fade edge, and it varies across Adreno driver revisions. This directly contradicts the spec's own stated invariant. **Fix:** sample macro with `SAMPLE_TEXTURE2D_GRAD` (compute `mdx/mdy` before the branch) or `SAMPLE_TEXTURE2D_LOD(..., 0)` since it's low-frequency.

**3. Runtime degradation keywords will be stripped and/or hitch â€” and contradict the "+1 variant" budget.**
Â§6 tiers 2â€“3 flip `_NO_MACRO` / `_DETAIL_ALBEDO_ONLY` at runtime via the thermal governor, but Â§4 declares "**+1** ForwardLit variant." Two problems: (a) with `shader_feature`, any variant no material ships enabled is **stripped from the build** â†’ `Material.EnableKeyword("_DETAIL_ALBEDO_ONLY")` silently falls back to a compiled variant and does nothing; (b) even if kept, toggling a *new* variant mid-session compiles/uploads a pipeline **while the device is hot** â€” a frame spike at the worst moment. **Fix:** make runtime-toggled keywords `multi_compile_local_fragment` (never stripped) **or** enumerate every degradation combination into the `ShaderVariantCollection` and prewarm them all at boot â€” not just the single `_REGOLITH_DETAIL` variant. Update the Â§4 variant count accordingly (it's ~4â€“6, not +1).

**4. Asset-gen seam: clast stamps and Sobel aren't wrapped on the periodic domain.**
The FFT fBm (step 1) wraps exactly, but seamlessness is then broken by later steps the spec doesn't wrap: Poisson-disk clast stamping (step 2) near an edge and the Sobel normal (step 4) both read/write across the tile boundary. Under 0.75 m tiling with the camera on the ground, a seam every 0.75 m is glaring. **Fix:** stamp clasts with toroidal (modulo) coordinates and compute Sobel with `np.roll` so both wrap. Cheap, but omitted.

## SOFTER NOTES (won't block the build; worth fixing)

- **World-space UV fp32 precision at terrain extent.** Detail UV = `positionWS.xz * rcp(0.75)`. The terrain is centered on origin (`TerrainImporter.cs:142`, `Â±width/2`), so a 20 km clip reaches Â±10 000 m â†’ UV â‰ˆ 13 333, where fp32 ULP â‰ˆ 1.6 texels of a 1024Â² map. Pristine at spawn (origin), but the **entire surface is a `TeleportationArea`** (`:130`), so a POI 8 km out will show sub-texel detail swim. Naive camera-relative UV fixes precision but makes the pattern slide underfoot; the clean fix is a per-site double-precision origin offset uploaded to the shader. Low-severity, unaddressed.
- **VRAM math is internally inconsistent.** "ASTC 6Ã—6 â€¦ ~1.4 MB" for 1024Â² is wrong â€” 6Ã—6 is ~0.47 MB (+mips ~0.6 MB); 1.4 MB is the ASTC **4Ã—4** figure. Harmless (over-estimate), but the number implies a different block size than stated.
- **"BC5" is desktop-only.** On Adreno the normal ships as ASTC; "BC5-equivalent" overstates fidelity (ASTC 6Ã—6 â‰ˆ 3.56 bpp vs BC5 8 bpp). Fine for subtle crunch, but the property label `Detail Normal (BC5)` is misleading on-device.
- **Injection lands in the splat branch, not the basepass branch.** `:442` is inside the `#else` (non-`TERRAIN_SPLAT_BASEPASS`) block ending `:459`. Cleaner to place the call at `:460` (after `#endif`) so it's path-agnostic. Moot in practice (`basemapDistance=20000` â†’ basepass never runs), but tidy.

## Frame-budget assessment (the crux, honestly flagged by the spec itself)

The estimate is **order-of-magnitude plausible but unproven, and the spec is right that the look-down case is the real gate.** 3 ASTC taps Ã— ~2.2 M frags â‰ˆ 6.7 M extra fetches; at Adreno-740-class texture rates that's ~0.4â€“0.5 ms â€” consistent with the claim. Two attacks the spec under-weights:

- **ETFR doesn't help in the worst case.** The ~30% foveation saving is applied to the general estimate, but when the player pitches down to inspect regolith the fovea is *on* the detail â€” foveation reduces only the (cheap, high-mip) periphery. So the worst-case ~1 ms has less headroom than the "5.6 MP effective" framing implies.
- **Standing + look-down puts the whole floor inside `_FadeStart`.** At 1.6 m eye height the ground directly below is ~1.6 m away â€” well inside 6 m â€” so the `UNITY_BRANCH` skip saves *nothing* in exactly the framing that stresses the frame, and it lands on the same frame as near POI props on a bandwidth-bound, throttling GPU. The spec acknowledges this as its #1 unknown; that's correct, and it is genuinely only answerable with an on-device Profiler capture **camera-pitched-fully-down over each site** (not the standing-horizon view). Per CLAUDE.md, that device trace is the gate â€” treat all Â§4 ms figures as `[unverified until on-device]`, which the spec already does.

**Bottom line:** stack-fit is real and verified (URP 17.5 / Vulkan / Forward+ / per-pixel-normal terrain all check out), the free-asset claim is true, comfort is clean, and the budget is plausible with working degradation levers. It is not buildable *as-is* because Fix #1 makes it silently do nothing on the existing materials, and #2/#3/#4 are correctness bugs â€” but all four are small, local, and require no architectural change. **BUILDABLE-WITH-FIXES.**

---

## agent

**Caveat up front:** the four SPEC and VERIFICATION bodies arrived empty (single stray characters â€” `I`, `R`, `'`, etc.), so I'm reasoning from the track *names* plus Wayfinder's fixed constraints (Galaxy XR / Snapdragon XR2+ Gen2, URP+Vulkan, 72fps floor = 13.8ms stereo, hand-tracking, real-DEM terrain, persistent Bridge scene, Sites Oneâ€“Three already shipped with real orbital-photo terrains and real skies). Every ms figure below is a planning estimate `[unverified]` until we measure on-device â€” but the *ordering, conflicts, and cut list* hold regardless of the exact numbers. Send the real spec bodies and I'll tighten the ledger.

---

## 1. Recommended build order

Treat "atmospherics" as two things: **1a the lighting/sky contract** (foundational, cheap) and **1b volumetric polish** (expensive, cuttable). That split drives the order.

| # | Track | Why here |
|---|-------|----------|
| **1** | **Atmospherics â€” lighting contract only** (sun dir/color/intensity, ambient SH, exposure, Mars vs Moon sky model as skybox) | Dependency root. Regolith BRDF, rock materials, and the Bridge window all consume these values; author them once now or the tracks fight over "what colour is the sun." Near-zero incremental ms if kept analytic (skybox + no raymarch yet). |
| **2** | **Regolith-shader** | The substrate everything sits on, and the single largest fill-rate risk (whole lower hemisphere, both eyes). Build it *after* the lighting contract so its opposition-surge/backscatter BRDF is tuned against the real sun/ambient, not re-tuned twice. Prototype its worst case first (see Â§4) â€” this number sizes the whole budget. |
| **3** | **Rock-scatter** | Sits on regolith literally and in the pipeline: shares the terrain height source (rocks must sample the *same* displaced height or they float/sink), the shared lighting include, and contact AO/shadow. Cheap presence-per-ms if instanced well; can't be finalized until regolith's height/lighting contract is frozen. |
| **4** | **Starship-bridge (ultra pass)** | Most self-contained (its own persistent scene, interior geometry, holo UI, hand-tracking) so the *interaction + geometry* work can run in parallel from day one. But its **window-to-space composite must land last**, because it should frame the *finished* world look, and it's where the mutually-exclusive-context lever gets wired (see Â§3). |
| **5 (cuttable)** | **Atmospherics 1b â€” volumetrics/god-rays/dust shafts** | Last and first to be cut. Fake it (billboarded dust, analytic light-shaft, baked) before you consider any raymarch on a mobile tiler. |

Presence-per-ms ranking (what each ms buys): regolith â‰ˆ Bridge (constant framing) > rock-scatter > Mars atmo >> Moon atmo (near-zero â€” no air) >> volumetrics.

---

## 2. Cross-track conflicts and how to sequence around them

1. **Shared fragment/fill budget (regolith âŠ— atmospherics).** Both are full-screen. On a tile-based Adreno, overdraw is the killer. â†’ Sky renders as **skybox behind opaque depth**, never as a full-screen post *over* the ground. Regolith writes depth early (front-to-back / depth-priming) so atmo dust doesn't overdraw solid ground. Settle exposure/tonemap jointly in track 1.
2. **Terrain-layer / detail-instancing contention (regolith âŠ— rock-scatter).** A custom URP terrain material can break Unity Terrain's detail/tree instancing path, and rock placement must sample the *same* heightmap the regolith displaces. â†’ **Freeze the terrain material contract first** (heightmap, normal, splat source). Rocks read that exact height source. Prefer a **standalone GPU indirect instancer** over Terrain "trees" for cull/LOD control â€” don't route rocks through the same system that's fighting the custom material.
3. **Lighting-model / shader-pass collision (all four).** Regolith's opposition/backscatter term, rock materials, and the Bridge exterior view must share **one lighting include** (sun, ambient SH, opposition function). Two competing sky/ambient sources = rocks that don't sit in the world. â†’ One `.hlsl` include authored in track 1, consumed everywhere.
4. **Shadow-atlas contention.** Rock shadows + terrain self-shadow + Bridge all want the directional shadow map. Moon needs razor-hard, very long shadows (bias/normal-offset tuning); Mars soft. â†’ One shared single-cascade directional atlas, tuned per-world via the World Package, sequenced right after lighting.
5. **Warp/transition ownership (Bridge âŠ— worlds).** Per architecture, worlds load *behind* the warp and the Bridge/travel state machine owns the transition. â†’ Keep transition code out of the per-track shaders; no track drives its own fade.

---

## 3. Combined frame-budget ledger â€” does it fit at 72fps?

**Not naively.** But it fits with the disciplined build, because of one architectural lever: **Bridge-context and full-surface-context are mutually exclusive at full fidelity.** When you've teleported to the surface, the Bridge isn't rendered; when you're on the Bridge, the world outside the window is a low-detail impostor/skybox, not a full world render. So there are really two ledgers, and neither pays the other's full cost.

**Worst case = surface walk on Mars** (target â‰¤13.8ms, leave ~15% spike headroom â†’ aim ~11.7ms of *content*):

| Item | Naive | After discipline |
|------|------:|-----------------:|
| Compositor / URP overhead / foveation resolve | 2.0 | 2.0 |
| Regolith (full lower hemisphere) | 3.5 (POM naive â†’ 6+) | **2.0** (POM near-camera only, distance-faded, 2-sample; height-blend beyond) |
| Rock-scatter | 2.0 | **1.5** (impostors >8m, GPU frustum cull) |
| Atmospherics (Mars sky + dust) | 2.0 | **1.5** (skybox + billboard dust, no raymarch) |
| Sun shadows (1 cascade) | 1.0 | 1.0 |
| Post (tonemap/foveation) | 1.0 | 1.0 |
| Bridge | â€” | **0** (not rendered on surface) |
| **Total** | **~11.5â€“14+** | **~9.0ms** |

Moon is cheaper still (atmo â‰ˆ 0). **Bridge-context** ledger: interior geo + holo UI (~2.5ms) + window impostor world (~2.0ms) + overhead â‰ˆ well under budget.

**Verdict: yes at 72, with margin â€” provided the cuts below are treated as design, not fallback.**

**Standing cut list (in cut order):**
1. Atmospheric **volumetrics/god-rays** â†’ analytic/baked/billboard. (First to go, biggest single risk.)
2. Regolith **POM** â†’ parallax only within a few metres of the camera, height-blend elsewhere; hard-cap texture samples.
3. Rock **draw distance/density** â†’ tighten impostor crossfade distance before adding geometry LODs.
4. Never let Bridge + full world render simultaneously â€” enforce the mutually-exclusive-context switch. This is the load-bearing assumption; if it breaks, nothing else fits.

---

## 4. Top 3 things to prototype first (de-risk)

1. **Regolith worst-case fill test, on-device.** Full-screen regolith material with POM + opposition/backscatter term at full per-eye res (1856Ã—2160 Ã—2), Vulkan, foveation on. Measure ms. **This single number sizes the entire budget** â€” if POM is unaffordable here, tracks 2â€“4 re-plan around it. Do this before writing any rock or Bridge polish.
2. **Rock-scatter instancing + height-match.** Target-density GPU-instanced rocks with impostor crossfade and GPU frustum cull; verify (a) draw-call/vertex/overdraw hold on-device (rock silhouettes = overdraw), and (b) rocks sit exactly on the *displaced* regolith height â€” proves the shared-height-source contract before it's baked into a package.
3. **Combined Mars worst-case frame + Bridge-window composite.** One scene with pink sky + dust + regolith + rocks to validate the Â§3 ledger sum and confirm the atmo-cut decision; plus the Bridge window looking out at that world (impostor) through the warp, to prove the **mutually-exclusive-context** lever and that lighting reads continuously from Bridge â†’ surface. This is the direct test of task question (3): "can it coexist at 72."

Gate rule stands: **none of this ships until Site One holds 72+ fps on the real headset** â€” Direct Preview and the editor are never framerate evidence.
