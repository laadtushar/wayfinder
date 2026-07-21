---
type: community
cohesion: 0.22
members: 13
---

# World Package & Companion Seam

**Cohesion:** 0.22 - loosely connected
**Members:** 13 nodes

## Members
- [[Android XR  Galaxy XR Platform Guide]] - document - docs/ANDROID-XR-PLATFORM.md
- [[FieldLog_1]] - code - core/Wayfinder.Core/FieldLog.cs
- [[FieldLogTests_1]] - code - core/Wayfinder.Core.Tests/FieldLogTests.cs
- [[Gemini In-World Companion]] - document - IDEATION.md
- [[POI Content Contract (per-site JSON, 5-8 POIs)]] - document - docs/specs/2026-07-21-terrain-and-poi-content.md
- [[Persistent Bridge Hub]] - document - docs/ARCHITECTURE.md
- [[PointOfInterest Record]] - document - docs/ARCHITECTURE.md
- [[Rationale Headset Renders Worlds, Never Builds Them Live]] - document - docs/ARCHITECTURE.md
- [[Rationale No Splat Renderer in v1 (removes riskiest dependency)]] - document - docs/ARCHITECTURE.md
- [[Rationale POI Data as the v1.1 Gemini Companion Seam]] - document - docs/ARCHITECTURE.md
- [[Splat Diorama-Per-Scene Pattern (bounded scene swapped at travel)]] - document - IDEATION.md
- [[Wayfinder.Core Module README]] - document - core/README.md
- [[World Package (worlds as data, not code)]] - document - docs/ARCHITECTURE.md

## Live Query (requires Dataview plugin)

```dataview
TABLE source_file, type FROM #community/World_Package_&_Companion_Seam
SORT file.name ASC
```

## Connections to other communities
- 6 edges to [[_COMMUNITY_Docs & Terrain Pipeline]]
- 2 edges to [[_COMMUNITY_Core Travel Loop]]
- 1 edge to [[_COMMUNITY_XR Quality & Perf Gates]]

## Top bridge nodes
- [[FieldLog_1]] - degree 8, connects to 3 communities
- [[World Package (worlds as data, not code)]] - degree 6, connects to 1 community
- [[Gemini In-World Companion]] - degree 4, connects to 1 community
- [[Rationale Headset Renders Worlds, Never Builds Them Live]] - degree 3, connects to 1 community
- [[Rationale No Splat Renderer in v1 (removes riskiest dependency)]] - degree 2, connects to 1 community