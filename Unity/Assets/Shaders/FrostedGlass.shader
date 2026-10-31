// Frosted glass: the picture behind the glass is blurred and tinted (needs "Opaque Texture" on in the URP asset).
// It is used by the window glass material (House/tex/glass_MAT).
Shader "Lords/FrostedGlass"
{
    Properties
    {
        _Tint ("Glass tint (alpha = how strong)", Color) = (0.72, 0.84, 0.92, 0.35)
        _Blur ("Blur size", Range(0, 0.05)) = 0.014
        _Frost ("Frost (white haze)", Range(0, 1)) = 0.10
        _Shine ("Edge shine", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "FrostedGlass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Blur;
                half _Frost;
                half _Shine;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.screenPos = ComputeScreenPos(position.positionCS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(position.positionWS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float2 scale = float2(_ScreenParams.y / _ScreenParams.x, 1.0) * _Blur;

                // a ring of samples around the point: what is behind the glass, blurred
                half3 sum = SampleSceneColor(uv);
                for (int k = 0; k < 12; k++)
                {
                    float angle = k * 2.39996;                   // golden angle: the samples spread evenly
                    float radius = sqrt((k + 0.5) / 12.0);
                    sum += SampleSceneColor(uv + float2(cos(angle), sin(angle)) * radius * scale);
                }
                sum /= 13.0;

                half3 color = sum * lerp(half3(1, 1, 1), _Tint.rgb, _Tint.a) + _Frost * half3(0.8, 0.88, 0.95);

                // a little shine at the glancing angles
                half3 normal = normalize(input.normalWS);
                half3 view = normalize(input.viewDirWS);
                half fresnel = pow(1.0 - saturate(abs(dot(normal, view))), 3.0);
                color += _Shine * fresnel * half3(0.7, 0.8, 0.9);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
