using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wayfinder.Unity.Scatter;

namespace Wayfinder.Unity.Tests
{
    /// The three placement invariants that matter (spec §1B): determinism,
    /// the cap is never exceeded on adversarial terrain, and slope is
    /// world-space (a cliff produces talus only at its base).
    public class ScatterPlacementTests
    {
        static ScatterPlacement.Rule TalusRule() => new ScatterPlacement.Rule
        {
            minSlopeDeg = 0f, maxSlopeDeg = 30f,
            minWorldY = -100000f, maxWorldY = 100000f,
            requireWallBase = true, clearRadius = 3f,
        };

        [Test]
        public void Hash01_Is_Deterministic_And_In_Range()
        {
            for (int i = 0; i < 500; i++)
            {
                float a = ScatterPlacement.Hash01(i, i * 2, 12345);
                float b = ScatterPlacement.Hash01(i, i * 2, 12345);
                Assert.AreEqual(a, b, "hash noise must be bit-stable (not Mathf.PerlinNoise)");
                Assert.GreaterOrEqual(a, 0f);
                Assert.Less(a, 1f);
            }
            // Distinct seeds decorrelate.
            Assert.AreNotEqual(
                ScatterPlacement.Hash01(3, 7, 1),
                ScatterPlacement.Hash01(3, 7, 2));
        }

        [Test]
        public void EnforceCap_Never_Exceeds_Cap_On_All_Flat_Terrain()
        {
            // Adversarial: 5000 candidates all accepted.
            var priorities = new List<float>();
            for (int i = 0; i < 5000; i++)
                priorities.Add(ScatterPlacement.Hash01(i, 0, 99) * 3f);

            var kept = ScatterPlacement.EnforceCap(priorities, 1300);
            Assert.AreEqual(1300, kept.Count, "cap must be hard regardless of terrain");
            // Kept indices returned in original order (for coherent compaction).
            for (int i = 1; i < kept.Count; i++)
                Assert.Less(kept[i - 1], kept[i], "kept indices must stay sorted");
        }

        [Test]
        public void EnforceCap_Keeps_The_Highest_Priority()
        {
            var priorities = new List<float> { 0.1f, 5f, 0.2f, 4f, 0.3f };
            var kept = ScatterPlacement.EnforceCap(priorities, 2);
            // Highest two are indices 1 (5) and 3 (4).
            CollectionAssert.AreEqual(new[] { 1, 3 }, kept);
        }

        [Test]
        public void EnforceCap_Under_Cap_Keeps_Everything()
        {
            var priorities = new List<float> { 1f, 2f, 3f };
            var kept = ScatterPlacement.EnforceCap(priorities, 10);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, kept);
        }

        [Test]
        public void Talus_Accepts_Only_At_The_Cliff_Base_Not_On_The_Wall()
        {
            var rule = TalusRule();

            // Flat apron at a cliff base: gentle self-slope, wall-base flag set.
            var apron = new ScatterPlacement.Candidate
            {
                worldPos = new Vector3(0, 10, 0),
                steepnessDeg = 12f, wallBaseFlag = true, nearestFeature = 50f,
            };
            Assert.IsTrue(ScatterPlacement.Accept(apron, rule), "talus must land at the cliff base");

            // The steep wall itself: no talus (both slope-rejected AND no flag).
            var wall = new ScatterPlacement.Candidate
            {
                worldPos = new Vector3(0, 40, 0),
                steepnessDeg = 55f, wallBaseFlag = false, nearestFeature = 50f,
            };
            Assert.IsFalse(ScatterPlacement.Accept(wall, rule), "no talus on the vertical wall");

            // Flat ground far from any cliff: gentle slope but no wall-base flag.
            var openFlat = new ScatterPlacement.Candidate
            {
                worldPos = new Vector3(0, 5, 0),
                steepnessDeg = 3f, wallBaseFlag = false, nearestFeature = 50f,
            };
            Assert.IsFalse(ScatterPlacement.Accept(openFlat, rule),
                "talus requires the wall-base flag — open flat is not an apron");
        }

        [Test]
        public void Fourth_World_Tranquillity_Is_A_Complete_Data_Package()
        {
            // Worlds-as-data: the fourth site (Apollo 11) is a full World
            // Package registered in the catalog, with real gravity, 8 sourced
            // POIs, and a scene — added with NO travel/locomotion/discovery code.
            var pkg = UnityEditor.AssetDatabase.LoadAssetAtPath<Wayfinder.Unity.WorldPackage>(
                "Assets/Wayfinder/Sites/moon-tranquillity.asset");
            Assert.IsNotNull(pkg, "moon-tranquillity package missing");
            var def = pkg.ToDefinition();
            Assert.AreEqual("moon-tranquillity", def.Id);
            Assert.AreEqual(1.62f, def.SurfaceGravity, 0.001f, "real lunar gravity");
            Assert.AreEqual("Site_moon-tranquillity", def.SceneName);
            Assert.IsNotNull(pkg.PoiData, "no POI data wired");

            var set = Wayfinder.Unity.PoiSet.Parse(pkg.PoiData.text);
            Assert.AreEqual("moon-tranquillity", set.siteId);
            Assert.AreEqual(8, set.pois.Count, "8 authored Apollo 11 POIs");
            foreach (var p in set.pois)
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.title), p.id + " missing title");
                Assert.IsFalse(string.IsNullOrEmpty(p.fact), p.id + " missing fact");
                Assert.IsFalse(string.IsNullOrEmpty(p.source), p.id + " missing source citation");
            }

            // Registered in the catalog (so it reaches the viewscreen).
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Wayfinder.Unity.WorldCatalog>(
                "Assets/Wayfinder/Sites/WorldCatalog.asset");
            bool inCatalog = false;
            foreach (var p in catalog.Packages) if (p == pkg) inCatalog = true;
            Assert.IsTrue(inCatalog, "fourth world not registered in the catalog");
            // The catalog still builds a valid registry (no dup ids).
            Assert.DoesNotThrow(() => catalog.BuildRegistry());
        }

        [Test]
        public void Fifth_World_Jezero_Is_A_Complete_Data_Package()
        {
            // Worlds-as-data, again: the fifth site (Jezero / Perseverance) is a
            // full World Package with real Mars gravity, 8 sourced POIs, and a
            // scene — added with NO travel/locomotion/discovery code.
            var pkg = UnityEditor.AssetDatabase.LoadAssetAtPath<Wayfinder.Unity.WorldPackage>(
                "Assets/Wayfinder/Sites/mars-jezero.asset");
            Assert.IsNotNull(pkg, "mars-jezero package missing");
            var def = pkg.ToDefinition();
            Assert.AreEqual("mars-jezero", def.Id);
            Assert.AreEqual(3.72f, def.SurfaceGravity, 0.001f, "real Mars gravity");
            Assert.AreEqual("Site_mars-jezero", def.SceneName);
            Assert.IsNotNull(pkg.PoiData, "no POI data wired");

            var set = Wayfinder.Unity.PoiSet.Parse(pkg.PoiData.text);
            Assert.AreEqual("mars-jezero", set.siteId);
            Assert.AreEqual(8, set.pois.Count, "8 authored Jezero POIs");
            foreach (var p in set.pois)
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.title), p.id + " missing title");
                Assert.IsFalse(string.IsNullOrEmpty(p.fact), p.id + " missing fact");
                Assert.IsFalse(string.IsNullOrEmpty(p.source), p.id + " missing source citation");
            }

            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Wayfinder.Unity.WorldCatalog>(
                "Assets/Wayfinder/Sites/WorldCatalog.asset");
            bool inCatalog = false;
            foreach (var p in catalog.Packages) if (p == pkg) inCatalog = true;
            Assert.IsTrue(inCatalog, "fifth world not registered in the catalog");
            Assert.DoesNotThrow(() => catalog.BuildRegistry());
        }

        [Test]
        public void Accept_Rejects_Inside_The_Feature_Clear_Radius()
        {
            var rule = TalusRule();
            var tooClose = new ScatterPlacement.Candidate
            {
                worldPos = Vector3.zero,
                steepnessDeg = 10f, wallBaseFlag = true, nearestFeature = 1.5f,
            };
            Assert.IsFalse(ScatterPlacement.Accept(tooClose, rule),
                "no rock inside the spawn/POI clear radius");
        }

        [Test]
        public void Priority_Favors_Big_Rocks_And_Near_Features()
        {
            float bigNear = ScatterPlacement.Priority(3f, 2f);
            float bigFar = ScatterPlacement.Priority(3f, 100f);
            float smallNear = ScatterPlacement.Priority(0.5f, 2f);
            Assert.Greater(bigNear, bigFar, "near-feature rocks outrank far ones");
            Assert.Greater(bigNear, smallNear, "big rocks outrank small ones");
        }

        [TestCase("mars-olympus", 1200)]
        [TestCase("mars-valles", 2500)]
        [TestCase("moon-shackleton", 800)]
        [TestCase("moon-tranquillity", 800)]
        [TestCase("mars-jezero", 1000)]
        public void Site_Has_A_Baked_Scatter_Field_Within_Cap(string siteId, int cap)
        {
            var field = UnityEditor.AssetDatabase.LoadAssetAtPath<ScatterFieldData>(
                "Assets/Wayfinder/Scatter/ScatterField_" + siteId + ".asset");
            Assert.IsNotNull(field, siteId + " has no baked scatter field");
            Assert.Greater(field.Count, 0, siteId + " scatter field is empty");
            Assert.LessOrEqual(field.Count, cap, siteId + " scatter exceeds its cap");
            Assert.AreEqual(field.Count, field.Rotations.Length, siteId + " SoA arrays desynced");
            Assert.AreEqual(field.Count, field.Scales.Length, siteId + " SoA arrays desynced");

            var set = UnityEditor.AssetDatabase.LoadAssetAtPath<ScatterArchetypeSet>(
                "Assets/Wayfinder/Scatter/" + siteId + "_set.asset");
            Assert.IsNotNull(set, siteId + " has no archetype set");
            Assert.IsNotNull(set.Material, siteId + " set has no rock material");
            Assert.AreEqual("Wayfinder/RockInstanced", set.Material.shader.name, siteId + " wrong rock shader");
        }

        [TestCase("Assets/Scenes/Site_mars-olympus.unity")]
        [TestCase("Assets/Scenes/Site_mars-valles.unity")]
        [TestCase("Assets/Scenes/Site_moon-shackleton.unity")]
        public void Site_Scene_Has_A_Wired_Scatter_Renderer(string scenePath)
        {
            var saved = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                ScatterRenderer rend = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    rend = root.GetComponentInChildren<ScatterRenderer>(true);
                    if (rend != null) break;
                }
                Assert.IsNotNull(rend, scenePath + " has no ScatterRenderer");
                var so = new UnityEditor.SerializedObject(rend);
                Assert.IsNotNull(so.FindProperty("field").objectReferenceValue, scenePath + " renderer field unwired");
                Assert.IsNotNull(so.FindProperty("set").objectReferenceValue, scenePath + " renderer set unwired");
            }
            finally
            {
                if (saved != null && saved.Length > 0)
                    UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(saved);
            }
        }

        [Test]
        public void LogNormalScale_Is_Monotonic_And_Positive()
        {
            float lo = ScatterPlacement.LogNormalScale(0.2f, 1f, 0.5f);
            float mid = ScatterPlacement.LogNormalScale(0.5f, 1f, 0.5f);
            float hi = ScatterPlacement.LogNormalScale(0.8f, 1f, 0.5f);
            Assert.Greater(lo, 0f);
            Assert.Less(lo, mid);
            Assert.Less(mid, hi);
            Assert.AreEqual(1f, mid, 0.01f, "median u=0.5 -> median scale");
        }
    }
}
