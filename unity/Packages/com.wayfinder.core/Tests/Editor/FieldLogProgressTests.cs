using System.Collections.Generic;
using NUnit.Framework;
using Wayfinder.Core;

namespace Wayfinder.Core.Tests
{
    public class FieldLogProgressTests
    {
        static readonly List<string> Order = new List<string> { "mars-olympus", "mars-valles", "moon-shackleton" };
        static readonly Dictionary<string, int> Totals = new Dictionary<string, int>
        {
            { "mars-olympus", 8 }, { "mars-valles", 8 }, { "moon-shackleton", 8 }
        };

        [Test]
        public void PerWorld_Counts_By_Id_Prefix_In_World_Order()
        {
            var discovered = new HashSet<string>
            {
                "mars-olympus/caldera-rim-drop",
                "mars-olympus/six-collapse-pits",
                "moon-shackleton/rim-overlook",
            };

            var progress = FieldLogProgress.PerWorld(discovered, Order, Totals);

            Assert.AreEqual(3, progress.Count);
            Assert.AreEqual("mars-olympus", progress[0].WorldId);
            Assert.AreEqual(2, progress[0].Discovered);
            Assert.AreEqual(8, progress[0].Total);
            Assert.AreEqual(0, progress[1].Discovered);   // valles untouched
            Assert.AreEqual(1, progress[2].Discovered);   // shackleton
        }

        [Test]
        public void PerWorld_Does_Not_Cross_Contaminate_Similar_Prefixes()
        {
            // "mars-olympus" must not swallow a hypothetical "mars-olympus-2".
            var discovered = new HashSet<string> { "mars-olympus-2/foo", "mars-olympus/real" };
            var order = new List<string> { "mars-olympus" };
            var totals = new Dictionary<string, int> { { "mars-olympus", 8 } };

            var progress = FieldLogProgress.PerWorld(discovered, order, totals);

            Assert.AreEqual(1, progress[0].Discovered, "prefix must be worldId + '/'");
        }

        [Test]
        public void TotalAvailable_Sums_Every_Worlds_Pois()
        {
            Assert.AreEqual(24, FieldLogProgress.TotalAvailable(Totals));
        }

        [Test]
        public void PerWorld_Missing_Total_Reads_As_Zero_Not_Crash()
        {
            var progress = FieldLogProgress.PerWorld(
                new HashSet<string>(), new List<string> { "unknown" }, Totals);
            Assert.AreEqual(0, progress[0].Total);
        }
    }
}
