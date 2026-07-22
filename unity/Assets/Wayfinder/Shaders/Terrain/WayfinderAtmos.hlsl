#ifndef WAYFINDER_ATMOS_INCLUDED
#define WAYFINDER_ATMOS_INCLUDED
// Per-site analytic atmospherics (#18): Mars height+distance butterscotch
// haze dissolving terrain into the baked sky's horizon, and the Moon's
// opposition surge (shadow-hiding backscatter hotspot around the anti-solar
// point). 0 draw calls, 0 tris, no depth read, no extra pass.
//
// Globals pushed once per world-load by WorldAtmospherics.cs (Shader.SetGlobal*)
// — declared OUTSIDE UnityPerMaterial, SRP-Batcher-safe. Values are data on
// the WorldPackage, real-referenced:
//   Mars haze:  Curiosity Mastcam horizon photometry (NASA/JPL-Caltech/MSSS,
//               public domain) — linear RGB ~(0.68,0.49,0.36). Density
//               ~4e-4 /m = optical depth 1 at 2.5 km (37% transmission);
//               Koschmieder VISIBILITY at that density is ~10 km — a normal
//               dusty Martian day, not a dust storm.
//   Moon surge: LRO/Apollo surface photometry (public domain) — strength
//               ~0.6, lobe sharpness ~48 (HWHM ~9.7 deg).
//
// SCOPE (deliberate): this fogs the TERRAIN only. The sky cubemap already
// carries the haze at infinity, and today's sites are terrain+sky only. Any
// future distant prop (rock scatter #18 part 3, ship hull) must include this
// file and apply WayfinderFogFactor/WayfinderApplyFog itself or it will pop
// out of the butterscotch.
float4 _WFFogColor;          // linear (pusher must send .linear — inspector colors are sRGB)
float  _WFFogEnable;
float  _WFFogDensity;        // 1/m, distance
float  _WFFogHeightFalloff;  // 1/m, altitude above datum
float  _WFFogGroundY;        // world-space haze datum (m)
float  _WFFogHeightStrength; // 0 = pure distance fog, 1 = fully stratified
float4 _WFSurgeTint;
float  _WFSurgeStrength;
float  _WFSurgeSharpness;

// Per-VERTEX: analytic height + distance haze factor in [0,1].
// GetCameraPositionWS() (not raw _WorldSpaceCameraPos) — per-eye correct
// under single-pass instanced stereo; a per-eye mismatch is worst exactly
// at the near ground where retinal rivalry is most visible.
half WayfinderFogFactor(float3 posWS)
{
    float dist = distance(posWS, GetCameraPositionWS());
    float distFog = 1.0 - exp(-_WFFogDensity * dist);           // Beer–Lambert
    float h = max(posWS.y - _WFFogGroundY, 0.0);
    float heightAtten = exp(-_WFFogHeightFalloff * h);          // exponential atmosphere
    float fog = distFog * lerp(1.0, heightAtten, _WFFogHeightStrength);
    return (half)saturate(fog * _WFFogEnable);
}

// Fragment: dissolve lit color into the horizon color the baked sky shows.
half3 WayfinderApplyFog(half3 col, half fog)
{
    return lerp(col, (half3)_WFFogColor.rgb, fog);
}

// Per-PIXEL opposition surge: cos(phase)^n lobe peaking at the anti-solar
// point (the ground around your own shadow). Per-pixel because the hotspot
// is metres wide near your feet — per-vertex would smear it across terrain
// triangles. ~6 ALU: one dot, one pow.
half3 WayfinderSurge(half3 col, float3 posWS)
{
    float3 V = normalize(GetCameraPositionWS() - posWS);
    float3 L = _MainLightPosition.xyz;
    float  c = saturate(dot(V, L));
    float  surge = _WFSurgeStrength * pow(c, _WFSurgeSharpness);
    return col * ((half3)1.0 + (half)surge * (half3)_WFSurgeTint.rgb);
}
#endif
