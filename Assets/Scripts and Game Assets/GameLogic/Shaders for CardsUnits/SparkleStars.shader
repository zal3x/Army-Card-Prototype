Shader "Cards/SparkleStars_Combined"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _SparkleColor ("Sparkle Color", Color) = (1,1,1,1)
        _SparkleIntensity ("Sparkle Intensity", Range(0,4)) = 1.5
        _SparkleAmount ("Sparkle Amount", Range(0,1)) = 0.5

        _Density ("Star Density", Range(10, 400)) = 120
        _StarSize ("Star Size", Range(0.001, 0.05)) = 0.012
        _TwinkleSpeed ("Twinkle Speed", Range(0, 12)) = 5.0
        _TwinkleSharpness ("Twinkle Sharpness", Range(1, 12)) = 6.0

        _Scroll ("UV Scroll (XY)", Vector) = (0.02, 0.01, 0, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            float4 _Color;

            float4 _SparkleColor;
            float _SparkleIntensity;
            float _SparkleAmount;

            float _Density;
            float _StarSize;
            float _TwinkleSpeed;
            float _TwinkleSharpness;
            float4 _Scroll;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

            // Hash functions for pseudo-random
            float hash12(float2 p)
            {
                float3 p3  = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 hash22(float2 p)
            {
                float n = hash12(p);
                return float2(n, hash12(p + n));
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;
                float mask = baseCol.a;

                // Grid space for star placement
                float2 uv = i.uv + _Time.y * _Scroll.xy;
                float2 p = uv * _Density;

                float2 cell = floor(p);
                float2 f = frac(p);

                // Random star center per cell
                float2 rnd = hash22(cell);
                float2 starPos = rnd; // 0..1 inside cell

                float2 d = f - starPos;
                float dist = length(d);

                // Star core
                float core = 1.0 - smoothstep(_StarSize, _StarSize * 1.8, dist);

                // Simple 4-point star sparkle (cross)
                float cross = max(0, 1.0 - abs(d.x) / (_StarSize * 0.9)) + max(0, 1.0 - abs(d.y) / (_StarSize * 0.9));
                cross = saturate(cross - 1.0); // keep it from blowing up

                float starShape = saturate(core + cross);

                // Twinkle per-cell
                float tw = 0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed + rnd.x * 6.2831 + rnd.y * 9.123);
                tw = pow(tw, _TwinkleSharpness);

                float sparkle = starShape * tw;

                float3 add = _SparkleColor.rgb * sparkle * _SparkleIntensity;

                // Blend sparkle into base
                baseCol.rgb += add * (_SparkleAmount * mask);
                return baseCol;
            }
            ENDHLSL
        }
    }
}
