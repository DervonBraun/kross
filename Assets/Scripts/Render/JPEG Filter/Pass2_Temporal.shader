Shader "Hidden/Compression/Pass2_Temporal"
{
    Properties
    {
        // Значения по умолчанию, чтобы шейдер не ломался без скрипта
        _CompMotion ("Motion", Range(0, 1)) = 0
        _CompKeyframe ("Keyframe", Range(0, 1)) = 0
        _CompHistoryValid ("History Valid", Range(0, 1)) = 1

        _HistBlendMin ("Blend Min", Float) = 0.0
        _HistBlendMax ("Blend Max", Float) = 0.85 // Увеличил для более сильного эффекта "шлейфа"

        _HistBlockMin ("Block Min", Float) = 8
        _HistBlockMax ("Block Max", Float) = 64
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Pass2_Temporal"
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // В Pass 2 мы читаем текущий кадр (уже пожатый Pass 1) из _BlitTexture
            // А прошлый кадр из внешней текстуры _CompHistoryTex
            
            TEXTURE2D_X(_CompHistoryTex);
            SAMPLER(sampler_CompHistoryTex);

            float _CompMotion;
            float _CompKeyframe;
            float _CompHistoryValid;

            float _HistBlendMin;
            float _HistBlendMax;
            float _HistBlockMin;
            float _HistBlockMax;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            half4 Frag (Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 pix = uv * _ScreenParams.xy;

                float motion = saturate(_CompMotion);

                // --- ЛОГИКА СМЕШИВАНИЯ ---
                // Если Motion высокий -> blend высокий -> мы видим больше старого кадра (шлейф)
                // Если Keyframe (смена сцены) -> blend обнуляется
                float histBlend = lerp(_HistBlendMin, _HistBlendMax, motion);
                histBlend *= (1.0 - saturate(_CompKeyframe));
                histBlend *= saturate(_CompHistoryValid);

                // Защита от деления на ноль
                float blockSize = max(1.0, lerp(_HistBlockMin, _HistBlockMax, motion));

                // --- СЭМПЛИНГ ИСТОРИИ (Блочный) ---
                // Мы берем старый кадр "блоками", имитируя то, что кодек не обновил эти квадраты
                float2 blockPix = (floor(pix / blockSize) * blockSize) + (blockSize * 0.5);
                float2 uvBlock  = blockPix / _ScreenParams.xy;

                // Текущий кадр (из Pass 1) читаем честно
                float3 cur = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                
                // Прошлый кадр читаем блоками
                float3 hist = SAMPLE_TEXTURE2D_X(_CompHistoryTex, sampler_LinearClamp, uvBlock).rgb;

                // --- АРТЕФАКТЫ НА ГРАНИЦАХ БЛОКОВ ---
                // Вычисляем координаты внутри блока (0..1)
                float2 local = (pix - floor(pix / blockSize) * blockSize) / blockSize;
                // Рисуем "шум" на стыках блоков, как будто макроблоки не стыкуются
                float edge = smoothstep(0.40, 0.50, abs(local.x - 0.5)) + smoothstep(0.40, 0.50, abs(local.y - 0.5));
                edge = saturate(edge);

                float n = Hash21(floor(pix / blockSize));
                // Джиттер сдвигает цвета на границах блоков при сильном движении
                float jitter = (n - 0.5) * 0.15 * motion * edge; 
                hist = saturate(hist + jitter);

                // --- ФИНАЛ ---
                // Если движения нет, histBlend ~ 0, мы видим чистый Pass 1.
// Если движение есть, мы подмешиваем старые блоки.
                float3 outRgb = lerp(cur, hist, histBlend);
                
                return half4(outRgb, 1);
            }
            ENDHLSL
        }
    }
}