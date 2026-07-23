using System;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Wayfinder.Unity
{
    /// Requests the Android XR runtime permissions Wayfinder's optional
    /// features need — hand tracking (fine hand joints) and eye tracking
    /// (eye-tracked foveation + gaze) — and exposes the granted state so those
    /// features degrade gracefully on denial (audit risk #2: never hard-require
    /// a permissioned feature in the core loop). The guaranteed floor — the
    /// permissionless hand INTERACTION profile (pinch/aim) + fixed foveation —
    /// always works, so a denied permission never breaks the game.
    public sealed class XRPermissions : MonoBehaviour
    {
        // Android XR permission strings.
        const string HandTracking = "android.permission.HAND_TRACKING";
        const string EyeTrackingFine = "android.permission.EYE_TRACKING_FINE";

        public static bool HandTrackingGranted { get; private set; }
        public static bool EyeTrackingGranted { get; private set; }

        /// Raised once both requests have resolved (granted or denied), so
        /// feature controllers can enable the permissioned path or the floor.
        public static event Action Resolved;

        int _pending;

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Request(HandTracking, granted => HandTrackingGranted = granted);
            Request(EyeTrackingFine, granted => EyeTrackingGranted = granted);
            if (_pending == 0) RaiseResolved();
#else
            // Editor / non-Android: assume granted so in-editor iteration works;
            // the real grant only happens on device.
            HandTrackingGranted = true;
            EyeTrackingGranted = true;
            RaiseResolved();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        void Request(string permission, Action<bool> onResolved)
        {
            if (Permission.HasUserAuthorizedPermission(permission))
            {
                onResolved(true);
                return;
            }
            _pending++;
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => { onResolved(true); Done(); };
            callbacks.PermissionDenied += _ => { onResolved(false); Done(); };
            callbacks.PermissionDeniedAndDontAskAgain += _ => { onResolved(false); Done(); };
            Permission.RequestUserPermission(permission, callbacks);
        }

        void Done()
        {
            _pending--;
            if (_pending <= 0) RaiseResolved();
        }
#endif

        void RaiseResolved()
        {
            Debug.Log($"[XRPermissions] resolved — hand={HandTrackingGranted} eye={EyeTrackingGranted}");
            Resolved?.Invoke();
        }
    }
}
