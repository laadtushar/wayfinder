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
                return new CompanionContext(null, "the bridge", 0f, false, null);

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
    }
}
