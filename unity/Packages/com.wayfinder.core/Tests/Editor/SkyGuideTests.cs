using NUnit.Framework;
using Wayfinder.Core;

namespace Wayfinder.Core.Tests
{
    /// The "what am I looking at?" sky lookup: real RA/Dec -> direction, and
    /// nearest-feature by angle.
    public class SkyGuideTests
    {
        [Test]
        public void RaDecToDir_North_Celestial_Pole_Points_Up()
        {
            var d = SkyGuide.RaDecToDir(6.0, 90.0);   // Dec +90 -> +Y (north pole up)
            Assert.AreEqual(1f, d.Y, 0.002f);
        }

        [Test]
        public void RaDecToDir_Equator_Is_On_The_Horizon_Plane()
        {
            var d = SkyGuide.RaDecToDir(9.0, 0.0);    // Dec 0 -> y ~ 0
            Assert.AreEqual(0f, d.Y, 0.002f);
        }

        [Test]
        public void RaDecToDir_Returns_A_Unit_Vector()
        {
            var d = SkyGuide.RaDecToDir(3.4, 22.0);
            Assert.AreEqual(1f, d.Dot(d), 0.001f);
        }

        [Test]
        public void Nearest_Finds_The_Constellation_You_Face()
        {
            SkyFeature dipper = null;
            foreach (var f in SkyGuide.Constellations) if (f.Name == "the Big Dipper") dipper = f;
            Assert.IsNotNull(dipper);
            var hit = SkyGuide.NearestFeature(SkyGuide.Constellations, dipper.Dir, 15f);
            Assert.AreEqual("the Big Dipper", hit.Name);
        }

        [Test]
        public void Nearest_Returns_Null_For_Empty_Sky()
        {
            // The south celestial pole (0,-1,0): the nearest listed feature (Crux,
            // Dec ~-60) is ~30 deg away, outside a 15 deg cone.
            var hit = SkyGuide.NearestFeature(SkyGuide.Constellations, new SkyVec(0f, -1f, 0f), 15f);
            Assert.IsNull(hit);
        }

        [Test]
        public void Every_Constellation_Has_A_Name_And_A_Fact()
        {
            foreach (var f in SkyGuide.Constellations)
            {
                Assert.IsFalse(string.IsNullOrEmpty(f.Name));
                Assert.IsFalse(string.IsNullOrEmpty(f.Fact));
            }
        }
    }
}
