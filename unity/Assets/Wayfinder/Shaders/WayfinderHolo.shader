// ============================================================================
// Wayfinder/Holo — the bridge destination globe as a projected hologram (#51).
//
// Hand-written URP *Unlit transparent* HLSL (NOT Shader Graph). Follow-up to
// #48, which shipped the globe on a flat additive-cyan material that read as a
// solid ball. This makes it read as a volume of light a projector throws:
//   * FRESNEL rim  — bright at grazing angles, near-transparent through the
//                    centre, so you see an edge-lit shell, not a filled sphere;
//   * SCANLINES    — faint latitude bands scrolling slowly upward (stable under
//                    the globe's own Y-spin, which only moves longitude);
//   * DRAPE detail — the world's terrain map (_BaseMap) as a subtle surface
//                    texture multiplied into the holo colour, so Mars still
//                    reads different from the Moon;
//   * FLICKER      — a slight global brightness jitter for the "projection"
//                    feel. Comfort-safe: low amplitude, no geometry motion.
//
// Red-Matter-cheap: ONE transparent pass, Blend SrcAlpha One (additive, faded
// by an intensity mask so the centre genuinely reads transparent), ZWrite Off,
// one texture sample, a handful of ALU ops. No depth/normals passes (a
// hologram writes no depth). Mobile-XR friendly: no per-pixel loops, no
// dependent reads. Single-Pass Instanced stereo is honoured.
//
// PROPERTY CONTRACT: HoloGlobe.cs drives _BaseMap (per-world drape) and
// _BaseColor (HDR holo colour + per-world tint) via a MaterialPropertyBlock.
// Those two names MUST stay for the swap to be transparent to the C#.
// ============================================================================
Shader "Wayfinder/Holo"
{
    Properties
    {
        [MainTexture] _BaseMap("Drape (world map)", 2D) = "white" {}
        [HDR][MainColor] _BaseColor("Holo colour (HDR)", Color) = (0.40, 1.50, 2.10, 1)

        _RimPower("Rim power (edge tightness)", Range(0.5, 8)) = 2.5
        _RimStrength("Rim strength", Range(0, 4)) = 1.6
        _BaseFill("Centre fill (faint volume)", Range(0, 0.5)) = 0.06

        _ScanCount("Scanline count", Range(4, 200)) = 64
        _ScanSpeed("Scanline scroll speed", Range(0, 3)) = 0.55
        _ScanStrength("Scanline strength", Range(0, 1)) = 0.28

        _DrapeStrength("Drape detail strength", Range(0, 1)) = 0.35

        _FlickerStrength("Flicker strength", Range(0, 0.3)) = 0.06
        _FlickerSpeed("Flicker speed", Range(0, 30)) = 9

        // Bridge is static geometry; declared for material-inspector parity.
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "UniversalPipeline"
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "HoloAdditive"
            Tags { "LightMode" = "UniversalForward" }

            // Additive, faded by src alpha (our intensity mask) → transparent
            // centre, bright rim. No depth write (a hologram occludes nothing).
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex   HoloVert
            #pragma fragment HoloFrag
            #pragma target   3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _RimPower;
                half   _RimStrength;
                half   _BaseFill;
                half   _ScanCount;
                half   _ScanSpeed;
                half   _ScanStrength;
                half   _DrapeStrength;
                half   _FlickerStrength;
                half   _FlickerSpeed;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float hash11(float x) { return frac(sin(x * 12.9898) * 43758.5453); }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings HoloVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS  = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 HoloFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);

                // Fresnel rim: bright at grazing angles, dark facing us.
                half ndv  = saturate(dot(N, V));
                half rim  = pow(1.0h - ndv, _RimPower) * _RimStrength;

                // Latitude scanlines scrolling upward (stable under the Y-spin,
                // which only turns longitude). uv.y = latitude on a UV sphere.
                half scan = 0.5h + 0.5h * sin(input.uv.y * _ScanCount - _Time.y * _ScanSpeed * 6.2831853h);
                scan *= _ScanStrength;

                // Drape as subtle surface detail (luminance only — the colour is
                // the holo tint, not the map's real albedo).
                half3 drape = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half  detail = dot(drape, half3(0.299h, 0.587h, 0.114h)) * _DrapeStrength;

                // Projector flicker: one global brightness jitter, stepped in
                // time so it strobes like an unstable projection (subtle).
                half flick = 1.0h + _FlickerStrength *
                    (hash11(floor(_Time.y * _FlickerSpeed)) - 0.5h) * 2.0h;

                // Intensity mask → additive contribution. Centre = _BaseFill
                // (faint volume), edge = rim; scanlines + drape ride on top.
                half mask = saturate((_BaseFill + rim + scan + detail) * flick);

                half3 col = _BaseColor.rgb * mask;
                return half4(col, mask);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
