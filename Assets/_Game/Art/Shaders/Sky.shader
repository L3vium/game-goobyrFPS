// Author : Baptiste Vu Liem
// The Goobyr sky: an atmospheric gradient (zenith, horizon haze, ground), a sun with a soft
// halo, and a layer of volumetric clouds - a slab between two altitudes raymarched through
// layered noise, lit toward the sun (silver lining, dark bellies), drifting with the wind.
Shader "Goobyr/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.28, 0.5, 0.82, 1)
        _HorizonColor ("Horizon", Color) = (0.82, 0.88, 0.92, 1)
        _GroundColor ("Ground", Color) = (0.5, 0.52, 0.55, 1)
        _HazeColor ("Haze near sun", Color) = (1, 0.86, 0.65, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.85, 1)
        _SunSize ("Sun Size", Range(0.0005, 0.05)) = 0.006
        _SunHalo ("Sun Halo", Range(0, 4)) = 1.2
        _Exponent ("Gradient Exponent", Range(0.2, 4)) = 0.9

        _CloudColor ("Cloud Lit", Color) = (1, 0.98, 0.95, 1)
        _CloudShade ("Cloud Shadow", Color) = (0.58, 0.64, 0.74, 1)
        _CloudCoverage ("Coverage", Range(0, 1)) = 0.48
        _CloudDensity ("Density", Range(0, 4)) = 1.4
        _CloudScale ("Scale", Range(0.05, 3)) = 0.55
        _CloudSpeed ("Speed", Range(0, 2)) = 0.25
        _CloudBottom ("Layer Bottom", Float) = 1.0
        _CloudTop ("Layer Top", Float) = 1.6
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

            half4 _ZenithColor, _HorizonColor, _GroundColor, _HazeColor, _SunColor, _CloudColor, _CloudShade;
            half _SunSize, _SunHalo, _Exponent, _CloudCoverage, _CloudDensity, _CloudScale, _CloudSpeed, _CloudBottom, _CloudTop;

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            V Vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float v = 0, a = 0.5;
                for (int i = 0; i < 5; i++) { v += a * Noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            // Cloud density at a point of the slab (layer coordinates, units of ~1 km).
            float CloudDensity(float3 p)
            {
                float h = saturate((p.y - _CloudBottom) / (_CloudTop - _CloudBottom));
                float shape = smoothstep(0.0, 0.25, h) * smoothstep(1.0, 0.55, h);
                float3 q = p * _CloudScale + float3(_Time.y * _CloudSpeed * 0.05, 0, _Time.y * _CloudSpeed * 0.02);
                float base = Fbm(q);
                float detail = Noise(q * 6.0 + 3.1);
                float d = saturate((base - (1.0 - _CloudCoverage)) * 3.0) * shape;
                d = saturate(d - detail * 0.18 * (1.0 - d));
                return d * _CloudDensity;
            }

            half4 Frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                Light sun = GetMainLight();
                float up = d.y;
                float sunDot = dot(d, sun.direction);

                // Gradient with a warm haze around the sun near the horizon.
                half3 sky = up > 0 ? lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(up), _Exponent)) : lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-up * 5));
                float haze = pow(saturate(sunDot * 0.5 + 0.5), 6) * (1 - saturate(up * 1.5));
                sky = lerp(sky, _HazeColor.rgb, haze * 0.65);
                sky += _SunColor.rgb * pow(saturate(sunDot), 64) * _SunHalo * 0.6 + _SunColor.rgb * pow(saturate(sunDot), 8) * _SunHalo * 0.08;
                float disc = smoothstep(1 - _SunSize, 1 - _SunSize * 0.6, sunDot);
                sky += _SunColor.rgb * disc * 6.0;

                // Clouds: march the slab above the camera.
                half4 clouds = 0;
                if (up > 0.01)
                {
                    float t0 = _CloudBottom / up, t1 = _CloudTop / up;
                    const int STEPS = 22;
                    float stepLen = (t1 - t0) / STEPS;
                    float jitter = frac(sin(dot(i.positionCS.xy, float2(12.9898, 78.233))) * 43758.5453);
                    float trans = 1.0;
                    half3 light = 0;
                    [loop]
                    for (int s = 0; s < STEPS; s++)
                    {
                        float3 p = d * (t0 + (s + jitter) * stepLen);
                        float dens = CloudDensity(p);
                        if (dens < 0.01) continue;
                        // Two short steps toward the sun for self-shadowing.
                        float toSun = CloudDensity(p + sun.direction * 0.06) + CloudDensity(p + sun.direction * 0.16);
                        float lit = exp(-toSun * 1.6);
                        float powder = 1 - exp(-dens * 4);
                        half3 col = lerp(_CloudShade.rgb, _CloudColor.rgb * sun.color, lit * lerp(0.6, 1, powder));
                        col += _HazeColor.rgb * pow(saturate(sunDot), 12) * lit * 0.8;
                        float a = 1 - exp(-dens * stepLen * 6);
                        light += trans * a * col;
                        trans *= 1 - a;
                        if (trans < 0.02) break;
                    }
                    float fade = smoothstep(0.01, 0.12, up);
                    clouds = half4(light, (1 - trans) * fade);
                    clouds.rgb *= fade;
                    // Far clouds melt into the horizon haze.
                    clouds.rgb = lerp(clouds.rgb, sky * clouds.a, 1 - smoothstep(0.02, 0.35, up));
                }
                half3 col = sky * (1 - clouds.a) + clouds.rgb;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
