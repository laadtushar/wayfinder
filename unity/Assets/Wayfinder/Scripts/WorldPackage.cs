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

        /// Throw-free id accessor for lookups — ToDefinition() throws on
        /// half-authored packages, which must not poison scans for OTHER
        /// worlds (e.g. FindPackage during a warp).
        public string Id => id;

        [Header("Atmospherics (#18) — real per-site values, data not magic numbers")]
        [Tooltip("Master: Mars true (dust haze), Moon false (vacuum — zero fog cost).")]
        [SerializeField] private bool hazeEnabled = false;
        [Tooltip("Horizon color the terrain dissolves into — MUST match the baked sky's horizon band. Mars ref: Curiosity Mastcam (NASA/JPL-Caltech/MSSS).")]
        [SerializeField] private Color hazeColor = new Color(0.68f, 0.49f, 0.36f, 1f);
        [Tooltip("Beer–Lambert distance density, 1/m. Mars dust visibility ~2.5 km -> ~0.0004.")]
        [SerializeField] private float hazeDistanceDensity = 0.0004f;
        [Tooltip("Height falloff, 1/m. Larger = haze hugs low ground harder.")]
        [SerializeField] private float hazeHeightFalloff = 0.0015f;
        [Tooltip("World-space Y (m) of the haze datum (valley floor).")]
        [SerializeField] private float hazeGroundY = 0f;
        [Tooltip("0 = pure distance fog, 1 = fully height-stratified.")]
        [Range(0f, 1f)][SerializeField] private float hazeHeightStrength = 0.85f;

        [Header("Regolith opposition surge (#18) — Moon strong, Mars faint")]
        [Tooltip("Peak extra brightness at the anti-solar point. Moon ~0.6 (LRO/Apollo photometry), Mars ~0.1.")]
        [SerializeField] private float surgeStrength = 0f;
        [Tooltip("Lobe tightness: 48 -> ~9.7 deg HWHM (shadow-hiding scale).")]
        [SerializeField] private float surgeSharpness = 48f;
        [Tooltip("Surge tint; white = pure brightness.")]
        [SerializeField] private Color surgeTint = Color.white;

        public bool HazeEnabled => hazeEnabled;
        public Color HazeColor => hazeColor;
        public float HazeDistanceDensity => hazeDistanceDensity;
        public float HazeHeightFalloff => hazeHeightFalloff;
        public float HazeGroundY => hazeGroundY;
        public float HazeHeightStrength => hazeHeightStrength;
        public float SurgeStrength => surgeStrength;
        public float SurgeSharpness => surgeSharpness;
        public Color SurgeTint => surgeTint;

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
