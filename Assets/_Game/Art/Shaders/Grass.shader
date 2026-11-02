// Author : Baptiste Vu Liem
// GPU grass (World/GrassField.cs): tufts of curved blades near the camera, 4-blade cards farther,
// built in the vertex shader from the instances GrassCull.compute placed (with the world's wind and
// the trampling already evaluated per tuft, now and last frame); each blade flutters; shading is
// Forward+: bent normals (each tuft lit like a soft rounded clump, the far ones like the ground under
// them), light through the blades when the sun is behind them, darker roots, the map's colour and
// dryness, receive-only sun shadows, the clustered additional lights, SSAO. Depth-normals for the near
// tufts only (SSAO), motion vectors for both (TAA would smear the wind otherwise). No shadow caster.
Shader "Goobyr/Grass"
{
    Properties
    {
        _RootColor("Root", Color) = (0.045, 0.08, 0.022, 1)
        _TipColor("Tip", Color) = (0.3, 0.47, 0.13, 1)
        _DryColor("Dry Tip", Color) = (0.6, 0.54, 0.28, 1)
        _TranslucencyColor("Translucency", Color) = (0.55, 0.85, 0.2, 1)
        _BladeWidth("Blade Width (m)", Float) = 0.035
        _TuftRadius("Tuft Radius (m)", Float) = 0.14
        _WindScale("Wind", Float) = 0.9
        _Translucency("Translucency Amount", Range(0, 2)) = 1
        _RootOcclusion("Root Occlusion", Range(0, 1)) = 0.35
        _Gloss("Gloss", Range(0, 1)) = 0.25
        _FarNormal("Far Ground Normal", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" "AlwaysRenderMotionVectors" = "true" "DisableBatching" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "GoobyrWind.hlsl"

        struct GrassInstance
        {
            float3 position;
            uint shape;
            uint size;
            uint tint;
            uint normal;
            uint wind;
            uint windBefore;
        };

        StructuredBuffer<GrassInstance> _GrassInstances;

        CBUFFER_START(UnityPerMaterial)
            half4 _RootColor;
            half4 _TipColor;
            half4 _DryColor;
            half4 _TranslucencyColor;
            float _BladeWidth;
            float _TuftRadius;
            float _WindScale;
            half _Translucency;
            half _RootOcclusion;
            half _Gloss;
            half _FarNormal;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float4 blade : TEXCOORD0;   // x side (-1, 0, 1), y height along the blade 0..1, z blade rank, w blade yaw 0..1
            float4 place : TEXCOORD1;   // xy root offset in the tuft (unit radius), z height scale, w width scale
            float4 form : TEXCOORD2;    // x lean, y droop, z flutter phase
            uint instanceID : SV_InstanceID;
        };

        struct Blade
        {
            float3 positionWS;
            float3 normalWS;
            float along;
            float sway;         // how far the wind (or a walker) bends it, 0..1
            float4 look;        // x dryness, y variation, z tall, w far (0 near .. 1 farthest)
        };

        float3 GrassGroundNormal(GrassInstance pInstance)
        {
            float3 n = float3(f16tof32(pInstance.normal & 0xFFFFu), 0.0, f16tof32(pInstance.normal >> 16));
            n.y = sqrt(saturate(1.0 - n.x * n.x - n.z * n.z));
            return n;
        }

        // One blade vertex: the blade grows from its root in the tuft as a quadratic curve, leaning and
        // drooping by its own shape, pushed by the wind (steady lean and gusts, evaluated per tuft by
        // GrassCull.compute, plus a flutter of its own) and by whoever walks through it, keeping its
        // length; tapered to a point. pBefore: last frame's shape (motion vectors).
        Blade BuildBlade(Attributes pInput, bool pBefore)
        {
            float pTime = pBefore ? _GoobyrWindTime.y : _GoobyrWindTime.x;
            GrassInstance g = _GrassInstances[pInput.instanceID];
            float lYaw = f16tof32(g.shape & 0xFFFFu);
            float lHeight = f16tof32(g.shape >> 16);
            float lWiden = f16tof32(g.size & 0xFFFFu);
            float lKept = f16tof32(g.size >> 16);
            float3 lGround = GrassGroundNormal(g);

            // Blades ranked past the kept fraction fold away; the others widen to keep the coverage.
            float lAlive = saturate((lKept - pInput.blade.z) * 12.0 + 0.5);
            float lWidth = _BladeWidth * pInput.place.w * lWiden * rsqrt(max(lKept, 0.3));

            float s, c;
            sincos(lYaw, s, c);
            float2 lOffset = float2(c * pInput.place.x - s * pInput.place.y, s * pInput.place.x + c * pInput.place.y) * _TuftRadius * saturate(0.4 + lHeight);
            float3 lRoot = g.position + float3(lOffset.x, -0.04, lOffset.y);
            float sy, cy;
            sincos(lYaw + pInput.blade.w * 6.2831853, sy, cy);
            float3 lFacing = float3(cy, 0.0, sy);
            float3 lAcross = float3(-sy, 0.0, cy);
            float lLength = lHeight * pInput.place.z * lAlive;

            // Wind and trampling from the tuft, plus a flutter of each blade; stiffer near the root (curve below).
            uint lWind = pBefore ? g.windBefore : g.wind;
            float2 lPush = float2(f16tof32(lWind & 0xFFFFu), f16tof32(lWind >> 16));
            float lFlat = ((g.tint >> 24) & 0xFFu) / 255.0;
            float lTallness = ((g.tint >> 16) & 0xFFu) / 255.0;
            float lFlutter = sin(pTime * (3.5 + 2.5 * pInput.form.z) + pInput.form.z * 37.0 + dot(lRoot.xz, float2(0.73, 0.51)));
            lPush += lAcross.xz * (lFlutter * 0.1 * (0.3 + length(lPush)) * (1.0 - lFlat));

            // Flattened grass lies down along its push (away from the walker).
            // Tall grass stands straighter (it is long and leans less, so it hides what crouches in it).
            float lLean = pInput.form.x * (1.0 - 0.6 * lTallness);
            float3 lTip = normalize(float3(lFacing.x * lLean + lPush.x, 1.0, lFacing.z * lLean + lPush.y));
            float3 lDown = normalize(float3(lPush.x + lFacing.x * 0.3, 0.12, lPush.y + lFacing.z * 0.3));
            lTip = normalize(lerp(lTip, lDown, lFlat * 0.85));
            float3 p0 = lRoot;
            float3 p2 = lRoot + lTip * lLength;
            p2.y -= pInput.form.y * lLength * 0.25 * saturate(1.0 - lFlat) * (1.0 - 0.5 * lTallness);
            float3 p1 = lRoot + float3(0.0, lLength * 0.5 * (1.0 - lFlat * 0.7), 0.0) + (lTip - float3(0.0, 1.0, 0.0)) * (lLength * 0.15);
            float t = pInput.blade.y;
            float u = 1.0 - t;
            float3 lPosition = u * u * p0 + 2.0 * u * t * p1 + t * t * p2;
            float3 lTangent = normalize(2.0 * u * (p1 - p0) + 2.0 * t * (p2 - p1) + float3(0.0, 1e-4, 0.0));
            lPosition += lAcross * (pInput.blade.x * lWidth * 0.5 * pow(abs(u), 0.8));

            // Bent normal: the blade's face, rounded across its width, turned toward the viewer, blended
            // with the ground's normal (more with distance) so the meadow lights like one soft surface.
            float3 lFace = normalize(cross(lAcross, lTangent));
            float3 lView = _WorldSpaceCameraPos.xyz - lPosition;
            float lDistance = length(lView);
            if (dot(lFace, lView) < 0.0) lFace = -lFace;
            float lFar = saturate(lDistance / 70.0);
            float3 lNormal = normalize(lFace + lAcross * (pInput.blade.x * 0.45));
            lNormal = normalize(lerp(lNormal, lGround, 0.3 + _FarNormal * lFar * 0.7));

            Blade b;
            b.positionWS = lPosition;
            b.normalWS = lNormal;
            b.along = t;
            b.sway = saturate(length(lPush) * 1.3);
            b.look = float4((g.tint & 0xFFu) / 255.0, ((g.tint >> 8) & 0xFFu) / 255.0, ((g.tint >> 16) & 0xFFu) / 255.0, lFar);
            return b;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVertex
            #pragma fragment GrassFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 look : TEXCOORD2;     // dryness, variation, tall, far
                half along : TEXCOORD3;
                half sway : TEXCOORD4;
            };

            Varyings GrassVertex(Attributes input)
            {
                Blade b = BuildBlade(input, false);
                Varyings output;
                output.positionWS = b.positionWS;
                output.positionCS = TransformWorldToHClip(b.positionWS);
                output.normalWS = b.normalWS;
                output.look = b.look;
                output.along = b.along;
                output.sway = b.sway;
                return output;
            }

            half3 GrassLight(Light pLight, half3 pAlbedo, half3 pNormal, half3 pView, half pAlong)
            {
                half lAttenuation = pLight.shadowAttenuation * pLight.distanceAttenuation;
                half lDiffuse = saturate(dot(pNormal, pLight.direction) * 0.6h + 0.4h);
                half lThrough = pow(saturate(dot(pView, -pLight.direction)), 3.0h) * (0.25h + 0.75h * pAlong);
                half3 lHalf = SafeNormalize(pLight.direction + pView);
                half lGloss = pow(saturate(dot(pNormal, lHalf)), 28.0h) * _Gloss * pAlong;
                return pLight.color * lAttenuation * (pAlbedo * lDiffuse + pAlbedo * _TranslucencyColor.rgb * (lThrough * _Translucency) + lGloss);
            }

            half4 GrassFragment(Varyings input) : SV_Target
            {
                half lAlong = input.along;
                half lDry = input.look.x;
                half lVariation = input.look.y;
                half3 lTip = lerp(_TipColor.rgb, _DryColor.rgb, saturate(lDry));
                // Each tuft its own green: cooler and deeper or warmer and yellower, a little brighter or darker.
                half3 lHue = lerp(half3(0.86h, 1.0h, 0.84h), half3(1.12h, 1.02h, 0.8h), lVariation);
                half lBright = 0.85h + 0.3h * frac(lVariation * 7.31h);
                half3 lAlbedo = lerp(_RootColor.rgb, lTip * lHue, smoothstep(0.0h, 0.9h, lAlong)) * lBright;
                // A bent blade turns its paler back to the sky: gusts roll over the meadow as waves of light.
                lAlbedo *= 1.0h + 0.28h * input.sway * lAlong;
                half3 lNormal = normalize(input.normalWS);
                half3 lView = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 lScreenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                // Dark roots near the camera; far away the blades are seen whole and blend into the turf.
                half lRoot = lerp(lerp(_RootOcclusion, 0.75h, input.look.w), 1.0h, smoothstep(0.0h, 0.75h, lAlong));

                half lIndirectAO = 1.0h, lDirectAO = 1.0h;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    // Grass already darkens its own roots: SSAO counts a little over half.
                    AmbientOcclusionFactor lAO = GetScreenSpaceAmbientOcclusion(lScreenUv);
                    lIndirectAO = lerp(1.0h, lAO.indirectAmbientOcclusion, 0.6h);
                    lDirectAO = lerp(1.0h, lAO.directAmbientOcclusion, 0.6h);
                #endif

                Light lSun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1, 1, 1, 1));
                half3 lColor = GrassLight(lSun, lAlbedo, lNormal, lView, lAlong) * lDirectAO * lerp(0.6h, 1.0h, lRoot);
                lColor += SampleSH(lNormal) * lAlbedo * (lRoot * lIndirectAO);

                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = lScreenUv;
                    uint lCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            Light lLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                            lColor += GrassLight(lLight, lAlbedo, lNormal, lView, lAlong) * lRoot;
                        }
                    #endif
                    LIGHT_LOOP_BEGIN(lCount)
                        Light lLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        lColor += GrassLight(lLight, lAlbedo, lNormal, lView, lAlong) * lRoot;
                    LIGHT_LOOP_END
                #endif
                return half4(lColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            float4 DepthVertex(Attributes input) : SV_POSITION
            {
                return TransformWorldToHClip(BuildBlade(input, false).positionWS);
            }

            half DepthFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Blade b = BuildBlade(input, false);
                Varyings output;
                output.positionCS = TransformWorldToHClip(b.positionWS);
                output.normalWS = b.normalWS;
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                float3 lNormal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(lNormal) * 0.5 + 0.5)), 0.0);
                #else
                    return half4(lNormal, 0.0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            Cull Off
            ColorMask RG

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MotionVertex
            #pragma fragment MotionFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 positionCSNoJitter : TEXCOORD0;
                float4 previousPositionCSNoJitter : TEXCOORD1;
            };

            Varyings MotionVertex(Attributes input)
            {
                float3 lNow = BuildBlade(input, false).positionWS;
                float3 lBefore = BuildBlade(input, true).positionWS;
                Varyings output;
                output.positionCS = TransformWorldToHClip(lNow);
                output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(lNow, 1.0));
                output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(lBefore, 1.0));
                return output;
            }

            float4 MotionFragment(Varyings input) : SV_Target
            {
                return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0, 0);
            }
            ENDHLSL
        }
    }
}
