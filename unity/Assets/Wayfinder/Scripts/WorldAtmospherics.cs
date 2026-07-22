using UnityEngine;

namespace Wayfinder.Unity
{
    /// Pushes a world's atmospherics (haze + opposition surge) into the
    /// global shader uniforms consumed by WayfinderAtmos.hlsl. Pure static —
    /// called by TravelManager once per world arrival, inside the warp flash;
    /// never per-frame, allocation-free.
    ///
    /// INVARIANT (verification FIX E): TravelManager calls Apply for EVERY
    /// world, unconditionally — Shader.SetGlobal* persists across additive
    /// scene unloads, so a Moon that "forgot" to apply would inherit Mars
    /// haze. hazeEnabled=false worlds actively write _WFFogEnable=0.
    public static class WorldAtmospherics
    {
        static readonly int ID_Enable = Shader.PropertyToID("_WFFogEnable");
        static readonly int ID_FogColor = Shader.PropertyToID("_WFFogColor");
        static readonly int ID_Density = Shader.PropertyToID("_WFFogDensity");
        static readonly int ID_HeightFalloff = Shader.PropertyToID("_WFFogHeightFalloff");
        static readonly int ID_GroundY = Shader.PropertyToID("_WFFogGroundY");
        static readonly int ID_HeightStr = Shader.PropertyToID("_WFFogHeightStrength");
        static readonly int ID_SurgeStr = Shader.PropertyToID("_WFSurgeStrength");
        static readonly int ID_SurgeSharp = Shader.PropertyToID("_WFSurgeSharpness");
        static readonly int ID_SurgeTint = Shader.PropertyToID("_WFSurgeTint");

        /// Safety floor: vacuum, no surge. Pushed when a world has no package
        /// so stale haze from the previous world can never survive an arrival.
        public static void ApplyVacuum()
        {
            Shader.SetGlobalFloat(ID_Enable, 0f);
            Shader.SetGlobalFloat(ID_SurgeStr, 0f);
        }

        public static void Apply(WorldPackage w)
        {
            if (w == null) throw new System.ArgumentNullException(nameof(w));
            Shader.SetGlobalFloat(ID_Enable, w.HazeEnabled ? 1f : 0f);
            // .linear (FIX C): inspector colors serialize sRGB; the shader
            // lerps against linear lit color. Raw RGB here = washed horizon
            // and a visible band where the dissolve should be seamless.
            Shader.SetGlobalColor(ID_FogColor, w.HazeColor.linear);
            Shader.SetGlobalFloat(ID_Density, w.HazeDistanceDensity);
            Shader.SetGlobalFloat(ID_HeightFalloff, w.HazeHeightFalloff);
            Shader.SetGlobalFloat(ID_GroundY, w.HazeGroundY);
            Shader.SetGlobalFloat(ID_HeightStr, w.HazeHeightStrength);
            Shader.SetGlobalFloat(ID_SurgeStr, w.SurgeStrength);
            Shader.SetGlobalFloat(ID_SurgeSharp, w.SurgeSharpness);
            Shader.SetGlobalColor(ID_SurgeTint, w.SurgeTint.linear);
        }
    }
}
