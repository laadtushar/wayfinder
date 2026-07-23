using UnityEngine;
using Wayfinder.Core;

namespace Wayfinder.Unity.Companion
{
    /// Shows a companion panel only in the right place: the bridge planning
    /// console on the bridge, the rig-anchored surface console once you're
    /// walking a world. Lives on an always-active object and toggles a target
    /// panel by TravelManager.State — cheap (one compare per frame, no alloc,
    /// SetActive only on change). This is how one companion "brain"
    /// (BridgeCompanion, reading the persistent TravelManager) drives two views
    /// without either being visible in the wrong context.
    public sealed class CompanionVisibilityGate : MonoBehaviour
    {
        [SerializeField] private TravelManager travel;
        [SerializeField] private GameObject target;
        [Tooltip("True: visible on a surface (hidden on the bridge). False: visible on the bridge.")]
        [SerializeField] private bool showOnSurface;

        void Awake()
        {
            if (travel == null) throw new System.InvalidOperationException($"{name}: gate has no TravelManager.");
            if (target == null) throw new System.InvalidOperationException($"{name}: gate has no target.");
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            bool onSurface = travel.State == TravelState.OnSurface;
            bool show = showOnSurface ? onSurface : !onSurface;
            if (target.activeSelf != show) target.SetActive(show);
        }
    }
}
