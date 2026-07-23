using System.Collections.Generic;
using NUnit.Framework;
using Wayfinder.Core.Companion;

namespace Wayfinder.Core.Tests
{
    /// The grounding contract: the system instruction may inject only real,
    /// logged facts, must withhold facts for undiscovered POIs (the field log is
    /// the spoiler gate), and must forbid invention.
    public class CompanionContextBuilderTests
    {
        const string FlagFact = "the ascent engine's blast knocked it flat at liftoff";
        const string CraterFact = "car-sized boulders flung out around its rim";

        static CompanionContext Tranquillity(bool craterDiscovered)
        {
            var pois = new List<CompanionPoi>
            {
                new CompanionPoi("moon-tranquillity/us-flag", "The Flag That Fell", FlagFact, "src-a", true),
                new CompanionPoi("moon-tranquillity/west-crater", "The Crater That Almost Ended It", CraterFact, "src-b", craterDiscovered),
            };
            return new CompanionContext("moon-tranquillity", "Tranquility Base", 1.62f, true, pois);
        }

        [Test]
        public void Instruction_Injects_Discovered_Facts()
        {
            var s = CompanionContextBuilder.BuildSystemInstruction(Tranquillity(craterDiscovered: false));
            StringAssert.Contains(FlagFact, s, "a logged POI's real fact must be in the prompt");
            StringAssert.Contains("Tranquility Base", s);
            StringAssert.Contains("1.62", s, "real surface gravity grounds the answer");
            StringAssert.Contains("1 of 2", s, "discovered/total count is grounded");
        }

        [Test]
        public void Instruction_Withholds_Undiscovered_Facts()
        {
            var s = CompanionContextBuilder.BuildSystemInstruction(Tranquillity(craterDiscovered: false));
            StringAssert.DoesNotContain(CraterFact, s,
                "an unlogged POI's fact must NOT leak into the prompt (no spoilers)");
        }

        [Test]
        public void Instruction_Reveals_Fact_Once_Discovered()
        {
            var s = CompanionContextBuilder.BuildSystemInstruction(Tranquillity(craterDiscovered: true));
            StringAssert.Contains(CraterFact, s, "once logged, the fact is in scope");
        }

        [Test]
        public void Instruction_Hints_At_Unlogged_Count_Without_Spoiling()
        {
            var s = CompanionContextBuilder.BuildSystemInstruction(Tranquillity(craterDiscovered: false));
            StringAssert.Contains("still unlogged", s, "the companion knows something is out there");
            StringAssert.DoesNotContain(CraterFact, s);
        }

        [Test]
        public void Instruction_Forbids_Invention()
        {
            var s = CompanionContextBuilder.BuildSystemInstruction(Tranquillity(craterDiscovered: true));
            StringAssert.Contains("never invent", s, "anti-hallucination rule must be present");
        }

        [Test]
        public void Bridge_Context_Has_No_World_Facts()
        {
            var ctx = new CompanionContext(null, "the bridge", 0f, false, null);
            Assert.IsTrue(ctx.AtBridge);
            var s = CompanionContextBuilder.BuildSystemInstruction(ctx);
            StringAssert.Contains("docked at the bridge", s);
            StringAssert.DoesNotContain(FlagFact, s);
        }

        [Test]
        public void Context_Counts_Discovered_From_Poi_Flags()
        {
            var ctx = Tranquillity(craterDiscovered: true);
            Assert.AreEqual(2, ctx.TotalCount);
            Assert.AreEqual(2, ctx.DiscoveredCount);
            Assert.AreEqual(1, Tranquillity(craterDiscovered: false).DiscoveredCount);
        }
    }
}
