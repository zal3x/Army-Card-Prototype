Shader "Cards/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (1,0,0,1)
        _OutlineWidthPixels ("Outline Width (px)", Range(1, 8)) = 2

        _EffectAmount ("Effect Amount", Range(0,1)) = 1.0
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize; // x = 1/width, y = 1/height
            float4 _Color;
            float4 _OutlineColor;
            float _OutlineWidthPixels;
            float _EffectAmount;

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
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // If the main pixel is already opaque, pass through as-is
                if (col.a > 0.5)
                    return col;

                // Sample the 8 surrounding pixels at outline width offset
                float2 t = _MainTex_TexelSize.xy * _OutlineWidthPixels;

                float aL  = tex2D(_MainTex, i.uv + float2(-t.x,    0)).a;
                float aR  = tex2D(_MainTex, i.uv + float2( t.x,    0)).a;
                float aU  = tex2D(_MainTex, i.uv + float2(   0,  t.y)).a;
                float aD  = tex2D(_MainTex, i.uv + float2(   0, -t.y)).a;
                float aUL = tex2D(_MainTex, i.uv + float2(-t.x,  t.y)).a;
                float aUR = tex2D(_MainTex, i.uv + float2( t.x,  t.y)).a;
                float aDL = tex2D(_MainTex, i.uv + float2(-t.x, -t.y)).a;
                float aDR = tex2D(_MainTex, i.uv + float2( t.x, -t.y)).a;

                float maxNeighbor = max(max(max(aL, aR), max(aU, aD)),
                                       max(max(aUL, aUR), max(aDL, aDR)));

                if (maxNeighbor > 0.5)
                {
                    // Draw outline where a neighbor is inside the sprite but we are not
                    fixed4 outlineCol = _OutlineColor;
                    outlineCol.a *= _EffectAmount;
                    return outlineCol;
                }

                return fixed4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
