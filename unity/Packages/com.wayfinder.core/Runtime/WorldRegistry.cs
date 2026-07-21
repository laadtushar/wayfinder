using System;
using System.Collections.Generic;

namespace Wayfinder.Core
{
    /// Pure data describing one visitable world. In the Unity project the
    /// WorldPackage ScriptableObject holds one of these; everything the
    /// travel/registry logic needs lives here so it stays engine-free.
    public class WorldDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string SceneName { get; }
        public float SurfaceGravity { get; }   // m/s^2 — real values (Mars 3.72, Moon 1.62)

        public WorldDefinition(string id, string displayName, string sceneName, float surfaceGravity)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("A world needs a non-empty id.", nameof(id));
            Id = id;
            DisplayName = displayName;
            SceneName = sceneName;
            SurfaceGravity = surfaceGravity;
        }
    }

    /// Lookup + ordered listing of every registered world.
    /// Order is preserved because it drives the viewscreen list.
    public class WorldRegistry
    {
        private readonly List<WorldDefinition> _ordered = new List<WorldDefinition>();
        private readonly Dictionary<string, WorldDefinition> _byId =
            new Dictionary<string, WorldDefinition>();

        public WorldRegistry(IEnumerable<WorldDefinition> worlds)
        {
            foreach (var world in worlds)
            {
                if (_byId.ContainsKey(world.Id))
                    _ordered.RemoveAll(w => w.Id == world.Id);
                _byId[world.Id] = world;
                _ordered.Add(world);
            }
        }

        public IReadOnlyList<WorldDefinition> All => _ordered;

        public WorldDefinition GetById(string id) =>
            id != null && _byId.TryGetValue(id, out var world) ? world : null;
    }
}
