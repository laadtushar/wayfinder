using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wayfinder.Unity.EditorTools
{
    /// Headless lightmap bake for the Bridge (#30). The MCP-bridge-driven
    /// Progressive bake bails silently; this runs the bake from a real
    /// batchmode editor via -executeMethod, where the lightmapper works.
    ///   Unity.exe -batchmode -quit -projectPath unity \
    ///     -executeMethod Wayfinder.Unity.EditorTools.BridgeBaker.BakeBridge -logFile bake.log
    public static class BridgeBaker
    {
        public static void BakeBridge()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Bridge.unity", OpenSceneMode.Single);

            // Enable the reflection probe BEFORE the bake so its cubemap is
            // captured (it was disabled in #22 to avoid a black empty cubemap;
            // a bake with it active gives it a real one). Include-inactive find.
            var probeGo = GameObject.Find("BridgeReflection");
            if (probeGo == null)
            {
                foreach (var p in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (p.gameObject.name == "BridgeReflection") { probeGo = p.gameObject; break; }
            }
            if (probeGo != null) { probeGo.SetActive(true); Debug.Log("[BridgeBaker] BridgeReflection enabled pre-bake"); }

            // Force the CPU lightmapper + directional + baked GI — the settings
            // the interactive bake wanted but that didn't persist over the bridge.
            var ls = Lightmapping.GetLightingSettingsForScene(scene);
            if (ls == null) { ls = new LightingSettings(); Lightmapping.SetLightingSettingsForScene(scene, ls); }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            ls.directionalityMode = LightmapsMode.CombinedDirectional;
            ls.lightmapResolution = 14f;
            ls.lightmapMaxSize = 1024;
            ls.ao = true;
            ls.aoMaxDistance = 0.6f;
            Lightmapping.SetLightingSettingsForScene(scene, ls);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Lightmapping.Clear();
            Debug.Log("[BridgeBaker] starting synchronous bake…");
            bool ok = Lightmapping.Bake(); // synchronous — fine in batchmode
            int maps = LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0;
            Debug.Log($"[BridgeBaker] Bake() returned {ok}; lightmaps={maps}");

            if (probeGo != null)
            {
                var probe = probeGo.GetComponent<ReflectionProbe>();
                if (probe != null) Debug.Log($"[BridgeBaker] BridgeReflection bakedTexture={(probe.bakedTexture != null)}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BridgeBaker] done — lightmaps={LightmapSettings.lightmaps.Length}");
        }
    }
}
