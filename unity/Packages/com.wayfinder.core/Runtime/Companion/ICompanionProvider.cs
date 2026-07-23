using System.Threading;
using System.Threading.Tasks;

namespace Wayfinder.Core.Companion
{
    /// A backend that turns a grounded context + a traveler's question into a
    /// spoken/written answer. Deliberately a thin transport: all grounding
    /// (persona, which facts are in scope, anti-hallucination rules) is built
    /// in CompanionContextBuilder, so every provider is interchangeable and the
    /// offline stub behaves like the Gemini one. Text-first; the same shape
    /// extends to Gemini Live voice when that lands for Unity.
    public interface ICompanionProvider
    {
        /// Short id for logging/telemetry ("stub", "gemini").
        string Name { get; }

        /// False when the backend can't serve right now (e.g. Firebase not
        /// configured). CompanionService skips unavailable providers instead of
        /// letting them throw, so the companion never hard-fails.
        bool IsAvailable { get; }

        /// Answer the question grounded in the context. Returns null/empty to
        /// signal "I couldn't produce an answer" — the service then falls back.
        Task<string> AskAsync(CompanionContext context, string question, CancellationToken cancellationToken);
    }
}
