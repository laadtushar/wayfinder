using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wayfinder.Core.Companion;

namespace Wayfinder.Core.Tests
{
    /// The fallback policy that makes the companion never hard-fail: prefer the
    /// primary, but drop to the guaranteed fallback when the primary is
    /// unavailable, empty, or throws — while still honoring an explicit cancel.
    public class CompanionServiceTests
    {
        sealed class FakeProvider : ICompanionProvider
        {
            readonly Func<string> _answer;
            public FakeProvider(string name, bool available, Func<string> answer)
            {
                Name = name; IsAvailable = available; _answer = answer;
            }
            public string Name { get; }
            public bool IsAvailable { get; }
            public Task<string> AskAsync(CompanionContext c, string q, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(_answer());
            }
        }

        static readonly CompanionContext Ctx =
            new CompanionContext("w", "World", 1f, true, null);

        static string Ask(CompanionService s) =>
            s.AskAsync(Ctx, "q", CancellationToken.None).GetAwaiter().GetResult();

        [Test]
        public void Uses_Primary_When_Available()
        {
            var s = new CompanionService(
                new FakeProvider("gemini", true, () => "PRIMARY"),
                new FakeProvider("stub", true, () => "FALLBACK"));
            Assert.AreEqual("PRIMARY", Ask(s));
            Assert.AreEqual("gemini", s.PreferredProviderName);
        }

        [Test]
        public void Falls_Back_When_Primary_Unavailable()
        {
            var s = new CompanionService(
                new FakeProvider("gemini", false, () => "PRIMARY"),
                new FakeProvider("stub", true, () => "FALLBACK"));
            Assert.AreEqual("FALLBACK", Ask(s));
            Assert.AreEqual("stub", s.PreferredProviderName, "an unavailable primary isn't preferred");
        }

        [Test]
        public void Falls_Back_When_Primary_Throws()
        {
            var s = new CompanionService(
                new FakeProvider("gemini", true, () => throw new InvalidOperationException("network")),
                new FakeProvider("stub", true, () => "FALLBACK"));
            Assert.AreEqual("FALLBACK", Ask(s));
        }

        [Test]
        public void Falls_Back_When_Primary_Returns_Blank()
        {
            var s = new CompanionService(
                new FakeProvider("gemini", true, () => "   "),
                new FakeProvider("stub", true, () => "FALLBACK"));
            Assert.AreEqual("FALLBACK", Ask(s));
        }

        [Test]
        public void Null_Primary_Uses_Fallback()
        {
            var s = new CompanionService(null, new FakeProvider("stub", true, () => "FALLBACK"));
            Assert.AreEqual("FALLBACK", Ask(s));
        }

        [Test]
        public void Null_Fallback_Is_Rejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CompanionService(new FakeProvider("gemini", true, () => "x"), null));
        }

        [Test]
        public void Explicit_Cancel_Is_Honored_Not_Swallowed()
        {
            var s = new CompanionService(
                new FakeProvider("gemini", true, () => "PRIMARY"),
                new FakeProvider("stub", true, () => "FALLBACK"));
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await s.AskAsync(Ctx, "q", cts.Token));
        }
    }
}
