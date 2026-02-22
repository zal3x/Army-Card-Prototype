Shader "Cards/GoldGlitter_Additive"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _GlitterTex ("Glitter (tiling)", 2D) = "gray" {}
        _GlitterTiling ("Glitter Tiling", Range(1, 300)) = 120
        _TwinkleSpeed ("Twinkle Speed", Range(0, 20)) = 6
        _Threshold ("Spark Threshold", Range(0, 1)) = 0.82
        _Intensity ("Intensity", Range(0, 6)) = 2.0

        _Gold ("Gold Color", Color) = (1.0, 0.85, 0.25, 1)
        _Alpha ("Overlay Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _GlitterTex;

            float4 _Color;
            float4 _Gold;

            float _GlitterTiling;
            float _TwinkleSpeed;
            float _Threshold;
            float _Intensity;
            float _Alpha;

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
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;
                float mask = baseCol.a;

                float2 gUV = i.uv * _GlitterTiling;
                float g = tex2D(_GlitterTex, frac(gUV)).r;

                // Twinkle: modulate threshold over time for “sparkle pop”
                float tw = 0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed + (gUV.x + gUV.y) * 0.25);
                float spark = step(_Threshold, g * tw);

                float3 col = _Gold.rgb * spark * _Intensity * mask;

                return float4(col, mask * _Alpha);
            }
            ENDHLSL
        }
    }
}
