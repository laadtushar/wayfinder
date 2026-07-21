# Android XR / Galaxy XR platform guide

What Google officially recommends, what shipped apps actually do, and which open-source repos to learn from. Compiled July 2026 from the primary docs (14 pages on developer.android.com fetched directly), shipped-app teardowns, and GitHub-verified repos. Everything cited; anything inferred is marked [unverified].

The one headline: **there is no Samsung developer surface.** Samsung's developer portal has no Galaxy XR docs at all. developer.android.com/xr is the single official channel, distribution is normal Google Play, and the Galaxy XR is a stock Android XR device. Everything below IS the Galaxy XR guidance.

---

## 1. How Google says to build (and what it means for us)

**Our lane is exactly the recommended one.** Unity/OpenXR apps run in "unmanaged Full Space": one app owns the whole 3D space. Home Space (flat panels side by side) is for 2D apps only. A custom immersive game is exactly what Google routes to Unity + OpenXR. The platform SDK is still Developer Preview 4, so pin package versions and re-check docs at each upgrade. ([overview](https://developer.android.com/develop/xr), [foundations](https://developer.android.com/design/ui/xr/guides/foundations))

**The day-one Unity build recipe** ([setup](https://developer.android.com/develop/xr/unity/setup)):

- Unity **6000.3.5f2 or newer** (our floor: Direct Preview needs 6000.3.5f2+, built-in crash Diagnostics needs 6.2+; 6000.3.x satisfies both).
- **Vulkan as the only graphics API**, listed first. URP with **HDR off** on the URP Asset and **post-processing off** on the Renderer Data.
- Minimum API level 24 (the OpenXR loader hard-fails below it).
- Entry point **GameActivity, not Activity**, and **Resizeable Activity on**. Without the latter, the hand/eye-tracking permission dialogs cannot display. These two are the easy-to-miss items that break things later.

**Package split** ([Unity guide](https://developer.android.com/develop/xr/unity)): the base is `com.unity.xr.androidxr-openxr` (planes, anchors, occlusion, eye-tracked foveation, raycasts). Add "Android XR Extensions for Unity" only for specific features: persistent anchors, hand mesh, **unbounded reference space** (matters for large terrain so tracking does not degrade far from the start point). Input permissions split: basic pinch/aim interaction needs **no** runtime permission; full hand-joint tracking needs `android.permission.HAND_TRACKING`; eye gaze needs `android.permission.EYE_TRACKING_FINE`. Design onboarding to survive a denial by falling back to the permissionless profile.

**The performance stack, in priority order** ([foveation](https://developer.android.com/develop/xr/unity/performance/openxr-feature-settings), [extensions perf](https://developer.android.com/develop/xr/unity/performance/androidxr-extension-settings), [GPU rendering](https://developer.android.com/develop/xr/unity/performance/gpu-rendering)):

1. **Late Latching**: always on, no drawbacks (cuts head-pose latency ~a full frame; small C# hookup).
2. **Eye-tracked foveated rendering**: the biggest GPU lever. Needs the feature group + SRP Foveation + Eye Gaze profile + the EYE_TRACKING_FINE permission + a runtime C# call. Wire all three early; retrofitting the permission touches onboarding.
3. **Vulkan Subsampling**: pairs with foveation, cuts peripheral resolution.
4. **Optimize Buffer Discards**: enable unconditionally.
5. **GPU Resident Drawer** (requires Forward+; adopt Forward+ from day one) for terrain scattered with repeated rocks/props, plus **GPU occlusion culling** (helps the bridge interior more than open terrain).
6. **SpaceWarp last and per-world**: every shader must write motion vectors; a real-sky planet shader is exactly the content that smears under it. Explicit DON'T from Google: leave Front-to-Back Rendering disabled.

**The quality checklist is the contract** ([quality guidelines](https://developer.android.com/docs/quality-guidelines/android-xr)): frame time under 13.8 ms at 72Hz (11.1 ms at 90Hz); at least 1856x2160 per eye; cold start under 2 s; ~1% crash ceiling; "don't rotate camera over time — snap to new orientation instead"; fully playable within a 2.0 m radius (declare `XR_BOUNDARY_TYPE_LARGE`; system passthrough fades in at 1.5 m); hand input as a baseline (playable without controllers); interactive target size = `DistanceInMeters x 0.868 x 48` minimum (a target 3 m away needs ~12.5 cm). The 2 s cold-start ceiling plus heavy terrain assets is why the bridge boots light and World Packages load additively: the checklist justifies our architecture.

**Spatial UI numbers for the bridge** ([spatial UI](https://developer.android.com/design/ui/xr/guides/spatial-ui), [motion](https://developer.android.com/design/ui/xr/guides/motion)): spawn panels at 1.75 m (valid 0.75 to 5 m), centered 5 degrees below eye line, keep content within the central 41 degrees of view; font 14dp+. Never spawn a POI card closer than 0.75 m. Motion rules: user-initiated motion only, fade-out/fade-in for large moves (the warp = fade, never fly the camera), a comfort settings menu (vignette strength, snap angle) is expected by the guidelines, and beware large moving set pieces (players read big movers as self-motion). Do NOT bind any game action to a palm-inward pinch on the dominant hand: that is the reserved system-menu gesture.

**Publishing** ([package & distribute](https://developer.android.com/develop/xr/package-and-distribute)): XR-only app on the dedicated Android XR release track, `<uses-feature android:name="android.software.xr.api.openxr"/>`, Android App Bundle, and **Play Asset Delivery for World Packages** (ship the bridge in the base module, deliver worlds on demand). One platform behavior that changes hub design: since the April 2026 update, **session resume** relaunches the app where the user left off, so the bridge must serialize and restore cheaply (current world, position, discovery progress) rather than assuming a cold boot to a title screen.

---

## 2. What shipped apps teach us

The market context first, honestly: the install base is tiny (community estimates ~70k units lifetime by late 2025 [unverified]) and the catalog is ~100+ immersive apps. Ship for craft, featuring potential, and the empty niche, not revenue, and keep everything on plain OpenXR/XRI abstractions so a Quest port stays a port, not a rewrite. **No shipped title does real-terrain exploration with game structure.** BRINK is passive, Maps is a tool, Green Hell is fiction. The niche is empty as of mid-2026. ([UploadVR catalog tracker](https://www.uploadvr.com/samsung-galaxy-xr-android-xr-games/))

**The shipped stack is our stack.** Unity's case study of the four launch studios (Owlchemy, Resolution Games, TRIPP, Litesport) converges on: Unity 6, OpenXR, URP, Vulkan, XR Hands, XRI, start from the official samples. The one pain every porter hit was converting built-in-pipeline shaders to URP; as a greenfield project we dodge it by authoring every shader URP-native from day one. ([Unity case study](https://unity.com/blog/porting-apps-games-over-android-xr-unity-6))

Per-app lessons:

- **[BRINK Traveler](https://www.vrvoyaging.com/review/brink-traveler/)** — the closest shipped analog, and it validates the World Package concept: ~1 GB per-location downloads with an in-headset progress bar, free-walk + teleport + snap/smooth turn on uneven terrain, discovery via national-park-style info boards + narration + find-the-POI collectibles. Copy: the wrist UI (settings, day/night), the hand compass for open terrain, the photo/scrapbook mode as a retention hook. Their reviewer flagged an unintuitive photo gesture: test gestures on outsiders early.
- **[Google Maps Immersive View](https://www.uploadvr.com/android-xr-google-maps-best-galaxy-xr/)** — pinch-grab world-drag is now the platform-native convention for large-scale travel; Wayfinder's world-grab should feel like Maps or it will feel wrong. Vignette during fast traversal is the accepted comfort answer. Its fly-then-land loop proves our warp-to-terrain fantasy is comfortable on this exact hardware; our differentiation is the on-foot guided-discovery layer Maps lacks.
- **[Inside [JOB]](https://www.uploadvr.com/inside-job-aims-to-onboard-android-xr-users-with-a-new-mixed-reality-game/)** (Owlchemy) — play it first: it is the platform's own definition of gesture vocabulary (pinch, poke, gaze+pinch). Hands-only is viable, but Owlchemy spent years on edge cases; a solo dev leans on XRI defaults and forgiving hit targets, not custom gesture recognizers.
- **Green Hell VR** — the closest sustained terrain-walking example, and its reviews are a warning: a single fixed walk speed makes fine positioning on slopes miserable, and downhill grades cause comfort complaints. Wayfinder's guided routes should follow contours, offer variable speed, and treat teleport as a first-class equal.
- **Thrasher** — 90fps is achievable on this hardware, but by a visually abstract game. For real terrain, 72Hz + dynamic refresh + foveation is the honest budget; reserve 90 for the bridge.
- **Oh My Galaxy!** ($9.99, nDreams) — the template of a post-launch indie release: tightly scoped, hand-native, ~$10. Also evidence that one signature physical gesture (their asteroid slingshot; our warp lever) carries an XR game's identity.
- **[Museum of All Things](https://github.com/m4ym4y/museum-of-all-things)** — a solo-ish dev shipped an open-source educational exploration app on this store. Structurally our loop: a real data source (Wikipedia; ours is NASA/USGS) rendered as walkable discovery space. Godot, so take architecture and pacing lessons, not code.

---

## 3. Open-source repos worth cloning (all GitHub-verified July 2026)

Mapped to the subsystem each informs. First three are the priority.

| Repo | License | What it gives us | Subsystem |
|---|---|---|---|
| [android/xr-unity-samples](https://github.com/android/xr-unity-samples) | Apache-2.0 | Google's official showcase: 16 samples in one Unity project — hand tracking/gestures/mesh, eye tracking, scene meshing, performance metrics ("Drone" sample = on-device fps/thermal readout), **and a working Gemini sample (camera + speech-to-text + Gemini + TTS on-device)**. Also the ground truth for package versions and project settings. | Everything; clone first. Gemini sample de-risks v1.1 |
| [android/android-xr-unity-package](https://github.com/android/android-xr-unity-package) | Apache-2.0 | The Extensions package source + per-feature samples: unbounded reference space (large terrain), "Recommended Settings" (Google's own perf defaults), hand mesh. Its issues tab is Google's stated support channel. | Platform wiring, perf defaults |
| [Unity-Technologies/XR-Interaction-Toolkit-Examples](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples) | Unity Companion | Working teleport/grab/locomotion/gaze scenes to copy settings from (reticles, snap turn, comfort). Dissect the 3-4 stations we need, not the whole tour. | Locomotion, interaction |
| [android-xr/android-xr-interaction-framework-unity-package](https://github.com/android-xr/android-xr-interaction-framework-unity-package) (AXRIF) | Apache-2.0 | New (June 2026): system-consistent automatic input transitions (hands/gaze+pinch/controllers/mouse) layered ON XRI, not replacing it. Very new; pin the version you validate. | Input mode switching |
| [UnityTechnologies/open-project-1](https://github.com/UnityTechnologies/open-project-1) "Chop Chop" | Apache-2.0 | The closest existing implementation of our architecture: persistent-managers scene + additively loaded locations + ScriptableObject event channels + a loading screen covering the swap. Maps one-to-one to bridge + World Package + warp. Old (2023) but design-level. | Bridge hub + warp |
| [Unity-Technologies/Addressables-Sample](https://github.com/Unity-Technologies/Addressables-Sample) | [unverified license] | Load/release patterns for streaming a World Package as an Addressables group without the memory leaks that kill a headset session; the path to worlds-as-DLC. | World Package streaming |
| [m4ym4y/museum-of-all-things](https://github.com/m4ym4y/museum-of-all-things) | MIT | Shipped Android XR exploration app with full source: how it streams/paces external-data content into walkable space on mobile XR. Godot: patterns, not code. | POI/discovery pacing |
| [SebLague/Procedural-Landmass-Generation](https://github.com/SebLague/Procedural-Landmass-Generation) + [Solar-System](https://github.com/SebLague/Solar-System) | MIT | Chunked terrain + distance LOD + threaded meshing; per-body gravity and sky. The fallback pattern if Unity's built-in terrain LOD falls short, and the gravity/sky reference for per-world physics. | Terrain, world physics |
| [firebase/quickstart-unity](https://github.com/firebase/quickstart-unity) (`firebaseai` sample) | Apache-2.0 | Official Firebase AI Logic (Gemini) from Unity without shipping an API key. Pair with the xr-unity-samples Gemini sample for the full voice-companion pipeline. | Gemini companion (v1.1) |
| [Jonek2208/heightmap-generator](https://github.com/Jonek2208/heightmap-generator) | MIT | Stale (2019) but documents the exact target format: 16-bit RAW at 2^n+1 for Unity terrain. Our GDAL pipeline supersedes it; keep as reference. | Terrain pipeline |
| [Fewes/TerrainPrettifier](https://github.com/Fewes/TerrainPrettifier) | **NO LICENSE** | GPU cleanup for satellite/DEM terrain (denoise, ridge preservation). **No license file = all rights reserved: study which filters matter, reimplement, never copy code.** | Terrain cleanup |
| [aras-p/UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting) | MIT | The v2 splat renderer (already in the catalog). Mobile framerate on Galaxy XR remains the open question; device spike before promising the feature. | v2 splats |
| [android/xr-samples](https://github.com/android/xr-samples) (Jetpack, Kotlin) | Apache-2.0 | Secondary only: Jetpack Compose XR samples. Not our stack; skim "Personal Museum" for POI presentation ideas. | Reference only |
| oculus-samples (TheWorldBeyond, Discover) | MIT + SDK caveats | Tertiary: whole-app structure polish of shipped-quality XR titles. Meta SDK layer does not transfer to Android XR; do not port interaction code. | Reference only |

Honest gap: a GitHub sweep for community Galaxy XR projects found only tiny 0-star repos. As of July 2026 there is no significant community Galaxy XR open-source game to dissect; the official samples above are the corpus.
