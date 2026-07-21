using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wayfinder.Core;
using Wayfinder.Unity;

namespace Wayfinder.Unity.Tests
{
    public class PoiContractTests
    {
        static string OlympusJson() =>
            AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Wayfinder/POI/mars-olympus.json").text;

        [TestCase("mars-olympus")]
        [TestCase("mars-valles")]
        [TestCase("moon-shackleton")]
        public void Site_Poi_File_Parses_With_Eight_Placed_Pois(string siteId)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Wayfinder/POI/" + siteId + ".json").text;
            var set = PoiSet.Parse(json);
            Assert.AreEqual(siteId, set.siteId);
            Assert.AreEqual(8, set.pois.Count);
            foreach (var poi in set.pois)
            {
                Assert.IsTrue(poi.HasPosition, poi.id + " has no baked position");
                Assert.IsFalse(string.IsNullOrEmpty(poi.title), poi.id + " has no title");
                Assert.IsFalse(string.IsNullOrEmpty(poi.fact), poi.id + " has no fact");
                Assert.IsFalse(string.IsNullOrEmpty(poi.source), poi.id + " has no source");
            }
        }

        [TestCase("mars-olympus")]
        [TestCase("mars-valles")]
        [TestCase("moon-shackleton")]
        public void Every_Baked_Position_Is_Inside_Its_Sites_Terrain(string siteId)
        {
            // The valles clip is a narrow HiRISE strip (±1800 m x) — a
            // position baked against the wrong frame spawns an unreachable
            // marker, and worse, the runtime bounds guard would fire inside
            // the warp fade. Catch it here instead.
            var terrain = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Wayfinder/Terrain/" + siteId + ".asset");
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Wayfinder/POI/" + siteId + ".json").text;
            float halfX = terrain.size.x / 2f, halfZ = terrain.size.z / 2f;
            foreach (var poi in PoiSet.Parse(json).pois)
            {
                Assert.LessOrEqual(Mathf.Abs(poi.positionX), halfX, poi.id + " x outside terrain");
                Assert.LessOrEqual(Mathf.Abs(poi.positionZ), halfZ, poi.id + " z outside terrain");
            }
        }

        [Test]
        public void Parse_Fails_Loudly_On_Garbage_And_Duplicates()
        {
            Assert.Throws<System.ArgumentException>(() => PoiSet.Parse(""));
            Assert.Throws<System.ArgumentException>(() => PoiSet.Parse("{}"));
            Assert.Throws<System.ArgumentException>(() => PoiSet.Parse(
                "{\"siteId\":\"x\",\"pois\":[{\"id\":\"a\"},{\"id\":\"a\"}]}"));
        }

        [Test]
        public void Every_WorldPackage_Has_Its_Poi_TextAsset_Wired()
        {
            foreach (var site in new[] { "mars-olympus", "mars-valles", "moon-shackleton" })
            {
                var pkg = AssetDatabase.LoadAssetAtPath<WorldPackage>("Assets/Wayfinder/Sites/" + site + ".asset");
                Assert.IsNotNull(pkg.PoiData, site + " WorldPackage has no POI data");
                var set = PoiSet.Parse(pkg.PoiData.text);
                Assert.AreEqual(site, set.siteId, site + " package wired to the wrong POI file");
            }
        }

        [Test]
        public void PoiSystem_Spawns_A_Marker_Per_Placed_Poi_On_The_Terrain()
        {
            var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Wayfinder/Terrain/mars-olympus.asset");
            var terrainGo = Terrain.CreateTerrainGameObject(terrainData);
            terrainGo.transform.position = new Vector3(-terrainData.size.x / 2f, 0f, -terrainData.size.z / 2f);
            var systemGo = new GameObject("poi-test");
            var head = new GameObject("head-test");
            try
            {
                var system = systemGo.AddComponent<PoiSystem>();
                var log = new FieldLog();
                system.Build(PoiSet.Parse(OlympusJson()), terrainGo.GetComponent<Terrain>(), log, head.transform);

                Assert.AreEqual(8, system.MarkerCount);
                Assert.AreEqual(0, system.RevealedCount, "nothing should be revealed at build time");
                Assert.AreEqual(0, log.Count, "field log must be untouched at build time");

                // Every marker sits on the terrain surface, not floating or buried.
                var terrain = terrainGo.GetComponent<Terrain>();
                foreach (Transform marker in systemGo.transform)
                {
                    float surface = terrain.SampleHeight(marker.position) + terrainGo.transform.position.y;
                    Assert.AreEqual(surface, marker.position.y, 0.5f, marker.name + " not on the surface");
                }
            }
            finally
            {
                Object.DestroyImmediate(systemGo);
                Object.DestroyImmediate(terrainGo);
                Object.DestroyImmediate(head);
            }
        }
    }
}
