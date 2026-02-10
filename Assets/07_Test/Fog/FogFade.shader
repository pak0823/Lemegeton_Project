Shader "Custom/FogFade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
                // Sample the current state of the fog texture
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Subtract fade amount to slowly darken the cleared area back to fog
                // Assuming Clear = White(1), Fog = Black(0)
                // Use max() instead of saturate() to allow values > 1.0 for delayed fade
                col.r = max(0, col.r - _FadeAmount);
                col.g = max(0, col.g - _FadeAmount);
                col.b = max(0, col.b - _FadeAmount);
                
                return col;
            }
            ENDCG
        }
    }
}
