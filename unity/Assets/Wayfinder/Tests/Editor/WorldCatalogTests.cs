using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wayfinder.Core;
using Wayfinder.Unity;

namespace Wayfinder.Unity.Tests
{
    /// EditMode tests for the Unity-side world data wrappers. Engine-touching
    /// on purpose (ScriptableObjects, AssetDatabase) — the pure logic tests
    /// live in com.wayfinder.core and stay engine-free.
    public class WorldCatalogTests
    {
        const string CatalogPath = "Assets/Wayfinder/Sites/WorldCatalog.asset";

        [Test]
        public void Catalog_Asset_Exists_And_Builds_Registry_With_The_Shipped_Sites()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "WorldCatalog.asset missing at " + CatalogPath);

            WorldRegistry registry = catalog.BuildRegistry();

            // Five worlds shipped (the fifth, Jezero, again proves worlds-as-data).
            Assert.AreEqual(5, registry.All.Count);
            Assert.IsNotNull(registry.GetById("mars-olympus"));
            Assert.IsNotNull(registry.GetById("mars-valles"));
            Assert.IsNotNull(registry.GetById("moon-shackleton"));
            Assert.IsNotNull(registry.GetById("moon-tranquillity"));
            Assert.IsNotNull(registry.GetById("mars-jezero"));
        }

        [Test]
        public void Catalog_Preserves_Package_Order_For_The_Viewscreen_List()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldCatalog>(CatalogPath);
            var all = catalog.BuildRegistry().All;

            Assert.AreEqual("mars-olympus", all[0].Id);
            Assert.AreEqual("mars-valles", all[1].Id);
            Assert.AreEqual("moon-shackleton", all[2].Id);
        }

        [Test]
        public void Site_Assets_Carry_Real_Surface_Gravity_Not_Placeholders()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldCatalog>(CatalogPath);
            var registry = catalog.BuildRegistry();

            Assert.AreEqual(3.72f, registry.GetById("mars-olympus").SurfaceGravity, 0.001f);
            Assert.AreEqual(3.72f, registry.GetById("mars-valles").SurfaceGravity, 0.001f);
            Assert.AreEqual(1.62f, registry.GetById("moon-shackleton").SurfaceGravity, 0.001f);
            Assert.AreEqual(1.62f, registry.GetById("moon-tranquillity").SurfaceGravity, 0.001f);
            Assert.AreEqual(3.72f, registry.GetById("mars-jezero").SurfaceGravity, 0.001f);
        }

        [Test]
        public void WorldPackage_With_Empty_Id_Fails_Loudly_On_ToDefinition()
        {
            var package = ScriptableObject.CreateInstance<WorldPackage>();
            try
            {
                Assert.Throws<System.ArgumentException>(() => package.ToDefinition());
            }
            finally
            {
                Object.DestroyImmediate(package);
            }
        }

        [Test]
        public void Catalog_With_Duplicate_World_Ids_Fails_Loudly()
        {
            var a = MakePackage("same-id", "A", "Site_A");
            var b = MakePackage("same-id", "B", "Site_B");
            var catalog = ScriptableObject.CreateInstance<WorldCatalog>();
            var so = new SerializedObject(catalog);
            var list = so.FindProperty("packages");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = a;
            list.GetArrayElementAtIndex(1).objectReferenceValue = b;
            so.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => catalog.BuildRegistry());
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Package_Without_Scene_Name_Fails_Loudly()
        {
            var package = MakePackage("has-id", "Named", "");
            try
            {
                Assert.Throws<System.ArgumentException>(() => package.ToDefinition());
            }
            finally
            {
                Object.DestroyImmediate(package);
            }
        }

        static WorldPackage MakePackage(string id, string displayName, string sceneName)
        {
            var pkg = ScriptableObject.CreateInstance<WorldPackage>();
            var so = new SerializedObject(pkg);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("sceneName").stringValue = sceneName;
            so.FindProperty("surfaceGravity").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pkg;
        }

        [Test]
        public void Every_Site_Package_Declares_A_Scene_Name()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldCatalog>(CatalogPath);
            foreach (var def in catalog.BuildRegistry().All)
                Assert.IsFalse(string.IsNullOrEmpty(def.SceneName), def.Id + " has no scene name");
        }
    }
}
