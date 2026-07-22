using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// The expedition log on the Bridge viewscreen: total discoveries and
    /// per-world progress, driven entirely by the session FieldLog + the
    /// catalog's POI counts (no hardcoded numbers). Refreshed by TravelManager
    /// whenever the player returns from a surface.
    public sealed class FieldLogPanel : MonoBehaviour
    {
        [SerializeField] private WorldCatalog catalog;
        [SerializeField] private Text bodyText;

        Dictionary<string, int> _totals;
        List<string> _order;

        void Awake()
        {
            if (catalog == null) throw new System.InvalidOperationException($"{name}: no WorldCatalog assigned.");
            if (bodyText == null) throw new System.InvalidOperationException($"{name}: no body text assigned.");
            BuildTotals();
        }

        void BuildTotals()
        {
            _totals = new Dictionary<string, int>();
            _order = new List<string>();
            foreach (var package in catalog.Packages)
            {
                if (package == null) continue;
                var def = package.ToDefinition();
                _order.Add(def.Id);
                int count = 0;
                if (package.PoiData != null)
                {
                    try
                    {
                        var set = PoiSet.Parse(package.PoiData.text);
                        if (set.siteId != def.Id)
                            Debug.LogError($"[FieldLogPanel] POI siteId '{set.siteId}' != package id '{def.Id}' — progress prefix will mis-count.");
                        count = set.pois.Count;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[FieldLogPanel] POI count failed for '{def.Id}': {e.Message}");
                    }
                }
                _totals[def.Id] = count;
            }
        }

        /// Repaint from the current log. Called on return to the Bridge.
        public void Refresh(FieldLog log)
        {
            if (_totals == null) BuildTotals(); // lazy guard against Awake-order
            var perWorld = FieldLogProgress.PerWorld(log.DiscoveredIds, _order, _totals);
            int grandTotal = FieldLogProgress.TotalAvailable(_totals);

            var sb = new System.Text.StringBuilder();
            sb.Append("<b>FIELD LOG</b>   ").Append(log.Count).Append(" / ").Append(grandTotal).Append('\n');
            foreach (var w in perWorld)
            {
                string name = DisplayName(w.WorldId);
                sb.Append('\n').Append(name).Append("   ")
                  .Append(w.Discovered).Append('/').Append(w.Total);
            }
            bodyText.text = sb.ToString();
        }

        string DisplayName(string worldId)
        {
            foreach (var package in catalog.Packages)
                if (package != null && package.ToDefinition().Id == worldId)
                    return package.ToDefinition().DisplayName;
            return worldId;
        }
    }
}
