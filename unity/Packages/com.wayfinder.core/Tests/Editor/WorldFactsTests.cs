using NUnit.Framework;
using Wayfinder.Core;

namespace Wayfinder.Core.Tests
{
    /// The bridge nav readout's real-data sheet: lookup by world id + formatting.
    public class WorldFactsTests
    {
        [Test]
        public void Known_Worlds_Resolve_With_Real_Gravity()
        {
            Assert.IsTrue(WorldFactSheet.TryGet("moon-tranquillity", out var moon));
            Assert.AreEqual(1.62f, moon.Gravity, 0.001f);
            Assert.IsTrue(WorldFactSheet.TryGet("mars-jezero", out var mars));
            Assert.AreEqual(3.72f, mars.Gravity, 0.001f);
        }

        [Test]
        public void All_Five_Worlds_Are_Present()
        {
            foreach (var id in new[] { "moon-tranquillity", "moon-shackleton",
                "mars-jezero", "mars-olympus", "mars-valles" })
                Assert.IsTrue(WorldFactSheet.Has(id), id + " missing from the fact sheet");
        }

        [Test]
        public void Unknown_World_Returns_False()
        {
            Assert.IsFalse(WorldFactSheet.TryGet("pluto-nowhere", out _));
            Assert.IsFalse(WorldFactSheet.Has(null));
        }

        [Test]
        public void GEarth_Is_Gravity_Over_Earth()
        {
            WorldFactSheet.TryGet("moon-tranquillity", out var moon);
            Assert.AreEqual(1.62f / 9.807f, moon.GEarth, 0.001f);   // ~0.165 g
        }

        [Test]
        public void Solar_Day_Formats_Long_Lunar_And_Short_Martian()
        {
            WorldFactSheet.TryGet("moon-tranquillity", out var moon);
            Assert.AreEqual("29.5 Earth days", moon.SolarDayText);   // 708.7 h
            WorldFactSheet.TryGet("mars-jezero", out var mars);
            Assert.AreEqual("24 h 40 m", mars.SolarDayText);         // 24.66 h
        }

        [Test]
        public void Readout_Has_Four_Nonempty_Rows()
        {
            WorldFactSheet.TryGet("mars-olympus", out var o);
            var rows = o.ReadoutLines();
            Assert.AreEqual(4, rows.Length);
            foreach (var r in rows) Assert.IsFalse(string.IsNullOrEmpty(r));
            StringAssert.Contains("3.72", rows[0]);
            StringAssert.Contains("Olympus", o.Body);
        }
    }
}
