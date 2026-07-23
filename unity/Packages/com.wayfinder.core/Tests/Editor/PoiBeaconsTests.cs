using NUnit.Framework;
using Wayfinder.Core;

namespace Wayfinder.Core.Tests
{
    /// Gaze weighting + discovered-state policy for surface POI beacons.
    public class PoiBeaconsTests
    {
        [Test]
        public void Gaze_Straight_At_Beacon_Is_Full()
        {
            // Head faces +Z, beacon is due +Z.
            Assert.AreEqual(1f, PoiBeacons.GazeWeight(0f, 1f, 0f, 1f, 25f), 0.001f);
        }

        [Test]
        public void Gaze_Outside_The_Cone_Is_Zero()
        {
            // Beacon 90 deg to the side, cone is 25 deg.
            Assert.AreEqual(0f, PoiBeacons.GazeWeight(1f, 0f, 0f, 1f, 25f), 0.0001f);
        }

        [Test]
        public void Gaze_Falls_Off_With_Angle()
        {
            // A beacon just inside the cone weighs less than one dead ahead.
            float ahead = PoiBeacons.GazeWeight(0f, 1f, 0f, 1f, 40f);
            float edge = PoiBeacons.GazeWeight(0.6f, 1f, 0f, 1f, 40f);  // ~31 deg off
            Assert.Greater(ahead, edge);
            Assert.GreaterOrEqual(edge, 0f);
        }

        [Test]
        public void Discovered_Beacon_Is_Dim_And_Short()
        {
            Assert.Less(PoiBeacons.Intensity(true, 1f), PoiBeacons.Intensity(false, 0f));
            Assert.Less(PoiBeacons.Height(true), PoiBeacons.Height(false));
        }

        [Test]
        public void Undiscovered_Beacon_Swells_Toward_Gaze()
        {
            float idle = PoiBeacons.Intensity(false, 0f);
            float looked = PoiBeacons.Intensity(false, 1f);
            Assert.Greater(looked, idle);
            Assert.LessOrEqual(looked, 1.001f);
        }

        [Test]
        public void Degenerate_Directions_Are_Safe()
        {
            Assert.AreEqual(0f, PoiBeacons.GazeWeight(0f, 0f, 0f, 1f, 25f), 0.0001f);
            Assert.AreEqual(0f, PoiBeacons.GazeWeight(0f, 1f, 0f, 0f, 25f), 0.0001f);
        }
    }
}
