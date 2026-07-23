# Android XR platform-feature audit + integration map (#19)

Produced by a 15-agent research workflow (wf_3ca6d0cc-35d), grounded against
`docs/ANDROID-XR-PLATFORM.md` (Google's priority order) and the live feature
state in `unity/Assets/XR/Settings/OpenXR Package Settings.asset`. Stack:
Unity 6000.5.4f1, URP 17.5, Vulkan, OpenXR + `com.unity.xr.androidxr-openxr`
1.3.1, XRI 3.5.1, XR Hands; Galaxy XR (Snapdragon XR2+ Gen 2); 72 fps floor,
hands-first, eye-tracked foveation mandated.

## 1. Feature verdicts

| Domain | Feature (extension) | Verdict | Why |
|---|---|---|---|
| Eye-tracked foveation | Eye-tracked foveated rendering + Eye Gaze (`XR_EXT_eye_gaze_interaction`) | **ADOPT NOW** | The #1 GPU lever + master 72 fps constraint. Foveation API is on but Eye Gaze profile is OFF, so it runs as *fixed* foveation — half-used. |
| Perf levers | Late Latching, Optimize Buffer Discards, Vulkan Subsampling, Performance Metrics | **ADOPT NOW** | These are the frame budget. Late latching + buffer discards are "free, no drawbacks" (Google) and both OFF today (`m_latencyOptimization: 0`, `m_optimizeBufferDiscards: 0`); Perf Metrics OFF, so the fps gate can't be read on device. |
| System integration | Session-resume lifecycle, permission plumbing, boundary, recenter, GameActivity | **ADOPT NOW (subset)** | Store-readiness spine: session-resume (April 2026 behavior), permission-denial fallback, 2 s cold-start ceiling all gate placement. |
| Hand tracking | Joint subsystem (`XR_EXT_hand_tracking`) = NOW; system hand **mesh** (`XR_ANDROID_hand_mesh`) = LATER | **SPLIT** | Interaction profile (pinch/aim) already on; full joint subsystem OFF and needed for richer interaction + glove visuals. Raw mesh heavier, beaten by joint-rigged gloves — defer. |
| Passthrough / MR | AR camera / passthrough | **SKIP** | Wayfinder is opaque Full Space — you stand on Mars, not your living room. System boundary passthrough is automatic, no app work. |
| Spatial anchors | ARAnchor + persistence | **SKIP** | Content is world-locked to a play origin, seated/standing 2 m. (But *unbounded reference space* — different feature — is worth adopting for large terrain; see ticket 7.) |
| Depth / occlusion | AR occlusion (`XR_ANDROID_depth_texture`) | **SKIP** | Occlusion matters only when compositing behind real furniture; an immersive game renders its own depth. Pure cost. |

All AR/MR features (Camera, Plane, Anchor, Occlusion, Scene Mesh, Bounding Box,
Face) are confirmed **disabled** — the correct posture. Do not re-enable them.

## 2. ADOPT-NOW tickets (presence-value-per-effort)

1. **Flip the free perf wins** — enable Late Latching + Optimize Buffer Discards on the active Android OpenXR block. Zero-drawback per Google. **S**, no deps.
2. **On-device measurement** — enable Android XR Performance Metrics + XR Performance Settings; wire the fps/thermal readout into the frame-budget report. **S**, no deps.
3. **Eye-tracked foveation (make it real)** — enable Eye Gaze Interaction profile + SRP foveation runtime call + Vulkan Subsampling; convert fixed → eye-tracked foveation. **M**, dep #4.
4. **Permission + lifecycle spine** — runtime request for HAND_TRACKING + EYE_TRACKING_FINE with graceful denial → permissionless pinch/aim + fixed foveation; OpenXR session-resume; confirm GameActivity + Resizeable; map recenter. **M**, unblocks 3/5/6.
5. **Gaze + pinch interaction** — add XRI `XRGazeInteractor`; gaze-hover POIs/UI + pinch-to-select (less arm fatigue than hand-ray). **M**, deps #3,#4.
6. **Full hand-joint subsystem** — enable Hand Tracking Subsystem (only the interaction profile is on) so joint data drives interaction + glove visuals. **S/M**, dep #4.
7. **Unbounded reference space** — add via the Android XR Extensions package so head tracking doesn't degrade far from origin on large terrain. **M**, dep: Extensions pkg.

Land order: **1 → 2 → 4 → 3 → 5 → 6 → 7**. Tickets 1, 2, 4 are doable before the next on-device build.

## 3. Top 3 platform risks

1. **Thermal throttling on sustained play.** Real orbital-photo terrains + real star skies are bandwidth-heavy; XR2+ Gen 2 sustains below its burst clock — a 2-minute spot check can pass and 15 minutes fail. De-risk: land tickets 1+3 now; Perf Metrics (2) for on-device thermal readout; thermal quality tiers (mip bias, foveation strength, dynamic refresh); **make the Site One gate a 10-minute sustained run, not a spot check**.
2. **Permission-gated features failing silently.** Eye-tracked foveation + gaze need EYE_TRACKING_FINE; full hand joints need HAND_TRACKING. Deny → foveation reverts, gaze dies. De-risk: treat the already-on Hand Interaction Profile + fixed foveation as the guaranteed floor; ticket 4 onboarding works either way; test the denied path on device; never hard-require a permissioned feature in the core loop.
3. **Over-adoption drag (MR stack / SpaceWarp).** Passthrough/anchors/occlusion cost TDP + permissions + review surface for zero immersive-game value; SpaceWarp smears against a real-sky shader. De-risk: keep all AR features off; declare only the opaque blend mode + only permissions used; gate SpaceWarp behind a per-world on-device A/B after the Site One gate — opt-in, never global.

## 4. What Wayfinder already does right

- **Dead-center in Google's lane:** unmanaged Full Space, Unity 6 + OpenXR + URP + Vulkan, **Single Pass Instanced on** (`m_renderMode: 1`), foveation API on.
- **MR stack correctly OFF** — no passthrough/anchor/occlusion/scene-mesh enabled. Most teams leave these on by accident and pay frames + permission surface for nothing.
- **Hands-first, denial-proof:** the permissionless Hand Interaction Profile is enabled — playable without controllers, survives a permission denial.
- **Comfort model correct:** warp = fade (never fly the camera), teleport/world-grab/snap-turn, no continuous rotation, 2 m radius.
- **Architecture platform-justified:** light bridge + additive World Packages + Play Asset Delivery answers the 2 s cold-start ceiling; docs account for the April 2026 session-resume behavior.

**Net gap:** the platform's biggest GPU lever (eye-*tracked* foveation) and its native input (gaze+pinch) are the two highest-value things still on the floor, plus three free perf toggles (late latching, buffer discards, perf metrics). Tickets 1–5 close all of them and are mostly S/M.
