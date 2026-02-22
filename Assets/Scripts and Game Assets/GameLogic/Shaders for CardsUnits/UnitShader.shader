Shader "Custom/PixelMaskRecolor"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "black" {}

        _BodyColor ("Body Color", Color) = (1,1,1,1)
        _AccentColor ("Accent Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            float4 _BodyColor;
            float4 _AccentColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                // Respect transparency
                if (baseCol.a <= 0.001) return baseCol;

                fixed4 mask = tex2D(_MaskTex, i.uv);

                // Only recolor where mask marks it:
                // Red channel = body
                if (mask.r > 0.5)
                {
                    baseCol.rgb = _BodyColor.rgb;
                }
                // Green channel = accent
                else if (mask.g > 0.5)
                {
                    baseCol.rgb = _AccentColor.rgb;
                }

                return baseCol;
            }
            ENDCG
        }
    }
}
