Shader "Custom/BoardEnergy"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.01, 0.08, 0.09, 0.45)

        _EdgeFade ("Edge Fade", Float) = 0.0
        _BottomFade ("Bottom Fade", Float) = 0.22
        _TopFade ("Top Fade", Float) = 0.0

        _NoiseStrength ("Noise Strength", Float) = 0.02
        _NoiseScale ("Noise Scale", Float) = 18.0
        _ScrollSpeed ("Scroll Speed", Float) = 0.05

        _PulseStrength ("Pulse Strength", Float) = 0.01
        _PulseSpeed ("Pulse Speed", Float) = 0.45

        _ScanlineStrength ("Scanline Strength", Float) = 0.025
        _ScanlineCount ("Scanline Count", Float) = 180.0

        _BandStrength ("Band Strength", Float) = 0.02
        _BandSpeed ("Band Speed", Float) = 0.12
        _BandFrequency ("Band Frequency", Float) = 18.0

        _Alpha ("Alpha", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;

                float _EdgeFade;
                float _BottomFade;
                float _TopFade;

                float _NoiseStrength;
                float _NoiseScale;
                float _ScrollSpeed;

                float _PulseStrength;
                float _PulseSpeed;

                float _ScanlineStrength;
                float _ScanlineCount;

                float _BandStrength;
                float _BandSpeed;
                float _BandFrequency;

                float _Alpha;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                float leftFade = (_EdgeFade <= 0.0001) ? 1.0 : smoothstep(0.0, _EdgeFade, uv.x);
                float rightFade = (_EdgeFade <= 0.0001) ? 1.0 : smoothstep(0.0, _EdgeFade, 1.0 - uv.x);
                float topFade = (_TopFade <= 0.0001) ? 1.0 : smoothstep(0.0, _TopFade, 1.0 - uv.y);
                float bottomFade = (_BottomFade <= 0.0001) ? 1.0 : smoothstep(0.0, _BottomFade, uv.y);

                float edgeMask = leftFade * rightFade * topFade * bottomFade;

                float n = noise(float2(
                    uv.x * _NoiseScale,
                    uv.y * _NoiseScale + _Time.y * _ScrollSpeed
                ));

                float noiseFactor = 1.0 + (n - 0.5) * _NoiseStrength;
                float pulseFactor = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;

                float scanline = sin(uv.y * _ScanlineCount + _Time.y * 0.8);
                scanline = scanline * 0.5 + 0.5;
                float scanlineFactor = 1.0 - scanline * _ScanlineStrength;

                float band = sin((uv.y + _Time.y * _BandSpeed) * _BandFrequency);
                band = smoothstep(0.92, 1.0, band);
                float bandFactor = 1.0 + band * _BandStrength;

                float alpha = _BaseColor.a * _Alpha;
                alpha *= edgeMask;
                alpha *= noiseFactor;
                alpha *= pulseFactor;
                alpha *= scanlineFactor;
                alpha *= bandFactor;

                return half4(_BaseColor.rgb, saturate(alpha));
            }

            ENDHLSL
        }
    }
}