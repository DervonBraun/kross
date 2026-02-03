Shader "UI/LensRampPingPong"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Base Tint", Color) = (1,1,1,1)

        // Ramp: 1x256 (or 256x1)
        _RampTex ("Gradient Ramp (1x256)", 2D) = "white" {}
        _Intensity ("Intensity", Range(0,5)) = 1
        _BlendToGradient ("Blend To Gradient", Range(0,1)) = 1
        _Alpha ("Alpha", Range(0,1)) = 1

        // Gradient mapping
        _AngleDeg ("Angle (deg)", Range(-180,180)) = 0
        _Scale ("Gradient Scale", Range(0.1, 10)) = 1
        _BaseOffset ("Base Offset", Range(-2,2)) = 0

        // PingPong scroll
        _Speed ("PingPong Speed", Range(0, 10)) = 1
        _Range ("PingPong Range", Range(0, 1)) = 0.35
        _Center ("PingPong Center (0..1)", Range(0,1)) = 0.5

        // optional smoothing at turning points (0 = sharp, 1 = smooth-ish)
        _SmoothPingPong ("Smooth PingPong", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "UI"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            sampler2D _RampTex;

            float _Intensity;
            float _BlendToGradient;
            float _Alpha;

            float _AngleDeg;
            float _Scale;
            float _BaseOffset;

            float _Speed;
            float _Range;
            float _Center;
            float _SmoothPingPong;

            float2 Rotate(float2 v, float a)
            {
                float s = sin(a);
                float c = cos(a);
                return float2(c*v.x - s*v.y, s*v.x + c*v.y);
            }

            // PingPong in [0..1]
            float PingPong01(float t)
            {
                // classic triangle wave: 0..1..0
                float x = frac(t);
                float tri = 1.0 - abs(2.0 * x - 1.0);

                // optional smoothing at endpoints (very mild)
                // smoothstep-ish remap: tri' = lerp(tri, smoother(tri), k)
                float s = saturate(_SmoothPingPong);
                float smoothTri = tri * tri * (3.0 - 2.0 * tri); // smoothstep(0,1,tri)
                return lerp(tri, smoothTri, s);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPos = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                #ifdef UNITY_UI_CLIP_RECT
                if (UnityGet2DClipping(i.worldPos.xy, _ClipRect) < 0.5)
                    discard;
                #endif

                float4 baseCol = tex2D(_MainTex, i.uv) * i.color;
                float spriteAlpha = baseCol.a;

                if (spriteAlpha <= 0.001)
                    return 0;

                // Build base gradient coordinate from rotated UV
                float2 uv = i.uv;
                float2 centered = uv - 0.5;
                float2 ruv = Rotate(centered, radians(_AngleDeg));

                // coord grows along x after rotation
                float coord = (ruv.x * _Scale) + 0.5 + _BaseOffset;

                // PingPong scroll offset in [-Range..+Range] around Center
                float pp = PingPong01(_Time.y * _Speed); // 0..1..0
                float offset = (pp - 0.5) * 2.0 * _Range; // -Range..+Range

                // final ramp coordinate
                float t = coord + ( _Center - 0.5 ) + offset;

                // Clamp because ramp should not wrap (with Clamp wrap mode)
                t = saturate(t);

                // Sample ramp along X
                float3 grad = tex2D(_RampTex, float2(t, 0.5)).rgb * _Intensity;

                // Blend with original sprite color (optional)
                float3 rgb = lerp(baseCol.rgb, grad, _BlendToGradient);

                float a = spriteAlpha * _Alpha;
                return float4(rgb, a);
            }
            ENDHLSL
        }
    }
}
