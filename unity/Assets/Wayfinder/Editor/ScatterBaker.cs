using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wayfinder.Unity;
using Wayfinder.Unity.Scatter;

namespace Wayfinder.Unity.EditorTools
{
    /// Bakes the rock scatter field for a site: reads its TerrainData, runs the
    /// pure ScatterPlacement rules over a jittered candidate grid in WORLD
    /// space (never grid space — Valles' cells are anisotropic), enforces the
    /// hard cap, cell-sorts, and writes ScatterFieldData_<site>.asset. Seeded,
    /// so re-bakes are stable.
    public static class ScatterBaker
    {
        [MenuItem("Wayfinder/Scatter/Bake Scatter/mars-olympus")]
        public static void BakeOlympus() => Bake("mars-olympus", 1200);
        [MenuItem("Wayfinder/Scatter/Bake Scatter/mars-valles")]
        public static void BakeValles() => Bake("mars-valles", 2500);
        [MenuItem("Wayfinder/Scatter/Bake Scatter/moon-shackleton")]
        public static void BakeShackleton() => Bake("moon-shackleton", 800);

        [MenuItem("Wayfinder/Scatter/Bake Scatter/ALL")]
        public static void BakeAll() { BakeOlympus(); BakeValles(); BakeShackleton(); }

        public static void Bake(string siteId, int cap)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Wayfinder/Terrain/" + siteId + ".asset");
            if (data == null) throw new System.InvalidOperationException(siteId + ": no TerrainData — import terrain first.");
            var set = AssetDatabase.LoadAssetAtPath<ScatterArchetypeSet>("Assets/Wayfinder/Scatter/" + siteId + "_set.asset");
            if (set == null) throw new System.InvalidOperationException(siteId + ": no ScatterArchetypeSet at Assets/Wayfinder/Scatter/" + siteId + "_set.asset");
            var archs = set.Archetypes;
            if (archs == null || archs.Length == 0) throw new System.InvalidOperationException(siteId + ": archetype set is empty.");

            // Terrain world offset — read off the scene's SiteTerrain, same
            // transform the importer applied (never recompute).
            Vector3 terrainOffset = ReadTerrainOffset(siteId);

            // Spawn is placed at WORLD (spawnOffset.x, spawnOffset.y) by
            // TravelManager.ApplySpawnOffset — NOT offset by terrain centre.
            var pkg = AssetDatabase.LoadAssetAtPath<WorldPackage>("Assets/Wayfinder/Sites/" + siteId + ".asset");
            Vector2 spawnXZ = pkg != null ? pkg.SpawnOffset : Vector2.zero;

            // Exclusion discs: spawn + POIs (player must not land inside rock).
            var exclusions = new List<Vector2> { spawnXZ };
            if (pkg != null && pkg.PoiData != null)
            {
                try
                {
                    var poiSet = PoiSet.Parse(pkg.PoiData.text);
                    foreach (var poi in poiSet.pois)
                        if (poi.HasPosition) exclusions.Add(new Vector2(poi.positionX, poi.positionZ));
                }
                catch (System.Exception e) { Debug.LogWarning($"[ScatterBaker] {siteId}: POI exclusions skipped — {e.Message}"); }
            }

            int seed = (int)(uint)siteId.GetHashCode();

            // Rocks concentrate in the WALKABLE zone around spawn, not spread
            // over the whole 20 km site (that gives near-zero local density).
            // A disc of this radius around spawn holds the whole cap.
            const float bakeRadius = 320f;
            float minX = spawnXZ.x - bakeRadius, minZ = spawnXZ.y - bakeRadius;
            float span = bakeRadius * 2f;
            // Grid resolution so accepted count comfortably exceeds the cap.
            int gridN = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(cap * 3f)), 48, 260);
            var pos = new List<Vector3>();
            var rot = new List<Quaternion>();
            var scl = new List<float>();
            var archIdx = new List<byte>();
            var priorities = new List<float>();

            for (int gz = 0; gz < gridN; gz++)
            for (int gx = 0; gx < gridN; gx++)
            {
                float jx = ScatterPlacement.Hash01(gx, gz, seed);
                float jz = ScatterPlacement.Hash01(gx, gz, seed ^ 0x5bd1e995);
                // World XZ inside the bake disc around spawn.
                float wx = minX + (gx + jx) / gridN * span;
                float wz = minZ + (gz + jz) / gridN * span;
                float ddx = wx - spawnXZ.x, ddz = wz - spawnXZ.y;
                if (ddx * ddx + ddz * ddz > bakeRadius * bakeRadius) continue; // disc, not box

                // World XZ -> terrain UV for sampling.
                float u = (wx - terrainOffset.x) / data.size.x;
                float v = (wz - terrainOffset.z) / data.size.z;
                if (u < 0f || u > 1f || v < 0f || v > 1f) continue; // off-terrain

                float steep = data.GetSteepness(u, v);
                float hy = data.GetInterpolatedHeight(u, v) + terrainOffset.y;
                Vector3 normal = data.GetInterpolatedNormal(u, v);
                Vector3 world = new Vector3(wx, hy, wz);

                bool wallBase = DetectWallBase(data, u, v, steep);
                float nearest = NearestFeature(world, exclusions);

                var cand = new ScatterPlacement.Candidate
                {
                    worldPos = world, steepnessDeg = steep,
                    wallBaseFlag = wallBase, nearestFeature = nearest,
                };

                // Try each archetype; first accepting rule wins (biased by hash).
                int a0 = (int)(ScatterPlacement.Hash01(gx, gz, seed ^ 0x1234) * archs.Length);
                for (int k = 0; k < archs.Length; k++)
                {
                    int a = (a0 + k) % archs.Length;
                    if (!ScatterPlacement.Accept(cand, archs[a].ToRule())) continue;

                    float su = ScatterPlacement.Hash01(gx, gz, seed ^ (a * 0x9e3779b1u).GetHashCode());
                    float scale = ScatterPlacement.LogNormalScale(su, archs[a].MedianScale, archs[a].ScaleSigma);
                    float yaw = ScatterPlacement.Hash01(gx, gz, seed ^ 0x77 ^ a) * 360f;
                    // Tilt: blend world-up with terrain normal 70/30 (sit, don't conform).
                    Vector3 up = Vector3.Slerp(Vector3.up, normal, 0.3f);
                    Quaternion q = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.FromToRotation(Vector3.up, up);
                    // Embed 15-30% of radius so it sits IN the regolith.
                    float embed = archs[a].ColliderRadius * scale * Mathf.Lerp(0.15f, 0.30f,
                        ScatterPlacement.Hash01(gx, gz, seed ^ 0xEE));
                    Vector3 p = world - new Vector3(0, embed, 0);

                    pos.Add(p); rot.Add(q); scl.Add(scale);
                    archIdx.Add((byte)a);
                    priorities.Add(ScatterPlacement.Priority(scale, nearest));
                    break;
                }
            }

            // Hard cap.
            var kept = ScatterPlacement.EnforceCap(priorities, cap);
            int keptN = kept.Count;
            var fPos = new Vector3[keptN]; var fRot = new Quaternion[keptN];
            var fScl = new float[keptN]; var fArch = new byte[keptN];
            for (int i = 0; i < keptN; i++)
            {
                int src = kept[i];
                fPos[i] = pos[src]; fRot[i] = rot[src]; fScl[i] = scl[src];
                fArch[i] = archIdx[src];
            }

            // World-space AABB enclosing every rock (+ a rock's reach), so the
            // renderer's frustum-cull passes even when the field sits kilometres
            // from origin (Shackleton rim).
            Bounds bounds;
            if (keptN > 0)
            {
                bounds = new Bounds(fPos[0], Vector3.zero);
                for (int i = 1; i < keptN; i++) bounds.Encapsulate(fPos[i]);
                bounds.Expand(20f); // margin for rock scale + embed
            }
            else bounds = new Bounds(new Vector3(spawnXZ.x, 0f, spawnXZ.y), Vector3.one);

            // Write asset.
            string fieldPath = "Assets/Wayfinder/Scatter/ScatterField_" + siteId + ".asset";
            var fieldAsset = AssetDatabase.LoadAssetAtPath<ScatterFieldData>(fieldPath);
            bool isNew = fieldAsset == null;
            if (isNew) fieldAsset = ScriptableObject.CreateInstance<ScatterFieldData>();
            fieldAsset.Set(siteId, fPos, fRot, fScl, fArch, bounds);
            if (isNew) AssetDatabase.CreateAsset(fieldAsset, fieldPath);
            else EditorUtility.SetDirty(fieldAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ScatterBaker] {siteId}: {keptN}/{pos.Count} rocks kept (cap {cap}), bounds {bounds.size}.");
        }

        static bool DetectWallBase(TerrainData data, float u, float v, float selfSteep)
        {
            if (selfSteep >= 20f) return false; // base must itself be gentle
            float du = 15f / data.size.x, dv = 15f / data.size.z;
            for (int k = 0; k < 4; k++)
            {
                float nu = Mathf.Clamp01(u + (k == 0 ? du : k == 1 ? -du : 0));
                float nv = Mathf.Clamp01(v + (k == 2 ? dv : k == 3 ? -dv : 0));
                if (data.GetSteepness(nu, nv) > 32f) return true; // a steep neighbour uphill
            }
            return false;
        }

        static float NearestFeature(Vector3 world, List<Vector2> features)
        {
            float best = float.MaxValue;
            var p = new Vector2(world.x, world.z);
            foreach (var f in features)
            {
                float d = Vector2.Distance(p, f);
                if (d < best) best = d;
            }
            return best == float.MaxValue ? 9999f : best;
        }

        static Vector3 ReadTerrainOffset(string siteId)
        {
            var savedSetup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    "Assets/Scenes/Site_" + siteId + ".unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
                var t = GameObject.Find("SiteTerrain");
                return t != null ? t.transform.position : Vector3.zero;
            }
            finally
            {
                if (savedSetup != null && savedSetup.Length > 0)
                    UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(savedSetup);
            }
        }
    }
}
