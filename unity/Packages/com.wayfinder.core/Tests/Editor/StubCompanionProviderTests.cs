using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Wayfinder.Core.Companion;

namespace Wayfinder.Core.Tests
{
    /// The offline companion must be useful and grounded: name a logged POI and
    /// it returns that real fact; ask generally and it gives a world overview;
    /// it never leaks an unlogged POI's fact.
    public class StubCompanionProviderTests
    {
        const string FlagFact = "the ascent engine's blast knocked it flat at liftoff";
        const string CraterFact = "car-sized boulders flung out around its rim";

        static readonly StubCompanionProvider Stub = new StubCompanionProvider();

        static CompanionContext Ctx(bool craterDiscovered)
        {
            var pois = new List<CompanionPoi>
            {
                new CompanionPoi("moon-tranquillity/us-flag", "The Flag That Fell", FlagFact, "src-a", true),
                new CompanionPoi("moon-tranquillity/west-crater", "West Crater", CraterFact, "src-b", craterDiscovered),
            };
            return new CompanionContext("moon-tranquillity", "Tranquility Base", 1.62f, true, pois);
        }

        static string Ask(CompanionContext c, string q) =>
            Stub.AskAsync(c, q, CancellationToken.None).GetAwaiter().GetResult();

        [Test]
        public void Names_A_Logged_Poi_Returns_Its_Fact()
        {
            Assert.AreEqual(FlagFact, Ask(Ctx(false), "tell me about the flag"));
        }

        [Test]
        public void General_Question_Returns_A_Grounded_Overview()
        {
            var a = Ask(Ctx(false), "where am I?");
            StringAssert.Contains("Tranquility Base", a);
            StringAssert.Contains("1.62", a);
            StringAssert.Contains("1 of 2", a);
            StringAssert.Contains("The Flag That Fell", a, "overview lists what's logged");
        }

        [Test]
        public void Never_Reveals_An_Unlogged_Fact()
        {
            // Ask directly about the still-unlogged crater.
            var a = Ask(Ctx(craterDiscovered: false), "what is west crater?");
            StringAssert.DoesNotContain(CraterFact, a, "unlogged fact must never surface");
        }

        [Test]
        public void Reveals_The_Fact_Once_Logged()
        {
            Assert.AreEqual(CraterFact, Ask(Ctx(craterDiscovered: true), "what is west crater?"));
        }

        [Test]
        public void At_Bridge_Invites_A_Destination()
        {
            var ctx = new CompanionContext(null, "the bridge", 0f, false, null);
            var a = Ask(ctx, "what's out there?");
            StringAssert.Contains("bridge", a);
            StringAssert.Contains("viewscreen", a);
        }

        [Test]
        public void Is_Always_Available()
        {
            Assert.IsTrue(Stub.IsAvailable);
            Assert.AreEqual("stub", Stub.Name);
        }
    }
}
