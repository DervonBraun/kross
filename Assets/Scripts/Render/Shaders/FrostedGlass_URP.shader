Shader "Custom/FrostedGlass_URP"
{
    Properties
    {
        _BaseColor   ("Tint Color", Color) = (1,1,1,0.5)
        _MainTex     ("Albedo (optional)", 2D) = "white" {}
        _NormalMap   ("Normal Map (distortion)", 2D) = "bump" {}
        _RoughnessMap("Roughness (0..1 blur)", 2D) = "gray" {}
        _BlurRadius  ("Blur Radius (px)", Range(0, 6)) = 2
        _Distortion  ("Refraction Distortion", Range(0, 1)) = 0.2
        _Alpha       ("Master Opacity", Range(0, 1)) = 0.6
        _Cutoff      ("Alpha Cutoff (optional)", Range(0,1)) = 0.001
    }

    SubShader
    {
        Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline" }
        LOD 300
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Forward"
            Tags{ "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ HIGH_QUALITY_SAMPLES

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uvMain      : TEXCOORD0;
                float2 uvNorm      : TEXCOORD1;
                float2 uvRgh       : TEXCOORD2;
                float4 screenPos   : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float  _BlurRadius;
            float  _Distortion;
            float  _Alpha;
            float  _Cutoff;
            CBUFFER_END

            TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);     SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMap);  SAMPLER(sampler_RoughnessMap);

            float4 _MainTex_ST, _NormalMap_ST, _RoughnessMap_ST;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uvMain = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.uvNorm = TRANSFORM_TEX(IN.uv, _NormalMap);
                OUT.uvRgh  = TRANSFORM_TEX(IN.uv, _RoughnessMap);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            // Simple box blur sampling _CameraOpaqueTexture
            float3 SampleBlur(float2 uv, float blurPx)
            {
                float2 texel = _ScreenSize.zw; // 1/width, 1/height
                float2 r = blurPx * texel;

                #if defined(HIGH_QUALITY_SAMPLES)
                    const int K = 6;
                #else
                    const int K = 4;
                #endif

                float wSum = 0;
                float3 cSum = 0;

                [unroll]
                for (int x = -K; x <= K; x+=2)
                {
                    [unroll]
                    for (int y = -K; y <= K; y+=2)
                    {
                        float2 o = float2(x, y) * r;
                        cSum += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + o).rgb;
                        wSum += 1.0;
                    }
                }
                return cSum / max(wSum, 1e-3);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uvScreen = IN.screenPos.xy / IN.screenPos.w; // 0..1

                // Roughness controls blur amount
                float rough = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, IN.uvRgh).r;

                // Normal-based distortion (approx tangent-space)
                float3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uvNorm), 1.0);
                float2 distort = nTS.xy * _Distortion;

                float blurPx = _BlurRadius * lerp(0.25, 1.0, rough);
                float2 uvDistorted = uvScreen + distort * _ScreenSize.zw; // scale by 1/width,1/height

                float3 bg = SampleBlur(uvDistorted, blurPx);

                float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uvMain);
                float3 tinted = bg * _BaseColor.rgb;
                tinted = lerp(tinted, tinted * albedo.rgb, albedo.a);

                float a = saturate(_BaseColor.a * _Alpha);
                clip(a - _Cutoff);

                return float4(tinted, a);
            }
            ENDHLSL
        }
    }
}

// =============================
// 3) Optional: URP renderer feature reminder (not code)
//    If you need stronger/cheaper blur: implement a Renderer Feature with downsampled Kawase blur (two-pass)
//    and feed its blurred texture into this shader instead of _CameraOpaqueTexture. This file focuses on a
//    self-contained per-object approach to stay simple for drop-in use.
