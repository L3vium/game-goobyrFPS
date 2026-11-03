// Author : Baptiste Vu Liem
// Rock for large cliff faces: a PBR rock set projected triplanar in world space (no UV stretching on
// tall faces, any mesh), at two scales mixed by a slow noise so a 60 m face does not repeat; tinted in
// horizontal strata (bands of a few metres, warped by noise so they wander like bedding planes); and,
// for snowy maps, snow settled on every ledge that faces up (the normal map's, so it follows the rock's
// relief), thinning on steep faces. Lit by URP (Forward+, shadows, probes, SSAO) through
// UniversalFragmentPBR; shadow and depth passes from URP Lit.
Shader "Goobyr/Rock"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Rock albedo", 2D) = "white" {}
        [NoScaleOffset][Normal] _BumpMap ("Rock normal", 2D) = "bump" {}
        [NoScaleOffset] _MaskMap ("Rock packed (_ms: R metallic, G occlusion, A smoothness)", 2D) = "white" {}
        _Tile ("Tile (m)", Float) = 6
        _Smoothness ("Smoothness", Range(0, 1)) = 0.6
        _StrataA ("Strata tone A", Color) = (1, 1, 1, 1)
        _StrataB ("Strata tone B", Color) = (0.82, 0.76, 0.7, 1)
        _StrataHeight ("Strata thickness (m)", Float) = 3.5
        _StrataStrength ("Strata strength", Range(0, 1)) = 0.6
        [NoScaleOffset] _SnowMap ("Snow albedo", 2D) = "white" {}
        _SnowColor ("Snow tint", Color) = (1, 1, 1, 1)
        _SnowAmount ("Snow amount (0 none)", Range(0, 1)) = 0
        _SnowTile ("Snow tile (m)", Float) = 4

        // URP Lit's names, read by its shadow and depth passes.
        [HideInInspector] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            TEXTURE2D(_MaskMap);
            TEXTURE2D(_SnowMap);

            CBUFFER_START(UnityPerMaterial)
                float _Tile;
                half _Smoothness;
                half4 _StrataA, _StrataB;
                float _StrataHeight;
                half _StrataStrength;
                half4 _SnowColor;
                half _SnowAmount;
                float _SnowTile;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half fog : TEXCOORD2; };

            V Vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // Triplanar blend weights, sharpened so the seams between projections stay narrow.
            float3 Weights(float3 n) { float3 w = pow(abs(n), 6.0); return w / (w.x + w.y + w.z); }

            half4 Tri(TEXTURE2D_PARAM(pTex, pSampler), float3 p, float3 w, float pScale)
            {
                return SAMPLE_TEXTURE2D(pTex, pSampler, p.zy * pScale) * w.x + SAMPLE_TEXTURE2D(pTex, pSampler, p.xz * pScale) * w.y + SAMPLE_TEXTURE2D(pTex, pSampler, p.xy * pScale) * w.z;
            }

            // Whiteout-blended triplanar normal (tangent-space maps projected on the three planes).
            float3 TriNormal(float3 p, float3 n, float3 w, float pScale)
            {
                float3 tx = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, p.zy * pScale));
                float3 ty = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, p.xz * pScale));
                float3 tz = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, p.xy * pScale));
                tx = float3(tx.xy + n.zy, abs(tx.z) * n.x);
                ty = float3(ty.xy + n.xz, abs(ty.z) * n.y);
                tz = float3(tz.xy + n.xy, abs(tz.z) * n.z);
                return normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
            }

            half4 Frag(V i) : SV_Target
            {
                float3 p = i.positionWS;
                float3 n0 = normalize(i.normalWS);
                float3 w = Weights(n0);
                float lScale = 1.0 / _Tile;

                // Two scales of the same rock, mixed by a slow noise: no visible repeat on a big face.
                half lMix = (half)(0.35 + 0.4 * Noise(p.xz * 0.03 + p.y * 0.02));
                half4 lNear = Tri(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), p, w, lScale);
                half4 lFar = Tri(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), p, w, lScale * 0.31);
                half3 lAlbedo = lerp(lNear.rgb, lFar.rgb, lMix);
                half4 lMask = Tri(TEXTURE2D_ARGS(_MaskMap, sampler_BaseMap), p, w, lScale);
                float3 n = TriNormal(p, n0, w, lScale);

                // Strata: horizontal bands warped by noise, alternating two tones, with thin darker seams.
                float lWarp = Noise(p.xz * 0.05) * 2.5 + Noise(p.xz * 0.21) * 0.6;
                float lBand = (p.y + lWarp) / max(_StrataHeight, 0.1);
                float lLayer = Hash(floor(lBand).xx);
                half3 lStrata = lerp(_StrataA.rgb, _StrataB.rgb, (half)lLayer);
                half lSeam = (half)smoothstep(0.0, 0.06, frac(lBand)) * (half)smoothstep(1.0, 0.94, frac(lBand));
                lAlbedo *= lerp(half3(1, 1, 1), lStrata * lerp(0.72h, 1.0h, lSeam), _StrataStrength);

                // Snow on what faces up (the rock's own relief), thinning on steep faces; ragged edge.
                half lSnow = 0;
                if (_SnowAmount > 0)
                {
                    float lUp = n.y + (Noise(p.xz * 0.6) - 0.5) * 0.25 + (_SnowAmount - 0.5) * 0.6;
                    lSnow = (half)smoothstep(0.55, 0.75, lUp);
                    half3 lSnowAlbedo = SAMPLE_TEXTURE2D(_SnowMap, sampler_BaseMap, p.xz / _SnowTile).rgb * _SnowColor.rgb;
                    lAlbedo = lerp(lAlbedo, lSnowAlbedo, lSnow);
                    n = normalize(lerp(n, n0, lSnow * 0.6));
                }

                SurfaceData s = (SurfaceData)0;
                s.albedo = lAlbedo;
                s.alpha = 1;
                s.metallic = 0;
                s.smoothness = lerp(lMask.a * _Smoothness, 0.35h, lSnow);
                s.occlusion = lerp(lMask.g, 1.0h, lSnow);
                s.normalTS = half3(0, 0, 1);

                InputData d = (InputData)0;
                d.positionWS = p;
                d.normalWS = n;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(p);
                d.shadowCoord = TransformWorldToShadowCoord(p);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);
                d.bakedGI = SampleSH(n);
                d.fogCoord = i.fog;
                half4 c = UniversalFragmentPBR(d, s);
                c.rgb = MixFog(c.rgb, i.fog);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
