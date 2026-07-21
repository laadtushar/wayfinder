---
type: community
cohesion: 0.20
members: 11
---

# XR Quality & Perf Gates

**Cohesion:** 0.20 - loosely connected
**Members:** 11 nodes

## Members
- [[Android XR Quality Checklist]] - document - docs/ANDROID-XR-PLATFORM.md
- [[Comfort and Locomotion Rules]] - document - DESIGN.md
- [[Frame Budget 72 fps minimum  90 target]] - document - CLAUDE.md
- [[InteractionTargets_1]] - code - core/Wayfinder.Core/InteractionTargets.cs
- [[InteractionTargetsTests_1]] - code - core/Wayfinder.Core.Tests/InteractionTargetsTests.cs
- [[Interactive Target Sizing Rule (distance x 0.868 x 48dp)]] - document - docs/ANDROID-XR-PLATFORM.md
- [[Rationale Engine-Free Core, Built and Tested Before Unity Exists]] - document - core/README.md
- [[Rationale Prove Site One Before Authoring Three Worlds]] - document - docs/plans/2026-07-20-wayfinder-v1.md
- [[Site One Framerate Gate (Checkpoint 2)]] - document - docs/plans/2026-07-20-wayfinder-v1.md
- [[Three-Tier Iteration Loop (Simulator, Direct Preview, device build)]] - document - docs/SETUP-ROADMAP.md
- [[Windows-Primary Setup Roadmap]] - document - docs/SETUP-ROADMAP.md

## Live Query (requires Dataview plugin)

```dataview
TABLE source_file, type FROM #community/XR_Quality_&_Perf_Gates
SORT file.name ASC
```

## Connections to other communities
- 5 edges to [[_COMMUNITY_Docs & Terrain Pipeline]]
- 1 edge to [[_COMMUNITY_World Package & Companion Seam]]
- 1 edge to [[_COMMUNITY_Core Travel Loop]]

## Top bridge nodes
- [[Rationale Engine-Free Core, Built and Tested Before Unity Exists]] - degree 4, connects to 3 communities
- [[InteractionTargets_1]] - degree 4, connects to 1 community
- [[Site One Framerate Gate (Checkpoint 2)]] - degree 4, connects to 1 community
- [[Comfort and Locomotion Rules]] - degree 2, connects to 1 community
- [[Windows-Primary Setup Roadmap]] - degree 2, connects to 1 community