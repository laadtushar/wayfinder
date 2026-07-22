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

            // Exclusion discs: spawn + POIs (player must not land inside rock).
            var exclusions = new List<Vector2>();
            var pkg = AssetDatabase.LoadAssetAtPath<WorldPackage>("Assets/Wayfinder/Sites/" + siteId + ".asset");
            if (pkg != null) exclusions.Add(pkg.SpawnOffset + new Vector2(terrainOffset.x + data.size.x * 0.5f, terrainOffset.z + data.size.z * 0.5f));

            int seed = (int)(uint)siteId.GetHashCode();
            var rng = new System.Random(seed);

            // Jittered grid over terrain UV. Grid resolution scaled so accepted
            // count comfortably exceeds the cap before enforcement.
            int gridN = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(cap * 4f)), 32, 220);
            var pos = new List<Vector3>();
            var rot = new List<Quaternion>();
            var scl = new List<float>();
            var archIdx = new List<byte>();
            var tints = new List<Color>();
            var priorities = new List<float>();

            for (int gz = 0; gz < gridN; gz++)
            for (int gx = 0; gx < gridN; gx++)
            {
                float jx = ScatterPlacement.Hash01(gx, gz, seed);
                float jz = ScatterPlacement.Hash01(gx, gz, seed ^ 0x5bd1e995);
                float u = (gx + jx) / gridN;
                float v = (gz + jz) / gridN;

                float steep = data.GetSteepness(u, v);
                float hy = data.GetInterpolatedHeight(u, v) + terrainOffset.y;
                Vector3 normal = data.GetInterpolatedNormal(u, v);
                Vector3 world = new Vector3(terrainOffset.x + u * data.size.x, hy, terrainOffset.z + v * data.size.z);

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

                    Color tint = archs[a].Tint;
                    // Small per-instance value jitter so rocks aren't uniform.
                    float vj = (ScatterPlacement.Hash01(gx, gz, seed ^ 0xB5) - 0.5f) * 0.12f;
                    tint = new Color(Mathf.Clamp01(tint.r + vj), Mathf.Clamp01(tint.g + vj), Mathf.Clamp01(tint.b + vj), 1f);

                    pos.Add(p); rot.Add(q); scl.Add(scale);
                    archIdx.Add((byte)a); tints.Add(tint);
                    priorities.Add(ScatterPlacement.Priority(scale, nearest));
                    break;
                }
            }

            // Hard cap.
            var kept = ScatterPlacement.EnforceCap(priorities, cap);
            int keptN = kept.Count;
            var fPos = new Vector3[keptN]; var fRot = new Quaternion[keptN];
            var fScl = new float[keptN]; var fArch = new byte[keptN]; var fTint = new Color[keptN];
            for (int i = 0; i < keptN; i++)
            {
                int src = kept[i];
                fPos[i] = pos[src]; fRot[i] = rot[src]; fScl[i] = scl[src];
                fArch[i] = archIdx[src]; fTint[i] = tints[src];
            }

            // Cell-sort for coherent runtime iteration.
            float cellSize = 16f;
            float minX = terrainOffset.x, minZ = terrainOffset.z;
            int cellsX = Mathf.CeilToInt(data.size.x / cellSize);
            int cellsZ = Mathf.CeilToInt(data.size.z / cellSize);
            var order = new int[keptN];
            for (int i = 0; i < keptN; i++) order[i] = i;
            System.Func<int, int> cellOf = i =>
            {
                int cx = Mathf.Clamp((int)((fPos[i].x - minX) / cellSize), 0, cellsX - 1);
                int cz = Mathf.Clamp((int)((fPos[i].z - minZ) / cellSize), 0, cellsZ - 1);
                return cz * cellsX + cx;
            };
            System.Array.Sort(order, (a, b) => cellOf(a).CompareTo(cellOf(b)));

            var sPos = new Vector3[keptN]; var sRot = new Quaternion[keptN];
            var sScl = new float[keptN]; var sArch = new byte[keptN]; var sTint = new Color[keptN];
            int nCells = cellsX * cellsZ;
            var cellStart = new int[nCells]; var cellCount = new int[nCells];
            for (int i = 0; i < keptN; i++)
            {
                int src = order[i];
                sPos[i] = fPos[src]; sRot[i] = fRot[src]; sScl[i] = fScl[src];
                sArch[i] = fArch[src]; sTint[i] = fTint[src];
            }
            for (int i = 0; i < keptN; i++)
            {
                int cx = Mathf.Clamp((int)((sPos[i].x - minX) / cellSize), 0, cellsX - 1);
                int cz = Mathf.Clamp((int)((sPos[i].z - minZ) / cellSize), 0, cellsZ - 1);
                int cell = cz * cellsX + cx;
                if (cellCount[cell] == 0) cellStart[cell] = i;
                cellCount[cell]++;
            }

            // Write asset.
            string fieldPath = "Assets/Wayfinder/Scatter/ScatterField_" + siteId + ".asset";
            var fieldAsset = AssetDatabase.LoadAssetAtPath<ScatterFieldData>(fieldPath);
            bool isNew = fieldAsset == null;
            if (isNew) fieldAsset = ScriptableObject.CreateInstance<ScatterFieldData>();
            fieldAsset.Set(siteId, sPos, sRot, sScl, sArch, sTint, cellSize, cellsX, cellsZ,
                new Vector2(minX, minZ), cellStart, cellCount);
            if (isNew) AssetDatabase.CreateAsset(fieldAsset, fieldPath);
            else EditorUtility.SetDirty(fieldAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ScatterBaker] {siteId}: {keptN}/{pos.Count} rocks kept (cap {cap}), {nCells} cells.");
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
