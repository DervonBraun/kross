// Fullscreen pixelation with manual zoom control (Unity 6.2 / URP 6.x)
// - Самодостаточный шейдер для Full Screen Pass Renderer Feature.
// - Обязательно включи в фиче: Fetch Color Buffer = ON и Bind Color Buffer = ON.
// - Если авто-ремап не помогает, включи Use Manual Zoom и подстрой Scale/Bias вручную.
//   Подсказка: чтобы "отменить зум", включи Center Bias и уменьшай ScaleX/Y от 1.0 вниз,
//   bias автоматически будет (1 - scale) / 2 по каждой оси.

Shader "Custom/URP_PS1_Pixelate"
{
    Properties
    {
        _PixelsX    ("Pixels X", Float) = 320
        _PixelsY    ("Pixels Y", Float) = 240
        _ColorSteps ("Color Steps", Range(2,256)) = 16
        [Toggle] _FlipY ("Flip Y", Float) = 0

        // ----- Manual zoom override -----
        [Toggle] _UseManualZoom ("Use Manual Zoom", Float) = 0
        _ManualScaleX ("Manual Scale X", Range(0.25, 2.0)) = 1.0
        _ManualScaleY ("Manual Scale Y", Range(0.25, 2.0)) = 1.0
        _ManualBiasX  ("Manual Bias X",  Range(-1.0, 1.0)) = 0.0
        _ManualBiasY  ("Manual Bias Y",  Range(-1.0, 1.0)) = 0.0
        [Toggle] _CenterBias ("Center Bias (auto bias = (1-scale)/2)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline"
               "Queue"="Transparent" "RenderType"="Transparent" }

        ZWrite Off
        ZTest  Always
        Cull   Off
        Blend  One Zero // overwrite

        Pass
        {
            Name "FullscreenPixelate"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   Vert
            #pragma fragment Frag

            struct VSOut {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            // Процедурный полноэкранный треугольник
            VSOut Vert(uint vertexID : SV_VertexID)
            {
                float2 uv = float2((vertexID << 1) & 2, (vertexID & 2));
                VSOut o;
                o.positionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
                o.uv         = uv * 0.5f; // 0..1
                return o;
            }

            // Источник: подаётся Renderer Feature
            Texture2D    _BlitTexture;
            SamplerState sampler_BlitTexture;

            // URP может прокидывать авто-ремап (scale.xy, bias.zw)
            float4 _BlitScaleBias;

            // Параметры
            float _PixelsX, _PixelsY, _ColorSteps, _FlipY;
            float _UseManualZoom, _ManualScaleX, _ManualScaleY, _ManualBiasX, _ManualBiasY, _CenterBias;

            // --- helpers ---
            float3 QuantizeRGB(float3 c, float steps)
            {
                float s = max(steps, 2.0);
                float inv = 1.0 / (s - 1.0);
                return floor(c * (s - 1.0)) * inv;
            }

            float2 QuantizeUV(float2 uv, float2 grid)
            {
                return floor(uv * grid) / grid;
            }

            float2 ApplyAutoRemap(float2 uv)
            {
                if (any(_BlitScaleBias.xy) || any(_BlitScaleBias.zw))
                {
                    uv = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                }
                return uv;
            }

            float2 ApplyManualRemap(float2 uv)
            {
                float2 scale = float2(_ManualScaleX, _ManualScaleY);
                float2 bias  = float2(_ManualBiasX,  _ManualBiasY);

                if (_CenterBias > 0.5f)
                {
                    // центрируем автоматически
                    bias = (1.0 - scale) * 0.5;
                }

                return uv * scale + bias;
            }

            float4 Frag(VSOut i) : SV_Target
            {
                // Диагностика:
                // return float4(0,1,0,1);

                float2 uv = i.uv;
                if (_FlipY > 0.5f) uv.y = 1.0f - uv.y;

                // Ремап UV: либо авто (URP), либо ручной override
                float2 uvSrc = (_UseManualZoom > 0.5f) ? ApplyManualRemap(uv)
                                                       : ApplyAutoRemap(uv);

                // Квантование UV под виртуальную «низкую» сетку
                float2 grid = float2(max(_PixelsX, 2.0), max(_PixelsY, 2.0));
                float2 uvQ  = QuantizeUV(uvSrc, grid);

                float3 col = _BlitTexture.Sample(sampler_BlitTexture, uvQ).rgb;
                col = QuantizeRGB(col, _ColorSteps);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
