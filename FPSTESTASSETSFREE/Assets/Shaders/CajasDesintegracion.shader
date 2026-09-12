Shader "Custom/CajasDesintegracion"
{
    Properties
    {
        _BaseMap("Textura", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        [HDR] _EdgeColor("Energia", Color) = (0,3,4,1)
        _Progress("Desintegracion", Range(0,1)) = 0
        _CellSize("Tamano fragmentos", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "Desintegracion"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EdgeColor;
                float _Progress;
                float _CellSize;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float2 uv : TEXCOORD1; float3 normalWS : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 cell = floor(input.positionOS / max(_CellSize, 0.001));
                float noise = frac(sin(dot(cell, float3(127.1, 311.7, 74.7))) * 43758.5453);
                float remaining = noise - lerp(-0.12, 1.12, _Progress);
                clip(remaining);
                float edge = 1.0 - smoothstep(0.0, 0.12, remaining);
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                float lighting = 0.4 + 0.6 * saturate(dot(normalize(input.normalWS), normalize(float3(0.4, 0.8, 0.3))));
                float flicker = 0.85 + 0.15 * sin(_Time.y * 65.0 + noise * 20.0);
                half3 energy = lerp(_EdgeColor.rgb, half3(5.0, 1.2, 0.1), step(0.8, noise));
                return half4(baseColor * lighting + energy * edge * flicker, 1);
            }
            ENDHLSL
        }
    }
}
