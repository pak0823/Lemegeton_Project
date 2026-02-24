Shader "Custom/FogFade"
{
    Properties
    {
        _MainTex ("Accumulated Fog (History)", 2D) = "black" {}
        _CurrentTex ("Current Frame Fog", 2D) = "black" {}
        _FadeAmount ("Fade Amount", Range(0, 1)) = 0.005
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _CurrentTex;
            float4 _MainTex_ST;
            float _FadeAmount;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 누적된 이전 프레임의 안개 상태 샘플링
                fixed4 historyCol = tex2D(_MainTex, i.uv);

                // 현재 프레임의 안개 상태 샘플링 (현재 플레이어 위치 주변이 밝음)
                fixed4 currentCol = tex2D(_CurrentTex, i.uv);

                // 이전 기록을 서서히 어둡게 (안개가 다시 덮임)
                historyCol.r = max(0, historyCol.r - _FadeAmount);
                historyCol.g = max(0, historyCol.g - _FadeAmount);
                historyCol.b = max(0, historyCol.b - _FadeAmount);

                // 현재 상태와 서서히 지워지는 이전 기록 중 더 밝은 값을 최종 출력으로 유지
                fixed4 finalCol = max(historyCol, currentCol);

                return finalCol;
            }
            ENDCG
        }
    }
}
