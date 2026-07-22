using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// Drives the bridge â†’ warp â†’ surface â†’ return loop (ARCHITECTURE.md
    /// section 4). All travel LOGIC lives in the engine-free
    /// TravelStateMachine â€” this component only performs the Unity work each
    /// transition implies: the warp fade, the additive scene load, and
    /// showing/hiding the Bridge interior.
    public sealed class TravelManager : MonoBehaviour
    {
        [SerializeField] private WorldCatalog catalog;
        [SerializeField] private DestinationMenu menu;
        [SerializeField] private WarpFade warpFade;
        [Tooltip("Everything that is the Bridge interior â€” hidden while on a surface. The XR rig must NOT be under this root.")]
        [SerializeField] private GameObject bridgeVisualsRoot;
        [SerializeField] private global::Unity.XR.CoreUtils.XROrigin xrOrigin;
        [SerializeField] private SuitWardrobe wardrobe;
        [SerializeField] private WorldGrabController worldGrab;
        [SerializeField] private FieldLogPanel fieldLogPanel;
        [SerializeField] private SuitAudio suitAudio;
        [Tooltip("The rig's locomotion root â€” disabled during warp transitions so a queued teleport/turn can never apply across the rig reset.")]
        [SerializeField] private GameObject locomotionRoot;

        TravelStateMachine _machine;
        WorldRegistry _registry;
        readonly FieldLog _fieldLog = new FieldLog();
        string _loadedSceneName;
        GameObject _returnUi;
        PoiSystem _poiSystem;

        /// The player's discovery log â€” lives with the TravelManager for the
        /// session (persistence is a later ticket).
        public FieldLog FieldLog => _fieldLog;

        public TravelState State => _machine.State;

        void Awake()
        {
            if (catalog == null) throw new System.InvalidOperationException($"{name}: no WorldCatalog assigned.");
            if (menu == null) throw new System.InvalidOperationException($"{name}: no DestinationMenu assigned.");
            if (warpFade == null) throw new System.InvalidOperationException($"{name}: no WarpFade assigned.");
            if (bridgeVisualsRoot == null) throw new System.InvalidOperationException($"{name}: no bridge visuals root assigned.");
            if (xrOrigin == null) throw new System.InvalidOperationException($"{name}: no XROrigin assigned.");
            if (locomotionRoot == null) throw new System.InvalidOperationException($"{name}: no locomotion root assigned.");
            if (wardrobe == null) throw new System.InvalidOperationException($"{name}: no SuitWardrobe assigned.");
            if (worldGrab == null) throw new System.InvalidOperationException($"{name}: no WorldGrabController assigned.");

            _machine = new TravelStateMachine();
            _registry = catalog.BuildRegistry();
        }

        void Start()
        {
            // fieldLogPanel is optional — the Bridge shows it, but the loop
            // works without it. Painted in Start (not Awake) so the panel's own
            // Awake has definitely built its totals first.
            if (fieldLogPanel != null) fieldLogPanel.Refresh(_fieldLog);
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
            if (world == null)
            {
                // Unknown id: roll back before any visuals change.
                _machine.AbortWarp();
                Debug.LogError($"[TravelManager] '{worldId}' is not in the registry. Warp aborted.");
                yield break;
            }

            locomotionRoot.SetActive(false);
            // Fall silent under the bright wash — the warp is a quiet beat, then
            // the destination's beds fade up at arrival.
            if (suitAudio != null) suitAudio.Apply(TravelState.WarpingToSurface, false, null);
            var load = new AsyncOperation[1];
            bool loadFailed = false;

            yield return StartCoroutine(warpFade.FadeAcross(() =>
            {
                if (loadFailed) return true;
                if (load[0] == null)
                {
                    bridgeVisualsRoot.SetActive(false);
                    load[0] = SceneManager.LoadSceneAsync(world.SceneName, LoadSceneMode.Additive);
                    if (load[0] == null)
                    {
                        // Restore the bridge while still at full bright so the
                        // fade-down reveals a sane place, not empty void.
                        bridgeVisualsRoot.SetActive(true);
                        loadFailed = true;
                        return true;
                    }
                }
                if (!load[0].isDone) return false;
                // Site RenderSettings (sky/ambient â€” World Package data) only
                // apply while the site scene is the active scene. Spawn the
                // return UI inside the covered period too, so its construction
                // cost never lands on a visible frame.
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(world.SceneName));
                // Surfaces render their sky (stars / Mars gradient); the sealed
                // Bridge clears to black.
                xrOrigin.Camera.clearFlags = CameraClearFlags.Skybox;
                // INVARIANT: atmospherics apply for EVERY world, unconditionally
                // — shader globals persist across scene unloads, so a world
                // that skipped this would inherit the previous world's haze.
                var arrivedPackage = FindPackage(worldId);
                if (arrivedPackage != null)
                {
                    WorldAtmospherics.Apply(arrivedPackage);
                    if (suitAudio != null)
                        suitAudio.Apply(TravelState.OnSurface, arrivedPackage.HazeEnabled, arrivedPackage.SurfaceAmbience);
                }
                else
                {
                    // Never silently keep the previous world's haze.
                    Debug.LogError($"[TravelManager] no WorldPackage for '{worldId}' — atmospherics fall back to vacuum.");
                    WorldAtmospherics.ApplyVacuum();
                    if (suitAudio != null) suitAudio.Apply(TravelState.OnSurface, false, null);
                }
                ApplySpawnOffset(worldId);
                wardrobe.Apply(TravelState.OnSurface);
                worldGrab.SetEnabled(TravelState.OnSurface);
                SpawnReturnUi();
                SpawnPoiSystem(worldId);
                return true;
            }));

            locomotionRoot.SetActive(true);
            if (loadFailed)
            {
                _machine.AbortWarp();
                Debug.LogError($"[TravelManager] Scene '{world.SceneName}' failed to load â€” is it in Build Settings? Warp aborted.");
                yield break;
            }

            _loadedSceneName = world.SceneName;
            _machine.CompleteWarp();
        }

        IEnumerator WarpToBridge()
        {
            locomotionRoot.SetActive(false);
            if (suitAudio != null) suitAudio.Apply(TravelState.WarpingToBridge, false, null);
            var unload = new AsyncOperation[1];
            bool unloadFailed = false;

            yield return StartCoroutine(warpFade.FadeAcross(() =>
            {
                if (unloadFailed) return true;
                if (unload[0] == null)
                {
                    if (_returnUi != null) Destroy(_returnUi);
                    if (_poiSystem != null) { Destroy(_poiSystem.gameObject); _poiSystem = null; }
                    SceneManager.SetActiveScene(gameObject.scene);
                    unload[0] = SceneManager.UnloadSceneAsync(_loadedSceneName);
                    if (unload[0] == null) { unloadFailed = true; return true; }
                }
                if (!unload[0].isDone) return false;
                // The rig keeps its surface coordinates otherwise â€” the player
                // would materialize kilometres from the Bridge geometry.
                xrOrigin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                // Swap gloves at full bright, behind the wash â€” never a visible
                // material pop on the player's own hands.
                wardrobe.Apply(TravelState.OnBridge);
                worldGrab.SetEnabled(TravelState.OnBridge);
                if (suitAudio != null) suitAudio.Apply(TravelState.OnBridge, false, null);
                bridgeVisualsRoot.SetActive(true);
                return true;
            }));

            locomotionRoot.SetActive(true);
            if (unloadFailed)
            {
                // Still on the surface: keep the scene handle, put the return
                // button back, roll the machine back to OnSurface.
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(_loadedSceneName));
                SpawnReturnUi();
                _machine.AbortWarp();
                Debug.LogError($"[TravelManager] Unload of '{_loadedSceneName}' failed. Return aborted; still on surface.");
                yield break;
            }

            xrOrigin.Camera.clearFlags = CameraClearFlags.SolidColor;
            bridgeVisualsRoot.SetActive(true);
            if (fieldLogPanel != null) fieldLogPanel.Refresh(_fieldLog);
            _loadedSceneName = null;
            _machine.CompleteReturn();
        }

        void ApplySpawnOffset(string worldId)
        {
            var package = FindPackage(worldId);
            if (package == null || package.SpawnOffset == Vector2.zero) return;
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            var pos = new Vector3(package.SpawnOffset.x, 0f, package.SpawnOffset.y);
            if (terrain != null)
                pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            xrOrigin.transform.position = pos;
        }

        void SpawnPoiSystem(string worldId)
        {
            var package = FindPackage(worldId);
            if (package == null || package.PoiData == null)
            {
                Debug.LogWarning($"[TravelManager] no POI data on WorldPackage '{worldId}' â€” site has no discoveries.");
                return;
            }
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogWarning($"[TravelManager] no terrain in '{worldId}' â€” POI markers skipped.");
                return;
            }
            // POI failures must never brick travel â€” this runs inside the warp
            // fade callback, where an uncaught exception freezes the player at
            // full bright forever.
            try
            {
                var go = new GameObject("PoiSystem");
                _poiSystem = go.AddComponent<PoiSystem>();
                _poiSystem.Build(PoiSet.Parse(package.PoiData.text), terrain, _fieldLog, xrOrigin.Camera.transform);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TravelManager] POI system failed for '{worldId}' â€” travelling on without discoveries. {e.Message}");
                if (_poiSystem != null) { Destroy(_poiSystem.gameObject); _poiSystem = null; }
            }
        }

        WorldPackage FindPackage(string worldId)
        {
            // Id, not ToDefinition().Id: a half-authored package in the
            // catalog must not throw while scanning for a DIFFERENT world.
            foreach (var package in catalog.Packages)
                if (package != null && package.Id == worldId) return package;
            return null;
        }

        void SpawnReturnUi()
        {
            _returnUi = new GameObject("ReturnToBridge",
                typeof(Canvas), typeof(UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster));
            var canvas = _returnUi.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)_returnUi.transform;
            // Parented to the rig so it travels with every teleport â€” the way
            // home must never be left behind on the far side of the site.
            _returnUi.transform.SetParent(xrOrigin.transform, true);
            // Anchored to the player's HEAD (not the rig base): the rig root
            // doesn't move when the player walks the play space physically.
            // Never world-absolute coordinates (worlds-as-data rule).
            var head = xrOrigin.Camera.transform;
            Vector3 basePos = new Vector3(head.position.x, head.position.y - 0.4f, head.position.z);
            Quaternion baseYaw = Quaternion.Euler(0, head.eulerAngles.y, 0);
            Vector3 localOffset = new Vector3(0.6f, 0f, 1.3f);
            Vector3 pos = basePos + baseYaw * localOffset;
            // On sloped terrain the offset can bury the button â€” lift it above
            // any ground beneath it.
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var hit, 6f))
                pos.y = Mathf.Max(pos.y, hit.point.y + 0.8f);
            rect.position = pos;
            rect.rotation = baseYaw * Quaternion.LookRotation(new Vector3(localOffset.x, 0, localOffset.z));
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
