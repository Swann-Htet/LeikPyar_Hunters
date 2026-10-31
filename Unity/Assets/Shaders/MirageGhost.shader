Shader "Lords/MirageGhost"
{
    // A ghost: a see-through body that glows brighter at the edges (like glass with a cold light inside). Unlit, so it shows in the dark too.
    Properties
    {
        [MainTexture] _BaseMap ("Detail picture", 2D) = "white" {}
        _Tint ("Body colour", Color) = (0.30, 0.52, 0.68, 1)
        _RimColor ("Edge colour", Color) = (0.55, 0.80, 0.90, 1)
        _RimPower ("Edge power (higher = thinner edge)", Range(0.5, 6)) = 2.4
        _Alpha ("Body see-through (0 = clear)", Range(0, 1)) = 0.10
        _RimAlpha ("Edge strength", Range(0, 1)) = 0.5
        _Glow ("Brightness", Range(0.2, 3)) = 0.7
        _Pulse ("Pulse", Range(0, 0.6)) = 0.08
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Draw which side (Back = the outside, Off = both: thin wings)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        // 1. Only the depth: the nearest surface of the body is found (no colour). Without it the back and the inside of the body
        //    (an arm behind the chest) draw over the front and the ghost flickers.
        Pass
        {
            Name "GhostDepth"
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings vertDepth(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 fragDepth(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // 2. The colour, on that nearest surface only
        Pass
        {
            Name "GhostForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half4 _RimColor;
                half _RimPower;
                half _Alpha;
                half _RimAlpha;
                half _Glow;
                half _Pulse;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positions.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                half fresnel = pow(1.0h - saturate(abs(dot(n, v))), _RimPower);

                half3 detail = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half shade = lerp(0.75h, 1.15h, dot(detail, half3(0.333h, 0.333h, 0.333h)));

                // a slow glow that climbs the body
                half pulse = 1.0h + _Pulse * sin(_Time.y * 2.0 + input.positionWS.y * 3.0);

                half3 body = _Tint.rgb * shade;
                half3 colour = lerp(body, _RimColor.rgb, fresnel) * pulse * _Glow;
                half alpha = saturate((_Alpha + fresnel * _RimAlpha) * pulse);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
