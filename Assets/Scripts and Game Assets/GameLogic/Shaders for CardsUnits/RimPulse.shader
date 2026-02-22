Shader "Cards/RimPulse_Combined_V2"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _RimColor ("Rim Color", Color) = (0.2, 0.8, 1.0, 1)
        _RimIntensity ("Rim Intensity", Range(0,8)) = 2.5
        _RimWidthPixels ("Rim Width (px)", Range(1,12)) = 3

        _PulseSpeed ("Pulse Speed", Range(0,10)) = 2.0
        _PulseAmount ("Pulse Amount", Range(0,1)) = 0.7

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

            sampler2D _MainTex; float4 _MainTex_ST;
            float4 _MainTex_TexelSize; // x=1/width, y=1/height
            float4 _Color;

            float4 _RimColor;
            float _RimIntensity;
            float _RimWidthPixels;
            float _PulseSpeed;
            float _PulseAmount;
            float _EffectAmount;

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
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                float a = col.a;

                float2 texel = _MainTex_TexelSize.xy * _RimWidthPixels;

                // Sample alpha around current pixel
                float aC = a;
                float aL = tex2D(_MainTex, i.uv - float2(texel.x, 0)).a;
                float aR = tex2D(_MainTex, i.uv + float2(texel.x, 0)).a;
                float aD = tex2D(_MainTex, i.uv - float2(0, texel.y)).a;
                float aU = tex2D(_MainTex, i.uv + float2(0, texel.y)).a;

                // Edge strength: where alpha changes
                float edge = (abs(aR - aL) + abs(aU - aD));

                // Make edge hug the silhouette (only where sprite exists)
                edge *= smoothstep(0.01, 0.25, aC);

                float pulse = lerp(1.0, 0.5 + 0.5 * sin(_Time.y * _PulseSpeed), _PulseAmount);

                col.rgb += _RimColor.rgb * edge * _RimIntensity * pulse * _EffectAmount;

                return col;
            }
            ENDHLSL
        }
    }
}
