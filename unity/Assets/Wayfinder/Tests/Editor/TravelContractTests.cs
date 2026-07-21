using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wayfinder.Unity;

namespace Wayfinder.Unity.Tests
{
    /// Contract tests binding the travel loop's data together: every world the
    /// catalog offers must have a loadable scene in the build, and the Bridge
    /// scene must carry a fully-wired TravelManager. The loop's runtime
    /// behavior (state machine) is covered engine-free in com.wayfinder.core;
    /// the on-device double-loop is the human gate.
    public class TravelContractTests
    {
        [Test]
        public void Every_Catalog_World_Has_Its_Scene_In_Build_Settings()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldCatalog>(
                "Assets/Wayfinder/Sites/WorldCatalog.asset");
            Assert.IsNotNull(catalog, "WorldCatalog.asset not found at expected path");
            var registry = catalog.BuildRegistry();

            foreach (var world in registry.All)
            {
                bool found = false;
                foreach (var scene in EditorBuildSettings.scenes)
                {
                    if (!scene.enabled) continue;
                    var name = System.IO.Path.GetFileNameWithoutExtension(scene.path);
                    if (name == world.SceneName) { found = true; break; }
                }
                Assert.IsTrue(found,
                    $"world '{world.Id}' warps to scene '{world.SceneName}' which is not an enabled build scene");
            }
        }

        [Test]
        public void TravelManager_Without_Refs_Fails_Loudly()
        {
            var go = new GameObject("tm-test");
            go.SetActive(false); // keep Awake from running on AddComponent
            var tm = go.AddComponent<TravelManager>();
            try
            {
                // SendMessage no-ops on inactive objects — invoke Awake directly.
                var awake = typeof(TravelManager).GetMethod("Awake",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
                    () => awake.Invoke(tm, null));
                Assert.IsInstanceOf<System.InvalidOperationException>(ex.InnerException);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
