using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// Google-Maps-immersive movement: pinch-grab the world and pull yourself
    /// across the terrain. Surface-only (disabled on the Bridge), tuned for
    /// comfort per the presence research — one hand primary, moderate gain,
    /// NO rotation and NO scaling from the gesture (uncommanded camera motion
    /// is the comfort rule's whole prohibition). Teleport stays as the
    /// complementary big-hop locomotion.
    public sealed class WorldGrabController : MonoBehaviour
    {
        [SerializeField] private GrabMoveProvider leftGrab;
        [SerializeField] private GrabMoveProvider rightGrab;
        [SerializeField] private TwoHandedGrabMoveProvider twoHandedGrab;
        [Tooltip("The GameObject carrying the single-hand grab providers — gated by SetActive (component.enabled is a no-op on an inactive GO).")]
        [SerializeField] private GameObject singleHandGrabRoot;

        [Tooltip("Displacement gain — hand delta × this. Earth VR sits ~1.5–2×.")]
        [SerializeField] private float moveFactor = 1.6f;

        public bool GrabEnabled { get; private set; }

        void Awake()
        {
            if (leftGrab == null || rightGrab == null || twoHandedGrab == null || singleHandGrabRoot == null)
                throw new System.InvalidOperationException($"{name}: grab providers not all wired.");

            // Comfort lock, set once: the drag never rotates or scales the world.
            twoHandedGrab.enableRotation = false;
            twoHandedGrab.enableScaling = false;
            twoHandedGrab.requireTwoHandsForTranslation = false; // one hand primary
            leftGrab.moveFactor = moveFactor;
            rightGrab.moveFactor = moveFactor;
            twoHandedGrab.moveFactor = moveFactor;

            // The single-hand provider COMPONENTS stay enabled — gating is by
            // the GO's active state (a disabled component on an active GO would
            // silently kill one-hand drag even when the root is active).
            leftGrab.enabled = true;
            rightGrab.enabled = true;

            SetEnabled(TravelState.OnBridge);
        }

        /// Grab-move is on only while standing on a surface. The single-hand
        /// providers live on their own GO, gated by SetActive so their Update
        /// actually runs (component.enabled on an inactive GO is a no-op); the
        /// two-handed coordinator sits on the always-active locomotion root.
        public void SetEnabled(TravelState state)
        {
            GrabEnabled = state == TravelState.OnSurface;
            singleHandGrabRoot.SetActive(GrabEnabled);
            twoHandedGrab.enabled = GrabEnabled;
        }
    }
}
