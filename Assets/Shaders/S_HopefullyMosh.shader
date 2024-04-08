Shader "Unlit/S_HopefullyMosh"
{
   Properties
    {
        _MainTex("", 2D) = ""{}
        _WorkTex("", 2D) = ""{}
        _DispTex("", 2D) = ""{}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    //Normal Tex
    sampler2D _WorkTex;
    float4 _WorkTex_TexelSize;
    //Tex that we fuck up
    sampler2D _DispTex;
    float4 _DispTex_TexelSize;

    //built in pipeline stores values as
    //red channel = x dir in screen uv space
    //green channel = y dir in screen uv space
    sampler2D_half _CameraMotionVectorsTexture;
    float4 _CameraMotionVectorsTexture_TexelSize;

    float _BlockSize;
    float _Quality;
    float _Contrast;
    float _Velocity;
    float _Diffusion;

    // PRNG
    float UVRandom(float2 uv)
    {
        float f = dot(float2(12.9898, 78.232), uv);
        return frac(43758.5453 * sin(f));
    }

    // Vertex shader for multi texturing
    struct v2f_multitex
    {
        float4 pos : SV_POSITION;
        float2 uv0 : TEXCOORD0;
        float2 uv1 : TEXCOORD1;
    };

    v2f_multitex vert_multitex(appdata_full v)
    {
        v2f_multitex o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv0 = v.texcoord.xy;
        o.uv1 = v.texcoord.xy;
    #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0.0)
            o.uv1.y = 1.0 - v.texcoord.y;
    #endif
        return o;
    }

    // Initialization 
    half4 frag_init(v2f_img i) : SV_Target
    {
        return 0;
    }

    // Displacement buffer updating 
    half4 frag_update(v2f_img i) : SV_Target
    {
        float2 uv = i.uv;
        float2 t0 = float2(_Time.y, 0);

        // Random numbers
        float3 rand = float3(
            UVRandom(uv + t0.xy),
            UVRandom(uv + t0.yx),
            UVRandom(uv.yx - t0.xx)
        );
        
        // Motion vector updated with velocity modifier
        half2 mv = tex2D(_CameraMotionVectorsTexture, uv).rg;
        mv *= _Velocity;

        mv = mv * _ScreenParams.xy;

        mv += (rand.xy - 0.5) * _Diffusion;
        mv = round(mv);

        half acc = tex2D(_MainTex, i.uv).a;
        half mv_len = length(mv);
        
        half acc_update = acc + min(mv_len, _BlockSize) * 0.005;
        acc_update += rand.z * lerp(-0.02, 0.02, _Quality);
        
        half acc_reset = rand.z * 0.5 + _Quality;
        // - Reset if the amount of motion is larger than the block size.
        acc = saturate(mv_len > _BlockSize ? acc_reset : acc_update);

        mv *= (_ScreenParams.zw - 1);

        half mrand = UVRandom(uv + mv_len);

        return half4(mv, mrand, acc);
    }

    // Moshing 
    half4 frag_mosh(v2f_multitex i) : SV_Target
    {
        // Color from the original image
        half4 src = tex2D(_MainTex, i.uv1);

        // Displacement vector (x, y, random, acc)
        half4 disp = tex2D(_DispTex, i.uv0);

        // Color from the working buffer (slightly scaled to make it blurred)
        half3 work = tex2D(_WorkTex, i.uv1 - disp.xy * 0.98).rgb;

        float4 rand = frac(float4(1, 17.37535, 821.4272, 3305.121) * disp.z);

        float2 uv = i.uv1 * _DispTex_TexelSize.zw * (rand.x * 80 / _Contrast);
        // - Basis wave (vertical or horizontal)
        float dct = cos(lerp(uv.x, uv.y, 0.5 < rand.y));
        dct *= rand.z * (1 - rand.x) * _Contrast;

        // Conditional weighting
        // - DCT-ish noise: acc > 0.5
        float cw = (disp.w > 0.5) * dct;
        // - Original image: rand < (Q * 0.8 + 0.2) && acc == 1.0
        cw = lerp(cw, 1, rand.w < lerp(0.2, 1, _Quality) * (disp.w > 0.999));
        // - If the conditions above are not met, choose work.

        return half4(lerp(work, src.rgb, cw), src.a);
    }

    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_init
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_update
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_multitex
            #pragma fragment frag_mosh
            #pragma target 3.0
            ENDCG
        }
    }
}
