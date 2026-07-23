using System;

namespace Wayfinder.Core
{
    /// The look/behaviour policy for surface POI waypoint beacons, kept engine-free
    /// so the gaze weighting and discovered/undiscovered state are pure and
    /// testable. The Unity PoiBeaconField reads a site's POI records + the field
    /// log, then drives each beacon's column height + emissive intensity from
    /// these functions. Horizontal-only: a beacon is a vertical light column, so
    /// "looking at it" is a yaw comparison in the XZ plane (pitch-independent).
    public static class PoiBeacons
    {
        /// The tall column height (m) for an undiscovered beacon; a discovered one
        /// collapses to a short marker.
        public const float FullHeight = 4.0f;
        public const float DiscoveredHeight = 0.6f;

        /// How brightly a beacon burns, 0..1. Undiscovered beacons idle bright and
        /// swell toward the traveler's gaze; a discovered POI is logged, so its
        /// beacon dims to a quiet marker.
        public static float Intensity(bool discovered, float gazeWeight)
        {
            if (discovered) return 0.18f;
            return 0.55f + 0.45f * Clamp01(gazeWeight);
        }

        /// Column height (m) by discovered state.
        public static float Height(bool discovered) => discovered ? DiscoveredHeight : FullHeight;

        /// 0..1 weight for how squarely the head faces a beacon, in the horizontal
        /// plane. 1 when looking straight at it, 0 once the beacon is more than
        /// maxAngleDeg off the gaze yaw, smoothstepped between. dir* is the
        /// horizontal vector from the head to the beacon; fwd* is the head's
        /// horizontal forward.
        public static float GazeWeight(float dirX, float dirZ, float fwdX, float fwdZ, float maxAngleDeg)
        {
            double dl = Math.Sqrt(dirX * dirX + dirZ * dirZ);
            double fl = Math.Sqrt(fwdX * fwdX + fwdZ * fwdZ);
            if (dl < 1e-6 || fl < 1e-6) return 0f;
            double dot = (dirX * fwdX + dirZ * fwdZ) / (dl * fl);          // cos(angle)
            double minCos = Math.Cos(maxAngleDeg * Math.PI / 180.0);
            if (dot <= minCos) return 0f;
            double t = (dot - minCos) / (1.0 - minCos);                    // 0..1
            return (float)(t * t * (3.0 - 2.0 * t));                       // smoothstep
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
