using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayfinder.Core.Companion
{
    /// Orchestrates the companion: try the primary backend (Gemini), fall back
    /// to a guaranteed-available one (the offline stub) whenever the primary is
    /// unavailable, returns nothing, or throws. This is why the feature is
    /// always answerable — with or without Firebase configured. Engine-free so
    /// the fallback policy is unit-tested headless.
    public sealed class CompanionService
    {
        readonly ICompanionProvider _primary;
        readonly ICompanionProvider _fallback;

        /// <param name="primary">Preferred backend (may be null or unavailable).</param>
        /// <param name="fallback">Guaranteed-available backend; must not be null.</param>
        public CompanionService(ICompanionProvider primary, ICompanionProvider fallback)
        {
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _primary = primary;
        }

        /// Name of the backend that would be tried first (for telemetry/UI).
        public string PreferredProviderName =>
            (_primary != null && _primary.IsAvailable) ? _primary.Name : _fallback.Name;

        public async Task<string> AskAsync(CompanionContext context, string question, CancellationToken cancellationToken)
        {
            if (_primary != null && _primary.IsAvailable)
            {
                try
                {
                    var answer = await _primary.AskAsync(context, question, cancellationToken)
                        .ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(answer))
                        return answer;
                }
                catch (OperationCanceledException)
                {
                    throw; // an explicit cancel is not a provider failure — honor it.
                }
                catch
                {
                    // Any other failure (network, quota, misconfig) => fall back.
                }
            }

            return await _fallback.AskAsync(context, question, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
