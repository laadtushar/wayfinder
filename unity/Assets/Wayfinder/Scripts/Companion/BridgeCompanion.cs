using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Wayfinder.Core;
using Wayfinder.Core.Companion;

namespace Wayfinder.Unity.Companion
{
    /// The bridge-side AI companion. On demand it snapshots the current world,
    /// its real POI records, and the field log into an engine-free
    /// CompanionContext, then asks the active backend — Gemini if Firebase is
    /// configured (docs/companion-setup.md), otherwise the offline stub, which
    /// always answers. Attach to the Bridge and wire the TravelManager.
    public sealed class BridgeCompanion : MonoBehaviour
    {
        [SerializeField] private TravelManager travel;
        [Tooltip("For the docked 'expedition so far' summary (per-world discovery tallies).")]
        [SerializeField] private WorldCatalog catalog;

        [Header("Gemini backend (optional — see docs/companion-setup.md)")]
        [Tooltip("Gemini flash model id. [unverified] confirm against your Firebase console's available models.")]
        [SerializeField] private string modelName = "gemini-2.5-flash";
        [Tooltip("App Check debug token for the editor / a device without Play Integrity. Leave empty in production.")]
        [SerializeField] private string appCheckDebugToken = "";

        [Header("Sky naming — 'what am I looking at?'")]
        [Tooltip("The head/eye camera the gaze is read from. Falls back to Camera.main.")]
        [SerializeField] private Camera headCamera;

        readonly StubCompanionProvider _stub = new StubCompanionProvider();
        CompanionService _service;

        /// Which backend a question would hit right now ("stub" or "gemini").
        public string ActiveBackend => _service != null ? _service.PreferredProviderName : _stub.Name;

        void Awake()
        {
            if (travel == null)
                throw new System.InvalidOperationException($"{name}: no TravelManager assigned.");
            // Offline stub only, until (and unless) Firebase initializes in Start.
            _service = new CompanionService(null, _stub);
        }

        async void Start()
        {
            var gemini = await FirebaseCompanionProvider.TryCreateAsync(
                modelName, appCheckDebugToken, msg => Debug.Log(msg));
            if (gemini != null && gemini.IsAvailable)
                _service = new CompanionService(gemini, _stub);
        }

        /// Ask the companion a question, grounded in the current world + field log.
        public Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
        {
            // "What am I looking at?" is answered locally from the head's gaze —
            // it needs the live camera direction, not a text backend. Always
            // grounded, always offline.
            if (IsSkyQuestion(question))
                return Task.FromResult(AnswerSky());
            return _service.AskAsync(BuildContext(), question, cancellationToken);
        }

        /// True for a gaze/sky-identification question ("what am I looking at?",
        /// "name that constellation", "what's that in the sky?").
        internal static bool IsSkyQuestion(string q)
        {
            if (string.IsNullOrEmpty(q)) return false;
            q = q.ToLowerInvariant();
            return q.Contains("looking at")
                || q.Contains("what's that") || q.Contains("whats that") || q.Contains("what is that")
                || q.Contains("name that") || q.Contains("constellation")
                || (q.Contains("what") && q.Contains("sky"));
        }

        /// Names the nearest bright sky feature to where the head is pointed: a
        /// constellation (airless worlds only — a daytime haze washes stars out),
        /// the Sun, or Earth where it hangs. Directions come from the live scene
        /// (the star map's fixed RA/Dec, the sun light, the Earth disc); the
        /// nearest-by-angle pick is the engine-free, unit-tested SkyGuide.
        string AnswerSky()
        {
            if (travel.State != TravelState.OnSurface)
                return "We're aboard the bridge — warp down to a surface and I'll read the sky with you.";

            var cam = headCamera != null ? headCamera : Camera.main;
            if (cam == null)
                return "I can't tell where you're looking right now.";
            Vector3 f = cam.transform.forward;
            var gaze = new SkyVec(f.x, f.y, f.z);

            var package = travel.CurrentPackage;
            bool airless = package == null || !package.HazeEnabled;

            var features = new List<SkyFeature>();
            if (airless)
                foreach (var c in SkyGuide.Constellations) features.Add(c);

            // The Sun — the direction its light arrives FROM.
            var sun = RenderSettings.sun != null ? RenderSettings.sun : FindDirectionalLight();
            if (sun != null)
            {
                Vector3 s = -sun.transform.forward;
                string sunFact = airless
                    ? "That's the Sun — the same star as home, but with no air to scatter its light it burns in a black daytime sky, and every shadow is knife-sharp."
                    : "That's the Sun — smaller and paler than from Earth, dimmed to a wan butterscotch disc by the dust always hanging in the Martian air.";
                features.Add(new SkyFeature("the Sun", sunFact, new SkyVec(s.x, s.y, s.z)));
            }

            // Earth — only where it actually hangs in the sky (the disc object the
            // surface scene places; e.g. fixed high over Tranquillity).
            var earthGo = GameObject.Find("EarthSky");
            if (earthGo != null)
            {
                Vector3 e = earthGo.transform.position - cam.transform.position;
                features.Add(new SkyFeature("Earth",
                    "That's Earth — the whole of home hanging in the black, small enough to hide behind your thumb. Everyone who ever lived, save a couple dozen, is on that one blue dot.",
                    new SkyVec(e.x, e.y, e.z)));
            }

            var hit = SkyGuide.NearestFeature(features, gaze, 24f);
            if (hit != null) return hit.Fact;

            return airless
                ? "Just open black sky and scattered stars that way — nothing named in your line of sight. Sweep around and I'll call out what I know."
                : "The daytime sky here is a dusty butterscotch — the stars are drowned out until dusk. Look for the Sun, small and pale through the haze.";
        }

        static Light FindDirectionalLight()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        CompanionContext BuildContext()
        {
            var world = travel.CurrentWorld;                 // null on the bridge
            if (world == null)
                return new CompanionContext(null, "the bridge", 0f, false, null, BuildExpedition());

            var pois = new List<CompanionPoi>();
            var package = travel.CurrentPackage;
            if (package != null && package.PoiData != null)
            {
                try
                {
                    var set = PoiSet.Parse(package.PoiData.text);
                    var log = travel.FieldLog;
                    foreach (var e in set.pois)
                        pois.Add(new CompanionPoi(e.id, e.title, e.fact, e.source, log.HasDiscovered(e.id)));
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Companion] POI parse failed for '{world.Id}': {ex.Message}");
                }
            }

            bool onSurface = travel.State == TravelState.OnSurface;
            return new CompanionContext(world.Id, world.DisplayName, world.SurfaceGravity, onSurface, pois);
        }

        /// Cross-world discovery tally for the docked companion, computed from the
        /// catalog's POI counts + the session field log (reuses the tested
        /// FieldLogProgress prefix accounting). Empty if no catalog is wired.
        System.Collections.Generic.List<CompanionWorldTally> BuildExpedition()
        {
            var list = new System.Collections.Generic.List<CompanionWorldTally>();
            if (catalog == null) return list;

            var order = new System.Collections.Generic.List<string>();
            var totals = new System.Collections.Generic.Dictionary<string, int>();
            var names = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var pkg in catalog.Packages)
            {
                if (pkg == null) continue;
                var def = pkg.ToDefinition();
                order.Add(def.Id);
                names[def.Id] = def.DisplayName;
                int count = 0;
                if (pkg.PoiData != null)
                {
                    try { count = PoiSet.Parse(pkg.PoiData.text).pois.Count; }
                    catch (System.Exception e) { Debug.LogWarning($"[Companion] POI count failed for '{def.Id}': {e.Message}"); }
                }
                totals[def.Id] = count;
            }

            foreach (var w in FieldLogProgress.PerWorld(travel.FieldLog.DiscoveredIds, order, totals))
                list.Add(new CompanionWorldTally(
                    names.TryGetValue(w.WorldId, out var n) ? n : w.WorldId, w.Discovered, w.Total));
            return list;
        }
    }
}
