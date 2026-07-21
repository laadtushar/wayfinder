---
type: community
cohesion: 0.29
members: 13
---

# Docs & Terrain Pipeline

**Cohesion:** 0.29 - loosely connected
**Members:** 13 nodes

## Members
- [[CLAUDE.md Project Instructions]] - document - CLAUDE.md
- [[GDAL DEM-to-Unity-Terrain Pipeline]] - document - CLAUDE.md
- [[Heightmap Output Contract (2049x2049 16-bit big-endian RAW, 8396802 bytes)]] - document - docs/specs/2026-07-21-terrain-and-poi-content.md
- [[IDEATION Frontier-Tech Survey]] - document - IDEATION.md
- [[Offline Authoring Reframe (scan, reconstruct, bake; headset only renders)]] - document - IDEATION.md
- [[Public Name Trademark Risk (Classes 9 and 41)]] - document - IDEATION.md
- [[Terrain + POI Content Spec (Mac-side Pre-production)]] - document - docs/specs/2026-07-21-terrain-and-poi-content.md
- [[Terrain Data Sources and Processing Audit]] - document - docs/data-sources.md
- [[Wayfinder README]] - document - README.md
- [[Wayfinder Runtime Architecture]] - document - docs/ARCHITECTURE.md
- [[Wayfinder v1 Design (Locked Decisions)]] - document - DESIGN.md
- [[Wayfinder v1 Implementation Plan]] - document - docs/plans/2026-07-20-wayfinder-v1.md
- [[WorldRegistry_1]] - code - core/Wayfinder.Core/WorldRegistry.cs

## Live Query (requires Dataview plugin)

```dataview
TABLE source_file, type FROM #community/Docs_&_Terrain_Pipeline
SORT file.name ASC
```

## Connections to other communities
- 6 edges to [[_COMMUNITY_World Package & Companion Seam]]
- 5 edges to [[_COMMUNITY_XR Quality & Perf Gates]]
- 5 edges to [[_COMMUNITY_Core Travel Loop]]

## Top bridge nodes
- [[Wayfinder v1 Implementation Plan]] - degree 11, connects to 3 communities
- [[Wayfinder Runtime Architecture]] - degree 7, connects to 2 communities
- [[WorldRegistry_1]] - degree 5, connects to 2 communities
- [[Wayfinder v1 Design (Locked Decisions)]] - degree 9, connects to 1 community
- [[CLAUDE.md Project Instructions]] - degree 5, connects to 1 community