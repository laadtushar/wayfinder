using System.Collections.Generic;

namespace Wayfinder.Core
{
    /// Pure progress accounting over a FieldLog. POI ids are "worldId/slug",
    /// so per-world progress is a prefix count against each world's known
    /// total. Engine-free so the Bridge panel's math is testable headless.
    public static class FieldLogProgress
    {
        public readonly struct WorldProgress
        {
            public readonly string WorldId;
            public readonly int Discovered;
            public readonly int Total;

            public WorldProgress(string worldId, int discovered, int total)
            {
                WorldId = worldId;
                Discovered = discovered;
                Total = total;
            }
        }

        /// Per-world discovered/total, in the given world order. `totals` maps
        /// each worldId to its POI count.
        public static List<WorldProgress> PerWorld(
            IReadOnlyCollection<string> discoveredIds,
            IReadOnlyList<string> worldOrder,
            IReadOnlyDictionary<string, int> totals)
        {
            var result = new List<WorldProgress>(worldOrder.Count);
            foreach (var worldId in worldOrder)
            {
                int discovered = 0;
                string prefix = worldId + "/";
                foreach (var id in discoveredIds)
                    if (id != null && id.StartsWith(prefix, System.StringComparison.Ordinal)) discovered++;
                totals.TryGetValue(worldId, out int total);
                result.Add(new WorldProgress(worldId, discovered, total));
            }
            return result;
        }

        /// Grand total discovered across every world's known POIs.
        public static int TotalAvailable(IReadOnlyDictionary<string, int> totals)
        {
            int sum = 0;
            foreach (var kv in totals) sum += kv.Value;
            return sum;
        }
    }
}
