---
type: community
cohesion: 0.24
members: 10
---

# Core Travel Loop

**Cohesion:** 0.24 - loosely connected
**Members:** 10 nodes

## Members
- [[Core Loop Bridge to Warp to Surface to Return]] - document - DESIGN.md
- [[Mars Express HRSC Level-4 DTM, orbit 0037]] - document - docs/data-sources.md
- [[Rationale Four Travel States Instead of the Plan's Three]] - document - core/README.md
- [[Rationale Warp as Loading Screen]] - document - docs/ARCHITECTURE.md
- [[TravelManager (planned MonoBehaviour wrapper)]] - document - docs/ARCHITECTURE.md
- [[TravelStateMachine_1]] - code - core/Wayfinder.Core/TravelStateMachine.cs
- [[TravelStateMachineTests_1]] - code - core/Wayfinder.Core.Tests/TravelStateMachineTests.cs
- [[WorldDefinition_1]] - code - core/Wayfinder.Core/WorldRegistry.cs
- [[WorldRegistryTests_1]] - code - core/Wayfinder.Core.Tests/WorldRegistryTests.cs
- [[mars-olympus Site (Olympus Mons caldera rim)]] - document - docs/data-sources.md

## Live Query (requires Dataview plugin)

```dataview
TABLE source_file, type FROM #community/Core_Travel_Loop
SORT file.name ASC
```

## Connections to other communities
- 5 edges to [[_COMMUNITY_Docs & Terrain Pipeline]]
- 2 edges to [[_COMMUNITY_World Package & Companion Seam]]
- 1 edge to [[_COMMUNITY_XR Quality & Perf Gates]]

## Top bridge nodes
- [[TravelStateMachine_1]] - degree 9, connects to 3 communities
- [[WorldDefinition_1]] - degree 4, connects to 2 communities
- [[TravelManager (planned MonoBehaviour wrapper)]] - degree 3, connects to 1 community
- [[WorldRegistryTests_1]] - degree 3, connects to 1 community