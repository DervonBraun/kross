// Fullscreen hallucination (Unity 6.2 / URP 6.x)
// - Самодостаточный шейдер под Full Screen Pass Renderer Feature
// - Требования в Renderer Feature:
//   Fetch Color Buffer = ON
//   Bind  Color Buffer = ON
// - UV remap: авто через _BlitScaleBias, либо manual override


Shader "Custom/URP_Hallucination_Fullscreen"
{
    
    Properties
    {
        _Intensity ("Intensity", Range(0,2)) = 1
        _Warp      ("Warp", Range(0,1)) = 0.25
        _Jitter    ("Jitter", Range(0,2)) = 0.35
        _Chromatic ("Chromatic", Range(0,3)) = 1.0
        _Grain     ("Grain", Range(0,1)) = 0.25
        _NoiseScale("Noise Scale", Range(10,400)) = 120
        _TimeScale ("Time Scale", Range(0,5)) = 1.0
        _Pressure  ("Edge Pressure", Range(0,1)) = 0.35

        [Toggle] _FlipY ("Flip Y", Float) = 0

        // ----- Manual zoom override (как в твоём шейдере) -----
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
        Blend  One Zero

        Pass
        {
            Name "FullscreenHallucination"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Fullscreen triangle output
            struct VSOut {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            VSOut Vert(uint vertexID : SV_VertexID)
            {
                float2 uv = float2((vertexID << 1) & 2, (vertexID & 2));
                VSOut o;
                o.positionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
                o.uv = uv * 0.5f; // 0..1
                return o;
            }

            // Provided by Full Screen Pass / Renderer Feature
            Texture2D    _BlitTexture;
            SamplerState sampler_BlitTexture;

            // URP auto remap (scale.xy, bias.zw)
            float4 _BlitScaleBias;

            // Params
            float _Intensity, _Warp, _Jitter, _Chromatic, _Grain, _NoiseScale, _TimeScale, _Pressure;
            float _FlipY;
            float _UseManualZoom, _ManualScaleX, _ManualScaleY, _ManualBiasX, _ManualBiasY, _CenterBias;

            // --- helpers ---
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float2 ApplyAutoRemap(float2 uv)
            {
                // keep exactly your behavior
                if (any(_BlitScaleBias.xy) || any(_BlitScaleBias.zw))
                    uv = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return uv;
            }

            float2 ApplyManualRemap(float2 uv)
            {
                float2 scale = float2(_ManualScaleX, _ManualScaleY);
                float2 bias  = float2(_ManualBiasX,  _ManualBiasY);

                if (_CenterBias > 0.5f)
                    bias = (1.0 - scale) * 0.5;

                return uv * scale + bias;
            }

            float2 WarpUV(float2 uv, float t)
            {
                // "wrong lens" warp around center
                float2 p = uv * 2 - 1;
                float r2 = dot(p, p);

                float k = _Warp * _Intensity;

                // mixed-frequency wobble; intentionally not physically correct
                float wobble =
                    sin(t * 1.7 + r2 * 6.0) * 0.35 +
                    sin(t * 2.9 - p.x * 7.0) * 0.15 +
                    sin(t * 1.1 + p.y * 9.0) * 0.10;

                p *= (1 + k * (r2 + wobble));

                // micro jitter in view space
                float n = Hash21(uv * _NoiseScale + t);
                float2 j = (n - 0.5) * 0.0025 * _Jitter * _Intensity;
                p += j;

                return (p * 0.5 + 0.5);
            }

            float3 SampleRGBSplit(float2 uv, float t)
            {
                float ang = t * 0.35;
                float2 dir = float2(cos(ang), sin(ang));

                float amt = 0.0015 * _Chromatic * _Intensity;

                float2 uvR = uv + dir * amt;
                float2 uvG = uv;
                float2 uvB = uv - dir * amt;

                float r = _BlitTexture.Sample(sampler_BlitTexture, uvR).r;
                float g = _BlitTexture.Sample(sampler_BlitTexture, uvG).g;
                float b = _BlitTexture.Sample(sampler_BlitTexture, uvB).b;

                return float3(r,g,b);
            }

            float4 Frag(VSOut i) : SV_Target
            {
                float t = _TimeParameters.y * _TimeScale;

                float2 uv = i.uv;
                if (_FlipY > 0.5f) uv.y = 1.0f - uv.y;

                // remap like your shader (auto or manual)
                float2 uvSrc = (_UseManualZoom > 0.5f) ? ApplyManualRemap(uv)
                                                       : ApplyAutoRemap(uv);

                // warp AFTER remap (важно: чтобы зум/скейл не ломал эффект)
                float2 wuv = WarpUV(uvSrc, t);

                // sample with chromatic split
                float3 col = SampleRGBSplit(wuv, t);

                // grain/noise (subtle)
                float g = Hash21(uv * (_NoiseScale * 0.5) + t * 2.0);
                col += (g - 0.5) * (0.12 * _Grain) * _Intensity;

                // edge "pressure" (vignette-like, но слегка агрессивнее)
                float2 p = uv * 2 - 1;
                float v = saturate(1 - dot(p, p));     // 1 center -> 0 edges
                float pressure = lerp(1.0, 1.0 + _Pressure * _Intensity, (1 - v));
                col *= pressure;

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
