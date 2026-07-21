using System.Collections.Generic;

namespace Wayfinder.Core
{
    /// Tracks which points of interest the player has discovered.
    /// Discover returns true only the first time, so the UI can
    /// distinguish a fresh discovery (fanfare) from a revisit.
    public class FieldLog
    {
        private readonly HashSet<string> _discovered = new HashSet<string>();

        public int Count => _discovered.Count;

        public IReadOnlyCollection<string> DiscoveredIds => _discovered;

        public bool Discover(string poiId)
        {
            if (string.IsNullOrEmpty(poiId)) return false;
            return _discovered.Add(poiId);
        }

        public bool HasDiscovered(string poiId) => _discovered.Contains(poiId);
    }
}
