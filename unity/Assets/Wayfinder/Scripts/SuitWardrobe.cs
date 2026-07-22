using UnityEngine;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// Dresses the player's tracked hands for the current context: sleek
    /// jumpsuit gloves aboard the Bridge, white EVA gloves on a surface
    /// (embodiment direction — you should see yourself suited). Driven by
    /// TravelManager at each transition; materials are data, no per-world code.
    public sealed class SuitWardrobe : MonoBehaviour
    {
        [SerializeField] private Material bridgeGloveMaterial;
        [SerializeField] private Material surfaceGloveMaterial;
        [Tooltip("The skinned meshes of both tracked hands (from the hand visualizer prefabs on the rig).")]
        [SerializeField] private SkinnedMeshRenderer[] handRenderers;

        public Material CurrentGlove { get; private set; }

        void Awake()
        {
            if (bridgeGloveMaterial == null)
                throw new System.InvalidOperationException($"{name}: no bridge glove material.");
            if (surfaceGloveMaterial == null)
                throw new System.InvalidOperationException($"{name}: no surface glove material.");
            if (handRenderers == null || handRenderers.Length == 0)
                throw new System.InvalidOperationException($"{name}: no hand renderers wired.");
            Apply(TravelState.OnBridge);
        }

        /// Swap gloves for the state we are arriving in. Shared materials —
        /// zero per-frame cost, no instances.
        public void Apply(TravelState state)
        {
            bool surface = state == TravelState.OnSurface || state == TravelState.WarpingToSurface;
            CurrentGlove = surface ? surfaceGloveMaterial : bridgeGloveMaterial;
            foreach (var renderer in handRenderers)
            {
                if (renderer == null) continue;
                renderer.sharedMaterial = CurrentGlove;
            }
        }
    }
}
