// Author : Baptiste Vu Liem
// Volumetric height fog with sun light shafts (see VolumetricFogFeature).
// Pass 0 marches the view ray at half resolution: rgb = in-scattered light, a = transmittance.
// Pass 1 composites it over the frame.
Shader "Hidden/Goobyr/VolumetricFog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        ENDHLSL

        Pass
        {
            Name "March"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _FogDensity, _FogHeight, _FogFalloff, _FogSunScatter, _FogAnisotropy, _FogMaxDistance, _FogSteps;
            float4 _FogAmbient, _FogNoise, _FogWind;

            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x), lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x), lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Density(float3 p)
            {
                float h = max(0.0, p.y - _FogHeight);
                float d = _FogDensity * exp2(-h / _FogFalloff);
                float3 q = p * _FogNoise.y + _FogWind.xyz * _Time.y * 0.08;
                float n = Noise3(q) * 0.6 + Noise3(q * 2.3) * 0.3 + Noise3(q * 5.1) * 0.1;
                return d * lerp(1.0, saturate(n * 1.8 - 0.25), _FogNoise.x);
            }

            float Phase(float c, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * PI * pow(abs(1.0 + g2 - 2.0 * g * c), 1.5));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                float3 worldEnd = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 origin = _WorldSpaceCameraPos;
                float3 ray = worldEnd - origin;
                float sceneDist = length(ray);
                float3 dir = ray / max(sceneDist, 1e-4);
                float dist = min(sceneDist, _FogMaxDistance);

                int steps = (int)_FogSteps;
                float stepLen = dist / steps;
                // Interleaved gradient noise jitter hides the banding.
                float jitter = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))) + _Time.y * 7.1);

                Light sun = GetMainLight();
                float phase = Phase(dot(dir, sun.direction), _FogAnisotropy) * 4.0 * PI;
                float3 sunLight = sun.color * _FogSunScatter;
                float3 ambient = _FogAmbient.rgb;

                float transmittance = 1.0;
                float3 scatter = 0;
                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float t = (i + jitter) * stepLen;
                    float3 p = origin + dir * t;
                    float d = Density(p);
                    if (d < 1e-5) continue;
                    float shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                    float3 lightHere = sunLight * shadow * phase + ambient;
                    float stepT = exp(-d * stepLen);
                    scatter += transmittance * lightHere * (1.0 - stepT);
                    transmittance *= stepT;
                    if (transmittance < 0.01) break;
                }
                return float4(scatter, transmittance);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_FogTex);
            SAMPLER(sampler_FogTex);
            float4 _FogTex_TexelSize;

            // Depth-aware upsample of the half-resolution fog: a 3x3 bilateral filter whose taps
            // only count where the scene sits at the same depth as this pixel. No halos around
            // foliage and railings, and the jittered march averages out into smooth shafts.
            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float centreDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float4 sum = 0;
                float weights = 0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 tapUv = uv + float2(x, y) * _FogTex_TexelSize.xy;
                        float tapDepth = LinearEyeDepth(SampleSceneDepth(tapUv), _ZBufferParams);
                        float w = (x == 0 && y == 0 ? 2.0 : 1.0) / (1e-3 + abs(tapDepth - centreDepth) / max(centreDepth, 0.1) * 40.0 + 0.02);
                        sum += SAMPLE_TEXTURE2D(_FogTex, sampler_LinearClamp, tapUv) * w;
                        weights += w;
                    }
                }
                float4 fog = sum / max(weights, 1e-4);
                return float4(scene.rgb * fog.a + fog.rgb, scene.a);
            }
            ENDHLSL
        }
    }
}
