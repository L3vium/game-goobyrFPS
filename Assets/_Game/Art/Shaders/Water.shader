// Author : Baptiste Vu Liem
// Water: clear in the shallows and deep blue-green further out (absorption from the scene depth),
// the bed seen through it bent by the waves (opaque texture), two layers of moving ripples
// computed in the shader, a sun glint, the sky in it at grazing angles, foam on the shores and
// around anything that breaks the surface, and a slow swell of the vertices.
Shader "Goobyr/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.32, 0.62, 0.6, 1)
        _DeepColor ("Deep", Color) = (0.04, 0.2, 0.27, 1)
        _SkyColor ("Sky Reflection", Color) = (0.62, 0.76, 0.86, 1)
        _FoamColor ("Foam", Color) = (0.92, 0.95, 0.95, 1)
        _Absorption ("Absorption (per metre)", Range(0.05, 2)) = 0.45
        _Clarity ("Clarity (alpha per metre)", Range(0.05, 3)) = 0.7
        _Refraction ("Refraction", Range(0, 0.1)) = 0.03
        _RippleScale ("Ripple Scale", Range(0.05, 2)) = 0.35
        _RippleStrength ("Ripple Strength", Range(0, 1.5)) = 0.55
        _RippleSpeed ("Ripple Speed", Range(0, 3)) = 0.6
        _Swell ("Swell Height", Range(0, 0.6)) = 0.06
        _FoamWidth ("Foam Width (m)", Range(0, 2)) = 0.6
        _Gloss ("Sun Glint Sharpness", Range(16, 2048)) = 600
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor;
                half _Absorption, _Clarity, _Refraction, _RippleScale, _RippleStrength, _RippleSpeed, _Swell, _FoamWidth, _Gloss;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screen : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // Value noise with its analytic gradient (xy) and value (z).
            float3 Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f), du = 6.0 * f * (1.0 - f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                float v = a + (b - a) * u.x + (c - a) * u.y + (a - b - c + d) * u.x * u.y;
                float2 g = du * (float2(b - a, c - a) + (a - b - c + d) * u.yx);
                return float3(g, v);
            }

            float2 RippleSlope(float2 xz)
            {
                float t = _Time.y * _RippleSpeed;
                float2 s = Noise(xz * _RippleScale + float2(t, t * 0.6)).xy;
                s += Noise(xz * _RippleScale * 2.3 + float2(-t * 0.8, t * 1.1)).xy * 0.5;
                s += Noise(xz * _RippleScale * 5.1 + float2(t * 1.7, -t * 1.3)).xy * 0.25;
                return s * _RippleStrength;
            }

            V Vert(A i)
            {
                V o;
                float3 p = TransformObjectToWorld(i.positionOS.xyz);
                float t = _Time.y;
                p.y += (sin(p.x * 0.11 + t * 0.9) + sin(p.z * 0.07 - t * 0.7) * 0.8 + sin((p.x + p.z) * 0.19 + t * 1.3) * 0.35) * _Swell;
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.screen = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(V i, bool pFront : SV_IsFrontFace) : SV_Target
            {
                float2 lSlope = RippleSlope(i.positionWS.xz);
                float3 n = normalize(float3(-lSlope.x, 1.0, -lSlope.y));
                if (!pFront) n = -n;
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                // How much water lies behind this pixel, along the view.
                float2 uv = i.screen.xy / i.screen.w;
                float lSurface = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float lScene = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float lDepth = max(0.0, lScene - lSurface);

                // The bed through the water, bent by the ripples (never pull in what is in front).
                float2 lBent = uv + n.xz * _Refraction * saturate(lDepth * 0.5);
                float lBentScene = LinearEyeDepth(SampleSceneDepth(lBent), _ZBufferParams);
                if (lBentScene < lSurface) lBent = uv;
                else lDepth = max(0.0, lBentScene - lSurface);
                half3 lBed = SampleSceneColor(lBent);

                half lAbsorb = 1.0 - exp(-lDepth * _Absorption);
                half3 lWater = lerp(_ShallowColor.rgb, _DeepColor.rgb, lAbsorb);
                half3 lColor = lerp(lBed * _ShallowColor.rgb * 1.15, lWater, saturate(lAbsorb * 1.1));

                Light lSun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half lShadow = lerp(0.55, 1.0, lSun.shadowAttenuation);
                lColor *= lerp(0.6, 1.0, saturate(dot(n, lSun.direction) * 0.5 + 0.5)) * lShadow;

                // Sky at grazing angles, sun glint.
                half lFresnel = pow(1.0 - saturate(dot(n, v)), 5.0) * 0.9 + 0.04;
                lColor = lerp(lColor, _SkyColor.rgb * (0.55 + 0.45 * lSun.color), lFresnel);
                float3 h = normalize(lSun.direction + v);
                lColor += lSun.color * pow(saturate(dot(n, h)), _Gloss) * 6.0 * lSun.shadowAttenuation;

                // Foam where the water thins out against the shore or anything standing in it.
                float lFoamNoise = Noise(i.positionWS.xz * 1.7 + _Time.y * 0.3).z;
                half lFoam = saturate(1.0 - lDepth / max(0.01, _FoamWidth)) * step(0.35, lFoamNoise + saturate(1.0 - lDepth / max(0.01, _FoamWidth * 0.4)));
                lColor = lerp(lColor, _FoamColor.rgb, lFoam * 0.85);

                half lAlpha = saturate(0.35 + (1.0 - exp(-lDepth * _Clarity)) * 0.65 + lFresnel * 0.4 + lFoam);
                lColor = MixFog(lColor, i.fog);
                return half4(lColor, lAlpha);
            }
            ENDHLSL
        }
    }
}
