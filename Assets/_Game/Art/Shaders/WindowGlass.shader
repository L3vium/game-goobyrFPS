// Author : Baptiste Vu Liem
// Window glass that lets the interiors read: clear and slightly dark (it only dims what is behind it),
// reflecting the probes and the sun by Fresnel alone (a grazing pane mirrors, a pane seen face-on is
// almost invisible). Where the pane looks into nothing (the scene's depth far behind it: an unbuilt
// shell, the sky through a back window) it shows a dim room instead, by interior mapping: a box of
// storey height behind the glass with a back wall, side walls every few metres, floor and ceiling,
// lit by the ambient so it darkens at night. Never a grey void.
Shader "Goobyr/WindowGlass"
{
    Properties
    {
        _GlassTint ("Transmission (what passes through)", Color) = (0.78, 0.84, 0.82, 1)
        _Reflectance ("Reflectance face-on (F0)", Range(0, 0.2)) = 0.04
        _Roughness ("Roughness", Range(0, 0.3)) = 0.04
        _VoidDepth ("Void after (m behind the pane)", Float) = 9
        _RoomDepth ("Fallback room depth (m)", Float) = 4
        _RoomWidth ("Fallback room width (m)", Float) = 3.6
        _Storey ("Storey height (m)", Float) = 3.6
        _WallColor ("Fallback wall", Color) = (0.36, 0.31, 0.26, 1)
        _FloorColor ("Fallback floor", Color) = (0.16, 0.11, 0.08, 1)
        _CurtainColor ("Fallback curtain", Color) = (0.42, 0.24, 0.18, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Glass"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlassTint;
                half _Reflectance;
                half _Roughness;
                float _VoidDepth;
                float _RoomDepth;
                float _RoomWidth;
                float _Storey;
                half4 _WallColor;
                half4 _FloorColor;
                half4 _CurtainColor;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            V Vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // A dim room behind the pane (interior mapping in world space): -n is into the room.
            half3 Room(float3 p, float3 r, float3 n)
            {
                float3 lIn = -n;
                float3 lSide = normalize(cross(float3(0, 1, 0), n) + float3(1e-4, 0, 0));
                float lFloorY = floor(p.y / _Storey) * _Storey + 0.05;
                float lCeilY = lFloorY + _Storey - 0.5;
                float tBack = _RoomDepth / max(dot(r, lIn), 1e-3);
                float tFloor = r.y < 0 ? (p.y - lFloorY) / -r.y : 1e5;
                float tCeil = r.y > 0 ? (lCeilY - p.y) / r.y : 1e5;
                float s = dot(p, lSide) / _RoomWidth, rs = dot(r, lSide) / _RoomWidth;
                float tSide = rs > 0 ? (ceil(s) - s) / rs : rs < 0 ? (s - floor(s)) / -rs : 1e5;
                float t = min(min(tBack, tSide), min(tFloor, tCeil));
                float3 h = p + r * t;
                half3 c;
                if (t == tFloor) c = _FloorColor.rgb;
                else if (t == tCeil) c = _WallColor.rgb * 1.15;
                else if (t == tSide) c = _WallColor.rgb * 0.8;
                else c = _WallColor.rgb;
                // Curtains drawn at the sides of the pane's cell, folded.
                float lAcross = frac(dot(p, lSide) / _RoomWidth);
                float lCurtain = step(lAcross, 0.18) + step(0.82, lAcross);
                c = lerp(c, _CurtainColor.rgb * (0.75 + 0.25 * sin(dot(p, lSide) * 40.0)), saturate(lCurtain));
                // Deeper is darker; the room is lit by the sky's ambient (dark at night).
                half3 lAmbient = SampleSH(lIn);
                return c * lAmbient * (1.0 / (1.0 + 0.15 * t)) * (lCurtain > 0.5 ? 1.0 : 0.8);
            }

            half4 Frag(V i, bool pFront : SV_IsFrontFace) : SV_Target
            {
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 n = normalize(i.normalWS) * (pFront ? 1.0 : -1.0);
                if (dot(n, v) < 0) n = -n;
                half lNdotV = saturate(dot(n, v));
                half lFresnel = _Reflectance + (1.0h - _Reflectance) * pow(1.0h - lNdotV, 5.0h);

                float2 lScreen = GetNormalizedScreenSpaceUV(i.positionCS);
                half3 lReflect = GlossyEnvironmentReflection(reflect(-v, n), i.positionWS, _Roughness, 1.0h, lScreen);
                Light lSun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lHalf = normalize(lSun.direction + v);
                half lSpec = pow(saturate(dot(n, lHalf)), 900.0h) * 40.0h * lSun.shadowAttenuation;
                half3 lMirror = (lReflect + lSun.color * lSpec) * lFresnel;

                // The room behind: real geometry if there is some close behind the pane, else the fallback.
                float lScene = LinearEyeDepth(SampleSceneDepth(lScreen), _ZBufferParams);
                float lPane = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                half lVoid = smoothstep(_VoidDepth, _VoidDepth * 1.6, lScene - lPane);
                half3 lRoom = Room(i.positionWS, -v, n);

                // Premultiplied: dst * (1 - a) + rgb. Glass dims what passes (1 - tint); the void is covered.
                half lDim = 1.0h - dot(_GlassTint.rgb, half3(0.3h, 0.59h, 0.11h));
                half a = lerp(lerp(lDim, 1.0h, lVoid), 1.0h, lFresnel);
                half3 rgb = lMirror + lRoom * lVoid * (1.0h - lFresnel);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
