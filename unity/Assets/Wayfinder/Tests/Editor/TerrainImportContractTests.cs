using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wayfinder.Unity;

namespace Wayfinder.Unity.Tests
{
    /// Contract tests for the mars-olympus terrain import (build-plan Task 2.2).
    /// These assert the data promises: true metric scale from meta.json, full
    /// use of the 16-bit height range, and smoothness (a byte-order misread
    /// turns real terrain into per-pixel noise — the classic gotcha).
    public class TerrainImportContractTests
    {
        const string TerrainAssetPath = "Assets/Wayfinder/Terrain/mars-olympus.asset";

        static TerrainData Load()
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
            Assert.IsNotNull(data, "terrain data missing at " + TerrainAssetPath);
            return data;
        }

        [Test]
        public void Terrain_Has_The_Contract_Resolution_And_True_Metric_Size()
        {
            var data = Load();
            Assert.AreEqual(2049, data.heightmapResolution);
            Assert.AreEqual(20000f, data.size.x, 1f);
            Assert.AreEqual(2070f, data.size.y, 1f);
            Assert.AreEqual(20000f, data.size.z, 1f);
        }

        [Test]
        public void Heights_Use_The_Full_Normalized_Range()
        {
            var data = Load();
            var heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
            float min = 1f, max = 0f;
            foreach (var h in heights) { if (h < min) min = h; if (h > max) max = h; }
            Assert.Less(min, 0.05f, "minimum height far from 0 — scaling wrong");
            Assert.Greater(max, 0.95f, "maximum height far from 1 — scaling wrong");
        }

        [Test]
        public void Terrain_Is_Smooth_Not_ByteOrder_Noise()
        {
            var data = Load();
            int n = data.heightmapResolution;
            var heights = data.GetHeights(0, 0, n, n);
            // Real 50 m/px-sourced terrain resampled to ~9.8 m cells changes
            // slowly; a little-endian misread yields ~random neighbors. Mean
            // absolute neighbor delta in normalized units stays far below 0.01
            // for real data and lands near 0.25 for noise.
            double sum = 0; long count = 0;
            for (int y = 0; y < n; y += 8)
                for (int x = 1; x < n; x += 8)
                { sum += Mathf.Abs(heights[y, x] - heights[y, x - 1]); count++; }
            double meanDelta = sum / count;
            Assert.Less(meanDelta, 0.01, $"mean neighbor delta {meanDelta:F4} — looks like byte-order noise, not terrain");
        }

        [Test]
        public void Terrain_Orientation_Is_Not_Mirrored_NorthSouth()
        {
            // Anchor computed from the visually-verified import: the NE-rim
            // high ground sits along the clip's NORTH edge (mean ≈ 0.87), the
            // caldera floor along the south (≈ 0.50). A dropped row flip in the
            // importer mirrors these. Every other contract test is
            // mirror-invariant — this one pins orientation.
            var data = Load();
            int n = data.heightmapResolution;
            var h = data.GetHeights(0, 0, n, n);
            double north = 0, south = 0; long c = 0;
            for (int strip = 0; strip < 64; strip++)
                for (int x = 0; x < n; x += 4)
                {
                    south += h[strip, x];
                    north += h[n - 1 - strip, x];
                    c++;
                }
            Assert.Greater(north / c, south / c + 0.2,
                $"north strip mean {north / c:F3} not clearly above south {south / c:F3} — terrain looks north-south mirrored");
        }

        [Test]
        public void WorldPackage_References_The_Terrain()
        {
            var pkg = AssetDatabase.LoadAssetAtPath<WorldPackage>("Assets/Wayfinder/Sites/mars-olympus.asset");
            Assert.IsNotNull(pkg);
            Assert.IsNotNull(pkg.Terrain, "mars-olympus WorldPackage has no terrain assigned");
            Assert.AreEqual(TerrainAssetPath, AssetDatabase.GetAssetPath(pkg.Terrain));
        }

        [Test]
        public void Site_Scene_Contains_A_Terrain_With_A_Collider()
        {
            var savedSetup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    "Assets/Scenes/Site_mars-olympus.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
                Terrain terrain = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    terrain = root.GetComponentInChildren<Terrain>(true);
                    if (terrain != null) break;
                }
                Assert.IsNotNull(terrain, "no Terrain in Site_mars-olympus");
                Assert.IsNotNull(terrain.GetComponent<TerrainCollider>(), "terrain has no collider");
                var area = terrain.GetComponent<global::UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
                Assert.IsNotNull(area, "terrain is not a TeleportationArea — the site can't be walked");
                Assert.AreEqual(1 << 31, area.interactionLayers.value,
                    "TeleportationArea must sit on the Teleport interaction layer (bit 31) — on Default the giant collider swallows every near-far interaction");
                Assert.AreEqual(TerrainAssetPath, AssetDatabase.GetAssetPath(terrain.terrainData));
                // The player spawns at the origin: terrain surface there must sit
                // near y=0 so the rig stands on the ground, not inside or above it.
                float surfaceY = terrain.SampleHeight(Vector3.zero) + terrain.transform.position.y;
                Assert.AreEqual(0f, surfaceY, 2f, "terrain surface at spawn is far from y=0");
            }
            finally
            {
                if (savedSetup != null && savedSetup.Length > 0)
                    UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(savedSetup);
            }
        }
    }
}
