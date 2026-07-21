---
name: frame-budget-report
description: Turn an android-xr-perf profiling pass into a standard pass/fail report against the 72/90 fps floor, for the Site One gate and every perf pass after it. Use after capturing Unity Profiler/Frame Debugger/Memory Profiler data on the real headset.
---

# Frame budget report

Standardize the output of the `android-xr-perf` drill so passes are comparable over time and the Site One gate has a single yes/no.

## Inputs required (refuse to report without them)

- A **development build on the real Galaxy XR over adb** — not editor, not Direct Preview, not emulator. Those are not framerate evidence per CLAUDE.md.
- Unity Profiler capture (CPU/GPU/GC lanes), Frame Debugger draw-call count, Memory Profiler (allocations/GC spikes).
- What changed since the last capture (one-variable-at-a-time, per the perf drill).

## Report format

```
Frame Budget Report — <date> — <scene/site>
Device: Galaxy XR, build <dev/release>, CPU/GPU locked: <yes/no>

Measured: __ ms/frame avg (target 13.8ms @72Hz / 11.1ms @90Hz)
Bound: CPU / GPU / balanced
Draw calls: __
GC allocs this frame: __ B (target: 0 in steady state)

PASS/FAIL @ 72 fps: 
PASS/FAIL @ 90 fps (stretch):

Top 3 costs (ranked):
1. ...
2. ...
3. ...

Next optimization (cheapest-first per android-xr-perf order):
```

## Gate check

For Site One specifically: state plainly whether the **72+ fps bar is met on the real headset**, per the build plan ([docs/plans/2026-07-20-wayfinder-v1.md](../../docs/plans/2026-07-20-wayfinder-v1.md)). Do not soften a fail. Do not accept an editor/Direct Preview number as gate evidence.
