# Wayfinder.Core — engine-free game logic

The pure C# heart of the game, written and tested without Unity so it could be built (and verified) before the Unity project exists. 24 NUnit tests, run with `dotnet test Wayfinder.Core.Tests` from this folder.

> **Migrated (Phase 1, ticket #1).** The sources now live in the embedded Unity package `unity/Packages/com.wayfinder.core/` (`Runtime/` + `Tests/Editor/`) — a single source of truth compiled by **both** the Unity editor (asmdef, `noEngineReferences: true`) and the thin `.csproj` shells in this folder (`<Compile Include=...>` reaching into the package). The same 24 tests run green in the Unity Test Runner and via `dotnet test`; the dotnet path stays because it is seconds-fast and headless-verifiable.

What lives here and why it is engine-free on purpose:

- `TravelStateMachine` — guards the bridge → warp → surface → return loop (no double-warp, no completing a warp that isn't happening). Build-plan Task 1.2.
- `FieldLog` — discover-once tracking for points of interest; `Discover` returns true only the first time so the UI can celebrate fresh discoveries. Task 2.5.
- `WorldDefinition` + `WorldRegistry` — pure data for a visitable world (id, display name, scene, real gravity) and ordered lookup driving the viewscreen list. Task 1.1.
- `InteractionTargets` — the Android XR quality-checklist target-size formula (distance × 0.868 × 48dp min / 56dp recommended) so POI markers and bridge controls are sized to pass review.

## How the migration actually landed (differs from the original copy-then-delete plan)

Instead of copying files into `Assets/` and deleting `core/`, the sources moved into an **embedded Unity package** and both build systems compile them in place:

- `unity/Packages/com.wayfinder.core/Runtime/` — the four logic files + `Wayfinder.Core.asmdef` (`noEngineReferences: true` keeps them provably engine-free).
- `unity/Packages/com.wayfinder.core/Tests/Editor/` — the four test files + a test asmdef; `"testables"` in `unity/Packages/manifest.json` surfaces them in the Unity Test Runner.
- `core/Wayfinder.Core*/*.csproj` — thin shells whose `<Compile Include>` pulls the package sources, so `dotnet test` stays the fast headless verification path.

The `WorldPackage` ScriptableObject (Task 1.1) holds a `WorldDefinition` plus Unity-only references (scene asset, ambient audio, POI positions); registry/travel logic consumes only `WorldDefinition` and stays untouched.

Deviations from the build plan, both deliberate:
- The travel machine has four states (separate `WarpingToSurface` / `WarpingToBridge`) instead of the plan's three — the return leg needs its own guarded transitions.
- `WorldRegistry` operates on plain `WorldDefinition` rather than the ScriptableObject so it stays testable off-engine; the plan's ScriptableObject test becomes a thin wrapper test in Unity.
