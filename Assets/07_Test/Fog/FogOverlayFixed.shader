Shader "Custom/FogOverlayFixed"
{
    Properties
    {
        _FogTex ("Fog Mask Texture", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (0, 0, 0, 0.5)
        _FogSensitivity ("Fog Sensitivity", Float) = 1.0
    }
    SubShader
    {
        // 핵심 수정 1: 투명(Transparent) 큐 사용
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        
        // 핵심 수정 2: 알파 블렌딩 활성화
        // (SrcAlpha, OneMinusSrcAlpha) = 전형적인 투명도 합성 방식
        Blend SrcAlpha OneMinusSrcAlpha
        
        LOD 100
        ZWrite Off // 오버레이이므로 깊이 버퍼 쓰기 끔

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
            
            sampler2D _FogTex;
            float4 _FogTex_ST;
            
            fixed4 _FogColor;
            float _FogSensitivity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // FogTex의 Tiling/Offset 적용
                o.uv = TRANSFORM_TEX(v.uv, _FogTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 안개 마스크 값 샘플링 (R 채널)
                fixed4 maskCol = tex2D(_FogTex, i.uv);
                
                // 마스크 값이 클수록(밝을수록) => 안개가 걷힘(투명해야 함)
                // Sensitivity 적용 후 0~1로 Clamp (밝기 문제 해결)
                float clearAmount = saturate(maskCol.r * _FogSensitivity);
                
                // 안개 밀도(Alpha) 계산
                // clearAmount가 1이면(완전 걷힘) -> fogAlpha는 0 (완전 투명)
                // clearAmount가 0이면(안개) -> fogAlpha는 1 (불투명)
                float fogAlpha = 1.0 - clearAmount;
                
                // 디버깅: 만약 안개가 안 보인다면 아래 주석을 풀어보세요.
                // 1. 텍스처가 제대로 들어오는지 확인 (R채널 출력)
                // return fixed4(maskCol.r, 0, 0, 1); 

                // 2. Alpha 값 확인 (Alpha가 0이면 투명해서 안 보임)
                // return fixed4(fogAlpha, fogAlpha, fogAlpha, 1);

                return fixed4(_FogColor.rgb, _FogColor.a * fogAlpha);
            }
            ENDCG
        }
    }
}
