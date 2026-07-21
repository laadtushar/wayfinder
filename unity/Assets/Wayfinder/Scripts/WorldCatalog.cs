using System.Collections.Generic;
using UnityEngine;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// The ordered set of worlds this build ships. Order here IS the
    /// viewscreen order (WorldRegistry preserves registration order).
    [CreateAssetMenu(fileName = "WorldCatalog", menuName = "Wayfinder/World Catalog")]
    public sealed class WorldCatalog : ScriptableObject
    {
        [SerializeField] private List<WorldPackage> packages = new List<WorldPackage>();

        public IReadOnlyList<WorldPackage> Packages => packages;

        /// Builds the engine-free registry the travel/menu systems consume.
        /// Null entries fail loudly: a hole in the catalog is authoring error,
        /// not something to skip silently.
        public WorldRegistry BuildRegistry()
        {
            var definitions = new List<WorldDefinition>(packages.Count);
            var seenIds = new HashSet<string>();
            for (int i = 0; i < packages.Count; i++)
            {
                if (packages[i] == null)
                    throw new System.InvalidOperationException(
                        $"WorldCatalog '{name}' has a null entry at index {i}.");
                var def = packages[i].ToDefinition();
                if (!seenIds.Add(def.Id))
                    throw new System.InvalidOperationException(
                        $"WorldCatalog '{name}' has a duplicate world id '{def.Id}' at index {i} — a copied site asset with an unchanged id would silently drop a world from the viewscreen.");
                definitions.Add(def);
            }
            return new WorldRegistry(definitions);
        }
    }
}
