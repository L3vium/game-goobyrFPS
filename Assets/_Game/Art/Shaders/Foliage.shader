// Author : Baptiste Vu Liem
// Trees, palms, bushes and ferns (the Veg_* models): URP lit (Forward+), alpha-tested and two-sided
// for leaves, with the world's wind read from the models' vertex colours (R ambient occlusion,
// G per-branch phase, B wind weight 0 at the trunk base -> 1 at the leaf tips), light glowing
// through the leaves, dithered LOD cross-fade, and motion vectors for TAA (the wind moves the
// vertices of objects that never move).
Shader "Goobyr/Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Colour", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.45
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        _MetallicGlossMap("Mask (G occlusion, A smoothness)", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0, 1)) = 1
        _OcclusionStrength("Occlusion", Range(0, 1)) = 1
        _VertexAO("Vertex Occlusion", Range(0, 1)) = 0.5
        _Translucency("Translucency", Range(0, 2)) = 0.7
        _TranslucencyColor("Translucency Colour", Color) = (0.85, 1, 0.45, 1)
        _WindBend("Wind Bend (m at full wind)", Float) = 0.4
        _WindFlutter("Leaf Flutter", Range(0, 1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
        // URP Lit's names, so a material switched over from Lit keeps its state.
        [HideInInspector] _Surface("__surface", Float) = 0
        [HideInInspector] _Blend("__blend", Float) = 0
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 0
        [HideInInspector] _ZWrite("__zw", Float) = 1
        [HideInInspector] _OcclusionMap("Occlusion", 2D) = "white" {}
        [HideInInspector] _Metallic("Metallic", Range(0, 1)) = 0
        [HideInInspector] _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "AlphaTest" "AlwaysRenderMotionVectors" = "true" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "GoobyrWind.hlsl"

        TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _TranslucencyColor;
            half _Cutoff;
            half _BumpScale;
            half _Smoothness;
            half _OcclusionStrength;
            half _VertexAO;
            half _Translucency;
            float _WindBend;
            half _WindFlutter;
            half _Metallic;
            half _Surface;
        CBUFFER_END

        // The plant leans with the wind (weight squared, so the trunk base stays put), sways round it
        // with its own phase, branches bob, leaves flutter along their normal. pPivot is the plant's
        // root (its object origin), so every vertex of one tree sees the same gust.
        float3 FoliageWind(float3 pPositionWS, float3 pPivotWS, float3 pNormalWS, half4 pColor, float pTime)
        {
            float lWeight = pColor.b;
            float lPhase = pColor.g;
            float2 lPush = GoobyrWindPush(pPivotWS.xz, pTime);
            float lStrength = length(lPush);
            float lSeed = dot(pPivotWS.xz, float2(0.37, 0.21));
            float lW2 = lWeight * lWeight;
            float lSway = sin(pTime * 1.6 + lSeed + lPhase * 1.5);
            float3 lBend = float3(lPush.x, 0.0, lPush.y) * (_WindBend * lW2 * (1.0 + 0.35 * lSway));
            float lBob = sin(pTime * 2.7 + lPhase * 23.0 + lSeed) * lStrength;
            lBend += float3(-lPush.y, 0.0, lPush.x) * (_WindBend * lW2 * 0.25 * lBob);
            lBend.y += _WindBend * 0.12 * lWeight * lBob;
            float lFlutter = sin(pTime * 10.0 + lPhase * 61.0 + dot(pPositionWS, float3(3.1, 2.3, 2.7)));
            lBend += pNormalWS * (_WindFlutter * 0.035 * lWeight * lFlutter * (0.2 + lStrength));
            // A branch keeps its length: it drops a little as it leans.
            lBend.y -= dot(lBend.xz, lBend.xz) * 0.5 / max(_WindBend * 6.0, 0.6);
            return pPositionWS + lBend;
        }

        float3 FoliagePivot() { return UNITY_MATRIX_M._m03_m13_m23; }
        float3 FoliagePreviousPivot() { return UNITY_PREV_MATRIX_M._m03_m13_m23; }

        half FoliageAlpha(float2 pUv)
        {
            half lAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, pUv).a * _BaseColor.a;
            #if defined(_ALPHATEST_ON)
                clip(lAlpha - _Cutoff);
            #endif
            return lAlpha;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ForwardVertex
            #pragma fragment ForwardFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half occlusion : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ForwardVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexNormalInputs lNormals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                float3 lPositionWS = FoliageWind(TransformObjectToWorld(input.positionOS.xyz), FoliagePivot(), lNormals.normalWS, input.color, _GoobyrWindTime.x);
                output.positionWS = lPositionWS;
                output.positionCS = TransformWorldToHClip(lPositionWS);
                output.normalWS = lNormals.normalWS;
                output.tangentWS = half4(lNormals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.occlusion = lerp(1.0h, input.color.r, _VertexAO);
                return output;
            }

            half4 ForwardFragment(Varyings input, FRONT_FACE_TYPE pFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 lAlbedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(lAlbedo.a - _Cutoff);
                #endif
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif

                half lFacing = IS_FRONT_VFACE(pFace, 1.0h, -1.0h);
                half3 lVertexNormal = input.normalWS * lFacing;
                half4 lMask = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);

                SurfaceData lSurface = (SurfaceData)0;
                lSurface.albedo = lAlbedo.rgb;
                lSurface.alpha = 1.0h;
                lSurface.metallic = 0.0h;
                lSurface.specular = half3(0.0h, 0.0h, 0.0h);
                lSurface.smoothness = lMask.a * _Smoothness;
                lSurface.occlusion = lerp(1.0h, lMask.g, _OcclusionStrength) * input.occlusion;
                lSurface.normalTS = half3(0.0h, 0.0h, 1.0h);

                InputData lInput = (InputData)0;
                lInput.positionWS = input.positionWS;
                #if defined(_NORMALMAP)
                    half3 lNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    half3 lBitangent = input.tangentWS.w * cross(lVertexNormal, input.tangentWS.xyz);
                    lInput.normalWS = TransformTangentToWorld(lNormalTS, half3x3(input.tangentWS.xyz, lBitangent, lVertexNormal));
                #else
                    lInput.normalWS = lVertexNormal;
                #endif
                lInput.normalWS = NormalizeNormalPerPixel(lInput.normalWS);
                lInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lInput.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lInput.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
                lInput.bakedGI = SampleSH(lInput.normalWS);

                half4 lColor = UniversalFragmentPBR(lInput, lSurface);

                // Light through the leaf: strong when the sun is behind it (looking into the light),
                // a little from any back-lit side, dimmed by the shadow the leaf sits in.
                Light lSun = GetMainLight(lInput.shadowCoord, lInput.positionWS, lInput.shadowMask);
                half lBehind = saturate(dot(lInput.viewDirectionWS, -lSun.direction));
                half lThrough = pow(lBehind, 6.0h) * 0.85h + saturate(dot(-lInput.normalWS, lSun.direction)) * 0.25h;
                lColor.rgb += lAlbedo.rgb * _TranslucencyColor.rgb * lSun.color * (lSun.shadowAttenuation * lThrough * _Translucency * lSurface.occlusion);
                return half4(lColor.rgb, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 lNormalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 lPositionWS = FoliageWind(TransformObjectToWorld(input.positionOS.xyz), FoliagePivot(), lNormalWS, input.color, _GoobyrWindTime.x);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lLight = normalize(_LightPosition - lPositionWS);
                #else
                    float3 lLight = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(lPositionWS, lNormalWS, lLight)));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FoliageAlpha(input.uv);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 lNormalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(FoliageWind(TransformObjectToWorld(input.positionOS.xyz), FoliagePivot(), lNormalWS, input.color, _GoobyrWindTime.x));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half DepthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                FoliageAlpha(input.uv);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 lNormalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(FoliageWind(TransformObjectToWorld(input.positionOS.xyz), FoliagePivot(), lNormalWS, input.color, _GoobyrWindTime.x));
                output.normalWS = lNormalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input, FRONT_FACE_TYPE pFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                FoliageAlpha(input.uv);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif
                float3 lNormalWS = normalize(input.normalWS * IS_FRONT_VFACE(pFace, 1.0, -1.0));
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 lOct = saturate(PackNormalOctQuadEncode(lNormalWS) * 0.5 + 0.5);
                    return half4(PackFloat2To888(lOct), 0.0);
                #else
                    return half4(lNormalWS, 0.0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MotionVertex
            #pragma fragment MotionFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 positionCSNoJitter : TEXCOORD1;
                float4 previousPositionCSNoJitter : TEXCOORD2;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings MotionVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 lNormalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 lNow = FoliageWind(TransformObjectToWorld(input.positionOS.xyz), FoliagePivot(), lNormalWS, input.color, _GoobyrWindTime.x);
                float3 lBefore = FoliageWind(mul(UNITY_PREV_MATRIX_M, float4(input.positionOS.xyz, 1.0)).xyz, FoliagePreviousPivot(), lNormalWS, input.color, _GoobyrWindTime.y);
                output.positionCS = TransformWorldToHClip(lNow);
                output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(lNow, 1.0));
                output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(lBefore, 1.0));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float4 MotionFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                FoliageAlpha(input.uv);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif
                return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
