Shader "Blur/Object"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (1, 1, 1, 0.5)
        _Intensity ("Blur Intensity", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "BlurObject"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_GlobalBlurTexture);
            SAMPLER(sampler_GlobalBlurTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                half4 blurred = SAMPLE_TEXTURE2D(_GlobalBlurTexture, sampler_GlobalBlurTexture, screenUV);

                half4 color;
                color.rgb = lerp(half3(0, 0, 0), blurred.rgb, _Intensity) * _TintColor.rgb;
                color.a = _TintColor.a;

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
