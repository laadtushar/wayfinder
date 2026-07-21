using NUnit.Framework;
using Wayfinder.Core;

namespace Wayfinder.Core.Tests
{
    // Encodes the Android XR quality-checklist rule: minimum interactive target
    // size scales with distance as DistanceInMeters x 0.868 x 48dp (56dp recommended).
    public class InteractionTargetsTests
    {
        [Test]
        public void MinimumSize_At3Meters_IsAbout12Point5Centimeters()
        {
            // Google's own worked example: 3 x 0.868 x 48 = ~125 mm.
            Assert.AreEqual(0.125f, InteractionTargets.MinimumSizeMeters(3f), 0.001f);
        }

        [Test]
        public void RecommendedSize_UsesThe56dpTarget()
        {
            Assert.AreEqual(3f * 0.868f * 56f / 1000f,
                InteractionTargets.RecommendedSizeMeters(3f), 0.0001f);
        }

        [Test]
        public void Sizes_ScaleLinearlyWithDistance()
        {
            Assert.AreEqual(
                InteractionTargets.MinimumSizeMeters(1f) * 2f,
                InteractionTargets.MinimumSizeMeters(2f),
                0.0001f);
        }

        [Test]
        public void NonPositiveDistance_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => InteractionTargets.MinimumSizeMeters(0f));
        }
    }
}
