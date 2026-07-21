using System;

namespace Wayfinder.Core
{
    /// Android XR quality-checklist rule: an interactive target must scale with
    /// distance as DistanceInMeters x 0.868 x 48dp minimum (56dp recommended).
    /// Used to size POI markers and bridge controls so they pass review.
    public static class InteractionTargets
    {
        private const float DpToDmmFactor = 0.868f;
        private const float MinimumDp = 48f;
        private const float RecommendedDp = 56f;

        public static float MinimumSizeMeters(float distanceMeters) =>
            SizeMeters(distanceMeters, MinimumDp);

        public static float RecommendedSizeMeters(float distanceMeters) =>
            SizeMeters(distanceMeters, RecommendedDp);

        private static float SizeMeters(float distanceMeters, float dp)
        {
            if (distanceMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(distanceMeters),
                    "Distance must be positive.");
            return distanceMeters * DpToDmmFactor * dp / 1000f;
        }
    }
}
