using System.Collections.Generic;

namespace Wayfinder.Core
{
    /// A unit direction on the sky sphere. Engine-free (no UnityEngine) so the
    /// "what am I looking at?" lookup is pure and testable.
    public readonly struct SkyVec
    {
        public readonly float X, Y, Z;

        public SkyVec(float x, float y, float z)
        {
            float m = (float)System.Math.Sqrt(x * x + y * y + z * z);
            if (m < 1e-9f) m = 1f;
            X = x / m; Y = y / m; Z = z / m;
        }

        public float Dot(SkyVec o) => X * o.X + Y * o.Y + Z * o.Z;
    }

    /// A nameable bright feature of the sky (a constellation, Earth, the Sun).
    public sealed class SkyFeature
    {
        public string Name { get; }
        public string Fact { get; }
        public SkyVec Dir { get; }

        public SkyFeature(string name, string fact, SkyVec dir)
        {
            Name = name; Fact = fact; Dir = dir;
        }
    }

    /// Maps a gaze direction to the sky feature you're looking at. The RA/Dec ->
    /// world-direction transform matches Unity's Skybox/Panoramic LatitudeLongitude
    /// layout the star map is baked for (calibrated in-editor): +Y is the north
    /// celestial pole, and u = RA/24 with _Rotation = 0.
    public static class SkyGuide
    {
        /// RA (hours), Dec (degrees) -> a unit world direction on the shared sky.
        public static SkyVec RaDecToDir(double raHours, double decDeg)
        {
            double theta = System.Math.PI - raHours * System.Math.PI / 12.0;
            double dec = decDeg * System.Math.PI / 180.0;
            double c = System.Math.Cos(dec);
            return new SkyVec(
                (float)(c * System.Math.Cos(theta)),
                (float)System.Math.Sin(dec),
                (float)(c * System.Math.Sin(theta)));
        }

        /// The fixed deep-sky features (constellations), by a representative star.
        /// Real positions + real one-line facts. Sun/Earth are added at runtime
        /// (their directions are per-scene).
        public static IReadOnlyList<SkyFeature> Constellations { get; } = new List<SkyFeature>
        {
            new SkyFeature("Orion",
                "That's Orion, the Hunter. The three stars in a row are his belt; the red one above, Betelgeuse, is a dying supergiant so huge it would swallow Jupiter's orbit, and the blue one, Rigel, is his foot.",
                RaDecToDir(5.60, -1.2)),
            new SkyFeature("the Big Dipper",
                "That's the Big Dipper, part of Ursa Major. Follow the two stars at the lip of its bowl and they point straight to Polaris, the North Star.",
                RaDecToDir(12.90, 55.96)),
            new SkyFeature("Cassiopeia",
                "That's Cassiopeia, the queen on her throne — five bright stars in a great W, wheeling around the pole opposite the Big Dipper.",
                RaDecToDir(0.945, 60.7)),
            new SkyFeature("the Southern Cross",
                "That's Crux, the Southern Cross — the smallest constellation in the sky, and a signpost that points the way to the south celestial pole.",
                RaDecToDir(12.45, -60.0)),
            new SkyFeature("Scorpius",
                "That's Scorpius, the scorpion. Its heart is the red supergiant Antares, whose name means 'rival of Mars' for its ruddy colour.",
                RaDecToDir(16.49, -26.4)),
        };

        /// The feature whose direction is closest to the gaze, or null if nothing
        /// is within maxAngleDeg (the traveler is looking at empty sky).
        public static SkyFeature NearestFeature(IReadOnlyList<SkyFeature> features, SkyVec gaze, float maxAngleDeg)
        {
            float minCos = (float)System.Math.Cos(maxAngleDeg * System.Math.PI / 180.0);
            SkyFeature best = null;
            float bestDot = minCos;
            for (int i = 0; i < features.Count; i++)
            {
                float d = features[i].Dir.Dot(gaze);
                if (d > bestDot) { bestDot = d; best = features[i]; }
            }
            return best;
        }
    }
}
