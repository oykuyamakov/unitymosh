Shader "View/Split"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _SecTex ("Second Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _Radius ("Radius", Range(0, 1)) = 0.5
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            sampler2D _SecTex;
            float4 _SecTex_ST;
            
            sampler2D _MaskTex;
            float4 _MaskTex_ST;

            float _Radius;
            float4 _Center;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 col2 = tex2D(_SecTex, i.uv);
                fixed4 col3 = tex2D(_MaskTex, i.uv);
                
                float dist = distance(i.uv, _Center);
                float alpha = smoothstep(_Radius, _Radius + 0.01, dist);
                col3.a = alpha;

                fixed4 outColor = col * col3.a   + col2 * (1 - col3.a) ;
                
                return outColor;
            }
            ENDCG
        }
    }
}
