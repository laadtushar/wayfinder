using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wayfinder.Unity.Tests
{
    /// Contract tests for the persistent Bridge scene (build-plan Task 1.3).
    /// These assert the scene's structural promises — boot slot, XR rig,
    /// comfort layout — not its look; visual/frame verification is on-device.
    public class BridgeSceneTests
    {
        const string ScenePath = "Assets/Scenes/Bridge.unity";

        SceneSetup[] _savedSetup;

        [OneTimeSetUp]
        public void SaveEditorScenes()
        {
            _savedSetup = EditorSceneManager.GetSceneManagerSetup();
        }

        [OneTimeTearDown]
        public void RestoreEditorScenes()
        {
            if (_savedSetup != null && _savedSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(_savedSetup);
        }

        [Test]
        public void Bridge_Is_The_First_Scene_In_Build_Settings()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Length > 0, "no scenes in build settings");
            Assert.AreEqual(ScenePath, EditorBuildSettings.scenes[0].path);
            Assert.IsTrue(EditorBuildSettings.scenes[0].enabled);
        }

        [Test]
        public void Bridge_Scene_Has_An_XR_Origin_With_A_Tracked_Camera()
        {
            var scene = OpenBridge();
            var origin = FindInScene<global::Unity.XR.CoreUtils.XROrigin>(scene);
            Assert.IsNotNull(origin, "no XROrigin in Bridge scene");
            Assert.IsNotNull(origin.Camera, "XROrigin has no camera wired");
        }

        [Test]
        public void Bridge_Playables_Sit_Within_The_Two_Metre_Comfort_Radius()
        {
            var scene = OpenBridge();
            var origin = FindInScene<global::Unity.XR.CoreUtils.XROrigin>(scene);
            var viewscreen = GameObject.Find("Viewscreen");
            Assert.IsNotNull(viewscreen, "no Viewscreen object");

            // The viewscreen is gaze content, but its interaction surface must be
            // reachable-adjacent: centre within 2.0 m horizontal of the origin.
            Vector3 delta = viewscreen.transform.position - origin.transform.position;
            delta.y = 0;
            Assert.LessOrEqual(delta.magnitude, 2.0f + 0.001f,
                "Viewscreen centre outside the 2.0 m comfort radius");

            // The console is the future warp-lever surface — the thing the
            // comfort rule actually governs.
            var console = GameObject.Find("Console");
            Assert.IsNotNull(console, "no Console object");
            Vector3 consoleDelta = console.transform.position - origin.transform.position;
            consoleDelta.y = 0;
            Assert.LessOrEqual(consoleDelta.magnitude, 2.0f + 0.001f,
                "Console outside the 2.0 m comfort radius");
        }

        [Test]
        public void Bridge_Has_A_Flat_Floor_At_Ground_Level_Under_The_Player()
        {
            var scene = OpenBridge();
            var floor = GameObject.Find("BridgeFloor");
            Assert.IsNotNull(floor, "no BridgeFloor object");

            // The collider surface the teleport/interaction systems will see
            // must be flat and at y≈0 — a scaled capsule would dome upward.
            var collider = floor.GetComponent<Collider>();
            Assert.IsNotNull(collider, "floor has no collider");
            Assert.IsFalse(collider is CapsuleCollider,
                "floor collider is a capsule — scales into a dome, not a plane");
            float topAtCentre = collider.bounds.max.y;
            Assert.LessOrEqual(Mathf.Abs(topAtCentre), 0.05f,
                $"floor collider top at y={topAtCentre}, expected ~0");
        }

        static Scene OpenBridge()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return scene;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
