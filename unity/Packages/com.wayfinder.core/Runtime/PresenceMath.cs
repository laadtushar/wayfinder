using System.Collections.Generic;

namespace Wayfinder.Core
{
    /// Engine-free presence-instrument math (#21): the palm compass's bearing
    /// to the nearest undiscovered point of interest, and the boot-print ring
    /// buffer's slot allocation. Pure so it is testable headless — the Unity
    /// components just render what these return.
    public static class PresenceMath
    {
        /// A 2D world position (metres, XZ plane). Engine-free, no UnityEngine.
        public readonly struct P2
        {
            public readonly float X;
            public readonly float Z;
            public P2(float x, float z) { X = x; Z = z; }
        }

        /// Index of the nearest POI whose id is NOT in `discovered`, or -1 if
        /// every POI is discovered / the list is empty. `outDistance` is the
        /// planar distance to it (0 when none).
        public static int NearestUndiscovered(
            P2 player, IReadOnlyList<P2> poiPositions, IReadOnlyList<string> poiIds,
            ISet<string> discovered, out float outDistance)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            outDistance = 0f;
            if (poiPositions == null) return -1;
            for (int i = 0; i < poiPositions.Count; i++)
            {
                if (poiIds != null && i < poiIds.Count && discovered != null && discovered.Contains(poiIds[i]))
                    continue;
                float dx = poiPositions[i].X - player.X;
                float dz = poiPositions[i].Z - player.Z;
                float d2 = dx * dx + dz * dz;
                if (d2 < bestSq) { bestSq = d2; best = i; }
            }
            if (best >= 0)
                outDistance = (float)System.Math.Sqrt(bestSq);
            return best;
        }

        /// Signed bearing in degrees from the player's forward to the target,
        /// in [-180, 180]. 0 = dead ahead, +90 = to the right, -90 = left.
        /// `forward` need not be normalized; a zero forward returns 0.
        public static float RelativeBearing(P2 player, P2 forward, P2 target)
        {
            float fx = forward.X, fz = forward.Z;
            float flen = (float)System.Math.Sqrt(fx * fx + fz * fz);
            if (flen < 1e-6f) return 0f;
            fx /= flen; fz /= flen;

            float tx = target.X - player.X, tz = target.Z - player.Z;
            float tlen = (float)System.Math.Sqrt(tx * tx + tz * tz);
            if (tlen < 1e-6f) return 0f;
            tx /= tlen; tz /= tlen;

            // Angle of target relative to forward. Dot = cos, 2D cross = sin.
            // Unity is left-handed (Y up, +X right, +Z forward): a target to the
            // player's right (+X of forward) must read +90, so cross = fz*tx - fx*tz.
            float dot = fx * tx + fz * tz;
            float cross = fz * tx - fx * tz;
            float deg = (float)(System.Math.Atan2(cross, dot) * 180.0 / System.Math.PI);
            return deg;
        }

        /// Next boot-print slot in a ring of `capacity`, given the count of
        /// prints laid so far. Beyond capacity it wraps to overwrite the oldest.
        public static int RingSlot(int printsLaid, int capacity)
        {
            if (capacity <= 0) return -1;
            int m = printsLaid % capacity;
            return m < 0 ? m + capacity : m;
        }
    }
}
