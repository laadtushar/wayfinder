using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// The viewscreen's destination list (build-plan Task 1.4). Populated
    /// from the WorldCatalog registry — never hardcoded — in registration
    /// order, which is the viewscreen-order contract. Selection raises
    /// WorldSelected with the world id; the TravelManager ticket consumes it.
    public sealed class DestinationMenu : MonoBehaviour
    {
        [SerializeField] private WorldCatalog catalog;
        [SerializeField] private RectTransform entryContainer;

        [Tooltip("Metres from the player's eyes to the viewscreen — drives Android XR minimum target sizing.")]
        [SerializeField] private float viewingDistanceMeters = 1.9f;

        /// Raised with the selected world id. TravelManager subscribes.
        public event Action<string> WorldSelected;

        /// Raised with a world id when the pointer/ray hovers an entry (before
        /// any click). The bridge holo-globe previews the hovered destination.
        public event Action<string> WorldHovered;

        internal void ReportHover(string worldId) => WorldHovered?.Invoke(worldId);

        public string SelectedWorldId { get; private set; }

        readonly List<Button> _entries = new List<Button>();

        public IReadOnlyList<Button> Entries => _entries;

        void Awake()
        {
            if (catalog == null)
                throw new InvalidOperationException($"{name}: DestinationMenu has no WorldCatalog assigned.");
            if (entryContainer == null)
                throw new InvalidOperationException($"{name}: DestinationMenu has no entry container assigned.");
            BuildMenu(catalog.BuildRegistry());
        }

        /// Builds one entry per registered world, in registry order. Public so
        /// EditMode tests can drive it without a scene.
        public void BuildMenu(WorldRegistry registry)
        {
            foreach (var entry in _entries)
            {
                if (entry == null) continue;
                if (Application.isPlaying) Destroy(entry.gameObject);
                else DestroyImmediate(entry.gameObject);
            }
            _entries.Clear();
            SelectedWorldId = null;

            float entryHeight = InteractionTargets.RecommendedSizeMeters(viewingDistanceMeters);
            foreach (var world in registry.All)
                _entries.Add(CreateEntry(world, entryHeight));
        }

        /// Selects a world by id: updates the highlight and raises WorldSelected.
        /// Unknown ids fail loudly — the menu only ever offers registry worlds,
        /// so an unknown id is a programming error, not a user action.
        public void Select(string worldId)
        {
            bool known = false;
            foreach (var entry in _entries)
            {
                var holder = entry.GetComponent<WorldIdHolder>();
                if (holder != null && holder.WorldId == worldId) { known = true; break; }
            }
            if (!known)
                throw new ArgumentException($"'{worldId}' is not a world this menu offers.");
            SelectedWorldId = worldId;
            foreach (var entry in _entries)
            {
                var holder = entry.GetComponent<WorldIdHolder>();
                bool isSelected = holder != null && holder.WorldId == worldId;
                var colors = entry.colors;
                colors.normalColor = isSelected
                    ? new Color(0.22f, 0.45f, 0.80f)
                    : new Color(0.13f, 0.16f, 0.20f);
                entry.colors = colors;
            }
            WorldSelected?.Invoke(worldId);
        }

        Button CreateEntry(WorldDefinition world, float heightMeters)
        {
            var go = new GameObject("Destination_" + world.Id,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(WorldIdHolder));
            go.transform.SetParent(entryContainer, false);
            go.AddComponent<DestinationEntryHover>().Init(this, world.Id);

            var rect = (RectTransform)go.transform;
            // The canvas is in world space at 1 unit = 1 m; sizeDelta is metres.
            rect.sizeDelta = new Vector2(1.8f, heightMeters);

            go.GetComponent<WorldIdHolder>().WorldId = world.Id;
            // Graphic stays white; ColorBlock (multiplied over it) owns the look.
            go.GetComponent<Image>().color = Color.white;

            // Counter-scaled label: a big rect scaled down so the bitmap font
            // rasterizes with real glyph room. RectTransform anchoring does NOT
            // compensate for localScale — the rect must be sized in scaled units.
            const float labelScale = 0.002f; // 500 units of rect per metre
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(1.8f / labelScale, heightMeters / labelScale);
            labelRect.localScale = Vector3.one * labelScale;
            var label = labelGo.GetComponent<Text>();
            label.text = world.DisplayName;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = (int)(heightMeters * 0.55f / labelScale);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.13f, 0.16f, 0.20f);
            colors.highlightedColor = new Color(0.20f, 0.26f, 0.34f);
            colors.pressedColor = new Color(0.22f, 0.45f, 0.80f);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;

            string id = world.Id;
            button.onClick.AddListener(() => Select(id));
            return button;
        }
    }

    /// Tiny data tag so tests and the menu can map a UI entry back to its world.
    /// Runtime-generated only — kept out of serialization on purpose.
    public sealed class WorldIdHolder : MonoBehaviour
    {
        public string WorldId { get; set; }
    }

    /// Reports pointer/ray hover on a destination entry back to the menu, so the
    /// bridge holo-globe can preview the hovered world before any click.
    public sealed class DestinationEntryHover : MonoBehaviour, IPointerEnterHandler
    {
        DestinationMenu _menu;
        string _worldId;

        public void Init(DestinationMenu menu, string worldId)
        {
            _menu = menu;
            _worldId = worldId;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_menu != null) _menu.ReportHover(_worldId);
        }
    }
}
