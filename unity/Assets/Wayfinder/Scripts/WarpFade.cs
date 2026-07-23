using System.Collections;
using UnityEngine;

namespace Wayfinder.Unity
{
    /// The warp visual: a brief bright wash on a camera-locked quad. A fast
    /// eased flash to full bright (tinted with warp energy), a short hold that
    /// also spans the additive scene load, then a slower graceful resolve as the
    /// destination fades up out of the light. Deliberately a FLAT full-screen
    /// fade — never a forward-acceleration tunnel, star-streaks, or any camera
    /// rotation (comfort rule, DESIGN.md/CLAUDE.md): all the drama is in the
    /// brightness curve and colour, none of it in motion, so there is zero
    /// vection. Full coverage at peak hides the scene swap; the minimum hold
    /// guarantees the flash always reads even when the load is instant.
    public sealed class WarpFade : MonoBehaviour
    {
        [SerializeField] private Renderer fadeQuad;
        [Header("Timing (seconds)")]
        [Tooltip("Fast flash up to full bright.")]
        [SerializeField] private float flashUpSeconds = 0.30f;
        [Tooltip("Minimum time held at full bright, even if the load finished instantly.")]
        [SerializeField] private float minHoldSeconds = 0.16f;
        [Tooltip("Slow graceful resolve as the world fades up out of the light.")]
        [SerializeField] private float resolveDownSeconds = 0.75f;
        [Header("Colour")]
        [Tooltip("Tint at the start of the flash — the ship's warp energy.")]
        [SerializeField] private Color flashColor = new Color(0.70f, 0.85f, 1.00f);
        [Tooltip("Tint at peak / during the hold — a clean bright wash.")]
        [SerializeField] private Color peakColor = new Color(1.00f, 1.00f, 1.00f);

        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _block;

        public bool IsFullyBright { get; private set; }

        void Awake()
        {
            if (fadeQuad == null)
                throw new System.InvalidOperationException($"{name}: WarpFade has no fade quad assigned.");
            _block = new MaterialPropertyBlock();
            SetColor(peakColor, 0f);
            fadeQuad.gameObject.SetActive(false);
        }

        /// Flash to full bright, hold (at least minHold, and until the caller's
        /// work signals done), then resolve back out.
        public IEnumerator FadeAcross(System.Func<bool> workIsDone)
        {
            fadeQuad.gameObject.SetActive(true);

            // Fast eased flash up (ease-out cubic: quick rise, gentle settle),
            // colour warping from energy-blue toward the clean peak wash.
            for (float t = 0; t < flashUpSeconds; t += Time.deltaTime)
            {
                float x = Mathf.Clamp01(t / flashUpSeconds);
                float a = 1f - Mathf.Pow(1f - x, 3f);          // easeOutCubic
                SetColor(Color.Lerp(flashColor, peakColor, x), a);
                yield return null;
            }
            SetColor(peakColor, 1f);
            IsFullyBright = true;

            // Hold at full bright for at least minHold AND until the work (the
            // additive scene load) is done — so the world is ready as the light
            // clears (no black gap, no half-loaded pop). An exception in the
            // callback must never strand the player at full bright.
            float held = 0f;
            while (true)
            {
                bool done = held >= minHoldSeconds;
                if (done)
                {
                    try { done = workIsDone(); }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                        Debug.LogError("[WarpFade] work callback threw — completing the fade to avoid a full-bright lock.");
                        done = true;
                    }
                }
                if (done) break;
                held += Time.deltaTime;
                yield return null;
            }

            IsFullyBright = false;
            // Slow graceful resolve (ease-in-out sine) — the world blooms up.
            for (float t = 0; t < resolveDownSeconds; t += Time.deltaTime)
            {
                float x = Mathf.Clamp01(t / resolveDownSeconds);
                float a = 1f - (-(Mathf.Cos(Mathf.PI * x) - 1f) / 2f);  // 1 - easeInOutSine
                SetColor(peakColor, a);
                yield return null;
            }
            SetColor(peakColor, 0f);
            fadeQuad.gameObject.SetActive(false);
        }

        void SetColor(Color rgb, float a)
        {
            fadeQuad.GetPropertyBlock(_block);
            _block.SetColor(ColorId, new Color(rgb.r, rgb.g, rgb.b, a));
            fadeQuad.SetPropertyBlock(_block);
        }
    }
}
