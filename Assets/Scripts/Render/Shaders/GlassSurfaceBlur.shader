Shader "Kross/Surface/FrostedGlass_URP"
{
    Properties
    {
        // Look
        _Tint              ("Tint (RGB) Opacity (A)", Color) = (1,1,1,0.65)
        _SpecColor         ("Specular Color", Color) = (1,1,1,0.25)
        _Smoothness        ("Smoothness", Range(0,1)) = 0.75

        // Blur
        _BlurRadiusPx      ("Blur Radius (px)", Range(0, 32)) = 12
        _BlurStrength      ("Blur Strength", Range(0, 2)) = 1
        _Quality           ("Blur Quality (samples)", Range(1, 8)) = 4
        _SharpMix          ("Sharp Mix (0=full blur)", Range(0,1)) = 0.05

        // Distortion (frost/noise)
        _NoiseTex          ("Noise (R)", 2D) = "white" {}
        _NoiseScale        ("Noise Scale", Range(0.1, 20)) = 6
        _Distortion        ("Distortion", Range(0, 0.05)) = 0.015
        _DistortionSpeed   ("Distortion Speed", Range(0, 2)) = 0.15

        // Edge / Fresnel
        _FresnelPower      ("Fresnel Power", Range(0.5, 8)) = 3
        _FresnelStrength   ("Fresnel Strength", Range(0, 2)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "FrostedGlass"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _SpecColor;
                half _Smoothness;

                half _BlurRadiusPx;
                half _BlurStrength;
                half _Quality;
                half _SharpMix;

                half _NoiseScale;
                half _Distortion;
                half _DistortionSpeed;

                half _FresnelPower;
                half _FresnelStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS);

                o.positionCS = pos.positionCS;
                o.normalWS   = NormalizeNormalPerVertex(nrm.normalWS);

                float3 posWS = pos.positionWS;
                o.viewDirWS  = normalize(GetWorldSpaceViewDir(posWS));

                o.screenPos  = ComputeScreenPos(o.positionCS);
                o.uv         = v.uv;
                return o;
            }

            // Cheap-ish blur around UV using a circular-ish kernel
            half3 SampleBlurredScene(float2 uv, float2 pixelSize, half radiusPx, half quality)
            {
                // quality: 1..8 (number of rings-ish)
                // We'll do a small fixed pattern: center + 4/8 directions * steps
                half q = max(1.0h, quality);
                half r = radiusPx;

                half3 col = SampleSceneColor(uv);
                half wsum = 1.0h;

                // 8 directions
                const half2 dirs[8] = {
                    half2( 1, 0), half2(-1, 0), half2(0, 1), half2(0,-1),
                    half2( 1, 1), half2(-1, 1), half2(1,-1), half2(-1,-1)
                };

                // Steps depend on quality, but clamp for sanity
                int steps = (int)clamp(q, 1.0h, 8.0h);

                // simple gaussian-ish falloff
                for (int s = 1; s <= 8; s++)
                {
                    if (s > steps) break;

                    half t = (half)s / (half)steps;      // 0..1
                    half rr = r * t;

                    half w = exp(-t * t * 2.0h);          // cheap gaussian weight

                    float2 offBase = pixelSize * rr;

                    [unroll] for (int i = 0; i < 8; i++)
                    {
                        float2 o = offBase * dirs[i];
                        col += SampleSceneColor(uv + o) * w;
                        wsum += w;
                    }
                }

                return col / max(wsum, 1e-3h);
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Screen UV
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Pixel size in UV (approx)
                float2 pixelSize = 1.0 / _ScreenParams.xy; // zw = 1/width, 1/height in URP Core

                // Noise-driven distortion
                float2 nUV = i.uv * _NoiseScale + _Time.y * _DistortionSpeed;
                half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nUV).r; // 0..1
                half2 dist = (n - 0.5h) * 2.0h * _Distortion;

                float2 uvDistorted = screenUV + dist;

                // Blur
                half radius = _BlurRadiusPx * _BlurStrength;
                half3 blurred = (radius <= 0.001h)
                    ? SampleSceneColor(uvDistorted)
                    : SampleBlurredScene(uvDistorted, pixelSize, radius, _Quality);

                // Optional keep a bit of sharpness
                half3 sharp = SampleSceneColor(uvDistorted);
                half3 sceneCol = lerp(blurred, sharp, _SharpMix);

                // Fresnel edge
                half3 N = normalize(i.normalWS);
                half3 V = normalize(i.viewDirWS);
                half fresnel = pow(saturate(1.0h - dot(N, V)), _FresnelPower) * _FresnelStrength;

                // Tint + fake spec highlight (very simple)
                half3 base = sceneCol * _Tint.rgb;
                half3 spec = _SpecColor.rgb * fresnel * (0.25h + _Smoothness);

                half alpha = saturate(_Tint.a + fresnel * 0.25h);
                return half4(base + spec, alpha);
            }
            ENDHLSL
        }
    }
}
