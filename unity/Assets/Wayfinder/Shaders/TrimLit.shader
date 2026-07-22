// ============================================================================
// Wayfinder/TrimLit — the Star-Trek bridge hull material (#22, specs §3a).
//
// Hand-written URP *Lit* HLSL (NOT Shader Graph) to match the existing fork
// pattern (see RockInstanced.shader) and to keep a tight, prewarm-friendly
// variant set. This is the LOAD-BEARING hull material: it must read as lit
// metal, so it plugs into URP's real forward-lighting path — baked lightmap
// (Directional), spherical-harmonic ambient, and the box-projected baked
// reflection probe (specs §4). All lighting is BAKED at runtime (zero realtime
// lights / zero realtime shadows), so the light loop and shadow pass are ~0 ms;
// the per-pixel spend here is: 3 texture samples (albedo/normal/mask) + 1
// lightmap sample (2 taps when Directional) + 1 reflection-probe cubemap sample
// + the BRDF. That is the entire cost — it MUST still be profiled on the real
// Galaxy XR (Vulkan dev APK, frame-budget-report); PC/editor/Direct-Preview do
// not prove GPU timing.
//
// One 2048² trim sheet, three maps:
//   _BaseMap   albedo (sRGB)            × _Tint          -> base color
//   _NormalMap tangent normal          × _NormalStrength -> normal
//   _MaskMap   R=metallic G=AO B=emit-mask A=smoothness  (Unity mask packing)
// Emission (cove strips) = mask.b × _CoveColor(HDR) × slow breathe. Set the
// material's Emission GI = Baked so the cove wash bakes onto adjacent hull via
// the Meta pass below (breathe phase is 0 at bake -> 0.85 baked intensity).
//
// SRP-batcher compatible: every per-material scalar/color lives in the
// UnityPerMaterial CBUFFER; textures are the only globals. Single-Pass Instanced
// stereo is honoured in every drawn pass (UNITY_VERTEX_OUTPUT_STEREO +
// setup/transfer macros) so the Android XR device build renders both eyes.
//
// The ForwardLit / ShadowCaster / DepthOnly / DepthNormals / Meta passes are
// hand-rolled equivalents of URP's LitForwardPass etc. — we do NOT #include
// LitInput/LitForwardPass because those hard-bind the stock CBUFFER + property
// names; instead we own the CBUFFER and call URP's own GI/BRDF/reflection
// machinery (SAMPLE_GI, OUTPUT_SH4, UniversalFragmentPBR) so the lighting math
// is byte-for-byte URP. APV (adaptive probe volumes) and SSGI variants are
// intentionally omitted — the bridge uses classic baked lightmaps + one probe,
// which trims the variant count for prewarm.
// ============================================================================
Shader "Wayfinder/TrimLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo (sRGB)", 2D) = "white" {}
        [MainColor]   _Tint("Tint", Color) = (1, 1, 1, 1)

        // All three maps are the one shared trim sheet -> same UV/tiling as
        // _BaseMap ([NoScaleOffset] so the inspector doesn't show dead fields).
        [NoScaleOffset][Normal] _NormalMap("Normal (tangent)", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0.0, 2.0)) = 1.0

        // R=Metallic  G=AO  B=Emissive mask  A=Smoothness  (Unity mask packing).
        [NoScaleOffset] _MaskMap("Mask (R:Metal G:AO B:Emit A:Smooth)", 2D) = "white" {}

        [HDR] _CoveColor("Cove Emission (HDR)", Color) = (0, 0, 0, 1)
        _CovePulse("Cove Pulse Speed", Float) = 1.5

        // Static-geometry XR posture: no MotionVectors pass exists on this
        // shader (see header), so the hull contributes zero object motion and
        // is safe under ASW. This property is kept only so the material inspector
        // round-trips with the rest of the bridge kit.
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"        = "UniversalPipeline"
            "RenderType"            = "Opaque"
            "UniversalMaterialType" = "Lit"
            "Queue"                 = "Geometry"
            "IgnoreProjector"       = "True"
        }
        LOD 300

        // Shared across every pass: Core + surface helpers, the material CBUFFER,
        // the two extra trim samplers, and the surface-data builder. SurfaceInput
        // pulls Core.hlsl, SurfaceData.hlsl, Packing (UnpackNormalScale) and
        // declares _BaseMap/sampler_BaseMap for us.
        HLSLINCLUDE
        #pragma target 3.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        // SRP-batcher CBUFFER: all per-material props live here.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _Tint;
            half   _NormalStrength;
            half4  _CoveColor;
            half   _CovePulse;
        CBUFFER_END

        // Extra trim maps (albedo sampler comes from SurfaceInput.hlsl).
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_MaskMap);   SAMPLER(sampler_MaskMap);

        // Builds URP SurfaceData from the trim sheet. Named to mirror URP's own
        // InitializeStandardLitSurfaceData so the Meta pass can reuse it.
        void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surfaceData)
        {
            surfaceData = (SurfaceData)0;

            half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            surfaceData.albedo = baseSample.rgb * _Tint.rgb;
            surfaceData.alpha  = 1.0h;

            half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
            surfaceData.metallic   = mask.r;
            surfaceData.occlusion  = mask.g;
            surfaceData.smoothness = mask.a;

            half4 nSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
            surfaceData.normalTS = UnpackNormalScale(nSample, _NormalStrength);

            // Slow cove breathe. Phase computed in FULL float — half-precision
            // sin() of a large _Time.y drifts/jitters after minutes on Adreno.
            float phase   = _Time.y * _CovePulse;
            half  breathe = 0.85h + 0.15h * (half)sin(phase);
            surfaceData.emission = mask.b * _CoveColor.rgb * breathe;

            surfaceData.specular           = half3(0.0h, 0.0h, 0.0h);
            surfaceData.clearCoatMask      = 0.0h;
            surfaceData.clearCoatSmoothness = 0.0h;
        }
        ENDHLSL

        // ------------------------------------------------------------------ //
        //  ForwardLit — full URP forward: lightmap/SH GI + box reflection      //
        //  probe + fog. This is where the metal look is earned.               //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   ForwardVert
            #pragma fragment ForwardFrag

            // -- Universal Pipeline lighting keywords (match stock Lit so
            //    UniversalFragmentPBR takes the identical code paths). APV
            //    (ProbeVolumeVariants) and SSGI (_SCREEN_SPACE_IRRADIANCE) are
            //    deliberately not compiled — the bridge is classic baked
            //    lightmaps + one box-projected reflection probe.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // Eye-tracked foveated rendering keyword (the fragment discount the
            // rest of the budget leans on). Brings its own #pragma.
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

            // -- Unity lightmap / GI keywords. DIRLIGHTMAP_COMBINED is required
            //    for the Directional lightmaps the bridge bakes (specs §4);
            //    without it the normal map contributes nothing to diffuse.
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // Fog keywords + helpers.
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #pragma multi_compile_instancing

            // Full lighting stack (GI, reflection probes, BRDF, realtime lights,
            // shadows, AO). Core already came in via SurfaceInput.hlsl.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS       : POSITION;
                float3 normalOS         : NORMAL;
                float4 tangentOS        : TANGENT;
                float2 texcoord         : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV: TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                half4  tangentWS   : TEXCOORD3; // xyz tangent, w sign

            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half4  fogFactorAndVertexLight : TEXCOORD5; // x fog, yzw vertex light
            #else
                half   fogFactor   : TEXCOORD5;
            #endif

                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
            #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV : TEXCOORD9;
            #endif
            #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion : TEXCOORD10;
            #endif

                float4 positionCS  : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
            {
                inputData = (InputData)0;
                inputData.positionWS = input.positionWS;

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Tangent-space normal -> world (normal map always present).
                float  sgn        = input.tangentWS.w;
                float3 bitangent  = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
                inputData.tangentToWorld = tangentToWorld;
                inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
                inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
                inputData.viewDirectionWS = viewDirWS;

            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif

            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                inputData.fogCoord      = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
            #else
                inputData.fogCoord      = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.vertexLighting = half3(0, 0, 0);
            #endif

                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            }

            void InitializeBakedGIData(Varyings input, inout InputData inputData)
            {
            #if defined(DYNAMICLIGHTMAP_ON)
                inputData.bakedGI   = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            #else
                inputData.bakedGI   = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            #endif
            }

            Varyings ForwardVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv         = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionWS = vertexInput.positionWS;
                output.normalWS   = normalInput.normalWS;

                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normalInput.tangentWS.xyz, sign);

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
            #ifdef DYNAMICLIGHTMAP_ON
                output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            #endif
                OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz,
                           GetWorldSpaceNormalizeViewDir(vertexInput.positionWS),
                           output.vertexSH, output.probeOcclusion);

                half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
            #else
                output.fogFactor = fogFactor;
            #endif

                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 ForwardFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);

            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif

                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                InitializeBakedGIData(input, inputData);

                // URP's own PBR: main light (baked shadows) + additional lights
                // + bakedGI (lightmap/SH) + box-projected reflection probe + AO.
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        //  ShadowCaster — present for full-Lit completeness. Runtime bridge    //
        //  bakes shadows (this pass is ~0 ms unless a realtime light is added).//
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            // Set by ShadowUtils per shadow-casting light.
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                positionCS = ApplyShadowClamping(positionCS);
                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        //  DepthOnly — depth prepass. No texture reads (opaque, no alpha clip),//
        //  so the Adreno tiler keeps its early-Z fast path.                    //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        //  DepthNormals — geometric world normal (cheap). Only runs if a       //
        //  renderer feature needs _CameraNormalsTexture (SSAO/decals); the     //
        //  bridge does not, but the pass is here for completeness/future use.  //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif

            #if defined(_GBUFFER_NORMALS_OCT)
                float3 normalWS = normalize(input.normalWS);
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                half3  packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                return half4(packedNormalWS, 0.0h);
            #else
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                return half4(half3(normalWS), 0.0h);
            #endif
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        //  Meta — lightmap baking. Feeds albedo + emission to the Progressive  //
        //  lightmapper so the cove wash (mask.b × _CoveColor) bakes onto the   //
        //  hull. At bake time _Time.y = 0 -> breathe = 0.85 baked intensity.   //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex   UniversalVertexMeta
            #pragma fragment TrimMetaFrag

            #pragma shader_feature EDITOR_VISUALIZATION

            // Provides UniversalVertexMeta / UniversalFragmentMeta / MetaInput
            // (and pulls Lighting.hlsl for InitializeBRDFData). Uses _BaseMap /
            // _BaseMap_ST supplied by our HLSLINCLUDE.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

            half4 TrimMetaFrag(Varyings input) : SV_Target
            {
                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);

                BRDFData brdfData;
                InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular,
                                   surfaceData.smoothness, surfaceData.alpha, brdfData);

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo   = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5;
                metaInput.Emission = surfaceData.emission;
                return UniversalFragmentMeta(input, metaInput);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
