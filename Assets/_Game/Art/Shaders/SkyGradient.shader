// Author : Baptiste Vu Liem
// Soft three-colour sky with a sun disc and drifting cloud bands: the pastel backdrop of every map.
Shader "Goobyr/SkyGradient"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.45, 0.78, 0.9, 1)
        _HorizonColor ("Horizon", Color) = (0.85, 0.95, 0.95, 1)
        _BottomColor ("Bottom", Color) = (0.7, 0.8, 0.85, 1)
        _SunColor ("Sun", Color) = (1, 0.97, 0.85, 1)
        _SunSize ("Sun Size", Range(0.001, 0.2)) = 0.04
        _Exponent ("Gradient Exponent", Range(0.2, 4)) = 1.2
        _CloudColor ("Cloud", Color) = (1, 1, 1, 0.5)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 _TopColor, _HorizonColor, _BottomColor, _SunColor, _CloudColor;
            half _SunSize, _Exponent;

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            V Vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 Frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = d.y;
                half3 col = up > 0 ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(up), 1 / _Exponent)) : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-up * 4));

                Light sun = GetMainLight();
                float s = dot(d, sun.direction);
                col += _SunColor.rgb * smoothstep(1 - _SunSize, 1 - _SunSize * 0.7, s) * 1.6;
                col += _SunColor.rgb * pow(saturate(s), 24) * 0.25;

                float2 uv = d.xz / max(0.08, up + 0.25) * 1.6 + _Time.x * float2(0.6, 0.2);
                float n = Noise(uv) * 0.6 + Noise(uv * 2.3) * 0.3 + Noise(uv * 5.1) * 0.1;
                float cloud = smoothstep(0.55, 0.7, n) * saturate(up * 4) * _CloudColor.a;
                col = lerp(col, _CloudColor.rgb, cloud);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
