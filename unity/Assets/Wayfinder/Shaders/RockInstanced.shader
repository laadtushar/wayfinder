// ============================================================================
// Wayfinder/RockInstanced — instanced procedural rock scatter (#18 part 3, §2).
//
// Hand-written URP HLSL (NOT Shader Graph) because the per-instance _Fade/_Tint
// ride a real instancing cbuffer that Shader Graph won't expose cleanly. Drawn
// via Graphics.RenderMeshInstanced + a MaterialPropertyBlock (the MPB
// instancing path — matches UNITY_ACCESS_INSTANCED_PROP, see F3), so it MUST
// participate in Single-Pass-Instanced stereo (F2) or the Android XR device
// build renders one eye / loses the instanceCount x 2 stereo multiplier.
//
// Fragment is deliberately the cheapest that reads as rock on a mobile Adreno:
// one directional light, NO shadow sample, NO additional lights, flat faceted
// normals baked into the mesh (no normal-map fetch), vertex-baked AO, one SH
// ambient tap. Foveated rendering discounts the periphery for free.
//
// F5 (early-Z on the Adreno tiler): the Bayer dither clip() is compiled ONLY
// into the LOD_FADE_CROSSFADE variant. Steady-state opaque rocks contain no
// clip()/discard, so the tiler keeps its low-res-Z / hidden-surface-removal
// fast path. Only the handful of transition-band instances pay for late-Z.
//
// FIX D (atmos scope): rocks fog with the world using the SAME analytic haze
// the terrain uses (Terrain/WayfinderAtmos.hlsl) — WayfinderFogFactor per
// vertex, WayfinderApplyFog after lighting — so they dissolve into the
// butterscotch horizon instead of popping out of it. Rocks are opaque, so no
// opposition surge is applied (surge is a ground-hotspot effect).
//
// Rough per-pixel cost: ~1 mul-heavy directional-light eval + 1 SH tap + 1 fog
// lerp; zero texture samples; zero dependent reads. Vertex is the real spend
// (faceted split verts, ~3 verts/tri, transformed every frame x2 eyes) — the
// runtime visible cap is derived from the tri ceiling, not the other way. This
// MUST be profiled with a frame-budget-report on the real Galaxy XR (Vulkan
// dev APK + RenderDoc: one instanced vkCmdDrawIndexed per (archetype,LOD) with
// instanceCount = visible x 2). The PC/editor/Direct-Preview never prove this.
// ============================================================================
Shader "Wayfinder/RockInstanced"
{
    Properties
    {
        // Material-level geology tint; per-instance _Tint (MPB) multiplies this.
        [MainColor] _BaseColor("Tint", Color) = (0.5, 0.4, 0.35, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "IgnoreProjector"= "True"
        }
        LOD 200

        // Shared across all three passes: includes that bring the instancing +
        // SPI macros, the material cbuffer, the per-instance MPB cbuffer, and
        // the F5-guarded dither. Pragmas placed here apply to every HLSLPROGRAM.
        HLSLINCLUDE
        #pragma target 3.5

        // Instancing (GPU) + stereo instancing share the instance-id channel.
        #pragma multi_compile_instancing
        // No per-instance light probe / lightmap arrays — we SampleSH manually.
        #pragma instancing_options nolightprobe nolightmap
        // Dithered LOD crossfade keyword. RenderMeshInstanced never sets
        // unity_LODFade, so our own _Fade drives the threshold (see below).
        #pragma multi_compile _ LOD_FADE_CROSSFADE

        // Core.hlsl FIRST (instancing + stereo macros, transforms), before any
        // lighting header — matches the URP include order convention.
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
        CBUFFER_END

        // MPB instancing cbuffer (F3). DrawMeshInstanced/RenderMeshInstanced +
        // MaterialPropertyBlock.SetVectorArray/SetFloatArray feed these; read
        // back with UNITY_ACCESS_INSTANCED_PROP.
        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float,  _Fade)   // signed crossfade: + = fade in, - = fade out
            UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)   // per-instance geology tint
        UNITY_INSTANCING_BUFFER_END(Props)

        // Bayer 4x4 ordered dither reproducing URP's LOD crossfade WITHOUT
        // unity_LODFade. Uses SV_POSITION pixel coords directly (i.pos.xy) — no
        // ComputeScreenPos (F10). Sign of fade selects fade-in vs fade-out.
        // Compiled ONLY into the LOD_FADE_CROSSFADE variant (F5).
        #if defined(LOD_FADE_CROSSFADE)
        void RockDitherClip(float2 pixelPos, float fade)
        {
            const float bayer[16] =
            {
                 0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                 3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
            };
            uint ix = (uint)pixelPos.x & 3u;
            uint iy = (uint)pixelPos.y & 3u;
            float threshold = bayer[iy * 4u + ix] + (0.5 / 16.0);
            clip(fade >= 0.0 ? (fade - threshold) : (threshold + fade));
        }
        #endif
        ENDHLSL

        // ------------------------------------------------------------------ //
        //  ForwardLit                                                        //
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

            // Lighting.hlsl pulls GetMainLight()/SampleSH; then the shared
            // terrain atmospherics so rocks haze exactly like the ground.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Terrain/WayfinderAtmos.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;   // baked vertex AO (grey)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0; // for the derivative flat normal
                half4  aoFog      : TEXCOORD1; // rgb: baked AO, a: Wayfinder haze factor
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ForwardVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS;
                o.aoFog.rgb  = (half3)v.color.rgb;
                o.aoFog.a    = WayfinderFogFactor(positionWS); // per-vertex haze
                return o;
            }

            half4 ForwardFrag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                #if defined(LOD_FADE_CROSSFADE)
                    float fade = UNITY_ACCESS_INSTANCED_PROP(Props, _Fade);
                    RockDitherClip(i.positionCS.xy, fade);
                #endif

                // Geology tint comes from _BaseColor (per-batch MPB uniform —
                // RenderMeshInstanced doesn't feed MPB arrays as per-instance
                // props, and tint is per-site not per-rock anyway).
                half3 tint = _BaseColor.rgb;

                // Flat face normal from screen-space derivatives — robust
                // against degenerate mesh facets, and gives the faceted look
                // angular rock wants anyway. NOTE the sign is derivative-
                // orientation dependent (D3D editor vs Vulkan device): must be
                // sign-verified on the Galaxy XR; if lighting inverts on device,
                // swap ddx/ddy. [device-verify]
                half3 nWS = (half3)normalize(cross(ddx(i.positionWS), ddy(i.positionWS)));
                Light mainLight = GetMainLight();                       // no shadow sample
                half  ndl = saturate(dot(nWS, (half3)mainLight.direction));
                half3 amb = (half3)SampleSH(nWS);                        // site SH (butterscotch / earthshine)

                // Floor the baked AO so crevices darken but rocks are never
                // pure black. Shadowed faces are filled by the SKY: on Mars the
                // bright butterscotch atmosphere scatters strong warm fill into
                // shade (the fog color doubles as the sky ambient); the floor
                // keeps airless worlds from going fully black too.
                half3 ao = lerp((half3)0.55, (half3)1.0, i.aoFog.rgb);
                half3 skyFill = max((half3)_WFFogColor.rgb * 0.65h, (half3)0.14);
                half3 lit = mainLight.color * ndl + max(amb, skyFill);
                half3 col = tint * ao * lit;

                col = WayfinderApplyFog(col, i.aoFog.a);                 // dissolve into the horizon haze
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        //  DepthOnly                                                         //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

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

            Varyings DepthVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Keep the prepass depth consistent with the crossfaded color
                // pass — but ONLY in the crossfade variant, so steady-state
                // depth stays clip-free and preserves early-Z (F5).
                #if defined(LOD_FADE_CROSSFADE)
                    float fade = UNITY_ACCESS_INSTANCED_PROP(Props, _Fade);
                    RockDitherClip(i.positionCS.xy, fade);
                #endif

                return i.positionCS.z;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        //  DepthNormals                                                      //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            // Matches URP's DepthNormals output packing (SSAO / decal normals).
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

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

            Varyings DepthNormalsVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 DepthNormalsFrag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                #if defined(LOD_FADE_CROSSFADE)
                    float fade = UNITY_ACCESS_INSTANCED_PROP(Props, _Fade);
                    RockDitherClip(i.positionCS.xy, fade);
                #endif

                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 normalWS = normalize(i.normalWS);
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3  packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0h);
                #else
                    float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
                    return half4(half3(normalWS), 0.0h);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
