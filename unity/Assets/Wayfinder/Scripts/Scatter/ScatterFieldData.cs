using UnityEngine;

namespace Wayfinder.Unity.Scatter
{
    /// The baked scatter field: flat, blittable per-instance arrays plus a
    /// coarse cell index for cache-coherent runtime culling. Produced by
    /// ScatterBaker (editor), consumed by ScatterRenderer (runtime). Re-bakes
    /// are stable (seeded), so referencers never churn.
    [CreateAssetMenu(fileName = "ScatterField", menuName = "Wayfinder/Scatter Field Data")]
    public sealed class ScatterFieldData : ScriptableObject
    {
        [SerializeField] private string siteId;

        // SoA per-instance — sorted by cell for coherent iteration.
        [SerializeField] private Vector3[] positions;
        [SerializeField] private Quaternion[] rotations;
        [SerializeField] private float[] scales;
        [SerializeField] private byte[] archetypeIndex;
        [SerializeField] private Color[] tints;

        // Uniform XZ cell grid over the terrain (cell = cellSize m).
        [SerializeField] private float cellSize = 16f;
        [SerializeField] private int cellsX;
        [SerializeField] private int cellsZ;
        [SerializeField] private Vector2 gridOrigin; // world XZ of cell (0,0) min corner
        [SerializeField] private int[] cellStart;
        [SerializeField] private int[] cellCount;

        public int Count => positions != null ? positions.Length : 0;
        public string SiteId => siteId;
        public Vector3[] Positions => positions;
        public Quaternion[] Rotations => rotations;
        public float[] Scales => scales;
        public byte[] ArchetypeIndex => archetypeIndex;
        public Color[] Tints => tints;
        public float CellSize => cellSize;
        public int CellsX => cellsX;
        public int CellsZ => cellsZ;
        public Vector2 GridOrigin => gridOrigin;
        public int[] CellStart => cellStart;
        public int[] CellCount => cellCount;

        /// Populate from the baker. Arrays are taken by reference (already
        /// cell-sorted); this asset is the owner after the call.
        public void Set(string site, Vector3[] pos, Quaternion[] rot, float[] scl,
            byte[] arch, Color[] tint, float cell, int cx, int cz, Vector2 origin,
            int[] start, int[] count)
        {
            siteId = site;
            positions = pos; rotations = rot; scales = scl;
            archetypeIndex = arch; tints = tint;
            cellSize = cell; cellsX = cx; cellsZ = cz; gridOrigin = origin;
            cellStart = start; cellCount = count;
        }
    }
}
