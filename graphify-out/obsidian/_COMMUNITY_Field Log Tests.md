---
type: community
cohesion: 0.25
members: 8
---

# Field Log Tests

**Cohesion:** 0.25 - loosely connected
**Members:** 8 nodes

## Members
- [[.Count_ReflectsDistinctDiscoveries()]] - code - core\Wayfinder.Core.Tests\FieldLogTests.cs
- [[.Discover_AddsOnce_IgnoresDuplicates()]] - code - core\Wayfinder.Core.Tests\FieldLogTests.cs
- [[.Discover_NullOrEmpty_IsRejected_AndNotCounted()]] - code - core\Wayfinder.Core.Tests\FieldLogTests.cs
- [[.DiscoveredIds_ReturnsEverythingDiscovered()]] - code - core\Wayfinder.Core.Tests\FieldLogTests.cs
- [[.HasDiscovered_IsFalse_ForUnknownId()]] - code - core\Wayfinder.Core.Tests\FieldLogTests.cs
- [[FieldLogTests]] - code - core\Wayfinder.Core.Tests\FieldLogTests.cs
- [[FieldLogTests.cs]] - code - core\Wayfinder.Core.Tests\FieldLogTests.cs
- [[Wayfinder.Core.Tests]] - code - core\Wayfinder.Core.Tests\FieldLogTests.cs

## Live Query (requires Dataview plugin)

```dataview
TABLE source_file, type FROM #community/Field_Log_Tests
SORT file.name ASC
```
