using System.Collections.Generic;

namespace Wayfinder.Core.Companion
{
    /// One point of interest as the companion sees it: the real fact and its
    /// citation, plus whether the traveler has actually logged it yet. Facts
    /// for undiscovered POIs are deliberately withheld from the model prompt
    /// (see CompanionContextBuilder) so the companion never spoils a discovery.
    public class CompanionPoi
    {
        public string Id { get; }
        public string Title { get; }
        public string Fact { get; }
        public string Source { get; }
        public bool Discovered { get; }

        public CompanionPoi(string id, string title, string fact, string source, bool discovered)
        {
            Id = id;
            Title = title;
            Fact = fact;
            Source = source;
            Discovered = discovered;
        }
    }

    /// One world's discovery tally, for the bridge "expedition so far" overview
    /// (what the companion reports when docked between worlds).
    public class CompanionWorldTally
    {
        public string WorldName { get; }
        public int Discovered { get; }
        public int Total { get; }

        public CompanionWorldTally(string worldName, int discovered, int total)
        {
            WorldName = worldName;
            Discovered = discovered;
            Total = total;
        }
    }

    /// Everything the bridge companion is allowed to know at one moment,
    /// assembled from the real World Package + POI records + the field log.
    /// Engine-free and immutable so it is trivially testable and can be handed
    /// to any provider (offline stub or Gemini) without a Unity dependency.
    public class CompanionContext
    {
        /// The world currently being visited, or null when docked at the bridge.
        public string WorldId { get; }
        public string WorldName { get; }
        public float SurfaceGravity { get; }   // m/s^2, real value; 0 on the bridge
        public bool OnSurface { get; }
        public IReadOnlyList<CompanionPoi> Pois { get; }
        public int DiscoveredCount { get; }
        public int TotalCount { get; }

        /// Cross-world expedition tally — populated when docked at the bridge so
        /// the companion can answer "what have I found?" across every world.
        /// Empty on a surface (the per-POI Pois list carries that detail).
        public IReadOnlyList<CompanionWorldTally> Expedition { get; }

        public bool AtBridge => string.IsNullOrEmpty(WorldId);

        public CompanionContext(
            string worldId, string worldName, float surfaceGravity, bool onSurface,
            IReadOnlyList<CompanionPoi> pois,
            IReadOnlyList<CompanionWorldTally> expedition = null)
        {
            WorldId = worldId;
            WorldName = worldName;
            SurfaceGravity = surfaceGravity;
            OnSurface = onSurface;
            Pois = pois ?? System.Array.Empty<CompanionPoi>();
            Expedition = expedition ?? System.Array.Empty<CompanionWorldTally>();
            TotalCount = Pois.Count;
            int discovered = 0;
            for (int i = 0; i < Pois.Count; i++)
                if (Pois[i].Discovered) discovered++;
            DiscoveredCount = discovered;
        }
    }
}
