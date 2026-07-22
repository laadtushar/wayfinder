using UnityEngine;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// Applies the engine-free SuitAudioState mix to AudioSources on the rig:
    /// surface wind (per world), suit breathing, comms bed, bridge hum, and
    /// footsteps triggered by how far the player actually travels. Driven by
    /// TravelManager at each transition (Apply) and self-updates footsteps.
    public sealed class SuitAudio : MonoBehaviour
    {
        [Header("Looping beds")]
        [SerializeField] private AudioSource ambienceSource; // per-world wind (clip set at warp)
        [SerializeField] private AudioSource breathSource;
        [SerializeField] private AudioSource radioSource;
        [SerializeField] private AudioSource humSource;
        [SerializeField] private AudioSource bootSource;      // one-shot footsteps

        [Header("Default clips")]
        [SerializeField] private AudioClip breathClip;
        [SerializeField] private AudioClip radioClip;
        [SerializeField] private AudioClip humClip;
        [SerializeField] private AudioClip bootClip;

        [Header("Footstep cadence")]
        [Tooltip("Metres of rig travel per footstep.")]
        [SerializeField] private float strideLength = 0.75f;
        [Tooltip("Rig transform whose XZ travel drives footsteps (the XR Origin).")]
        [SerializeField] private Transform rig;

        SuitAudioMix _mix;
        bool _onSurface;
        float _strideAccum;
        Vector3 _lastPos;
        float _speed01;

        void Awake()
        {
            RequireSource(ambienceSource, nameof(ambienceSource));
            RequireSource(breathSource, nameof(breathSource));
            RequireSource(radioSource, nameof(radioSource));
            RequireSource(humSource, nameof(humSource));
            RequireSource(bootSource, nameof(bootSource));
            if (rig == null) throw new System.InvalidOperationException($"{name}: no rig transform assigned.");

            breathSource.clip = breathClip; breathSource.loop = true;
            radioSource.clip = radioClip; radioSource.loop = true;
            humSource.clip = humClip; humSource.loop = true;
            ambienceSource.loop = true;
            bootSource.loop = false;

            _lastPos = rig.position;
            // Start on the Bridge.
            Apply(TravelState.OnBridge, worldHasAtmosphere: false, worldAmbience: null);
        }

        void RequireSource(AudioSource s, string field)
        {
            if (s == null) throw new System.InvalidOperationException($"{name}: {field} not assigned.");
        }

        /// Set the mix for the state we are arriving in. `worldAmbience` is the
        /// per-world wind clip (null on airless worlds / the Bridge).
        public void Apply(TravelState state, bool worldHasAtmosphere, AudioClip worldAmbience)
        {
            _mix = SuitAudioState.Resolve(state, worldHasAtmosphere);
            _onSurface = state == TravelState.OnSurface;

            // Ambience: the world clip IS the gate — a null clip (airless world,
            // or an atmospheric world that forgot to author wind) plays nothing,
            // never the previous world's retained wind.
            ambienceSource.clip = worldAmbience;
            SetLoop(ambienceSource, _mix.Ambience * (worldAmbience != null ? 1f : 0f));
            SetLoop(breathSource, _mix.Breathing);
            SetLoop(radioSource, _mix.Radio);
            SetLoop(humSource, _mix.BridgeHum);

            _strideAccum = 0f;
            _lastPos = rig.position;
        }

        static void SetLoop(AudioSource s, float volume)
        {
            s.volume = volume;
            if (volume > 0.001f)
            {
                if (!s.isPlaying && s.clip != null) s.Play();
            }
            else if (s.isPlaying) s.Stop();
        }

        void Update()
        {
            if (!_onSurface || _mix.Boots <= 0f) { _speed01 = 0f; return; }

            Vector3 p = rig.position;
            Vector3 d = p - _lastPos;
            d.y = 0f; // horizontal travel only
            float dist = d.magnitude;
            _lastPos = p;

            // Normalize speed (m/s) to 0..1 against a brisk walk (~1.6 m/s).
            float dt = Time.deltaTime > 1e-4f ? Time.deltaTime : 1e-4f;
            _speed01 = Mathf.Clamp01((dist / dt) / 1.6f);

            _strideAccum += dist;
            if (_strideAccum >= strideLength)
            {
                _strideAccum -= strideLength;
                float gain = SuitAudioState.BootGain(_mix, _speed01);
                if (gain > 0.02f && bootClip != null)
                    bootSource.PlayOneShot(bootClip, gain);
            }
        }
    }
}
