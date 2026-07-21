using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// Drives the bridge → warp → surface → return loop (ARCHITECTURE.md
    /// section 4). All travel LOGIC lives in the engine-free
    /// TravelStateMachine — this component only performs the Unity work each
    /// transition implies: the warp fade, the additive scene load, and
    /// showing/hiding the Bridge interior.
    public sealed class TravelManager : MonoBehaviour
    {
        [SerializeField] private WorldCatalog catalog;
        [SerializeField] private DestinationMenu menu;
        [SerializeField] private WarpFade warpFade;
        [Tooltip("Everything that is the Bridge interior — hidden while on a surface. The XR rig must NOT be under this root.")]
        [SerializeField] private GameObject bridgeVisualsRoot;

        TravelStateMachine _machine;
        WorldRegistry _registry;
        string _loadedSceneName;
        GameObject _returnUi;

        public TravelState State => _machine.State;

        void Awake()
        {
            if (catalog == null) throw new System.InvalidOperationException($"{name}: no WorldCatalog assigned.");
            if (menu == null) throw new System.InvalidOperationException($"{name}: no DestinationMenu assigned.");
            if (warpFade == null) throw new System.InvalidOperationException($"{name}: no WarpFade assigned.");
            if (bridgeVisualsRoot == null) throw new System.InvalidOperationException($"{name}: no bridge visuals root assigned.");

            _machine = new TravelStateMachine();
            _registry = catalog.BuildRegistry();
        }

        void OnEnable()
        {
            menu.WorldSelected += OnWorldSelected;
        }

        void OnDisable()
        {
            menu.WorldSelected -= OnWorldSelected;
        }

        void OnWorldSelected(string worldId)
        {
            // The state machine is the sole gate: double-warp attempts are
            // rejected here and nothing visual happens.
            if (!_machine.TryBeginWarp(worldId))
                return;
            StartCoroutine(WarpToSurface(worldId));
        }

        public void ReturnToBridge()
        {
            if (!_machine.TryBeginReturn())
                return;
            StartCoroutine(WarpToBridge());
        }

        IEnumerator WarpToSurface(string worldId)
        {
            var world = _registry.GetById(worldId);
            var load = new AsyncOperation[1];
            bool loadFailed = false;

            yield return StartCoroutine(warpFade.FadeAcross(() =>
            {
                if (loadFailed) return true;
                if (load[0] == null)
                {
                    bridgeVisualsRoot.SetActive(false);
                    load[0] = SceneManager.LoadSceneAsync(world.SceneName, LoadSceneMode.Additive);
                    if (load[0] == null) { loadFailed = true; return true; }
                }
                if (!load[0].isDone) return false;
                // Site RenderSettings (sky/ambient — World Package data) only
                // apply while the site scene is the active scene.
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(world.SceneName));
                return true;
            }));

            if (loadFailed)
            {
                // Fail loudly but leave the player somewhere sane: back on the
                // Bridge, fade cleared. The machine stays in WarpingToSurface on
                // purpose — travel is broken and must look broken in the logs.
                // (Follow-up noted on the tracker: TravelStateMachine needs an
                // abort transition to recover cleanly.)
                bridgeVisualsRoot.SetActive(true);
                Debug.LogError($"[TravelManager] Scene '{world.SceneName}' failed to load — is it in Build Settings? Travel is halted.");
                yield break;
            }

            _loadedSceneName = world.SceneName;
            _machine.CompleteWarp();
            SpawnReturnUi();
        }

        IEnumerator WarpToBridge()
        {
            var unload = new AsyncOperation[1];

            yield return StartCoroutine(warpFade.FadeAcross(() =>
            {
                if (unload[0] == null)
                {
                    if (_returnUi != null) Destroy(_returnUi);
                    SceneManager.SetActiveScene(gameObject.scene);
                    unload[0] = SceneManager.UnloadSceneAsync(_loadedSceneName);
                    if (unload[0] == null) return true;
                }
                if (!unload[0].isDone) return false;
                bridgeVisualsRoot.SetActive(true);
                return true;
            }));

            bridgeVisualsRoot.SetActive(true);
            _loadedSceneName = null;
            _machine.CompleteReturn();
        }

        void SpawnReturnUi()
        {
            _returnUi = new GameObject("ReturnToBridge",
                typeof(Canvas), typeof(UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster));
            var canvas = _returnUi.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)_returnUi.transform;
            rect.position = new Vector3(0.9f, 1.2f, 1.4f);
            rect.rotation = Quaternion.LookRotation(new Vector3(0.9f, 0, 1.4f));
            rect.sizeDelta = new Vector2(0.7f, 0.25f);
            rect.localScale = Vector3.one;

            var buttonGo = new GameObject("ReturnButton",
                typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(_returnUi.transform, false);
            var buttonRect = (RectTransform)buttonGo.transform;
            float height = Mathf.Max(0.18f, InteractionTargets.RecommendedSizeMeters(1.7f));
            buttonRect.sizeDelta = new Vector2(0.65f, height);
            buttonGo.GetComponent<Image>().color = Color.white;
            var button = buttonGo.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.13f, 0.16f, 0.20f);
            colors.pressedColor = new Color(0.22f, 0.45f, 0.80f);
            button.colors = colors;
            button.onClick.AddListener(ReturnToBridge);

            const float labelScale = 0.002f;
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(0.65f / labelScale, height / labelScale);
            labelRect.localScale = Vector3.one * labelScale;
            var label = labelGo.GetComponent<Text>();
            label.text = "Return to Bridge";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = (int)(height * 0.45f / labelScale);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
