---
type: community
cohesion: 0.15
members: 13
---

# Travel Machine Tests

**Cohesion:** 0.15 - loosely connected
**Members:** 13 nodes

## Members
- [[.CompleteReturn_WhenNotReturning_Throws()]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[.CompleteWarp_WhenNotWarping_Throws()]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[.FullLoop_CanRunTwice()]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[.Return_FromBridge_IsRejected()]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[.Return_FromSurface_EntersWarping_ThenBridge_AndClearsDestination()]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[.StartsOnBridge_WithNoDestination()]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[.Warp_FromBridge_EntersWarping_ThenSurface()]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[.Warp_WhileOnSurface_IsRejected()]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[.Warp_WhileWarping_IsRejected()]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[.Warp_WithNullOrEmptyId_IsRejected()]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[TravelStateMachineTests]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[TravelStateMachineTests.cs]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs
- [[Wayfinder.Core.Tests_2]] - code - core\Wayfinder.Core.Tests\TravelStateMachineTests.cs

## Live Query (requires Dataview plugin)

```dataview
TABLE source_file, type FROM #community/Travel_Machine_Tests
SORT file.name ASC
```
