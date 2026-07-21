using System.Collections;
using UnityEngine;

namespace Wayfinder.Unity
{
    /// Development-build measurement instrumentation: boots the app straight
    /// into Site One so an unattended headset produces frame evidence for the
    /// gate (build-plan Task 2.8). Compiled into every build but inert outside
    /// development builds and destroys itself either way after acting.
    public sealed class PerfProbe : MonoBehaviour
    {
        [SerializeField] private DestinationMenu menu;
        [Tooltip("Seconds on the Bridge before auto-warping to the probe site.")]
        [SerializeField] private float delaySeconds = 8f;
        [SerializeField] private string siteId = "mars-olympus";

        IEnumerator Start()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
            {
                Destroy(gameObject);
                yield break;
            }
            yield return new WaitForSeconds(delaySeconds);
            if (menu != null)
            {
                Debug.Log($"[PerfProbe] auto-warping to {siteId} for frame measurement");
                menu.Select(siteId);
            }
            Destroy(gameObject);
        }
    }
}
