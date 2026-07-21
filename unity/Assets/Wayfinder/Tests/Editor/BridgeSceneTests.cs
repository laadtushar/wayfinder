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
        public void No_Smooth_Locomotion_Exists_Anywhere_In_The_Bridge_Scene()
        {
            // Comfort rules (CLAUDE.md): teleport / snap turn only. A smooth
            // move or continuous turn provider must never ship.
            var scene = OpenBridge();
            foreach (var root in scene.GetRootGameObjects())
            {
                Assert.AreEqual(0,
                    root.GetComponentsInChildren<global::UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>(true).Length,
                    "ContinuousMoveProvider found under " + root.name);
                Assert.AreEqual(0,
                    root.GetComponentsInChildren<global::UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.ContinuousTurnProvider>(true).Length,
                    "ContinuousTurnProvider found under " + root.name);
            }
        }

        [Test]
        public void Bridge_Has_Teleport_And_SnapTurn_Providers()
        {
            var scene = OpenBridge();
            global::UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider teleport = null;
            global::UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider snap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (teleport == null) teleport = root.GetComponentInChildren<global::UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>(true);
                if (snap == null) snap = root.GetComponentInChildren<global::UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider>(true);
            }
            Assert.IsNotNull(teleport, "no TeleportationProvider in Bridge scene");
            Assert.IsNotNull(snap, "no SnapTurnProvider in Bridge scene");
            Assert.AreEqual(45f, snap.turnAmount, 0.1f, "snap turn amount should be 45 degrees");
        }

        [Test]
        public void Every_Controller_Manager_Has_A_Teleport_Interactor_Wired()
        {
            // A pinch/thumbstick teleport with no interactor behind it fails
            // silently — this pins the player-usable input path.
            var scene = OpenBridge();
            int managers = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var comp in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (comp == null || comp.GetType().Name != "ControllerInputActionManager") continue;
                    managers++;
                    var so = new SerializedObject(comp);
                    var ti = so.FindProperty("m_TeleportInteractor");
                    Assert.IsNotNull(ti, "manager has no teleport interactor property");
                    Assert.IsNotNull(ti.objectReferenceValue,
                        comp.gameObject.name + " has a null teleport interactor");
                    var sm = so.FindProperty("m_SmoothMotionEnabled");
                    if (sm != null) Assert.IsFalse(sm.boolValue, comp.gameObject.name + " smooth motion flag on");
                    var st = so.FindProperty("m_SmoothTurnEnabled");
                    if (st != null) Assert.IsFalse(st.boolValue, comp.gameObject.name + " smooth turn flag on");
                }
            Assert.GreaterOrEqual(managers, 2, "expected controller managers for both hands");
        }

        [Test]
        public void Modality_Manager_Serves_Hands_And_Controllers()
        {
            // Hands are the platform default input (CLAUDE.md) — null hand
            // slots would deactivate every interactor the moment hand
            // tracking acquires.
            var scene = OpenBridge();
            global::UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager modality = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                modality = root.GetComponentInChildren<global::UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager>(true);
                if (modality != null) break;
            }
            Assert.IsNotNull(modality, "no XRInputModalityManager");
            Assert.IsNotNull(modality.leftHand, "left hand tree missing");
            Assert.IsNotNull(modality.rightHand, "right hand tree missing");
            Assert.IsNotNull(modality.leftController, "left controller tree missing");
            Assert.IsNotNull(modality.rightController, "right controller tree missing");
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
