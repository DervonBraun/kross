Shader "Hidden/Compression/Pass1_Jpegish"
{
    Properties
    {
        _CompMotion ("Motion", Range(0, 1)) = 0.25
        _BlockMin ("Block Min", Float) = 8
        _BlockMax ("Block Max", Float) = 48
        _LumaStepsMin ("Luma Steps Min", Float) = 64
        _LumaStepsMax ("Luma Steps Max", Float) = 10
        _ChromaStepsMin ("Chroma Steps Min", Float) = 48
        _ChromaStepsMax ("Chroma Steps Max", Float) = 6
        _ChromaGridMin ("Chroma Grid Min", Float) = 8
        _ChromaGridMax ("Chroma Grid Max", Float) = 64

        // --- NEW: virtual low-res (smooth) ---
        _LowResStrength ("Low Res Strength", Range(0, 1)) = 1
        _LowResScale ("Low Res Scale", Range(0.25, 1)) = 0.75
        _LowResChromaScale ("Low Res Chroma Scale", Range(0.25, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Pass1_Jpegish"
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _CompMotion;

            float _BlockMin;
            float _BlockMax;
            float _LumaStepsMin;
            float _LumaStepsMax;
            float _ChromaStepsMin;
            float _ChromaStepsMax;
            float _ChromaGridMin;
            float _ChromaGridMax;

            // --- NEW ---
            float _LowResStrength;
            float _LowResScale;
            float _LowResChromaScale;

            float3 RGB_to_YCbCr(float3 c)
            {
                float Y  = dot(c, float3(0.299, 0.587, 0.114));
                float Cb = (c.b - Y) * 0.564 + 0.5;
                float Cr = (c.r - Y) * 0.713 + 0.5;
                return float3(Y, Cb, Cr);
            }

            float3 YCbCr_to_RGB(float3 ycc)
            {
                float Y = ycc.x;
                float Cb = ycc.y - 0.5;
                float Cr = ycc.z - 0.5;

                float R = Y + 1.403 * Cr;
                float B = Y + 1.773 * Cb;
                float G = (Y - 0.344 * Cb - 0.714 * Cr);
                return float3(R, G, B);
            }

            float Quantize(float v, float steps)
            {
                steps = max(2.0, steps);
                return round(v * (steps - 1.0)) / (steps - 1.0);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // --- NEW: virtual low-res sampling (smooth) ---
            float3 SampleVirtualLowRes(float2 uv, float scale01)
            {
                // clamp to sane range
                float s = clamp(scale01, 0.25, 1.0);

                // target "sensor" resolution in pixels
                float2 lowSize = max(1.0, _ScreenParams.xy * s);

                // snap to low-res pixel centers
                float2 lowPix = floor(uv * lowSize) + 0.5;

                // back to UV in low-res grid
                float2 uvLow = lowPix / lowSize;

                // IMPORTANT: sampler_LinearClamp => bilinear upscale automatically
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvLow).rgb;
            }

            half4 Frag (Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;

                float blockMin = max(1.0, _BlockMin);
                float blockMax = max(1.0, _BlockMax);

                float motion = saturate(_CompMotion);
                float blockSize = lerp(blockMin, blockMax, motion);

                // pixel coords
                float2 pix = uv * _ScreenParams.xy;

                // block UV center (как было)
                float2 blockUVPix = (floor(pix / blockSize) * blockSize) + (blockSize * 0.5);
                float2 uvBlock = blockUVPix / _ScreenParams.xy;

                // chroma UV center (как было)
                float chromaGrid = lerp(max(1.0, _ChromaGridMin), _ChromaGridMax, motion);
                float2 chromaPix = (floor(pix / chromaGrid) * chromaGrid) + (chromaGrid * 0.5);
                float2 uvChroma = chromaPix / _ScreenParams.xy;

                // --- NEW: blend between original and virtual low-res ---
                float lowStrength = saturate(_LowResStrength);

                // Base (original) samples
                float3 rgbBase = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvBlock).rgb;
                float3 rgbChromaBase = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvChroma).rgb;

                // Virtual low-res samples (smooth)
                float3 rgbLow = SampleVirtualLowRes(uvBlock, _LowResScale);
                float3 rgbChromaLow = SampleVirtualLowRes(uvChroma, _LowResChromaScale);

                // Mix: low-res only when you want it
                float3 rgb = lerp(rgbBase, rgbLow, lowStrength);
                float3 rgbChroma = lerp(rgbChromaBase, rgbChromaLow, lowStrength);

                // Processing (как было)
                float3 ycc  = RGB_to_YCbCr(rgb);
                float3 yccC = RGB_to_YCbCr(rgbChroma);
                ycc.yz = yccC.yz;

                float lSteps = lerp(_LumaStepsMin, _LumaStepsMax, motion);
                float cSteps = lerp(_ChromaStepsMin, _ChromaStepsMax, motion);

                ycc.x = Quantize(ycc.x, lSteps);
                ycc.y = Quantize(ycc.y, cSteps);
                ycc.z = Quantize(ycc.z, cSteps);

                float n = Hash21(floor(pix / blockSize));
                float d = (n - 0.5) * (1.0 / max(1.0, lSteps));
                ycc.x = saturate(ycc.x + d);

                float3 outRgb = saturate(YCbCr_to_RGB(ycc));
                return half4(outRgb, 1);
            }
            ENDHLSL
        }
    }
}
