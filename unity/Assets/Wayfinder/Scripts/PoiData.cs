using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wayfinder.Unity
{
    /// JsonUtility shapes for the authored per-site POI files
    /// (unity/Assets/Wayfinder/POI/<site>.json). Data only — the reveal
    /// behaviour lives in PoiSystem, the discover-once logic in
    /// Wayfinder.Core.FieldLog. This is the seam the Gemini companion reads
    /// later (ARCHITECTURE.md section 6).
    [Serializable]
    public sealed class PoiSet
    {
        public string siteId;
        public List<PoiEntry> pois = new List<PoiEntry>();

        public static PoiSet Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("POI json is empty.");
            var set = JsonUtility.FromJson<PoiSet>(json);
            if (set == null || string.IsNullOrEmpty(set.siteId) || set.pois == null || set.pois.Count == 0)
                throw new ArgumentException("POI json parsed to an empty set — schema drift?");
            var seen = new HashSet<string>();
            foreach (var poi in set.pois)
            {
                if (string.IsNullOrEmpty(poi.id))
                    throw new ArgumentException($"POI in '{set.siteId}' has no id.");
                if (!seen.Add(poi.id))
                    throw new ArgumentException($"Duplicate POI id '{poi.id}' in '{set.siteId}'.");
            }
            return set;
        }
    }

    [Serializable]
    public sealed class PoiEntry
    {
        public string id;
        public string title;
        public string fact;
        public string source;
        public string placementHint;
        // Site-local metres; y is sampled from the terrain at spawn. JsonUtility
        // has no nullable — a POI with both at exactly 0 AND no placement pass
        // yet is treated as unplaced only when placed == false.
        public float positionX;
        public float positionZ;

        public bool HasPosition =>
            // Authored positions are baked by the placement pass; files whose
            // sites have no terrain yet simply omit the fields (JsonUtility
            // leaves them 0,0). The only real (0,0) POI would sit exactly at
            // spawn — disallowed by authoring convention, documented in the
            // POI README.
            positionX != 0f || positionZ != 0f;
    }
}
