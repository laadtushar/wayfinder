using NUnit.Framework;
using Wayfinder.Core;

namespace Wayfinder.Core.Tests
{
    public class FieldLogTests
    {
        [Test]
        public void Discover_AddsOnce_IgnoresDuplicates()
        {
            var log = new FieldLog();
            Assert.IsTrue(log.Discover("mars-olympus/caldera-rim"));
            Assert.IsFalse(log.Discover("mars-olympus/caldera-rim"));
            Assert.AreEqual(1, log.Count);
            Assert.IsTrue(log.HasDiscovered("mars-olympus/caldera-rim"));
        }

        [Test]
        public void Count_ReflectsDistinctDiscoveries()
        {
            var log = new FieldLog();
            log.Discover("a");
            log.Discover("b");
            Assert.AreEqual(2, log.Count);
        }

        [Test]
        public void HasDiscovered_IsFalse_ForUnknownId()
        {
            var log = new FieldLog();
            Assert.IsFalse(log.HasDiscovered("never-seen"));
        }

        [Test]
        public void Discover_NullOrEmpty_IsRejected_AndNotCounted()
        {
            var log = new FieldLog();
            Assert.IsFalse(log.Discover(null));
            Assert.IsFalse(log.Discover(""));
            Assert.AreEqual(0, log.Count);
        }

        [Test]
        public void DiscoveredIds_ReturnsEverythingDiscovered()
        {
            var log = new FieldLog();
            log.Discover("a");
            log.Discover("b");
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, log.DiscoveredIds);
        }
    }
}
