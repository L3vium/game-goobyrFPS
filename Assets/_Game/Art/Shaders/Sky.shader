// Author : Baptiste Vu Liem
// The Goobyr sky: an atmospheric gradient (zenith, horizon haze, ground), a sun with a soft
// halo, and a layer of volumetric clouds - a slab between two altitudes raymarched through
// layered noise, drifting with the wind. Clouds have billowy eroded edges, lit tops and darker
// bellies (ambient from the sky above, the ground below), light scattered forward through their
// thin edges round the sun (silver lining), and are computed in float with a jittered march and a
// dithered output, so their gradients never band.
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
        _SilverLining ("Silver Lining", Range(0, 3)) = 1.2
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

            float4 _ZenithColor, _HorizonColor, _GroundColor, _HazeColor, _SunColor, _CloudColor, _CloudShade;
            float _SunSize, _SunHalo, _Exponent, _CloudCoverage, _CloudDensity, _CloudScale, _CloudSpeed, _CloudBottom, _CloudTop, _SilverLining;

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

            // Layered noise, normalized to 0..1: five octaves for the clouds' shape, three for their lighting.
            float Fbm(float3 p)
            {
                float v = 0, a = 0.5;
                [unroll] for (int i = 0; i < 5; i++) { v += a * Noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v / 0.96875;
            }

            float Fbm3(float3 p)
            {
                float v = 0, a = 0.5;
                [unroll] for (int i = 0; i < 3; i++) { v += a * Noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v / 0.875;
            }

            // Interleaved gradient noise: a well-spread per-pixel jitter (varied per frame, so TAA averages it).
            float Ign(float2 pixel, float frame)
            {
                pixel += frame * 5.588238;
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float3 CloudCoords(float3 p) { return p * _CloudScale + float3(_Time.y * _CloudSpeed * 0.05, 0, _Time.y * _CloudSpeed * 0.02); }

            // Height in the slab (0 bottom, 1 top) and the profile: flat-ish bottoms, rounded tops.
            float CloudProfile(float3 p, out float pHeight)
            {
                pHeight = saturate((p.y - _CloudBottom) / (_CloudTop - _CloudBottom));
                return smoothstep(0.0, 0.18, pHeight) * smoothstep(1.0, 0.45, pHeight);
            }

            // A soft coverage ramp (not a cut).
            float Coverage(float pNoise) { return smoothstep(1.0 - _CloudCoverage - 0.05, 1.0 - _CloudCoverage + 0.22, pNoise); }

            // Cloud density at a point of the slab (layer coordinates, units of ~1 km), and its height in the slab.
            float CloudDensity(float3 p, out float pHeight)
            {
                float lShape = CloudProfile(p, pHeight);
                float3 q = CloudCoords(p);
                float d = Coverage(Fbm(q)) * lShape;
                // Billows: higher frequencies eat into the thin edges more than into the cores.
                float lDetail = Noise(q * 5.3 + 3.1) * 0.62 + Noise(q * 11.7 + 7.7) * 0.38;
                d = saturate(d - lDetail * 0.42 * (1.0 - d));
                return d * _CloudDensity;
            }

            // The density the sunlight crosses: the broad shape only (smooth self-shadowing, a third of the cost).
            float CloudShadowDensity(float3 p)
            {
                float lHeight;
                float lShape = CloudProfile(p, lHeight);
                return Coverage(Fbm3(CloudCoords(p))) * lShape * _CloudDensity;
            }

            // Henyey-Greenstein phase: light scattered forward round the sun.
            float Phase(float pCos, float pG)
            {
                float g2 = pG * pG;
                return (1.0 - g2) / (12.566 * pow(max(1.0 + g2 - 2.0 * pG * pCos, 1e-4), 1.5));
            }

            float4 Frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                Light sun = GetMainLight();
                float up = d.y;
                float sunDot = dot(d, sun.direction);
                float lFrame = floor(_Time.y * 60.0);

                // Gradient with a warm haze around the sun near the horizon.
                float3 sky = up > 0 ? lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(up), _Exponent)) : lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-up * 5));
                float haze = pow(saturate(sunDot * 0.5 + 0.5), 6) * (1 - saturate(up * 1.5));
                sky = lerp(sky, _HazeColor.rgb, haze * 0.65);
                sky += _SunColor.rgb * pow(saturate(sunDot), 64) * _SunHalo * 0.6 + _SunColor.rgb * pow(saturate(sunDot), 8) * _SunHalo * 0.08;
                float disc = smoothstep(1 - _SunSize, 1 - _SunSize * 0.6, sunDot);
                sky += _SunColor.rgb * disc * 6.0;

                // Clouds: march the slab above the camera.
                float4 clouds = 0;
                if (up > 0.01)
                {
                    float t0 = _CloudBottom / up, t1 = _CloudTop / up;
                    const int STEPS = 24;
                    float stepLen = (t1 - t0) / STEPS;
                    float jitter = Ign(i.positionCS.xy, lFrame);
                    float trans = 1.0;
                    float3 light = 0;
                    // Ambient: the sky's light on the tops, the ground's on the bellies.
                    float3 lSkyLight = lerp(_HorizonColor.rgb, _ZenithColor.rgb, 0.55) * 1.15;
                    float3 lBelly = _CloudShade.rgb;
                    float lForward = Phase(sunDot, 0.62) * 12.566;
                    [loop]
                    for (int s = 0; s < STEPS; s++)
                    {
                        float3 p = d * (t0 + (s + jitter) * stepLen);
                        float lHeight;
                        float dens = CloudDensity(p, lHeight);
                        if (dens < 0.005) continue;
                        // Two steps toward the sun for self-shadowing (Beer); thin wisps scatter less light back (powder).
                        float toSun = CloudShadowDensity(p + sun.direction * 0.05) + CloudShadowDensity(p + sun.direction * 0.15) * 1.4;
                        // Light scattered many times inside the cloud reaches deeper (a second, weaker extinction).
                        float lit = exp(-toSun * 1.4) * 0.8 + exp(-toSun * 0.35) * 0.2;
                        float powder = 1 - exp(-dens * 3.0);
                        float3 lAmbient = lerp(lBelly, lSkyLight, 0.1 + 0.9 * smoothstep(0.0, 0.9, lHeight)) * (0.7 + 0.35 * lHeight);
                        float3 lDirect = _CloudColor.rgb * sun.color * lit * lerp(0.7, 1.0, powder);
                        // Silver lining: forward scattering through the thin edges, near the sun.
                        float lEdge = 1.0 - saturate(dens * 2.2);
                        lDirect += sun.color * _HazeColor.rgb * (lForward * _SilverLining * 0.12 * lit * lEdge);
                        float3 col = lAmbient + lDirect;
                        float a = 1 - exp(-dens * stepLen * 6);
                        light += trans * a * col;
                        trans *= 1 - a;
                        if (trans < 0.015) break;
                    }
                    float fade = smoothstep(0.01, 0.12, up);
                    clouds = float4(light, (1 - trans) * fade);
                    clouds.rgb *= fade;
                    // Far clouds melt into the horizon haze.
                    clouds.rgb = lerp(clouds.rgb, sky * clouds.a, 1 - smoothstep(0.02, 0.35, up));
                }
                float3 col = sky * (1 - clouds.a) + clouds.rgb;
                // A dither of about half a display step (relative, before the tonemapper) so smooth
                // gradients of sky and cloud never show bands.
                col *= 1.0 + (Ign(i.positionCS.yx + 17.0, lFrame) - 0.5) * 0.012;
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
