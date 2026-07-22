using System;
using UnityEngine;

namespace Wayfinder.Unity.Scatter
{
    /// One rock kind: its LOD meshes, placement rule, and size distribution.
    /// Data only — evaluated by ScatterPlacement, rendered by ScatterRenderer.
    [Serializable]
    public sealed class ScatterArchetype
    {
        [Tooltip("LOD meshes, coarsest last. Index 0 = LOD0 (near, most tris).")]
        [SerializeField] private Mesh[] lods = new Mesh[3];

        [Header("Placement rule (world-space slope, never grid-space)")]
        [SerializeField] private float minSlopeDeg = 0f;
        [SerializeField] private float maxSlopeDeg = 28f;
        [SerializeField] private float minWorldY = -100000f;
        [SerializeField] private float maxWorldY = 100000f;
        [Tooltip("Talus only: accept only at the base of a cliff (uphill neighbour steep, self flat).")]
        [SerializeField] private bool requireWallBase = false;
        [Tooltip("Reject candidates within this many metres of spawn / a POI.")]
        [SerializeField] private float clearRadius = 3f;

        [Header("Size distribution (log-normal — few big, many small)")]
        [SerializeField] private float medianScale = 1f;
        [SerializeField] private float scaleSigma = 0.5f;
        [SerializeField] private float colliderRadius = 0.5f;

        [Header("Tint (site geology ± per-instance jitter)")]
        [SerializeField] private Color tint = new Color(0.5f, 0.4f, 0.35f, 1f);

        public Mesh[] Lods => lods;
        public float MedianScale => medianScale;
        public float ScaleSigma => scaleSigma;
        public float ColliderRadius => colliderRadius;
        public Color Tint => tint;

        public ScatterPlacement.Rule ToRule() => new ScatterPlacement.Rule
        {
            minSlopeDeg = minSlopeDeg,
            maxSlopeDeg = maxSlopeDeg,
            minWorldY = minWorldY,
            maxWorldY = maxWorldY,
            requireWallBase = requireWallBase,
            clearRadius = clearRadius,
        };

        public Mesh Lod(int i) => (lods != null && i >= 0 && i < lods.Length) ? lods[i] : null;
    }
}
