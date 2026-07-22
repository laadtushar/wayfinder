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

        [TestCase("mars-olympus")]
        [TestCase("mars-valles")]
        [TestCase("moon-shackleton")]
        public void Terrain_Wears_Its_Real_Orbital_Imagery(string siteId)
        {
            // Realism contract: every site's terrain layer must carry the
            // clipped orbital photograph (see data-sources.md), tiled exactly
            // once across the full metric extent — never the solid fallback.
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
                "Assets/Wayfinder/Terrain/" + siteId + "_base.terrainlayer");
            Assert.IsNotNull(layer.diffuseTexture, siteId + " layer has no diffuse");
            Assert.AreEqual(siteId + "_albedo", layer.diffuseTexture.name,
                siteId + " not wearing its orbital imagery");
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(
                "Assets/Wayfinder/Terrain/" + siteId + ".asset");
            Assert.AreEqual(data.size.x, layer.tileSize.x, 1f, siteId + " imagery tiles horizontally");
            Assert.AreEqual(data.size.z, layer.tileSize.y, 1f, siteId + " imagery tiles vertically");
        }

        [TestCase("Assets/Scenes/Site_mars-olympus.unity")]
        [TestCase("Assets/Scenes/Site_mars-valles.unity")]
        [TestCase("Assets/Scenes/Site_moon-shackleton.unity")]
        public void Every_Site_Renders_A_Real_Sky(string scenePath)
        {
            var savedSetup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath,
                    UnityEditor.SceneManagement.OpenSceneMode.Single);
                Assert.IsNotNull(RenderSettings.skybox, scenePath + " has no skybox");
                if (scenePath.Contains("shackleton"))
                {
                    Assert.IsNotNull(GameObject.Find("Earth"),
                        "Shackleton has no Earth over the rim — the earth-on-horizon POI describes it");
                    Assert.AreEqual("StarSky", RenderSettings.skybox.name);
                }
                else
                {
                    Assert.AreEqual("MarsSky", RenderSettings.skybox.name);
                }
            }
            finally
            {
                if (savedSetup != null && savedSetup.Length > 0)
                    UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(savedSetup);
            }
        }

        [Test]
        public void Shackleton_Spawns_On_The_Rim_Not_The_Shadowed_Floor()
        {
            // The clip centres on the permanently shadowed floor — narratively
            // untouchable. The World Package carries a rim spawn offset; this
            // pins both the offset's presence and that it lands on high ground.
            var pkg = AssetDatabase.LoadAssetAtPath<WorldPackage>("Assets/Wayfinder/Sites/moon-shackleton.asset");
            Assert.AreNotEqual(Vector2.zero, pkg.SpawnOffset, "shackleton has no spawn offset");
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Wayfinder/Terrain/moon-shackleton.asset");
            int n = data.heightmapResolution;
            int col = Mathf.Clamp((int)((pkg.SpawnOffset.x + data.size.x / 2f) / data.size.x * (n - 1)), 0, n - 1);
            int row = Mathf.Clamp((int)((pkg.SpawnOffset.y + data.size.z / 2f) / data.size.z * (n - 1)), 0, n - 1);
            float h = data.GetHeights(col, row, 1, 1)[0, 0];
            Assert.Greater(h, 0.6f, $"spawn at normalized height {h:F2} — not on the rim");
        }

        [TestCase("mars-valles", 3600f, 4703f, 19500f)]
        [TestCase("moon-shackleton", 20000f, 4438f, 20000f)]
        public void Phase3_Sites_Have_True_Metric_Sizes(string siteId, float x, float y, float z)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Wayfinder/Terrain/" + siteId + ".asset");
            Assert.AreEqual(x, data.size.x, 1f, siteId + " width");
            Assert.AreEqual(y, data.size.y, 1f, siteId + " height range");
            Assert.AreEqual(z, data.size.z, 1f, siteId + " length");
        }

        [TestCase("mars-valles")]
        [TestCase("moon-shackleton")]
        public void Phase3_Sites_Meet_The_Shared_Import_Contract(string siteId)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Wayfinder/Terrain/" + siteId + ".asset");
            Assert.IsNotNull(data, siteId + " terrain missing");
            Assert.AreEqual(2049, data.heightmapResolution);

            var heights = data.GetHeights(0, 0, 2049, 2049);
            float min = 1f, max = 0f;
            double deltaSum = 0; long deltaCount = 0;
            for (int y = 0; y < 2049; y += 8)
                for (int x = 1; x < 2049; x += 8)
                {
                    float v = heights[y, x];
                    if (v < min) min = v;
                    if (v > max) max = v;
                    deltaSum += Mathf.Abs(v - heights[y, x - 1]);
                    deltaCount++;
                }
            Assert.Less(min, 0.05f, siteId + " min far from 0");
            Assert.Greater(max, 0.95f, siteId + " max far from 1");
            Assert.Less(deltaSum / deltaCount, 0.01, siteId + " looks like byte-order noise");

            var pkg = AssetDatabase.LoadAssetAtPath<WorldPackage>("Assets/Wayfinder/Sites/" + siteId + ".asset");
            Assert.AreEqual(data, pkg.Terrain, siteId + " package terrain not wired");
        }

        [Test]
        public void Valles_Orientation_South_Wall_High_Floor_North()
        {
            // Anchors from the verified import: southern plateau strip ≈ 0.83,
            // northern floor strip ≈ 0.09. A row-flip regression mirrors them.
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Wayfinder/Terrain/mars-valles.asset");
            int n = data.heightmapResolution;
            var h = data.GetHeights(0, 0, n, n);
            double north = 0, south = 0; long c = 0;
            for (int strip = 0; strip < 64; strip++)
                for (int x = 0; x < n; x += 4)
                { south += h[strip, x]; north += h[n - 1 - strip, x]; c++; }
            Assert.Greater(south / c, north / c + 0.5,
                $"south {south / c:F3} not clearly above north {north / c:F3} — valles looks mirrored");
        }

        [Test]
        public void Shackleton_Orientation_Is_A_Centered_Crater_Bowl()
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Wayfinder/Terrain/moon-shackleton.asset");
            int n = data.heightmapResolution;
            var h = data.GetHeights(0, 0, n, n);
            int mid = n / 2;
            double center = 0; long c = 0;
            for (int r = mid - 128; r < mid + 128; r += 4)
                for (int col = mid - 128; col < mid + 128; col += 4)
                { center += h[r, col]; c++; }
            double edge = 0; long e = 0;
            for (int strip = 0; strip < 64; strip++)
                for (int i = 0; i < n; i += 4)
                { edge += h[strip, i] + h[n - 1 - strip, i]; e += 2; }
            Assert.Less(center / c, 0.2, "crater centre not deep — bowl missing");
            Assert.Greater(edge / e, 0.6, "edges not high — rim missing");
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
