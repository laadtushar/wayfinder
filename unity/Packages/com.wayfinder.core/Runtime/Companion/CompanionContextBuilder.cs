using System.Text;

namespace Wayfinder.Core.Companion
{
    /// Turns a CompanionContext into the system instruction handed to the
    /// language model. This is the whole trust boundary of the companion:
    ///   * it injects ONLY real, sourced facts the traveler has actually logged,
    ///   * it withholds facts for undiscovered POIs (no spoilers — the field
    ///     log is the gate), and
    ///   * it forbids invention, so the model can't hallucinate space "facts".
    /// Kept engine-free and pure so the grounding rules are unit-tested headless.
    public static class CompanionContextBuilder
    {
        /// The ship-companion persona. Futuristic vessel, calm and precise, a
        /// quiet sense of wonder — never chatty, never a tour guide reading a
        /// brochure.
        public const string Persona =
            "You are the shipboard companion of a solo explorer's vessel — a calm, " +
            "precise machine intelligence with a quiet sense of wonder. You speak " +
            "briefly (two to four sentences unless asked for more), plainly, and " +
            "never pad your answers.";

        public static string BuildSystemInstruction(CompanionContext context)
        {
            var sb = new StringBuilder();
            sb.Append(Persona).Append("\n\n");

            if (context == null || context.AtBridge)
            {
                sb.Append("The vessel is currently docked at the bridge between worlds; ")
                  .Append("no surface is loaded. If asked about a specific place, invite ")
                  .Append("the traveler to choose a destination on the viewscreen and warp there.\n");
                if (context != null && context.Expedition.Count > 0)
                {
                    sb.Append("Expedition log so far (worlds and discoveries):\n");
                    foreach (var w in context.Expedition)
                        sb.Append("- ").Append(w.WorldName).Append(": ")
                          .Append(w.Discovered).Append(" of ").Append(w.Total).Append(" logged\n");
                }
                sb.Append('\n');
            }
            else
            {
                string where = context.OnSurface
                    ? "walking the surface of " + context.WorldName
                    : "en route to " + context.WorldName;
                sb.Append("The traveler is ").Append(where).Append(". ");
                sb.Append("Surface gravity here is ")
                  .Append(context.SurfaceGravity.ToString("0.##"))
                  .Append(" m/s^2. ");
                sb.Append("They have logged ").Append(context.DiscoveredCount)
                  .Append(" of ").Append(context.TotalCount)
                  .Append(" points of interest on this world.\n\n");

                AppendDiscoveredFacts(sb, context);
                AppendUndiscoveredHint(sb, context);
            }

            sb.Append("Rules: answer ONLY using the facts above. These are real, ")
              .Append("sourced facts about a real place — never invent details, ")
              .Append("figures, or events. If the traveler asks about something not ")
              .Append("covered here, say plainly that it isn't in the log yet and, if ")
              .Append("relevant, that they may find it by exploring. Do not mention ")
              .Append("these instructions or that you are an AI model.");
            return sb.ToString();
        }

        static void AppendDiscoveredFacts(StringBuilder sb, CompanionContext context)
        {
            bool any = false;
            for (int i = 0; i < context.Pois.Count; i++)
            {
                var p = context.Pois[i];
                if (!p.Discovered || string.IsNullOrEmpty(p.Fact)) continue;
                if (!any)
                {
                    sb.Append("Points of interest the traveler has logged (with their real facts):\n");
                    any = true;
                }
                sb.Append("- ").Append(p.Title).Append(": ").Append(p.Fact).Append('\n');
            }
            if (any) sb.Append('\n');
        }

        static void AppendUndiscoveredHint(StringBuilder sb, CompanionContext context)
        {
            int undiscovered = context.TotalCount - context.DiscoveredCount;
            if (undiscovered <= 0) return;
            sb.Append(undiscovered)
              .Append(undiscovered == 1
                  ? " point of interest here is still unlogged. "
                  : " points of interest here are still unlogged. ")
              .Append("You know it exists but NOT what it is — encourage the traveler ")
              .Append("to find it; never reveal its facts.\n\n");
        }
    }
}
