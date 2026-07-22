using UnityEngine;

namespace Wayfinder.Unity.Scatter
{
    /// The baked scatter field: flat, blittable per-instance arrays plus the
    /// field's world-space bounds (for the renderer's frustum-cull AABB).
    /// Produced by ScatterBaker (editor), consumed by ScatterRenderer
    /// (runtime). Re-bakes are stable (seeded), so referencers never churn.
    [CreateAssetMenu(fileName = "ScatterField", menuName = "Wayfinder/Scatter Field Data")]
    public sealed class ScatterFieldData : ScriptableObject
    {
        [SerializeField] private string siteId;

        // SoA per-instance.
        [SerializeField] private Vector3[] positions;
        [SerializeField] private Quaternion[] rotations;
        [SerializeField] private float[] scales;
        [SerializeField] private byte[] archetypeIndex;

        // World-space AABB enclosing every rock — the renderer's culling bounds.
        // Baked from the real positions, NOT the (origin) transform, so a field
        // placed kilometres from origin (Shackleton rim) still passes cull.
        [SerializeField] private Bounds worldBounds;

        public int Count => positions != null ? positions.Length : 0;
        public string SiteId => siteId;
        public Vector3[] Positions => positions;
        public Quaternion[] Rotations => rotations;
        public float[] Scales => scales;
        public byte[] ArchetypeIndex => archetypeIndex;
        public Bounds WorldBounds => worldBounds;

        /// Populate from the baker. Arrays are taken by reference; this asset
        /// is the owner after the call.
        public void Set(string site, Vector3[] pos, Quaternion[] rot, float[] scl,
            byte[] arch, Bounds bounds)
        {
            siteId = site;
            positions = pos; rotations = rot; scales = scl;
            archetypeIndex = arch;
            worldBounds = bounds;
        }
    }
}
