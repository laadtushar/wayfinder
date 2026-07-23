using UnityEngine;

namespace Wayfinder.Unity
{
    /// A slowly-spinning holographic globe over the bridge console that previews
    /// the destination the player is hovering on the viewscreen — the "command a
    /// starship" beat. Persistent-layer only: it reads the destination menu's
    /// hover event and a data-driven worldId->drape table (no per-world code).
    /// The OBJECT spins, never the camera, so it is comfort-safe.
    public sealed class HoloGlobe : MonoBehaviour
    {
        [System.Serializable]
        public struct WorldGlobe
        {
            public string worldId;
            public Texture drape;   // the world's terrain drape, shown as holo surface detail
            public Color tint;      // per-world nudge to the holo colour (Mars warmer, Moon cooler)
        }

        [SerializeField] private DestinationMenu menu;
        [SerializeField] private Renderer globe;
        [SerializeField] private WorldGlobe[] worlds;
        [Tooltip("Degrees/second the globe spins about its own axis (object spin — never the camera).")]
        [SerializeField] private float spinDegPerSec = 12f;
        [Tooltip("Base holographic colour; per-world tint multiplies this.")]
        [SerializeField] private Color holoTint = new Color(0.45f, 0.85f, 1.00f, 0.85f);

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _block;

        /// The world currently displayed (for tests / other UI).
        public string ShownWorldId { get; private set; }

        void Awake()
        {
            if (globe == null)
                throw new System.InvalidOperationException($"{name}: HoloGlobe has no globe renderer.");
            _block = new MaterialPropertyBlock();
            if (menu != null) menu.WorldHovered += Show;
            // Default to the first catalogued world so the bridge always has a
            // globe turning, even before the traveler hovers anything.
            if (worlds != null && worlds.Length > 0) Show(worlds[0].worldId);
        }

        void OnDestroy()
        {
            if (menu != null) menu.WorldHovered -= Show;
        }

        /// Swap the globe to a world's holographic scan.
        public void Show(string worldId)
        {
            if (worlds == null) return;
            for (int i = 0; i < worlds.Length; i++)
            {
                if (worlds[i].worldId != worldId) continue;
                globe.GetPropertyBlock(_block);
                _block.SetTexture(BaseMapId, worlds[i].drape);
                Color t = worlds[i].tint == default ? holoTint : worlds[i].tint * holoTint;
                _block.SetColor(BaseColorId, t);
                globe.SetPropertyBlock(_block);
                ShownWorldId = worldId;
                return;
            }
        }

        void Update()
        {
            globe.transform.Rotate(0f, spinDegPerSec * Time.deltaTime, 0f, Space.Self);
        }
    }
}
