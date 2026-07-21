using NUnit.Framework;
using Wayfinder.Core;

namespace Wayfinder.Core.Tests
{
    public class WorldRegistryTests
    {
        private static WorldDefinition Mars() =>
            new WorldDefinition("mars-olympus", "Mars — Olympus Mons", "MarsOlympus", 3.72f);

        [Test]
        public void GetById_ReturnsDefinition_WhenPresent()
        {
            var mars = Mars();
            var registry = new WorldRegistry(new[] { mars });
            Assert.AreSame(mars, registry.GetById("mars-olympus"));
        }

        [Test]
        public void GetById_ReturnsNull_WhenMissing()
        {
            var registry = new WorldRegistry(System.Array.Empty<WorldDefinition>());
            Assert.IsNull(registry.GetById("nope"));
        }

        [Test]
        public void All_PreservesRegistrationOrder_ForTheViewscreenList()
        {
            var mars = Mars();
            var moon = new WorldDefinition("moon-shackleton", "Moon — Shackleton", "MoonShackleton", 1.62f);
            var registry = new WorldRegistry(new[] { mars, moon });
            CollectionAssert.AreEqual(new[] { mars, moon }, registry.All);
        }

        [Test]
        public void DuplicateId_LastRegistrationWins()
        {
            var first = Mars();
            var second = new WorldDefinition("mars-olympus", "Mars — Olympus Mons (v2)", "MarsOlympus2", 3.72f);
            var registry = new WorldRegistry(new[] { first, second });
            Assert.AreSame(second, registry.GetById("mars-olympus"));
        }

        [Test]
        public void WorldDefinition_RejectsNullOrEmptyId()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new WorldDefinition("", "x", "X", 1f));
            Assert.Throws<System.ArgumentException>(() =>
                new WorldDefinition(null, "x", "X", 1f));
        }
    }
}
