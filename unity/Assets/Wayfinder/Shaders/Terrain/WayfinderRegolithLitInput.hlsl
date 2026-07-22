// ============================================================================
// Wayfinder fork of URP TerrainLitInput.hlsl
// (com.unity.render-pipelines.universal@e38be786c41e, URP 17.x / Unity 6).
// Included ONLY by the ForwardLit pass of Wayfinder/Terrain/RegolithLit; all
// other passes use the package original. Identical to stock except for the
// _REGOLITH_DETAIL block appended at the bottom (spec §2B + REQUIRED FIX #2).
// Keeps the stock include guard on purpose: this file and the package original
// define the same symbols and must be mutually exclusive within one program.
// ============================================================================
#ifndef UNIVERSAL_TERRAIN_LIT_INPUT_INCLUDED
#define UNIVERSAL_TERRAIN_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4 _BaseColor;
    half _Cutoff;

    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Control);
    float4 _Splat0_TexelSize, _Splat1_TexelSize, _Splat2_TexelSize, _Splat3_TexelSize;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat0);
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat1);
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat2);
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat3);
CBUFFER_END

#define _Surface 0.0 // Terrain is always opaque

CBUFFER_START(_Terrain)
    half _NormalScale0, _NormalScale1, _NormalScale2, _NormalScale3;
    half _Metallic0, _Metallic1, _Metallic2, _Metallic3;
    half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
    half4 _DiffuseRemapScale0, _DiffuseRemapScale1, _DiffuseRemapScale2, _DiffuseRemapScale3;
    half4 _MaskMapRemapOffset0, _MaskMapRemapOffset1, _MaskMapRemapOffset2, _MaskMapRemapOffset3;
    half4 _MaskMapRemapScale0, _MaskMapRemapScale1, _MaskMapRemapScale2, _MaskMapRemapScale3;

    float4 _Control_ST;
    float4 _Control_TexelSize;
    half _DiffuseHasAlpha0, _DiffuseHasAlpha1, _DiffuseHasAlpha2, _DiffuseHasAlpha3;
    half _LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3;
    half _SmoothnessSource0, _SmoothnessSource1, _SmoothnessSource2, _SmoothnessSource3;
    half4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
    half _HeightTransition;
    half _NumLayersCount;
    float _TerrainBasemapDistance;

#ifdef UNITY_INSTANCING_ENABLED
    float4 _TerrainHeightmapRecipSize;   // float4(1.0f/width, 1.0f/height, 1.0f/(width-1), 1.0f/(height-1))
#endif
    float4 _TerrainHeightmapScale;       // float4(hmScale.x, hmScale.y / (float)(kMaxHeight), hmScale.z, 0.0f)
    #ifdef SCENESELECTIONPASS
    int _ObjectId;
    int _PassValue;
    #endif
CBUFFER_END

TEXTURE2D(_Control);    SAMPLER(sampler_Control);
TEXTURE2D(_Splat0);     SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);     SAMPLER(sampler_Splat1);
TEXTURE2D(_Splat2);     SAMPLER(sampler_Splat2);
TEXTURE2D(_Splat3);     SAMPLER(sampler_Splat3);

TEXTURE2D(_Normal0);     SAMPLER(sampler_Normal0);
TEXTURE2D(_Normal1);     SAMPLER(sampler_Normal1);
TEXTURE2D(_Normal2);     SAMPLER(sampler_Normal2);
TEXTURE2D(_Normal3);     SAMPLER(sampler_Normal3);

TEXTURE2D(_Mask0);      SAMPLER(sampler_Mask0);
TEXTURE2D(_Mask1);      SAMPLER(sampler_Mask1);
TEXTURE2D(_Mask2);      SAMPLER(sampler_Mask2);
TEXTURE2D(_Mask3);      SAMPLER(sampler_Mask3);

TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
TEXTURE2D(_SpecGlossMap);  SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_MetallicTex);   SAMPLER(sampler_MetallicTex);

#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
#define ENABLE_TERRAIN_PERPIXEL_NORMAL
#endif

#ifdef UNITY_INSTANCING_ENABLED
TEXTURE2D(_TerrainHeightmapTexture);
TEXTURE2D(_TerrainNormalmapTexture);
SAMPLER(sampler_TerrainNormalmapTexture);
#endif

UNITY_INSTANCING_BUFFER_START(Terrain)
UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)  // float4(xBase, yBase, skipScale, ~)
UNITY_INSTANCING_BUFFER_END(Terrain)

#ifdef _ALPHATEST_ON
TEXTURE2D(_TerrainHolesTexture);
SAMPLER(sampler_TerrainHolesTexture);

float SampleTerrainHolesTexture(float2 uv)
{
    return SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r;
}

void ClipHoles(float2 uv)
{
    float hole = SampleTerrainHolesTexture(uv);
    // Fixes bug where compression is enabled and 0 isn't actually 0 but low like 1/2047. (UUM-61913)
    float epsilon = 0.0005f;
    clip(hole < epsilon ? -1 : 1);
}
#endif

#define SampleLayerAlbedo(i) (SAMPLE_TEXTURE2D(_Splat##i, sampler_Splat0, splat##i##uv) * half4(_DiffuseRemapScale##i.rgb, 1.0h))

#ifdef _NORMALMAP
    #define SampleLayerNormal(i) UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal##i, sampler_Normal0, splat##i##uv), _NormalScale##i)
#else
    #define SampleLayerNormal(i) half3(0.0, 0.0, 1.0)
#endif

#ifdef _MASKMAP
    #define SampleLayerMasks(i) (_MaskMapRemapOffset##i + _MaskMapRemapScale##i * lerp(0.5h, SAMPLE_TEXTURE2D(_Mask##i, sampler_Mask0, splat##i##uv), _LayerHasMask##i));
#else
    #define SampleLayerMasks(i) (_MaskMapRemapOffset##i + _MaskMapRemapScale##i * 0.5h);
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;
    specGloss = SAMPLE_TEXTURE2D(_MetallicTex, sampler_MetallicTex, uv);
    specGloss.a = albedoAlpha;
    return specGloss;
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;
    half4 albedoSmoothness = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    outSurfaceData.alpha = 1;

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoSmoothness.a);
    outSurfaceData.albedo = albedoSmoothness.rgb;

    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap));
    outSurfaceData.occlusion = 1;
    outSurfaceData.emission = 0;
}

void TerrainInstancing(inout float4 positionOS, inout float3 normal, inout float2 uv)
{
#ifdef UNITY_INSTANCING_ENABLED
    float2 patchVertex = positionOS.xy;
    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);

    float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z; // (xy + float2(xBase,yBase)) * skipScale
    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

    positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
    positionOS.y = height * _TerrainHeightmapScale.y;

#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
    normal = float3(0, 1, 0);
#else
    normal = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2 - 1;
#endif
    uv = sampleCoords * _TerrainHeightmapRecipSize.zw;
#endif
}

void TerrainInstancing(inout float4 positionOS, inout float3 normal)
{
    float2 uv = { 0, 0 };
    TerrainInstancing(positionOS, normal, uv);
}

void TerrainInstancing(inout float4 positionOS)
{
    float3 normal = { 0, 0, 0 };
    TerrainInstancing(positionOS, normal);
}

// ============================================================================
// Wayfinder: near-field regolith detail (spec §2B, with REQUIRED FIX #2).
// Guarded so the _REGOLITH_DETAIL-off variant is byte-for-byte stock TerrainLit.
// ============================================================================
#if defined(_REGOLITH_DETAIL)

TEXTURE2D(_DetailAlbedo);  SAMPLER(sampler_DetailAlbedo);   // one shared sampler for all three detail fetches
TEXTURE2D(_DetailNormal);
TEXTURE2D(_MacroNoise);

// Deliberately loose uniforms, NOT appended to UnityPerMaterial/_Terrain: a
// keyword-conditional cbuffer member would give the two variants mismatched
// UnityPerMaterial layouts. Terrain renders through the instanced terrain path
// (not the SRP Batcher), so loose uniforms are free here and keep the off-
// variant's constant buffers identical to stock.
float4 _DetailUVOrigin;    // xy: per-site world-XZ recenter (fp32 UV precision aid for POIs km from origin)
float _DetailTileMeters, _MacroTileMeters, _FadeStart, _FadeEnd;
float _DetailStrength, _DetailNormalScale, _MacroContrast;

// Cost inside the fade disc: 3 compressed bilinear fetches + ~20 ALU.
// Outside: ~6 ALU then a quad-coherent branch skip (0 fetches).
// Modifies albedo with Unity's detail-map convention (centered 0.5, x2) so the
// orbital basemap keeps all large-scale color truth, and blends the detail
// normal over normalTS (which is (0,0,1) here — no layer normal assigned — so
// the detail normal cleanly becomes the surface normal within the fade band).
void ApplyRegolithDetail(float3 positionWS, inout half3 albedo, inout half3 normalTS)
{
    // Distance fade: 1 near -> 0 far. GetCameraPositionWS() is per-eye correct
    // under single-pass instanced XR.
    float camDist = distance(positionWS, GetCameraPositionWS());
    half fade = (half)saturate(1.0 - smoothstep(_FadeStart, _FadeEnd, camDist)) * (half)_DetailStrength;

    // World-XZ planar UVs (the basemap layer is tiled ONCE over the whole clip,
    // so its UV is useless for near-field frequency; derive detail UVs from
    // world position). _DetailUVOrigin recenters per site so UV magnitudes stay
    // small and fp32-well-conditioned far from the world origin.
    float2 wxz = positionWS.xz - _DetailUVOrigin.xy;
    float2 duv = wxz * rcp(_DetailTileMeters);
    float2 muv = wxz * rcp(_MacroTileMeters);

    // REQUIRED FIX #2: ALL derivatives — including the macro's — are computed
    // BEFORE the branch, and every sample below is explicit-gradient. Implicit-
    // LOD sampling after divergent flow is undefined per the HLSL spec and
    // shimmers on the fade-boundary annulus (driver-dependent on Adreno).
    float2 dx  = ddx(duv), dy  = ddy(duv);
    float2 mdx = ddx(muv), mdy = ddy(muv);

    UNITY_BRANCH
    if (fade <= (half)0.002) return;   // far pixels skip all 3 fetches; quad-coherent, cheap on Adreno

    // Macro noise breaks tiling: remap to ~[1-c, 1+c] and modulate detail luminance.
    half macro = (half)SAMPLE_TEXTURE2D_GRAD(_MacroNoise, sampler_DetailAlbedo, muv, mdx, mdy).r;
    macro = lerp((half)(1.0 - _MacroContrast), (half)(1.0 + _MacroContrast), macro);

    // Detail albedo uses Unity's detail-map convention (centered on 0.5, *2), so the
    // real orbital photo keeps ALL large-scale color truth; detail only adds high-freq crunch.
    half4 dA = SAMPLE_TEXTURE2D_GRAD(_DetailAlbedo, sampler_DetailAlbedo, duv, dx, dy);
    half3 detail = dA.rgb * ((half)2.0 * macro);
    albedo *= lerp((half3)1.0, detail, fade);

    // Detail normal: with no layer normal, normalTS=(0,0,1) -> detail becomes the surface normal.
    half3 dN = UnpackNormalScale(
        SAMPLE_TEXTURE2D_GRAD(_DetailNormal, sampler_DetailAlbedo, duv, dx, dy),
        _DetailNormalScale);
    normalTS = normalize(lerp(normalTS, BlendNormalRNM(normalTS, dN), fade));
}

#endif // _REGOLITH_DETAIL

#endif
