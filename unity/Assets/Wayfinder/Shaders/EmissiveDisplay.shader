// ============================================================================
// Wayfinder/EmissiveDisplay — animated console screens (#22, specs §3b).
//
// Hand-written URP *Unlit* HLSL (NOT Shader Graph) — the entire look is
// procedural (NO texture): LCARS grid + rolling CRT scanlines + an animated
// bar-graph/waveform (telemetry) OR hashed twinkling star-chart points
// (starchart), lerped by _Mode and multiplied by a CRT flicker. Output is HDR
// so URP Bloom (or the spec's cheaper additive glow cards, §3c) picks it up.
//
// Red-Matter-cheap: opaque (writes depth, keeps the tiler's early-Z fast path),
// zero texture samples, a handful of sin()/frac()/step() and three hash taps.
// Cost is pure ALU in the fragment — no dependent reads, no lighting — so it is
// effectively free next to the hull. STILL profile on the Galaxy XR: many
// screens = overdraw-free but fragment-bound area; the emulator shows no GPU
// timing.
//
// Single-Pass Instanced stereo is honoured in every pass. There is deliberately
// NO MotionVectors / XRMotionVectors pass: the screens are static geometry, so
// they contribute zero object motion vectors and are safe under ASW — only the
// scrolling *content* (emission) half-rates under ASW, which is imperceptible
// (specs §3b). This matches the static-geometry posture of the hull material.
//
// PER-SCREEN VARIETY WITHOUT BREAKING SRP BATCHING (specs §3b):
//   One material drives N screens in one SetPass. Do NOT use MaterialProperty
//   Block (it breaks the SRP Batcher). Instead author per-screen data into the
//   MESH and enable the _EMISSIVE_PER_SCREEN keyword on the material:
//     * UV2 (TEXCOORD1).x = mode      0 = telemetry .. 1 = starchart (per screen)
//     * UV2 (TEXCOORD1).y = speed mul relative to _Speed (1 = material speed;
//                                  e.g. 1.5 = 50% faster). Author >= a small
//                                  value; 0 freezes that screen.
//     * Vertex COLOR.rgb  = emissive tint multiplier over _Glow (white = _Glow
//                                  unchanged; warm/cool it per screen)
//     * Vertex COLOR.a    = per-screen intensity scale (1 = full)
//   Because the keyword is a per-MATERIAL toggle and the variety rides mesh
//   attributes (not per-instance uniforms), all screens stay in one SRP batch.
//   With the keyword OFF the shader falls back to the material _Mode/_Speed/
//   _Glow and works on a bare quad (single-screen use, e.g. the Viewscreen).
// ============================================================================
Shader "Wayfinder/EmissiveDisplay"
{
    Properties
    {
        _Base("Base (dark)", Color) = (0.01, 0.02, 0.03, 1)
        [HDR] _Glow("Glow (HDR)", Color) = (0.1, 0.7, 1.0, 1)
        _Speed("Speed", Float) = 1.0
        [Toggle] _Mode("Mode (0 telemetry / 1 starchart)", Float) = 0.0

        // Enable to read per-screen mode/speed/tint from UV2 + vertex color
        // (see header). Off = material-driven (single-screen quads).
        [Toggle(_EMISSIVE_PER_SCREEN)] _PerScreen("Per-screen data (UV2 + color)", Float) = 0.0

        // Static-geometry XR posture: no MotionVectors pass exists on this
        // shader, so screens contribute zero object motion vectors (safe under
        // ASW). Kept for material-inspector parity with the bridge kit.
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "UniversalPipeline"
            "RenderType"      = "Opaque"
            "Queue"           = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 100

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Base;
            half4 _Glow;
            half  _Speed;
            half  _Mode;
        CBUFFER_END

        // Ported from the §3b sketch. Kept in FULL float precision: the star
        // cell coords (floor(uv*20)) push the sin() argument to the hundreds/
        // thousands, where a half-precision sin() bands badly on Adreno.
        float hash21(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }
        ENDHLSL

        // ------------------------------------------------------------------ //
        //  ForwardLit — unlit emission. HDR out for bloom pickup.              //
        // ------------------------------------------------------------------ //
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Opaque, faces the viewer (author the quad wound so its front
            // points into the room).
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   EmissiveVert
            #pragma fragment EmissiveFrag

            #pragma shader_feature_local _EMISSIVE_PER_SCREEN
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            #ifdef _EMISSIVE_PER_SCREEN
                float2 uv2        : TEXCOORD1; // x: mode, y: speed multiplier
                half4  color      : COLOR;     // rgb: glow tint, a: intensity
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS         : SV_POSITION;
                float2 uv                 : TEXCOORD0;
                half3  glow               : TEXCOORD1; // _Glow.rgb × per-screen tint
                half3  modeSpeedIntensity : TEXCOORD2; // x mode, y speed, z intensity
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings EmissiveVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;

            #ifdef _EMISSIVE_PER_SCREEN
                output.modeSpeedIntensity = half3(saturate(input.uv2.x), _Speed * input.uv2.y, input.color.a);
                output.glow = _Glow.rgb * input.color.rgb;
            #else
                output.modeSpeedIntensity = half3(_Mode, _Speed, 1.0h);
                output.glow = _Glow.rgb;
            #endif
                return output;
            }

            half4 EmissiveFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv        = input.uv;
                half   mode      = input.modeSpeedIntensity.x;
                float  speed     = input.modeSpeedIntensity.y;
                half   intensity = input.modeSpeedIntensity.z;
                float  t         = _Time.y * speed;

                // --- grid + rolling scanlines ---
                float grid = step(0.96, frac(uv.x * 24.0)) + step(0.98, frac(uv.y * 16.0));
                float scan = 0.5 + 0.5 * sin((uv.y * 220.0) - t * 6.0);

                // --- animated bar graph / waveform ---
                float wave = 0.5 + 0.35 * sin(uv.x * 18.0 + t * 3.0) * sin(uv.x * 5.0 - t);
                float bars = step(uv.y, wave) * step(frac(uv.x * 20.0), 0.7);

                // --- star chart: hashed blinking points ---
                float2 c    = floor(uv * 20.0);
                float  h    = hash21(c);
                float  star = step(0.92, h) * (0.5 + 0.5 * sin(t * 2.0 + h * 6.28318));

                // --- telemetry vs starchart, then CRT life ---
                float telemetry = saturate(grid * 0.6 + bars * 0.8 + scan * 0.15);
                float pat       = lerp(telemetry, star, mode);
                float flick     = 0.9 + 0.1 * hash21(float2(floor(t * 12.0), 0.0));

                // HDR emission for bloom pickup.
                half3 col = _Base.rgb + input.glow * (half)(pat * flick) * intensity;
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        //  DepthOnly — screens are opaque and participate in the depth prepass.//
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
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ //
        //  DepthNormals — geometric world normal (screens are flat quads).     //
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
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
