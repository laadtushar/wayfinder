using System.Collections.Generic;
using UnityEngine;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// A small compass on the back of the player's gloved hand: a needle that
    /// points to the nearest undiscovered point of interest (research: the
    /// "wayfinding instrument" — orientation without a map breaking presence).
    /// Reads the live FieldLog + PoiSystem, rotates a needle transform by the
    /// engine-free bearing. Surface-only; hidden on the Bridge.
    public sealed class PalmCompass : MonoBehaviour
    {
        [Tooltip("The needle child rotated to point at the target (local Z = forward).")]
        [SerializeField] private Transform needle;
        [Tooltip("Root shown/hidden with surface state.")]
        [SerializeField] private GameObject visualRoot;
        [Tooltip("The head/camera whose forward defines 'ahead' for the bearing.")]
        [SerializeField] private Transform head;

        readonly List<PresenceMath.P2> _poiXZ = new List<PresenceMath.P2>();
        readonly List<string> _poiIds = new List<string>();
        readonly List<Vector3> _poiWorld = new List<Vector3>();
        readonly HashSet<string> _discovered = new HashSet<string>(); // cached, not rebuilt per frame
        int _lastLogCount = -1;
        FieldLog _log;
        bool _active;

        void Awake()
        {
            if (needle == null) throw new System.InvalidOperationException($"{name}: no needle.");
            if (visualRoot == null) throw new System.InvalidOperationException($"{name}: no visual root.");
            if (head == null) throw new System.InvalidOperationException($"{name}: no head transform.");
            visualRoot.SetActive(false);
        }

        /// Arm the compass with the current site's POIs + the session log.
        /// positions are world-space; ids match the FieldLog's discovered ids.
        public void Arm(IReadOnlyList<Vector3> poiWorldPositions, IReadOnlyList<string> poiIds, FieldLog log)
        {
            _poiXZ.Clear(); _poiIds.Clear(); _poiWorld.Clear();
            if (poiWorldPositions != null)
                for (int i = 0; i < poiWorldPositions.Count; i++)
                {
                    _poiWorld.Add(poiWorldPositions[i]);
                    _poiXZ.Add(new PresenceMath.P2(poiWorldPositions[i].x, poiWorldPositions[i].z));
                    _poiIds.Add(poiIds != null && i < poiIds.Count ? poiIds[i] : null);
                }
            _log = log;
            _lastLogCount = -1; // force a discovered-set rebuild next Update
            _active = true;
            visualRoot.SetActive(true);
        }

        public void Disarm()
        {
            _active = false;
            visualRoot.SetActive(false);
        }

        void Update()
        {
            if (!_active || _log == null) return;

            // Rebuild the discovered set only when the log actually changed —
            // discoveries are rare; no per-frame allocation on the hot path.
            if (_log.Count != _lastLogCount)
            {
                _discovered.Clear();
                foreach (var id in _log.DiscoveredIds) _discovered.Add(id);
                _lastLogCount = _log.Count;
            }

            var player = new PresenceMath.P2(head.position.x, head.position.z);
            int idx = PresenceMath.NearestUndiscovered(player, _poiXZ, _poiIds, _discovered, out _);
            if (idx < 0)
            {
                needle.rotation = Quaternion.LookRotation(head.forward, Vector3.up); // all found — rest ahead
                return;
            }
            // Orient the needle in WORLD space toward the target — a real
            // compass points at a fixed bearing regardless of how it's held.
            Vector3 dir = _poiWorld[idx] - head.position; dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
                needle.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}
