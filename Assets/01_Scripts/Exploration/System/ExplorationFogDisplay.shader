Shader "Custom/ExplorationFogDisplay"
{
    Properties
    {
        _MainTex ("Fog Mask Texture", 2D) = "black" {}
        _FogColor ("Fog Color", Color) = (0, 0, 0, 1)
        _FogSensitivity ("Fog Light Output", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _FogColor;
            float _FogSensitivity;

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                // R 채널의 값이 높을수록 밝음(투명함 = 빈 구멍)
                fixed4 maskCol = tex2D(_MainTex, i.uv);

                float clearAmount = saturate(maskCol.r * _FogSensitivity);

                // 안개는 반대로 투명한 곳이 투명해야 하므로 (1 - 밝기)를 투명도로 씀
                float fogAlpha = 1.0 - clearAmount;

                return fixed4(_FogColor.rgb, _FogColor.a * fogAlpha);
            }
            ENDCG
        }
    }
}
