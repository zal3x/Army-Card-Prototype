Shader "Cards/Heatwave_Combined_V2"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _NoiseTex ("Noise (tiling)", 2D) = "gray" {}
        _DistortStrength ("Distort Strength", Range(0,0.15)) = 0.04
        _DistortSpeed ("Distort Speed", Range(0,6)) = 2.0
        _DistortTiling ("Distort Tiling", Range(0.5,40)) = 14.0

        _WaveStrength ("Wave Strength", Range(0,0.1)) = 0.02
        _WaveFreq ("Wave Frequency", Range(0.5,30)) = 10.0

        _TintColor ("Shimmer Tint", Color) = (0.4, 0.9, 1.0, 1)
        _TintAmount ("Tint Amount", Range(0,1)) = 0.25
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
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float4 _Color;

            float _DistortStrength;
            float _DistortSpeed;
            float _DistortTiling;

            float _WaveStrength;
            float _WaveFreq;

            float4 _TintColor;
            float _TintAmount;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

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
                float2 uv = i.uv;

                // two scrolling noise layers so motion is obvious
                float2 nUV1 = uv * _DistortTiling + float2(_Time.y, _Time.y * 0.73) * _DistortSpeed;
                float2 nUV2 = uv * (_DistortTiling * 1.7) + float2(-_Time.y * 0.6, _Time.y * 1.1) * _DistortSpeed;

                float n1 = tex2D(_NoiseTex, nUV1).r;
                float n2 = tex2D(_NoiseTex, nUV2).r;

                float2 noiseOffset = (float2(n1, n2) - 0.5) * 2.0 * _DistortStrength;

                // guaranteed visible “heat wobble”
                float wave = sin((uv.y + _Time.y * 0.7) * _WaveFreq) * _WaveStrength;

                float2 offset = noiseOffset + float2(wave, 0);

                fixed4 col = tex2D(_MainTex, uv + offset) * i.color;

                // shimmer tint on peaks
                float shimmer = saturate((n1 - 0.45) * 2.2) * col.a;
                col.rgb = lerp(col.rgb, col.rgb + _TintColor.rgb * shimmer, _TintAmount);

                return col;
            }
            ENDHLSL
        }
    }
}
