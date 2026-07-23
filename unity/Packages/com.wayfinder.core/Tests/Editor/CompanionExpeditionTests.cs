using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Wayfinder.Core.Companion;

namespace Wayfinder.Core.Tests
{
    /// When docked at the bridge the companion reports the cross-world
    /// expedition tally, so "what have I found?" is answerable between worlds.
    public class CompanionExpeditionTests
    {
        static readonly StubCompanionProvider Stub = new StubCompanionProvider();

        static CompanionContext Bridge(params CompanionWorldTally[] tallies)
        {
            return new CompanionContext(null, "the bridge", 0f, false, null, tallies);
        }

        static string Ask(CompanionContext c, string q) =>
            Stub.AskAsync(c, q, CancellationToken.None).GetAwaiter().GetResult();

        [Test]
        public void Bridge_With_Discoveries_Summarizes_Every_World()
        {
            var ctx = Bridge(
                new CompanionWorldTally("Olympus Mons", 2, 8),
                new CompanionWorldTally("Tranquility Base", 3, 8));
            var a = Ask(ctx, "what have I found?");
            StringAssert.Contains("5 of 16", a, "totals summed across worlds");
            StringAssert.Contains("Olympus Mons 2/8", a);
            StringAssert.Contains("Tranquility Base 3/8", a);
        }

        [Test]
        public void Bridge_With_Empty_Log_Invites_First_Discovery()
        {
            var ctx = Bridge(
                new CompanionWorldTally("Olympus Mons", 0, 8),
                new CompanionWorldTally("Tranquility Base", 0, 8));
            var a = Ask(ctx, "what have I found?");
            StringAssert.Contains("empty", a);
        }

        [Test]
        public void Bridge_Without_Expedition_Data_Falls_Back_To_Docked_Prompt()
        {
            var ctx = new CompanionContext(null, "the bridge", 0f, false, null);
            var a = Ask(ctx, "what have I found?");
            StringAssert.Contains("viewscreen", a);
        }

        [Test]
        public void Builder_Instruction_Lists_The_Expedition_Tally()
        {
            var ctx = Bridge(new CompanionWorldTally("Olympus Mons", 2, 8));
            var s = CompanionContextBuilder.BuildSystemInstruction(ctx);
            StringAssert.Contains("Olympus Mons", s);
            StringAssert.Contains("2 of 8", s);
        }
    }
}
