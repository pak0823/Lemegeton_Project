Shader "Custom/ExplorationFogLogic"
{
    Properties
    {
        _MainTex ("History", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        ZTest Always Cull Off ZWrite Off

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

            float4 _PlayerPos;
            float4 _MapSize;
            float4 _MapOrigin;
            float _ClearRadius;
            float _DarkenSpeed;
            float4 _UVMult;

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                // 이전 프레임의 안개 상태 샘플링 (RenderTexture Blit 연산용 UV 그대로 사용)
                // RenderTextureFormat.R8 を 사용하므로 r 채널에 값이 저장됨
                float historyVal = tex2D(_MainTex, i.uv).r;

                // 중심점을 0,0으로 맞춤 (-0.5 ~ 0.5)
                float2 uvCentered = i.uv - 0.5;

                // 방향 보정 (Manager에서 넘겨준 flip 적용)
                if (_UVMult.x < 0) uvCentered.x = -uvCentered.x;
                // Unity Plane의 기본 회전 특성 고려
                if (_UVMult.y < 0) uvCentered.y = -uvCentered.y;
                else uvCentered.y = -uvCentered.y; // Plane의 로컬 Z가 반대를 향하는 경우가 많음

                float2 mapWorldPos = _MapOrigin.xy + uvCentered * _MapSize.xy;

                // 플레이어와 거리 측정
                float dist = distance(mapWorldPos, _PlayerPos.xy);

                // 시야 반경 내에서 클리어 (1에 가까울수록 밝음/투명함)
                // 거리가 ClearRadius보다 작으면 1, 멀어질수록 0으로 떨어짐
                float currentClear = 1.0 - saturate(dist / _ClearRadius);

                // 이전 역사에서 프레임당 일정량(_DarkenSpeed)을 뺌 (서서히 어두워짐)
                float fadedHistory = saturate(historyVal - _DarkenSpeed);

                // 현재 플레이어 위치의 밝기(currentClear)와 서서히 지워지는 과거 밝기(fadedHistory) 중 최대값 유지
                float finalClear = max(fadedHistory, currentClear);

                // 단일 채널 R8 이므로 편의상 RGB 모두 할당
                return fixed4(finalClear, finalClear, finalClear, 1.0);
            }
            ENDCG
        }
    }
}
