using UnityEngine;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// One visitable world as data (docs/ARCHITECTURE.md section 3): the
    /// engine-free WorldDefinition fields plus Unity-side references. Adding a
    /// world to the game is authoring one of these — never new travel code.
    [CreateAssetMenu(fileName = "WorldPackage", menuName = "Wayfinder/World Package")]
    public sealed class WorldPackage : ScriptableObject
    {
        [Header("Identity (drives registry + viewscreen)")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string sceneName;

        [Header("Real physics — data, not magic numbers (CLAUDE.md)")]
        [Tooltip("m/s². Mars 3.72, Moon 1.62 — from the site's meta.json.")]
        [SerializeField] private float surfaceGravity;

        [Header("Arrival")]
        [Tooltip("Site-local metres from the terrain centre where the player lands. Worlds-as-data: the shackleton clip centres on the permanently shadowed floor, so its spawn must sit on the rim.")]
        [SerializeField] private Vector2 spawnOffset;

        public Vector2 SpawnOffset => spawnOffset;

        [Header("Content references (wired by later tickets)")]
        [Tooltip("Per-site POI list (unity/Assets/Wayfinder/POI/<site>.json) — consumed by the POI system and, later, the Gemini companion.")]
        [SerializeField] private TextAsset poiData;
        [Tooltip("Terrain for the site — assigned when the Phase 2 terrain import lands.")]
        [SerializeField] private TerrainData terrain;

        public TextAsset PoiData => poiData;
        public TerrainData Terrain => terrain;

        /// The engine-free view of this package. Throws (loudly, by design)
        /// when the asset is half-authored — a blank scene name would
        /// otherwise only surface as an opaque load failure mid-warp on device.
        public WorldDefinition ToDefinition()
        {
            if (string.IsNullOrEmpty(sceneName))
                throw new System.ArgumentException(
                    $"WorldPackage '{name}' has no scene name — it cannot be warped to.");
            return new WorldDefinition(id, displayName, sceneName, surfaceGravity);
        }
    }
}
