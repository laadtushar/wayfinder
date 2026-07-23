using UnityEngine;
using UnityEngine.UI;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// A bridge nav-computer readout beside the holo-globe: as the traveler
    /// hovers a world on the viewscreen it shows that world's REAL physical
    /// stats (surface gravity, mean temperature, solar-day length, distance)
    /// from the engine-free WorldFactSheet. Persistent-layer only, data-driven:
    /// it reads the same DestinationMenu.WorldHovered event the HoloGlobe uses,
    /// so adding a world needs no readout code. Built procedurally in Awake,
    /// matching the world-space uGUI conventions (1 unit = 1 m, counter-scaled
    /// bitmap text) used by CompanionPanel / DestinationMenu.
    public sealed class WorldStatsReadout : MonoBehaviour
    {
        [SerializeField] private DestinationMenu menu;
        [Tooltip("For the world display name + a gravity cross-check against the fact sheet.")]
        [SerializeField] private WorldCatalog catalog;

        const float PanelW = 1.16f, PanelH = 0.46f;
        const float LabelScale = 0.002f;                       // 500 rect-units / m
        static readonly Color Cyan = new Color(0.55f, 0.95f, 1.10f);

        Text _header, _body;

        /// The world currently shown (for tests / other UI).
        public string ShownWorldId { get; private set; }
        public string HeaderText => _header != null ? _header.text : null;
        public string BodyText => _body != null ? _body.text : null;

        void Awake()
        {
            if (menu == null)
                throw new System.InvalidOperationException($"{name}: WorldStatsReadout has no DestinationMenu.");
            Build();
            menu.WorldHovered += Show;
            // Default to the first catalogued world so the console reads something
            // before the traveler hovers anything.
            if (catalog != null && catalog.Packages != null)
                foreach (var p in catalog.Packages)
                    if (p != null) { Show(p.Id); break; }
        }

        void OnDestroy()
        {
            if (menu != null) menu.WorldHovered -= Show;
        }

        /// Repaint the readout for a world. Data-driven from WorldFactSheet; the
        /// display name (and a gravity sanity check) come from the catalog.
        public void Show(string worldId)
        {
            if (!WorldFactSheet.TryGet(worldId, out var f))
            {
                if (_header != null) _header.text = "NO DATA";
                if (_body != null) _body.text = worldId ?? "";
                ShownWorldId = worldId;
                return;
            }

            string displayName = worldId;
            if (catalog != null && catalog.Packages != null)
                foreach (var p in catalog.Packages)
                {
                    if (p == null || p.Id != worldId) continue;
                    var def = p.ToDefinition();
                    displayName = def.DisplayName;
                    if (Mathf.Abs(def.SurfaceGravity - f.Gravity) > 0.01f)
                        Debug.LogWarning($"[WorldStatsReadout] gravity mismatch for '{worldId}': " +
                            $"package {def.SurfaceGravity} vs fact sheet {f.Gravity}");
                    break;
                }

            if (_header != null) _header.text = displayName.ToUpperInvariant();
            if (_body != null) _body.text = f.Body + "\n" + string.Join("\n", f.ReadoutLines());
            ShownWorldId = worldId;
        }

        void Build()
        {
            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.06f, 0.09f, 0.80f);
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(PanelW, PanelH);

            _header = MakeText("Header", "", new Vector2(0f, PanelH * 0.5f - 0.055f),
                new Vector2(PanelW - 0.08f, 0.08f), 0.46f, TextAnchor.MiddleLeft, Cyan);
            // Body: the fact-sheet body line (dim) then the four stat rows.
            _body = MakeText("Body", "", new Vector2(0f, -0.055f),
                new Vector2(PanelW - 0.08f, 0.30f), 0.155f, TextAnchor.UpperLeft, Cyan);
        }

        Text MakeText(string goName, string content, Vector2 anchoredMeters, Vector2 sizeMeters,
            float fontFracOfHeight, TextAnchor anchor, Color color)
        {
            var go = new GameObject(goName, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredMeters;
            rt.localScale = Vector3.one * LabelScale;
            rt.sizeDelta = sizeMeters / LabelScale;
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.alignment = anchor;
            t.color = color;
            t.fontSize = (int)(sizeMeters.y * fontFracOfHeight / LabelScale);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.text = content;
            return t;
        }
    }
}
