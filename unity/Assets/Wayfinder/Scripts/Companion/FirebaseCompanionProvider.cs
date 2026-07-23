using System;
using System.Threading;
using System.Threading.Tasks;
using Wayfinder.Core.Companion;
#if WAYFINDER_FIREBASE_AI
using Firebase;
using Firebase.AI;
using Firebase.AppCheck;
#endif

namespace Wayfinder.Unity.Companion
{
    /// The Gemini backend, via Firebase AI Logic. Everything that touches the
    /// Firebase SDK is guarded by the WAYFINDER_FIREBASE_AI scripting define, so
    /// the project builds cleanly WITHOUT the SDK installed — with the define
    /// off this compiles to an always-unavailable provider and the companion
    /// uses the offline stub instead. Human setup (Firebase console, SDK import,
    /// App Check, the define) is in docs/companion-setup.md.
    ///
    /// Security: this never holds a raw Gemini key. App Check attestation is
    /// configured before any AI call, so Gemini is reached only through the
    /// Firebase proxy that verifies the app.
    public sealed class FirebaseCompanionProvider : ICompanionProvider
    {
        public string Name => "gemini";

#if WAYFINDER_FIREBASE_AI
        readonly GenerativeModel _model;

        FirebaseCompanionProvider(GenerativeModel model) { _model = model; }

        public bool IsAvailable => _model != null;

        public async Task<string> AskAsync(CompanionContext context, string question, CancellationToken cancellationToken)
        {
            // All grounding (persona, in-scope facts, anti-hallucination) is built
            // in the engine-free core so the Gemini and stub paths stay identical.
            string system = CompanionContextBuilder.BuildSystemInstruction(context);
            string prompt = system + "\n\nTraveler: " + (question ?? string.Empty);
            var response = await _model.GenerateContentAsync(prompt);
            return response.Text;
        }

        /// Configures App Check, verifies Firebase deps, and creates the model.
        /// Returns null on any failure so the companion falls back to the stub.
        /// A non-empty debugToken uses the App Check debug provider (editor / a
        /// device without Play Integrity); leave it empty in production.
        public static async Task<FirebaseCompanionProvider> TryCreateAsync(
            string modelName, string debugToken, Action<string> log)
        {
            try
            {
                // App Check MUST be set before any Firebase service call.
                if (!string.IsNullOrEmpty(debugToken))
                {
                    DebugAppCheckProviderFactory.Instance.SetDebugToken(debugToken);
                    FirebaseAppCheck.SetAppCheckProviderFactory(DebugAppCheckProviderFactory.Instance);
                }
                else
                {
                    FirebaseAppCheck.SetAppCheckProviderFactory(PlayIntegrityProviderFactory.Instance);
                }

                var status = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (status != DependencyStatus.Available)
                {
                    log?.Invoke("[Companion] Firebase dependencies unavailable: " + status + " — using offline stub.");
                    return null;
                }

                // Gemini Developer API backend (no-cost tier). App Check gates it.
                var ai = FirebaseAI.GetInstance(FirebaseAI.Backend.GoogleAI());
                var model = ai.GetGenerativeModel(modelName: modelName);
                log?.Invoke("[Companion] Gemini backend ready (" + modelName + ").");
                return new FirebaseCompanionProvider(model);
            }
            catch (Exception e)
            {
                log?.Invoke("[Companion] Firebase init failed (" + e.Message + ") — using offline stub.");
                return null;
            }
        }
#else
        public bool IsAvailable => false;

        public Task<string> AskAsync(CompanionContext context, string question, CancellationToken cancellationToken)
            => Task.FromResult<string>(null);

        /// Firebase SDK not compiled in (WAYFINDER_FIREBASE_AI off). Returns null
        /// so the companion uses the offline stub. See docs/companion-setup.md.
        public static Task<FirebaseCompanionProvider> TryCreateAsync(
            string modelName, string debugToken, Action<string> log)
        {
            log?.Invoke("[Companion] Firebase AI not compiled in (WAYFINDER_FIREBASE_AI off) — using offline stub.");
            return Task.FromResult<FirebaseCompanionProvider>(null);
        }
#endif
    }
}
