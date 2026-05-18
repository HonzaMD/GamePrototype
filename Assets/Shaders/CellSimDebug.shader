// Debug overlay pro CellSim veličiny. HDRP unlit, transparent, point-sampled.
// Vstup: R8 textura W×H, raw sbyte hodnota (-128..127) zakódovaná jako byte.
// Kreslí barevnou tečku per buňka: záporné/0/kladné odlišeno barvou (hue),
// magnituda jasem (lineární / log / sqrt dle _ScaleMode).
// Renderováno přes Graphics.DrawMesh z CellSimDebug.cs (per-channel MaterialPropertyBlock).
Shader "Hidden/CellSimDebug"
{
    Properties
    {
        _MainTex ("Values (R8)", 2D) = "black" {}
        _GridW ("Grid Width", Float) = 1
        _GridH ("Grid Height", Float) = 1
        _NegColor ("Negative", Color) = (0.25, 0.55, 1, 1)
        _ZeroColor ("Zero", Color) = (0.15, 0.15, 0.15, 0)
        _PosColor ("Positive", Color) = (1, 0.55, 0.1, 1)
        _FullScale ("Full Scale Value", Float) = 16
        _ScaleMode ("Scale Mode (0 lin,1 log,2 sqrt)", Float) = 1
        _DotX ("Dot Offset X", Float) = 0.5
        _DotY ("Dot Offset Y", Float) = 0.5
        _DotR ("Dot Radius", Float) = 0.4
        _GlobalAlpha ("Global Alpha", Float) = 0.85
        _TestMode ("Test Pattern (0 off, 1 on)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            // Mimo CBUFFER kvůli MaterialPropertyBlock override (vypíná SRP batcher — pro debug OK).
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _GridW, _GridH, _FullScale, _ScaleMode;
            float _DotX, _DotY, _DotR, _GlobalAlpha, _TestMode;
            float4 _NegColor, _ZeroColor, _PosColor;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes att)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(att);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionRWS = TransformObjectToWorld(att.positionOS);
                o.positionCS = TransformWorldToHClip(positionRWS);
                o.uv = att.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 grid = float2(_GridW, _GridH);
                float2 cell = i.uv * grid;
                float2 cellUV = frac(cell);

                // Point sample středu odpovídajícího texelu (buňky).
                float2 sampUV = (floor(cell) + 0.5) / grid;
                float raw = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampUV).r * 255.0;
                raw = floor(raw + 0.5);
                float v = raw < 128.0 ? raw : raw - 256.0;   // sbyte reinterpret

                // Test pattern: ignoruj data, syntetizuj hodnotu z pozice buňky.
                // Každá buňka dostane tečku (i při samých nulách v simulaci) —
                // ověří render pipeline (shader/HDRP/matice/Z/quad) nezávisle na datech.
                if (_TestMode > 0.5)
                {
                    float2 c = floor(cell);
                    v = (fmod(c.x + c.y, 32.0)) - 16.0;   // rozsah -16..15, střídá +/-
                }

                clip(abs(v) - 0.5);   // v == 0 → nic

                // Magnituda → 0..1 dle škály.
                float a = abs(v);
                float fs = max(_FullScale, 1e-4);
                float t;
                if (_ScaleMode < 0.5)       t = a / fs;                          // lineární
                else if (_ScaleMode < 1.5)  t = log(1.0 + a) / log(1.0 + fs);    // log
                else                        t = sqrt(a / fs);                    // sqrt
                t = saturate(t);

                float4 col = v > 0.0 ? lerp(_ZeroColor, _PosColor, t)
                                     : lerp(_ZeroColor, _NegColor, t);

                // Kruhová tečka uvnitř buňky.
                float dist = length(cellUV - float2(_DotX, _DotY));
                float mask = 1.0 - smoothstep(_DotR - 0.04, _DotR, dist);
                clip(mask - 0.001);

                col.a *= mask * _GlobalAlpha;
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
