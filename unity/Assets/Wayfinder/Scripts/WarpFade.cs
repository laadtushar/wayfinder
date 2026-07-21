using System.Collections;
using UnityEngine;

namespace Wayfinder.Unity
{
    /// The warp visual: a brief bright wash on a camera-locked quad.
    /// Deliberately a flat fade — never a forward-acceleration tunnel
    /// (comfort rule, DESIGN.md). The quad is assigned in the scene and
    /// covers the full FOV at its distance.
    public sealed class WarpFade : MonoBehaviour
    {
        [SerializeField] private Renderer fadeQuad;
        [Tooltip("Seconds for each half of the fade (up to full bright, then back down).")]
        [SerializeField] private float halfDurationSeconds = 0.45f;

        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock _block;

        public bool IsFullyBright { get; private set; }

        void Awake()
        {
            if (fadeQuad == null)
                throw new System.InvalidOperationException($"{name}: WarpFade has no fade quad assigned.");
            _block = new MaterialPropertyBlock();
            SetAlpha(0f);
            fadeQuad.gameObject.SetActive(false);
        }

        /// Fades to full bright, holds until the caller's work signals done
        /// (via the returned handle), then fades back out.
        public IEnumerator FadeAcross(System.Func<bool> workIsDone)
        {
            fadeQuad.gameObject.SetActive(true);
            for (float t = 0; t < halfDurationSeconds; t += Time.deltaTime)
            {
                SetAlpha(t / halfDurationSeconds);
                yield return null;
            }
            SetAlpha(1f);
            IsFullyBright = true;

            // An exception in the work callback must never strand the player at
            // full bright with a dead coroutine — treat it as "done", log, and
            // let the fade come back down (the caller's failure paths handle
            // state rollback).
            while (true)
            {
                bool done;
                try { done = workIsDone(); }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                    Debug.LogError("[WarpFade] work callback threw — completing the fade to avoid a full-bright lock.");
                    done = true;
                }
                if (done) break;
                yield return null;
            }

            IsFullyBright = false;
            for (float t = 0; t < halfDurationSeconds; t += Time.deltaTime)
            {
                SetAlpha(1f - t / halfDurationSeconds);
                yield return null;
            }
            SetAlpha(0f);
            fadeQuad.gameObject.SetActive(false);
        }

        void SetAlpha(float a)
        {
            fadeQuad.GetPropertyBlock(_block);
            _block.SetColor(ColorId, new Color(1f, 1f, 1f, a));
            fadeQuad.SetPropertyBlock(_block);
        }
    }
}
