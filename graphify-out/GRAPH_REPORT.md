# Graph Report - .  (2026-07-21)

## Corpus Check
- Corpus is ~18,650 words - fits in a single context window. You may not need a graph.

## Summary
- 112 nodes · 142 edges · 14 communities detected
- Extraction: 89% EXTRACTED · 10% INFERRED · 1% AMBIGUOUS · INFERRED: 14 edges (avg confidence: 0.78)
- Token cost: 29,000 input · 9,500 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Travel Machine Tests|Travel Machine Tests]]
- [[_COMMUNITY_Docs & Terrain Pipeline|Docs & Terrain Pipeline]]
- [[_COMMUNITY_World Package & Companion Seam|World Package & Companion Seam]]
- [[_COMMUNITY_XR Quality & Perf Gates|XR Quality & Perf Gates]]
- [[_COMMUNITY_Core Travel Loop|Core Travel Loop]]
- [[_COMMUNITY_World Registry Tests|World Registry Tests]]
- [[_COMMUNITY_Field Log Tests|Field Log Tests]]
- [[_COMMUNITY_Travel State Machine|Travel State Machine]]
- [[_COMMUNITY_Interaction Target Tests|Interaction Target Tests]]
- [[_COMMUNITY_Interaction Targets|Interaction Targets]]
- [[_COMMUNITY_Field Log|Field Log]]
- [[_COMMUNITY_World Registry|World Registry]]
- [[_COMMUNITY_Moon Shackleton Data|Moon Shackleton Data]]
- [[_COMMUNITY_Mars Valles Data|Mars Valles Data]]

## God Nodes (most connected - your core abstractions)
1. `TravelStateMachineTests` - 11 edges
2. `Wayfinder v1 Implementation Plan` - 11 edges
3. `TravelStateMachine` - 9 edges
4. `Wayfinder v1 Design (Locked Decisions)` - 9 edges
5. `FieldLog` - 8 edges
6. `WorldRegistryTests` - 7 edges
7. `Wayfinder README` - 7 edges
8. `Wayfinder Runtime Architecture` - 7 edges
9. `FieldLogTests` - 6 edges
10. `World Package (worlds as data, not code)` - 6 edges

## Surprising Connections (you probably didn't know these)
- `InteractionTargets` --implements--> `Wayfinder v1 Implementation Plan`  [AMBIGUOUS]
  core/Wayfinder.Core/InteractionTargets.cs → docs/plans/2026-07-20-wayfinder-v1.md
- `WorldDefinition` --shares_data_with--> `Heightmap Output Contract (2049x2049 16-bit big-endian RAW, 8396802 bytes)`  [INFERRED]
  core/Wayfinder.Core/WorldRegistry.cs → docs/specs/2026-07-21-terrain-and-poi-content.md
- `Splat Diorama-Per-Scene Pattern (bounded scene swapped at travel)` --semantically_similar_to--> `World Package (worlds as data, not code)`  [INFERRED] [semantically similar]
  IDEATION.md → docs/ARCHITECTURE.md
- `Offline Authoring Reframe (scan, reconstruct, bake; headset only renders)` --semantically_similar_to--> `Rationale: Headset Renders Worlds, Never Builds Them Live`  [INFERRED] [semantically similar]
  IDEATION.md → docs/ARCHITECTURE.md
- `FieldLog` --implements--> `Wayfinder v1 Implementation Plan`  [EXTRACTED]
  core/Wayfinder.Core/FieldLog.cs → docs/plans/2026-07-20-wayfinder-v1.md

## Hyperedges (group relationships)
- **Engine-Free Wayfinder.Core Module** — fieldlog_FieldLog, interactiontargets_InteractionTargets, travelstatemachine_TravelStateMachine, worldregistry_WorldRegistry, worldregistry_WorldDefinition [EXTRACTED 1.00]
- **Bridge-Warp-Surface-Discovery Core Loop** — design_core_loop, travelstatemachine_TravelStateMachine, arch_travel_manager, worldregistry_WorldRegistry, fieldlog_FieldLog [EXTRACTED 1.00]
- **Real-DEM Terrain Pipeline for the Three v1 Sites** — claudemd_terrain_pipeline, spec_heightmap_contract, datasources_mars_olympus, datasources_mars_valles, datasources_moon_shackleton [EXTRACTED 1.00]

## Communities

### Community 0 - "Travel Machine Tests"
Cohesion: 0.15
Nodes (2): TravelStateMachineTests, Wayfinder.Core.Tests

### Community 1 - "Docs & Terrain Pipeline"
Cohesion: 0.29
Nodes (13): Wayfinder Runtime Architecture, CLAUDE.md Project Instructions, GDAL DEM-to-Unity-Terrain Pipeline, Terrain Data Sources and Processing Audit, Wayfinder v1 Design (Locked Decisions), IDEATION Frontier-Tech Survey, Offline Authoring Reframe (scan, reconstruct, bake; headset only renders), Public Name Trademark Risk (Classes 9 and 41) (+5 more)

### Community 2 - "World Package & Companion Seam"
Cohesion: 0.22
Nodes (13): Android XR / Galaxy XR Platform Guide, Rationale: POI Data as the v1.1 Gemini Companion Seam, Rationale: No Splat Renderer in v1 (removes riskiest dependency), Rationale: Headset Renders Worlds, Never Builds Them Live, Persistent Bridge Hub, PointOfInterest Record, World Package (worlds as data, not code), Wayfinder.Core Module README (+5 more)

### Community 3 - "XR Quality & Perf Gates"
Cohesion: 0.2
Nodes (11): Android XR Quality Checklist, Interactive Target Sizing Rule (distance x 0.868 x 48dp), Frame Budget: 72 fps minimum / 90 target, Rationale: Engine-Free Core, Built and Tested Before Unity Exists, Comfort and Locomotion Rules, InteractionTargets, InteractionTargetsTests, Rationale: Prove Site One Before Authoring Three Worlds (+3 more)

### Community 4 - "Core Travel Loop"
Cohesion: 0.24
Nodes (10): TravelManager (planned MonoBehaviour wrapper), Rationale: Warp as Loading Screen, Rationale: Four Travel States Instead of the Plan's Three, Mars Express HRSC Level-4 DTM, orbit 0037, mars-olympus Site (Olympus Mons caldera rim), Core Loop: Bridge to Warp to Surface to Return, TravelStateMachine, TravelStateMachineTests (+2 more)

### Community 5 - "World Registry Tests"
Cohesion: 0.31
Nodes (2): Wayfinder.Core.Tests, WorldRegistryTests

### Community 6 - "Field Log Tests"
Cohesion: 0.25
Nodes (2): FieldLogTests, Wayfinder.Core.Tests

### Community 7 - "Travel State Machine"
Cohesion: 0.29
Nodes (2): TravelStateMachine, Wayfinder.Core

### Community 8 - "Interaction Target Tests"
Cohesion: 0.29
Nodes (2): InteractionTargetsTests, Wayfinder.Core.Tests

### Community 9 - "Interaction Targets"
Cohesion: 0.47
Nodes (2): InteractionTargets, Wayfinder.Core

### Community 10 - "Field Log"
Cohesion: 0.4
Nodes (2): FieldLog, Wayfinder.Core

### Community 11 - "World Registry"
Cohesion: 0.4
Nodes (3): Wayfinder.Core, WorldDefinition, WorldRegistry

### Community 12 - "Moon Shackleton Data"
Cohesion: 0.67
Nodes (3): Barker et al. 2021, Improved LOLA Elevation Maps for South Pole Landing Sites, LOLA South Pole DEM Mosaic 5 m/px (ldem_87s_5mpp), moon-shackleton Site (Shackleton crater rim)

### Community 13 - "Mars Valles Data"
Cohesion: 1.0
Nodes (2): HiRISE Stereo DTM DTEEC_046764_1660 (East Coprates Chasma), mars-valles Site (Coprates Chasma canyon wall)

## Ambiguous Edges - Review These
- `InteractionTargets` → `Wayfinder v1 Implementation Plan`  [AMBIGUOUS]
  core/README.md · relation: implements

## Knowledge Gaps
- **18 isolated node(s):** `Wayfinder.Core`, `Wayfinder.Core`, `Wayfinder.Core`, `Wayfinder.Core`, `WorldDefinition` (+13 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `Mars Valles Data`** (2 nodes): `HiRISE Stereo DTM DTEEC_046764_1660 (East Coprates Chasma)`, `mars-valles Site (Coprates Chasma canyon wall)`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `InteractionTargets` and `Wayfinder v1 Implementation Plan`?**
  _Edge tagged AMBIGUOUS (relation: implements) - confidence is low._
- **Why does `Wayfinder v1 Implementation Plan` connect `Docs & Terrain Pipeline` to `World Package & Companion Seam`, `XR Quality & Perf Gates`, `Core Travel Loop`?**
  _High betweenness centrality (0.059) - this node is a cross-community bridge._
- **Why does `TravelStateMachine` connect `Core Travel Loop` to `Docs & Terrain Pipeline`, `World Package & Companion Seam`, `XR Quality & Perf Gates`?**
  _High betweenness centrality (0.042) - this node is a cross-community bridge._
- **Why does `CLAUDE.md Project Instructions` connect `Docs & Terrain Pipeline` to `XR Quality & Perf Gates`?**
  _High betweenness centrality (0.030) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `TravelStateMachine` (e.g. with `WorldDefinition` and `FieldLog`) actually correct?**
  _`TravelStateMachine` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Wayfinder.Core`, `Wayfinder.Core`, `Wayfinder.Core` to the rest of the system?**
  _18 weakly-connected nodes found - possible documentation gaps or missing edges._