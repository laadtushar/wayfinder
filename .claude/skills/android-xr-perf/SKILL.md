---
name: android-xr-perf
description: The Android XR (Galaxy XR) frame-budget drill for Wayfinder — how to profile and optimize toward 72/90 fps on the Snapdragon XR2+ GPU. Use for any performance pass, before the on-headset framerate gate, or when a scene drops frames.
---

# Android XR performance drill

Galaxy XR runs a mobile Snapdragon XR2+ Gen 2. The budget is **~13.8 ms/frame at 72 Hz, ~11.1 ms at 90 Hz, in stereo.** Real elevation-mesh terrain is vertex- and draw-call heavy, exactly what blows this budget. There is no "AI profiler" — the instruments are Unity's, and Claude reads their output (via the Unity MCP bridge or logs) to suggest fixes. Follow the same drill every time so numbers are comparable.

## Before profiling

- **Lock CPU/GPU levels** on the device before capturing, or your numbers jitter and comparisons are meaningless.
- Profile a **development build on the real headset over adb**, not in the editor or emulator (the emulator does not reproduce GPU timing).
- Change one thing at a time; capture before and after.

## Measure

1. **Unity Profiler** (attach over USB/Wi-Fi): is the frame CPU-bound or GPU-bound? Look at the render, script, and GC lanes.
2. **Frame Debugger:** per-draw-call breakdown — draw-call count and what's expensive.
3. **Memory Profiler:** catch leaks and GC spikes (any per-frame allocation shows here).
4. If Unity says "GPU-bound" but not why, escalate to the **Qualcomm Snapdragon Profiler** for Adreno-level detail (overdraw, bandwidth, shader stalls). Steeper curve; only when you have a specific GPU bottleneck.

## Optimize (in this order, cheapest first)

1. **Enable eye-tracked foveated rendering** (Project Settings > XR Plug-in Management > OpenXR > Foveated Rendering). Biggest single GPU lever for a fragment-bound terrain scene, and Galaxy XR's eye tracking makes it dynamic.
2. **Right-size the terrain:** heightmap resolution, terrain pixel error, basemap distance, and LOD. Dense terrain doesn't need max vertices to read well.
3. **Cut draw calls and overdraw:** batching, fewer materials, watch transparent/overlapping geometry.
4. **Bake static lighting;** no realtime shadows you can avoid.
5. **Kill per-frame allocations** (see the unity-reviewer agent) — GC hitches read as dropped frames.
6. Only if still short: **Application SpaceWarp** (render half-rate, synthesize frames) — and only with correct motion vectors, or dense geometry artifacts.

## The gate

Per the build plan: **Site One must hold 72+ fps on the real Galaxy XR before any other site is built.** Confirm the actual measured number; do not claim it from the editor. Do not chase Meta/Quest perf tools (OVR Metrics, Meta XR Simulator) — wrong platform.
