Shader "UI/DiagonalFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillAmount ("Fill Amount", Range(0,1)) = 1
        _Angle ("Fill Angle", Range(0,360)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001,0.3)) = 0.05
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _FillAmount;
            float _Angle;
            float _EdgeSoftness;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color * _Color;

                float rad = radians(_Angle);
                float2 dir = float2(cos(rad), sin(rad));

                // UV 중심(0.5,0.5) 기준으로 회전된 방향의 진행도 계산 (0~1로 재정규화)
                float edge = dot(i.uv - 0.5, dir) + 0.5;

                float alpha = smoothstep(_FillAmount + _EdgeSoftness,
                                          _FillAmount - _EdgeSoftness, edge);

                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
}