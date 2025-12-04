Shader "Custom/ParallaxBlurUI"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurRadius ("Blur Radius", Range(0, 8)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv        : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float  _BlurRadius;

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                // taille d'un "step" en UV, multipliée par le slider
                float2 texel = _MainTex_TexelSize.xy * _BlurRadius;

                float4 col = 0;

                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-texel.x, -texel.y));
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-texel.x,  0.0));
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-texel.x,  texel.y));

                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( 0.0,    -texel.y));
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); // centre
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( 0.0,     texel.y));

                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( texel.x, -texel.y));
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( texel.x,  0.0));
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( texel.x,  texel.y));

                col /= 9.0;

                return col;
            }
            ENDHLSL
        }
    }
}
