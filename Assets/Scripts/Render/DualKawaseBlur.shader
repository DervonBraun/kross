Shader "Hidden/DualKawaseBlur"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BlitTexture);
        SAMPLER(sampler_BlitTexture);

        float4 _BlitTexture_TexelSize;
        float _Offset;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;

            // Full-screen triangle
            float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);

            // Flip Y for platforms that need it
            #if UNITY_UV_STARTS_AT_TOP
                output.uv = float2(uv.x, 1.0 - uv.y);
            #else
                output.uv = uv;
            #endif

            return output;
        }
        ENDHLSL

        // Pass 0: Downsample
        Pass
        {
            Name "Kawase Downsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsample

            float4 FragDownsample(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 halfPixel = _BlitTexture_TexelSize.xy * _Offset;

                // 5-tap downsample pattern (center + 4 corners)
                float4 sum = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv) * 4.0;
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(-halfPixel.x, -halfPixel.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2( halfPixel.x, -halfPixel.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(-halfPixel.x,  halfPixel.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2( halfPixel.x,  halfPixel.y));

                return sum / 8.0;
            }
            ENDHLSL
        }

        // Pass 1: Upsample
        Pass
        {
            Name "Kawase Upsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample

            float4 FragUpsample(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 halfPixel = _BlitTexture_TexelSize.xy * _Offset;

                // 8-tap upsample pattern (tent filter)
                float4 sum = 0;

                // Cardinal directions (weight 2 each)
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(-halfPixel.x, 0)) * 2.0;
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2( halfPixel.x, 0)) * 2.0;
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(0, -halfPixel.y)) * 2.0;
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(0,  halfPixel.y)) * 2.0;

                // Diagonal directions (weight 1 each)
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(-halfPixel.x, -halfPixel.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2( halfPixel.x, -halfPixel.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(-halfPixel.x,  halfPixel.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2( halfPixel.x,  halfPixel.y));

                return sum / 12.0;
            }
            ENDHLSL
        }
    }
}
