Shader "Cards/HoloFoil_Soft"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _FoilAmount ("Foil Amount", Range(0,1)) = 0.55
        _FoilBrightness ("Foil Brightness", Range(0,3)) = 1.2
        _SheenWidth ("Sheen Width", Range(0.01, 1)) = 0.22
        _SheenSpeed ("Sheen Speed", Range(0, 10)) = 1.2

        _NoiseTex ("Noise (tiling)", 2D) = "gray" {}
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.35
        _NoiseScroll ("Noise Scroll (XY)", Vector) = (0.12, 0.08, 0, 0)
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
            sampler2D _NoiseTex; float4 _NoiseTex_ST;
            float4 _Color;

            float _FoilAmount;
            float _FoilBrightness;
            float _SheenWidth;
            float _SheenSpeed;
            float _NoiseStrength;
            float4 _NoiseScroll;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float2 uvN:TEXCOORD1; float4 color:COLOR; };

            float3 Rainbow(float t)
            {
                t = frac(t);
                float3 c = saturate(abs(frac(t + float3(0, 2.0/3.0, 1.0/3.0)) * 6 - 3) - 1);
                return c * c * (3 - 2 * c);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                float2 nUV = TRANSFORM_TEX(v.uv, _NoiseTex);
                nUV += _Time.y * _NoiseScroll.xy;
                o.uvN = nUV;

                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                float mask = baseCol.a;
                float n = tex2D(_NoiseTex, i.uvN).r;
                float nCentered = (n - 0.5) * 2.0;

                float t = (i.uv.x * 1.1 + i.uv.y * 0.9) + _Time.y * 0.55 + nCentered * _NoiseStrength;
                float3 holo = Rainbow(t) * _FoilBrightness;

                float sweep = (i.uv.x + i.uv.y * 0.5) + _Time.y * _SheenSpeed;
                float band = 1.0 - saturate(abs(frac(sweep) - 0.5) / max(1e-4, _SheenWidth));
                float sheen = pow(band, 3.0);

                float3 mixed = lerp(baseCol.rgb, baseCol.rgb + holo + sheen, _FoilAmount * mask);

                return float4(mixed, baseCol.a);
            }
            ENDHLSL
        }
    }
}
