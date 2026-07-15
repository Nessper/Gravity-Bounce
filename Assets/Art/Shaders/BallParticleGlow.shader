Shader "404/Particles/Soft Glow"
{
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half distanceToCenter = length(input.uv - half2(0.5h, 0.5h)) * 2.0h;
                half softDisc = 1.0h - smoothstep(0.12h, 1.0h, distanceToCenter);
                half core = 1.0h - smoothstep(0.0h, 0.38h, distanceToCenter);
                half brightness = (softDisc + core * 1.45h) * 5.0h;

                return half4(
                    input.color.rgb * brightness,
                    input.color.a * softDisc
                );
            }
            ENDHLSL
        }
    }
}
