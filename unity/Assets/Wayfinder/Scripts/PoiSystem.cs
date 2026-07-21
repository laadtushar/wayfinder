using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// Spawns beacon markers for a site's POIs and reveals them on approach:
    /// first approach calls FieldLog.Discover (discover-once — the log is the
    /// single source of truth) and celebrates; later approaches just show the
    /// fact again. Built by TravelManager on arrival, destroyed on return.
    public sealed class PoiSystem : MonoBehaviour
    {
        const float RevealRadiusMeters = 6f;

        static readonly Color UndiscoveredColor = new Color(0.25f, 0.55f, 0.95f);
        static readonly Color DiscoveredColor = new Color(0.95f, 0.75f, 0.25f);

        FieldLog _log;
        Transform _head;
        readonly List<Marker> _markers = new List<Marker>();
        static MaterialPropertyBlock _block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        sealed class Marker
        {
            public PoiEntry Poi;
            public Transform Root;
            public Renderer Beacon;
            public GameObject FactPanel;
            public bool Revealed;
        }

        public int MarkerCount => _markers.Count;
        public int RevealedCount { get; private set; }

        public void Build(PoiSet set, Terrain terrain, FieldLog log, Transform head)
        {
            _log = log;
            _head = head;
            float halfX = terrain.terrainData.size.x / 2f;
            float halfZ = terrain.terrainData.size.z / 2f;
            foreach (var poi in set.pois)
            {
                if (!poi.HasPosition)
                {
                    Debug.LogWarning($"[PoiSystem] '{poi.id}' has no baked position — skipped until the placement pass runs for this site.");
                    continue;
                }
                if (Mathf.Abs(poi.positionX) > halfX || Mathf.Abs(poi.positionZ) > halfZ)
                    throw new System.InvalidOperationException(
                        $"POI '{poi.id}' at ({poi.positionX}, {poi.positionZ}) is outside the ±({halfX}, {halfZ}) site extents.");
                var marker = CreateMarker(poi, terrain);
                // The log is the single source of truth: a POI discovered on an
                // earlier visit arrives already revealed and gold.
                if (log.HasDiscovered(poi.id))
                {
                    marker.Revealed = true;
                    RevealedCount++;
                    SetBeaconColor(marker.Beacon, DiscoveredColor);
                }
                _markers.Add(marker);
            }
        }

        void Update()
        {
            if (_head == null) return;
            Vector3 headPos = _head.position;
            for (int i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                Vector3 delta = marker.Root.position - headPos;
                delta.y = 0;
                bool near = delta.sqrMagnitude < RevealRadiusMeters * RevealRadiusMeters;
                if (near && !marker.FactPanel.activeSelf)
                {
                    marker.FactPanel.SetActive(true);
                    if (!marker.Revealed)
                    {
                        marker.Revealed = true;
                        RevealedCount++;
                        // Discover returns true only the first time ever —
                        // the celebration hook for later polish.
                        bool fresh = _log.Discover(marker.Poi.id);
                        SetBeaconColor(marker.Beacon, DiscoveredColor);
                        if (fresh)
                            Debug.Log($"[PoiSystem] discovered {marker.Poi.id}");
                    }
                }
                else if (!near && marker.FactPanel.activeSelf)
                {
                    marker.FactPanel.SetActive(false);
                }
            }
        }

        Marker CreateMarker(PoiEntry poi, Terrain terrain)
        {
            var world = new Vector3(poi.positionX, 0, poi.positionZ);
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;

            var root = new GameObject("POI_" + poi.id.Replace('/', '_'));
            root.transform.SetParent(transform, false);
            root.transform.position = world;

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Beacon";
            beacon.transform.SetParent(root.transform, false);
            beacon.transform.localPosition = new Vector3(0, 1.25f, 0);
            beacon.transform.localScale = new Vector3(0.25f, 1.25f, 0.25f);
            var collider = beacon.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var renderer = beacon.GetComponent<Renderer>();
            SetBeaconColor(renderer, UndiscoveredColor);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var panel = BuildFactPanel(poi, root.transform, _head);
            panel.SetActive(false);

            return new Marker { Poi = poi, Root = root.transform, Beacon = renderer, FactPanel = panel };
        }

        static void SetBeaconColor(Renderer renderer, Color color)
        {
            // Property block, not .material: no instance leak in edit mode
            // (tests) and no per-marker material allocation in play mode.
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        static GameObject BuildFactPanel(PoiEntry poi, Transform parent, Transform head)
        {
            var canvasGo = new GameObject("FactPanel", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.transform.SetParent(parent, false);
            var rect = (RectTransform)canvasGo.transform;
            rect.localPosition = new Vector3(0, 2.9f, 0);
            rect.sizeDelta = new Vector2(1.6f, 0.9f);
            rect.localScale = Vector3.one;
            canvasGo.AddComponent<Billboard>().Face(head);

            const float s = 0.002f;
            var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRect = (RectTransform)bg.transform;
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.10f, 0.92f);

            var text = new GameObject("Text", typeof(RectTransform), typeof(Text));
            text.transform.SetParent(canvasGo.transform, false);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(1.5f / s, 0.82f / s);
            textRect.localScale = Vector3.one * s;
            var label = text.GetComponent<Text>();
            label.text = "<b>" + poi.title + "</b>\n\n" + poi.fact;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 26;
            label.alignment = TextAnchor.UpperLeft;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return canvasGo;
        }
    }

    /// Faces a transform at the player's head each frame (labels only). The
    /// head is injected — no Camera.main tag dependency, which would fail
    /// silently if the tag were ever lost.
    public sealed class Billboard : MonoBehaviour
    {
        Transform _head;

        public void Face(Transform head) => _head = head;

        void LateUpdate()
        {
            if (_head == null) return;
            Vector3 toHead = transform.position - _head.position;
            toHead.y = 0;
            if (toHead.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toHead);
        }
    }
}
