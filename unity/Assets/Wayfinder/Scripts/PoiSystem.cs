using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// Spawns holographic waypoint beacons for a site's POIs and reveals them on
    /// approach: first approach calls FieldLog.Discover (discover-once — the log
    /// is the single source of truth) and celebrates; later approaches just show
    /// the fact again. Built by TravelManager on arrival, destroyed on return.
    ///
    /// Each beacon is a thin cyan HDR light column over a soft ground pool
    /// (additive, reads as a projection, not solid geometry). An undiscovered
    /// beacon stands tall and swells brighter as you look at it (gaze weighting
    /// from the engine-free, unit-tested Wayfinder.Core.PoiBeacons); a discovered
    /// one collapses to a short, dim marker — the sky is read, the pin is logged.
    public sealed class PoiSystem : MonoBehaviour
    {
        const float RevealRadiusMeters = 6f;
        const float MaxGazeAngleDeg = 34f;

        // Holographic cyan; the per-beacon HDR intensity rides on top (property
        // block), scaled up so URP Bloom picks the columns out of the dark.
        static readonly Color BeaconCyan = new Color(0.35f, 0.85f, 1.05f);
        const float GlowScale = 2.6f;

        FieldLog _log;
        Transform _head;
        readonly List<Marker> _markers = new List<Marker>();
        Material _beaconMat;
        static MaterialPropertyBlock _block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        sealed class Marker
        {
            public PoiEntry Poi;
            public Transform Root;
            public Transform Column;     // the light shaft — rescaled on discovery
            public Renderer ColumnRenderer;
            public Renderer PoolRenderer; // the ground glow
            public GameObject FactPanel;
            public bool Revealed;
        }

        public int MarkerCount => _markers.Count;
        public int RevealedCount { get; private set; }

        /// World positions + ids of every placed POI, for the palm compass to
        /// point at the nearest undiscovered one. Fills the passed lists.
        public void CollectPoiTargets(List<Vector3> positions, List<string> ids)
        {
            positions.Clear(); ids.Clear();
            foreach (var m in _markers)
            {
                positions.Add(m.Root.position);
                ids.Add(m.Poi.id);
            }
        }

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
                // earlier visit arrives already revealed, collapsed and dim.
                if (log.HasDiscovered(poi.id))
                {
                    marker.Revealed = true;
                    RevealedCount++;
                    ApplyDiscovered(marker, true);
                }
                else
                {
                    ApplyDiscovered(marker, false);
                }
                _markers.Add(marker);
            }
        }

        void Update()
        {
            if (_head == null) return;
            Vector3 headPos = _head.position;
            Vector3 fwd = _head.forward;
            for (int i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                Vector3 delta = marker.Root.position - headPos;
                delta.y = 0;

                // Reveal on approach (unchanged behaviour: the field log is the
                // truth, discover-once, fact panel shown within the radius).
                bool near = delta.sqrMagnitude < RevealRadiusMeters * RevealRadiusMeters;
                if (near && !marker.FactPanel.activeSelf)
                {
                    marker.FactPanel.SetActive(true);
                    if (!marker.Revealed)
                    {
                        marker.Revealed = true;
                        RevealedCount++;
                        bool fresh = _log.Discover(marker.Poi.id);
                        ApplyDiscovered(marker, true);
                        if (fresh)
                            Debug.Log($"[PoiSystem] discovered {marker.Poi.id}");
                    }
                }
                else if (!near && marker.FactPanel.activeSelf)
                {
                    marker.FactPanel.SetActive(false);
                }

                // Gaze swell: an undiscovered beacon brightens as the head turns
                // toward it. Discovered beacons ignore gaze (they idle dim).
                if (!marker.Revealed)
                {
                    float gaze = PoiBeacons.GazeWeight(delta.x, delta.z, fwd.x, fwd.z, MaxGazeAngleDeg);
                    SetIntensity(marker, PoiBeacons.Intensity(false, gaze));
                }
            }
        }

        // Column height + a baseline intensity for the discovered/undiscovered
        // state; the per-frame gaze swell (undiscovered only) rides over this.
        void ApplyDiscovered(Marker marker, bool discovered)
        {
            float h = PoiBeacons.Height(discovered);
            var s = marker.Column.localScale;
            marker.Column.localScale = new Vector3(s.x, h * 0.5f, s.z);
            marker.Column.localPosition = new Vector3(0f, h * 0.5f, 0f);
            SetIntensity(marker, PoiBeacons.Intensity(discovered, 0f));
        }

        void SetIntensity(Marker marker, float intensity)
        {
            Color c = BeaconCyan * (intensity * GlowScale);
            SetColor(marker.ColumnRenderer, c);
            SetColor(marker.PoolRenderer, c * 0.6f);   // the pool is a softer echo
        }

        static void SetColor(Renderer renderer, Color color)
        {
            // Property block, not .material: no per-marker material allocation
            // and no instance leak in edit mode (the contract test builds markers
            // without entering play mode).
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        Marker CreateMarker(PoiEntry poi, Terrain terrain)
        {
            var world = new Vector3(poi.positionX, 0, poi.positionZ);
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;

            var root = new GameObject("POI_" + poi.id.Replace('/', '_'));
            root.transform.SetParent(transform, false);
            root.transform.position = world;

            // Light column (height set later by ApplyDiscovered).
            var column = MakePrimitive(PrimitiveType.Cylinder, "Column", root.transform,
                new Vector3(0f, 2f, 0f), new Vector3(0.1f, 2f, 0.1f));

            // Ground pool — a thin glowing disc at the base.
            var pool = MakePrimitive(PrimitiveType.Cylinder, "Pool", root.transform,
                new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.008f, 1.1f));

            var panel = BuildFactPanel(poi, root.transform, _head);
            panel.SetActive(false);

            return new Marker
            {
                Poi = poi,
                Root = root.transform,
                Column = column.transform,
                ColumnRenderer = column.GetComponent<Renderer>(),
                PoolRenderer = pool.GetComponent<Renderer>(),
                FactPanel = panel,
            };
        }

        GameObject MakePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var collider = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = BeaconMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // One shared additive-unlit material for every beacon (per-beacon colour
        // rides on a property block). Created at runtime because PoiSystem is
        // spawned by TravelManager, not authored in a scene.
        Material BeaconMaterial()
        {
            if (_beaconMat != null) return _beaconMat;
            _beaconMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _beaconMat.SetFloat("_Surface", 1f);                                   // transparent
            _beaconMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _beaconMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); // soft additive
            _beaconMat.SetFloat("_ZWrite", 0f);
            _beaconMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _beaconMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _beaconMat.SetColor(BaseColorId, BeaconCyan);
            return _beaconMat;
        }

        void OnDestroy()
        {
            if (_beaconMat == null) return;
            if (Application.isPlaying) Destroy(_beaconMat); else DestroyImmediate(_beaconMat);
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
