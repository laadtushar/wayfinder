namespace Wayfinder.Core
{
    /// Which audio beds should play, decided purely from travel state + world.
    /// Engine-free so the mixing logic is testable headless (same reason the
    /// travel machine lives here). The Unity SuitAudio component just applies
    /// these gains to AudioSources.
    public readonly struct SuitAudioMix
    {
        public readonly float Ambience;   // surface wind / world bed
        public readonly float Breathing;  // EVA suit breathing loop
        public readonly float Boots;      // footstep bed (gated further by motion)
        public readonly float Radio;      // faint comms/static bed
        public readonly float BridgeHum;  // low bridge machinery hum

        public SuitAudioMix(float ambience, float breathing, float boots, float radio, float bridgeHum)
        {
            Ambience = ambience;
            Breathing = breathing;
            Boots = boots;
            Radio = radio;
            BridgeHum = bridgeHum;
        }
    }

    public static class SuitAudioState
    {
        /// The target mix for a given state. `worldHasAtmosphere` gates surface
        /// ambience: Mars has wind, the airless Moon is near-silent (only the
        /// suit's own sounds — breathing, boots conducted through the body).
        public static SuitAudioMix Resolve(TravelState state, bool worldHasAtmosphere)
        {
            switch (state)
            {
                case TravelState.OnSurface:
                    return new SuitAudioMix(
                        ambience: worldHasAtmosphere ? 1f : 0f, // vacuum = no airborne sound
                        breathing: 1f,                          // always hear your own breath in the suit
                        boots: 1f,                              // enabled; motion gate scales it live
                        radio: 0.35f,                           // faint comms bed
                        bridgeHum: 0f);

                case TravelState.OnBridge:
                    return new SuitAudioMix(
                        ambience: 0f,
                        breathing: 0f,   // shirtsleeve on the pressurized bridge
                        boots: 0f,
                        radio: 0.15f,    // quiet comms chatter
                        bridgeHum: 1f);  // machinery hum

                // During the warp transitions, fade the world beds out — the
                // warp flash covers a silent beat, then the destination fades in.
                default:
                    return new SuitAudioMix(0f, 0f, 0f, 0f, 0f);
            }
        }

        /// Boot gain scales with locomotion: silent when still, up to full while
        /// grab-moving/teleport-settling. `speed01` is normalized 0..1 motion.
        public static float BootGain(in SuitAudioMix mix, float speed01)
        {
            float s = speed01 < 0f ? 0f : speed01 > 1f ? 1f : speed01;
            return mix.Boots * s;
        }
    }
}
