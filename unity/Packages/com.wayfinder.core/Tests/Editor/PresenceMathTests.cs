using System.Collections.Generic;
using NUnit.Framework;
using Wayfinder.Core;
using P2 = Wayfinder.Core.PresenceMath.P2;

namespace Wayfinder.Core.Tests
{
    public class PresenceMathTests
    {
        [Test]
        public void NearestUndiscovered_Picks_The_Closest_Not_Yet_Found()
        {
            var pos = new List<P2> { new P2(10, 0), new P2(3, 0), new P2(0, 5) };
            var ids = new List<string> { "a", "b", "c" };
            var found = new HashSet<string>();

            int idx = PresenceMath.NearestUndiscovered(new P2(0, 0), pos, ids, found, out float d);
            Assert.AreEqual(1, idx, "b at (3,0) is nearest");
            Assert.AreEqual(3f, d, 1e-4f);
        }

        [Test]
        public void NearestUndiscovered_Skips_Discovered_Ones()
        {
            var pos = new List<P2> { new P2(3, 0), new P2(0, 5) };
            var ids = new List<string> { "b", "c" };
            var found = new HashSet<string> { "b" }; // nearest already found

            int idx = PresenceMath.NearestUndiscovered(new P2(0, 0), pos, ids, found, out float d);
            Assert.AreEqual(1, idx, "b discovered -> c is the target");
            Assert.AreEqual(5f, d, 1e-4f);
        }

        [Test]
        public void NearestUndiscovered_All_Found_Returns_Minus_One()
        {
            var pos = new List<P2> { new P2(3, 0) };
            var ids = new List<string> { "b" };
            var found = new HashSet<string> { "b" };
            int idx = PresenceMath.NearestUndiscovered(new P2(0, 0), pos, ids, found, out float d);
            Assert.AreEqual(-1, idx);
            Assert.AreEqual(0f, d);
        }

        [Test]
        public void RelativeBearing_Dead_Ahead_Is_Zero()
        {
            // forward +Z, target +Z ahead
            float b = PresenceMath.RelativeBearing(new P2(0, 0), new P2(0, 1), new P2(0, 10));
            Assert.AreEqual(0f, b, 0.01f);
        }

        [Test]
        public void RelativeBearing_Right_Is_Positive_Ninety()
        {
            // forward +Z, target to the +X (player's right)
            float b = PresenceMath.RelativeBearing(new P2(0, 0), new P2(0, 1), new P2(10, 0));
            Assert.AreEqual(90f, b, 0.01f);
        }

        [Test]
        public void RelativeBearing_Left_Is_Negative_Ninety()
        {
            float b = PresenceMath.RelativeBearing(new P2(0, 0), new P2(0, 1), new P2(-10, 0));
            Assert.AreEqual(-90f, b, 0.01f);
        }

        [Test]
        public void RelativeBearing_Behind_Is_180()
        {
            float b = PresenceMath.RelativeBearing(new P2(0, 0), new P2(0, 1), new P2(0, -10));
            Assert.AreEqual(180f, System.Math.Abs(b), 0.01f);
        }

        [Test]
        public void RelativeBearing_Zero_Forward_Is_Safe()
        {
            Assert.AreEqual(0f, PresenceMath.RelativeBearing(new P2(0, 0), new P2(0, 0), new P2(5, 5)));
        }

        [Test]
        public void RingSlot_Wraps_To_Overwrite_Oldest()
        {
            Assert.AreEqual(0, PresenceMath.RingSlot(0, 4));
            Assert.AreEqual(3, PresenceMath.RingSlot(3, 4));
            Assert.AreEqual(0, PresenceMath.RingSlot(4, 4), "wraps at capacity");
            Assert.AreEqual(1, PresenceMath.RingSlot(5, 4));
            Assert.AreEqual(-1, PresenceMath.RingSlot(2, 0), "zero capacity is safe");
        }
    }
}
