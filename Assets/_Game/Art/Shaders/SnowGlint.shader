// Author : Baptiste Vu Liem
// Sparkles on snow: one tiny camera-facing star per ice crystal (instanced quads from FX/SnowGlints).
// Each crystal has its own facet normal (the instance's up axis); it flashes only when that facet
// mirrors the sun into the eye, so the snow twinkles as you move. Additive HDR, caught by the bloom.
Shader "Goobyr/SnowGlint"
{
    Properties
    {
        _Size ("Size (m)", Float) = 0.06
        _Sharpness ("Facet sharpness", Float) = 200
        _Intensity ("Intensity", Float) = 60
        _FadeFar ("Fade out distance (m)", Float) = 45
        [HDR] _Tint ("Tint", Color) = (0.92, 0.96, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Glint"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Size;
                float _Sharpness;
                float _Intensity;
                float _FadeFar;
                half4 _Tint;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half glint : TEXCOORD1; };

            V Vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                float3 lCenter = TransformObjectToWorld(float3(0, 0, 0));
                float3 lFacet = normalize(mul((float3x3)UNITY_MATRIX_M, float3(0, 1, 0)));
                float3 lToEye = _WorldSpaceCameraPos - lCenter;
                float lDistance = length(lToEye);
                float3 lView = lToEye / max(lDistance, 1e-4);

                // No sparkle in shade: the sun's shadow at the crystal (sampled per vertex, a quad is a point).
                Light lSun = GetMainLight(TransformWorldToShadowCoord(lCenter));
                float3 lHalf = normalize(lSun.direction + lView);
                float lSpecular = pow(saturate(dot(lFacet, lHalf)), _Sharpness);
                float lSunUp = saturate(lSun.direction.y * 4.0);
                float lFade = saturate(lDistance - 0.8) * saturate((_FadeFar - lDistance) / (_FadeFar * 0.35));
                half lGlint = lSpecular * lFade * lSunUp * lSun.shadowAttenuation * _Intensity * dot(lSun.color, half3(0.3, 0.59, 0.11));
                o.glint = lGlint;

                // Camera-facing, a little bigger far away so a distant crystal stays about a pixel; dim ones collapse to nothing.
                float3 lRight = UNITY_MATRIX_V[0].xyz;
                float3 lUp = UNITY_MATRIX_V[1].xyz;
                float lSize = _Size * (1.0 + lDistance * 0.05) * step(0.01, lGlint);
                float3 lPosition = lCenter + (lRight * i.positionOS.x + lUp * i.positionOS.y) * lSize;
                o.positionCS = TransformWorldToHClip(lPosition);
                o.uv = i.uv;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                float lCore = saturate(1.0 - length(d) * 2.4);
                float lStar = saturate(1.0 - abs(d.x) * 10.0) * saturate(1.0 - abs(d.y)) + saturate(1.0 - abs(d.y) * 10.0) * saturate(1.0 - abs(d.x));
                half lShape = lCore * lCore + lStar * 0.3;
                return half4(_Tint.rgb * lShape * i.glint, 0);
            }
            ENDHLSL
        }
    }
}
