using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Wayfinder.Core;

namespace Wayfinder.Unity.Companion
{
    /// The bridge companion's world-space console. Builds a dark panel with a
    /// header, an answer body, and a column of context-aware prompt buttons
    /// (hand-ray + pinch via the canvas's TrackedDeviceGraphicRaycaster — no
    /// controllers). A prompt calls BridgeCompanion.AskAsync and shows the
    /// grounded answer, with a "thinking" state and single-flight guard.
    /// Built procedurally in Awake, matching DestinationMenu's world-space uGUI
    /// conventions (1 unit = 1 m, counter-scaled bitmap text, ColorBlock look).
    public sealed class CompanionPanel : MonoBehaviour
    {
        [SerializeField] private BridgeCompanion companion;

        [Tooltip("Metres from the player's eyes to this panel — drives Android XR minimum target sizing.")]
        [SerializeField] private float viewingDistanceMeters = 1.9f;

        // (label shown on the button, question sent to the companion)
        static readonly (string label, string question)[] Prompts =
        {
            ("Where am I?",        "where am I?"),
            ("What have I found?", "what have I found?"),
            ("What's out there?",  "what is still out there?"),
        };

        const float PanelW = 1.4f, PanelH = 0.9f;
        const float LabelScale = 0.002f;              // 500 rect-units per metre
        static readonly Color Ink = new Color(0.85f, 0.90f, 1f);

        Text _body;
        readonly System.Collections.Generic.List<Button> _buttons = new System.Collections.Generic.List<Button>();
        bool _busy;

        /// The answer currently shown (for tests / other UI).
        public string BodyText => _body != null ? _body.text : null;
        public System.Collections.Generic.IReadOnlyList<Button> Buttons => _buttons;

        void Awake()
        {
            if (companion == null)
                throw new System.InvalidOperationException($"{name}: CompanionPanel has no BridgeCompanion assigned.");
            Build();
        }

        void Build()
        {
            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.07f, 0.10f, 0.92f);
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(PanelW, PanelH);

            MakeText("Header", "COMPANION", new Vector2(0f, PanelH * 0.5f - 0.08f),
                new Vector2(PanelW - 0.1f, 0.12f), 0.32f, TextAnchor.MiddleCenter,
                new Color(0.45f, 0.62f, 0.95f), wrap: false);

            _body = MakeText("Body", "", new Vector2(0f, 0.08f),
                new Vector2(PanelW - 0.16f, 0.44f), 0.075f, TextAnchor.UpperLeft, Ink, wrap: true);

            float btnH = InteractionTargets.RecommendedSizeMeters(viewingDistanceMeters);
            float y = -PanelH * 0.5f + btnH * 0.5f + 0.03f;
            float step = btnH + 0.02f;
            // stack buttons upward from the bottom
            for (int i = 0; i < Prompts.Length; i++)
                _buttons.Add(MakeButton(Prompts[i].label, Prompts[i].question,
                    new Vector2(0f, y + (Prompts.Length - 1 - i) * step),
                    new Vector2(PanelW - 0.12f, btnH), btnH));
        }

        Text MakeText(string goName, string content, Vector2 anchoredMeters, Vector2 sizeMeters,
            float fontFracOfHeight, TextAnchor anchor, Color color, bool wrap)
        {
            var go = new GameObject(goName, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            // anchoredPosition is in the parent (canvas) space = metres, unaffected
            // by this rect's own localScale; only the sizeDelta is pre-divided.
            rt.anchoredPosition = anchoredMeters;
            rt.localScale = Vector3.one * LabelScale;
            rt.sizeDelta = sizeMeters / LabelScale;
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.alignment = anchor;
            t.color = color;
            t.fontSize = (int)(sizeMeters.y * fontFracOfHeight / LabelScale);
            // Set overflow modes BEFORE assigning text so the first layout wraps.
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = wrap ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            if (wrap)
            {
                // Best-fit auto-wraps AND auto-scales the font so answers of any
                // length (short bridge summary or long POI fact) fill the box
                // without overflowing — robust against the counter-scaled rect.
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 4;
                t.resizeTextMaxSize = t.fontSize;
            }
            t.text = content;
            return t;
        }

        Button MakeButton(string label, string question, Vector2 anchoredMeters, Vector2 sizeMeters, float heightMeters)
        {
            var go = new GameObject("Prompt_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredMeters;
            rt.sizeDelta = sizeMeters;
            go.GetComponent<Image>().color = Color.white;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var lrt = (RectTransform)labelGo.transform;
            lrt.SetParent(go.transform, false);
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = sizeMeters / LabelScale;
            lrt.localScale = Vector3.one * LabelScale;
            var t = labelGo.GetComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.fontSize = (int)(heightMeters * 0.5f / LabelScale);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.13f, 0.16f, 0.22f);
            colors.highlightedColor = new Color(0.20f, 0.28f, 0.40f);
            colors.pressedColor = new Color(0.22f, 0.45f, 0.80f);
            colors.selectedColor = colors.normalColor;
            btn.colors = colors;

            string q = question;
            btn.onClick.AddListener(() => { _ = RunPromptAsync(q); });
            return btn;
        }

        /// Ask the companion and render the answer. Public + awaitable so play-mode
        /// tests can drive it; the buttons call it fire-and-forget. Single-flight:
        /// ignores a new prompt while one is in flight.
        public async Task<string> RunPromptAsync(string question)
        {
            if (_busy) return null;
            _busy = true;
            SetButtonsInteractable(false);
            _body.text = "…";
            string answer;
            try
            {
                answer = await companion.AskAsync(question);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CompanionPanel] ask failed: {e.Message}");
                answer = "(the companion is unavailable right now)";
            }
            finally
            {
                _busy = false;
                SetButtonsInteractable(true);
            }
            if (_body != null) _body.text = answer;
            return answer;
        }

        void SetButtonsInteractable(bool on)
        {
            foreach (var b in _buttons) if (b != null) b.interactable = on;
        }
    }
}
