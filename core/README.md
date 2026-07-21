# Wayfinder.Core — engine-free game logic

The pure C# heart of the game, written and tested without Unity so it could be built (and verified) before the Unity project exists. 24 NUnit tests, run with `dotnet test Wayfinder.Core.Tests` from this folder.

What lives here and why it is engine-free on purpose:

- `TravelStateMachine` — guards the bridge → warp → surface → return loop (no double-warp, no completing a warp that isn't happening). Build-plan Task 1.2.
- `FieldLog` — discover-once tracking for points of interest; `Discover` returns true only the first time so the UI can celebrate fresh discoveries. Task 2.5.
- `WorldDefinition` + `WorldRegistry` — pure data for a visitable world (id, display name, scene, real gravity) and ordered lookup driving the viewscreen list. Task 1.1.
- `InteractionTargets` — the Android XR quality-checklist target-size formula (distance × 0.868 × 48dp min / 56dp recommended) so POI markers and bridge controls are sized to pass review.

## Migration into the Unity project (Phase 1 on Windows)

1. Copy the four files in `Wayfinder.Core/` into `unity/Assets/Scripts/Core/`.
2. Copy the test files in `Wayfinder.Core.Tests/` into `unity/Assets/Tests/EditMode/` (they use NUnit 3.x classic asserts, which is what Unity Test Framework runs — no edits needed; give the EditMode folder its own `.asmdef` referencing the Core scripts).
3. The `.csproj` files stay behind; Unity has its own build. Keep this folder until the Unity copies are compiling and the tests are green in Unity's Test Runner, then delete `core/` in the same commit that lands them (one source of truth).
4. The `WorldPackage` ScriptableObject (Task 1.1) holds a `WorldDefinition` (or mirrors its fields) plus Unity-only references (scene asset, ambient audio, POI positions); registry/travel logic consumes only `WorldDefinition` and stays untouched.

Deviations from the build plan, both deliberate:
- The travel machine has four states (separate `WarpingToSurface` / `WarpingToBridge`) instead of the plan's three — the return leg needs its own guarded transitions.
- `WorldRegistry` operates on plain `WorldDefinition` rather than the ScriptableObject so it stays testable off-engine; the plan's ScriptableObject test becomes a thin wrapper test in Unity.
