Shader "Cards/HoloFoil_Additive"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _FoilIntensity ("Foil Intensity", Range(0,4)) = 1.25
        _SheenIntensity ("Sheen Intensity", Range(0,4)) = 1.5
        _SheenWidth ("Sheen Width", Range(0.01, 1)) = 0.25
        _SheenSpeed ("Sheen Speed", Range(0, 10)) = 1.5
        _RainbowScale ("Rainbow Scale", Range(0.1, 10)) = 2.0

        _NoiseTex ("Noise (tiling)", 2D) = "gray" {}
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.35
        _NoiseScroll ("Noise Scroll (XY)", Vector) = (0.15, 0.10, 0, 0)

        _Alpha ("Overlay Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;

            float _FoilIntensity;
            float _SheenIntensity;
            float _SheenWidth;
            float _SheenSpeed;
            float _RainbowScale;

            float _NoiseStrength;
            float4 _NoiseScroll;

            float _Alpha;

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float2 uvNoise  : TEXCOORD1;
                float4 color    : COLOR;
            };

            // Cheap HSV->RGB-ish rainbow
            float3 Rainbow(float t)
            {
                t = frac(t);
                float3 c = saturate(abs(frac(t + float3(0, 2.0/3.0, 1.0/3.0)) * 6 - 3) - 1);
                return c * c * (3 - 2 * c); // smooth
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                float2 nUV = TRANSFORM_TEX(v.uv, _NoiseTex);
                nUV += _Time.y * _NoiseScroll.xy;
                o.uvNoise = nUV;

                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                // Use sprite alpha as mask
                float mask = baseCol.a;

                // Noise drives distortion + intensity variation
                float n = tex2D(_NoiseTex, i.uvNoise).r;
                float nCentered = (n - 0.5) * 2.0;

                // Rainbow varies across UVs + noise + time
                float rainbowT = (i.uv.x + i.uv.y) * _RainbowScale + _Time.y * 0.6 + nCentered * _NoiseStrength;
                float3 rainbow = Rainbow(rainbowT) * _FoilIntensity;

                // Moving sheen band across the card
                float sweep = (i.uv.x * 1.2 + i.uv.y * 0.6) + _Time.y * _SheenSpeed + nCentered * (_NoiseStrength * 0.5);
                float band = 1.0 - saturate(abs(frac(sweep) - 0.5) / max(1e-4, _SheenWidth));
                float sheen = pow(band, 2.0) * _SheenIntensity;

                float3 foil = (rainbow + sheen) * mask;

                // Additive overlay, keep alpha controllable
                return float4(foil, mask * _Alpha);
            }
            ENDHLSL
        }
    }
}
