Shader "404/FX/BlackSpawnDistortion"
{
    Properties
    {
        _Strength ("Strength", Range(0, 0.08)) = 0.025
        _Radius ("Radius", Range(0.01, 1)) = 0.45
        _Softness ("Softness", Range(0.01, 1)) = 0.35
        _Alpha ("Alpha", Range(0, 1)) = 1
        _NoiseScale ("Noise Scale", Range(1, 60)) = 18
        _NoiseStrength ("Noise Strength", Range(0, 0.05)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "BlackSpawnDistortion"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _Radius;
                float _Softness;
                float _Alpha;
                float _NoiseScale;
                float _NoiseStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            float randomNoise(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                float2 local = input.uv - 0.5;
                float dist = length(local);

                float outer = smoothstep(_Radius, _Radius - _Softness, dist);
                float inner = smoothstep(0.02, 0.18, dist);
                float mask = outer * inner * _Alpha;

                float2 dir = normalize(local + 0.0001);

                float noise = randomNoise(input.uv * _NoiseScale + _Time.yy);
                float noiseOffset = (noise - 0.5) * _NoiseStrength;

                float wave = sin((dist * 35.0) - (_Time.y * 18.0)) * 0.5 + 0.5;
                float strength = (_Strength + noiseOffset) * mask * wave;

                float2 distortedUV = screenUV + dir * strength;

                half4 sceneColor = SAMPLE_TEXTURE2D_X(
                    _CameraOpaqueTexture,
                    sampler_CameraOpaqueTexture,
                    distortedUV
                );

                sceneColor.a = mask * 0.35;

                return sceneColor;
            }

            ENDHLSL
        }
    }
}