using UnityEngine;

namespace Wayfinder.Unity.Scatter
{
    /// Per-site archetype list + shared render settings. One asset per world,
    /// referenced by that world's ScatterRenderer. Worlds-as-data: adding rock
    /// variety to a site is authoring this, no code.
    [CreateAssetMenu(fileName = "ScatterSet", menuName = "Wayfinder/Scatter Archetype Set")]
    public sealed class ScatterArchetypeSet : ScriptableObject
    {
        [SerializeField] private string siteId;
        [SerializeField] private ScatterArchetype[] archetypes;
        [Tooltip("Shared instanced rock material (Wayfinder/RockInstanced).")]
        [SerializeField] private Material material;

        [Header("LOD distances (m) — LOD0 < d0, LOD1 < d1, LOD2 < radius")]
        [SerializeField] private float lod0Distance = 25f;
        [SerializeField] private float lod1Distance = 70f;
        [Tooltip("Beyond this, rocks are sub-pixel and skipped entirely.")]
        [SerializeField] private float scatterRadius = 110f;

        [Header("Budget (degradation levers — scale to zero never breaks a world)")]
        [Tooltip("Max rocks rendered per frame (culls farthest-smallest first).")]
        [SerializeField] private int runtimeVisibleCap = 1300;
        [Range(0f, 1f)]
        [SerializeField] private float scatterBudgetScale = 1f;

        public string SiteId => siteId;
        public ScatterArchetype[] Archetypes => archetypes;
        public Material Material => material;
        public float Lod0Distance => lod0Distance;
        public float Lod1Distance => lod1Distance;
        public float ScatterRadius => scatterRadius;
        public int RuntimeVisibleCap => Mathf.RoundToInt(runtimeVisibleCap * scatterBudgetScale);
    }
}
