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
            return _service.AskAsync(BuildContext(), question, cancellationToken);
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
