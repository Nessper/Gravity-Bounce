Shader "Custom/PlayerInnerScan"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.0, 0.9, 1.0, 0.35)

        _ScanColor ("Scan Color", Color) = (1.0, 1.0, 1.0, 0.9)
        _ScanWidth ("Scan Width", Float) = 0.12
        _TrailWidth ("Trail Width", Float) = 0.28
        _ScanSpeed ("Scan Speed", Float) = 0.65

        _EdgeFade ("Edge Fade", Float) = 0.08
        _ScanEdgeFade ("Scan Edge Fade", Float) = 0.08

        _Alpha ("Alpha", Float) = 1.0
        _HitFlash ("Hit Flash", Float) = 0.0
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
                float4 _ScanColor;

                float _ScanWidth;
                float _TrailWidth;
                float _ScanSpeed;

                float _EdgeFade;
                float _ScanEdgeFade;

                float _Alpha;
                float _HitFlash;
            CBUFFER_END

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

                // Aller-retour type K2000.
                float phase = frac(_Time.y * _ScanSpeed);
                float pingpong = phase < 0.5
                    ? phase * 2.0
                    : (1.0 - phase) * 2.0;

                // Smoothstep adoucit l'arrivée et le départ aux bords.
                float scanPos = pingpong * pingpong * (3.0 - 2.0 * pingpong);

                // Retraction propre du scan sur les bords.
                // Independante du fade du fond.
                float edgeDistance = min(scanPos, 1.0 - scanPos);
                float edgeRetract = smoothstep(
                    0.0,
                    max(0.001, _ScanEdgeFade),
                    edgeDistance
                );

                float minRetract = 0.4;

                float retractAmount = lerp(
                    minRetract,
                    1.0,
                    edgeRetract
                );

                float dynamicScanWidth = _ScanWidth * retractAmount;
                float dynamicTrailWidth = _TrailWidth * retractAmount;

                float dist = abs(uv.x - scanPos);

                float head = 1.0 - smoothstep(
                    0.0,
                    max(0.001, dynamicScanWidth),
                    dist
                );

                float trail = 1.0 - smoothstep(
                    0.0,
                    max(0.001, dynamicTrailWidth),
                    dist
                );

                trail *= 0.35;

                float scan = saturate(head + trail);

                // Fade horizontal du fond uniquement.
                float leftFade = smoothstep(0.0, _EdgeFade, uv.x);
                float rightFade = smoothstep(0.0, _EdgeFade, 1.0 - uv.x);
                float edgeMask = leftFade * rightFade;

                float3 color = _BaseColor.rgb;
                color += _ScanColor.rgb * scan;

                float alpha = _BaseColor.a;
                alpha *= edgeMask;

                alpha += _ScanColor.a * scan;
                alpha *= _Alpha;

                color = lerp(color, float3(1.0, 1.0, 1.0), _HitFlash);
                alpha = saturate(alpha + _HitFlash * 0.8);

                return half4(color, saturate(alpha));
            }

            ENDHLSL
        }
    }
}