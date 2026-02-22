Shader "Custom/PixelColorSwap"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}

        // Source colors to detect
        _Key1 ("Key Color 1", Color) = (0,1,0,1)      // green
        _Key2 ("Key Color 2", Color) = (1,0.5,0,1)    // orange
        _Key3 ("Key Color 3", Color) = (0,0,0,1)
        _Key4 ("Key Color 4", Color) = (0,0,0,1)

        // Replacement colors
        _Rep1 ("Replace 1", Color) = (0.76,0.69,0.50,1) // tan
        _Rep2 ("Replace 2", Color) = (0.35,0.22,0.10,1) // brown
        _Rep3 ("Replace 3", Color) = (1,1,1,1)
        _Rep4 ("Replace 4", Color) = (1,1,1,1)

        // How strict matching is (0 = exact match)
        _Tolerance ("Tolerance", Range(0, 0.2)) = 0.02

        // Keep black outlines untouched if they are “near black”
        _OutlineThreshold ("Outline Threshold", Range(0, 0.2)) = 0.05
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
            float4 _Key1, _Key2, _Key3, _Key4;
            float4 _Rep1, _Rep2, _Rep3, _Rep4;
            float _Tolerance;
            float _OutlineThreshold;

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
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // distance in RGB space
            float ColorDist(float3 a, float3 b) { return length(a - b); }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                if (c.a <= 0.001) return c;

                // Preserve outlines: anything “near black” stays as-is
                if (max(c.r, max(c.g, c.b)) < _OutlineThreshold)
                    return c;

                float3 rgb = c.rgb;

                if (ColorDist(rgb, _Key1.rgb) <= _Tolerance) c.rgb = _Rep1.rgb;
                else if (ColorDist(rgb, _Key2.rgb) <= _Tolerance) c.rgb = _Rep2.rgb;
                else if (ColorDist(rgb, _Key3.rgb) <= _Tolerance) c.rgb = _Rep3.rgb;
                else if (ColorDist(rgb, _Key4.rgb) <= _Tolerance) c.rgb = _Rep4.rgb;

                return c;
            }
            ENDCG
        }
    }
}
