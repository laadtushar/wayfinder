using System.Collections.Generic;
using UnityEngine;

namespace Wayfinder.Unity.Scatter
{
    /// Pure placement math for rock scatter — no UnityEditor, no TerrainData,
    /// no scene access, so it is EditMode-testable headless with synthetic
    /// candidates (same reason TravelStateMachine lives engine-free). The
    /// ScatterBaker feeds it real TerrainData samples; the tests feed it
    /// fabricated cliffs and flats.
    public static class ScatterPlacement
    {
        /// One evaluated terrain sample the baker produces per candidate.
        public struct Candidate
        {
            public Vector3 worldPos;     // sample point in world space
            public float steepnessDeg;   // 0..90, world-corrected (TerrainData.GetSteepness)
            public bool wallBaseFlag;    // uphill neighbour steep, self flat (talus apron)
            public float nearestFeature; // metres to nearest POI/spawn (exclusion + priority)
        }

        /// Per-archetype acceptance rule (data on ScatterArchetype).
        public struct Rule
        {
            public float minSlopeDeg;
            public float maxSlopeDeg;
            public float minWorldY;
            public float maxWorldY;
            public bool requireWallBase;  // Valles talus: only at cliff bases
            public float clearRadius;     // reject within this of a feature (spawn/POI)
        }

        /// True if the candidate satisfies the rule. World-space slope only —
        /// the anisotropy trap (grid-space slope on Valles' non-square cells)
        /// is avoided by the caller sampling GetSteepness, never cell deltas.
        public static bool Accept(in Candidate c, in Rule r)
        {
            if (c.nearestFeature < r.clearRadius) return false;
            if (c.steepnessDeg < r.minSlopeDeg || c.steepnessDeg > r.maxSlopeDeg) return false;
            if (c.worldPos.y < r.minWorldY || c.worldPos.y > r.maxWorldY) return false;
            if (r.requireWallBase && !c.wallBaseFlag) return false;
            return true;
        }

        /// Priority for cap enforcement: keep big rocks and rocks near features
        /// (spawn/POIs), cull small far ones. Higher = keep.
        public static float Priority(float scale, float nearestFeature)
        {
            // Near a feature => bias up (rocks frame the places players stand);
            // far => decays. Scale dominates so big rocks always survive.
            float featureBias = 1f / (1f + 0.02f * nearestFeature);
            return scale * (1f + featureBias);
        }

        /// Guarantee the hard cap regardless of terrain noise: if more than
        /// `cap` accepted, drop lowest-priority first. Stable — sorts a copy of
        /// indices so the caller's parallel arrays can be compacted the same way.
        /// Returns the kept indices (length <= cap), highest priority preserved.
        public static List<int> EnforceCap(IReadOnlyList<float> priorities, int cap)
        {
            var kept = new List<int>(priorities.Count);
            for (int i = 0; i < priorities.Count; i++) kept.Add(i);
            if (priorities.Count <= cap) return kept;

            kept.Sort((a, b) => priorities[b].CompareTo(priorities[a])); // desc by priority
            kept.RemoveRange(cap, kept.Count - cap);
            kept.Sort(); // restore original order for cache-coherent compaction
            return kept;
        }

        /// Deterministic hash noise in [0,1) from integer coords — bit-stable
        /// across platforms/Unity versions (unlike Mathf.PerlinNoise). Used for
        /// jitter and size sampling so re-bakes are identical (F8).
        public static float Hash01(int x, int y, int seed)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791);
            h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000; // 24-bit mantissa -> [0,1)
        }

        /// Log-normal size sample: few big, many small (natural size-frequency).
        /// u in [0,1); median scale `med`, spread `sigma`.
        public static float LogNormalScale(float u, float med, float sigma)
        {
            // inverse-normal approx (Beasley-Springer/Moro tail-free core is
            // overkill here) — use a cheap symmetric logistic-to-normal map.
            float z = Mathf.Log(Mathf.Max(u, 1e-6f) / Mathf.Max(1f - u, 1e-6f)) * 0.5513f; // logistic->~normal
            return med * Mathf.Exp(sigma * z);
        }
    }
}
