using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wayfinder.Core.Companion
{
    /// The always-available, fully offline companion. It never calls a network
    /// and produces a deterministic, grounded answer straight from the context:
    /// a matched point-of-interest fact when the question names one, otherwise a
    /// world overview. This is what ships until Firebase/Gemini is configured
    /// (see docs/companion-setup.md), the fallback if Gemini fails, and the
    /// reference the grounding tests assert against. It NEVER reveals a fact for
    /// an unlogged POI.
    public sealed class StubCompanionProvider : ICompanionProvider
    {
        public string Name => "stub";
        public bool IsAvailable => true;

        static readonly HashSet<string> Stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "what", "where", "when", "how", "why", "who", "tell", "me",
            "about", "this", "that", "here", "there", "is", "are", "was", "were", "did",
            "does", "do", "a", "an", "of", "on", "in", "at", "to", "for", "you", "your",
            "it", "its", "i", "we", "they", "can", "could", "would", "please",
        };

        public Task<string> AskAsync(CompanionContext context, string question, CancellationToken cancellationToken)
        {
            return Task.FromResult(Answer(context, question));
        }

        static string Answer(CompanionContext context, string question)
        {
            if (context == null || context.AtBridge)
                return "We're docked at the bridge between worlds. Choose a destination " +
                       "on the viewscreen and I'll tell you about it once we've warped in.";

            var match = BestDiscoveredMatch(context, question);
            if (match != null)
                return match.Fact;

            return Overview(context);
        }

        /// Score each logged POI by how many of its title words (minus stopwords)
        /// appear in the question; return the best hit, or null if none match.
        static CompanionPoi BestDiscoveredMatch(CompanionContext context, string question)
        {
            if (string.IsNullOrWhiteSpace(question)) return null;
            string q = question.ToLowerInvariant();

            CompanionPoi best = null;
            int bestScore = 0;
            foreach (var p in context.Pois)
            {
                if (!p.Discovered || string.IsNullOrEmpty(p.Fact) || string.IsNullOrEmpty(p.Title))
                    continue;

                int score = 0;
                if (q.Contains(p.Title.ToLowerInvariant())) score += 5;
                foreach (var word in p.Title.Split(new[] { ' ', '-', '\'', ',', '.' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (word.Length <= 3 || Stopwords.Contains(word)) continue;
                    if (q.Contains(word.ToLowerInvariant())) score++;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return bestScore > 0 ? best : null;
        }

        static string Overview(CompanionContext context)
        {
            var sb = new StringBuilder();
            sb.Append("You're ")
              .Append(context.OnSurface ? "standing on " : "approaching ")
              .Append(context.WorldName)
              .Append(". Surface gravity is ")
              .Append(context.SurfaceGravity.ToString("0.##"))
              .Append(" m/s². You've logged ")
              .Append(context.DiscoveredCount).Append(" of ").Append(context.TotalCount)
              .Append(" points of interest here");

            var logged = new List<string>();
            foreach (var p in context.Pois)
                if (p.Discovered && !string.IsNullOrEmpty(p.Title)) logged.Add(p.Title);

            if (logged.Count > 0)
            {
                sb.Append(": ");
                sb.Append(string.Join(", ", logged));
                sb.Append('.');
            }
            else
            {
                sb.Append(" — nothing yet. Walk the surface and I'll log what we find.");
            }

            int undiscovered = context.TotalCount - context.DiscoveredCount;
            if (undiscovered > 0 && logged.Count > 0)
                sb.Append(undiscovered == 1
                    ? " There's one more out there to find."
                    : " There are " + undiscovered + " more out there to find.");
            return sb.ToString();
        }
    }
}
