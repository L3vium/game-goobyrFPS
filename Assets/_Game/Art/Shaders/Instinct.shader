// Author : Baptiste Vu Liem
// Instinct vision (see InstinctFeature / InstinctVision).
// Pass 0 grades the frame: darker, cooler and desaturated, faint edges on the architecture, and
// the scan wave that rolls out when instinct turns on.
// Pass 1 draws one highlighted renderer: a solid glowing silhouette where it is hidden behind
// something, a rim of light where it is in plain sight. A stencil bit lets each pixel be claimed
// once, so the parts of a Goob don't stack and the first colour drawn (the target) wins.
Shader "Hidden/Goobyr/Instinct"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        float _InstinctAmount;
        float _InstinctWave;
        float _InstinctWaveFade;
        ENDHLSL

        Pass
        {
            Name "Grade"
            ZWrite Off ZTest Always Cull Off Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float EyeDepth(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float depth = EyeDepth(uv);

                // Architecture edges from the depth discontinuities.
                float2 px = _ScreenSize.zw * 1.5;
                float around = (EyeDepth(uv + float2(px.x, 0)) + EyeDepth(uv - float2(px.x, 0)) + EyeDepth(uv + float2(0, px.y)) + EyeDepth(uv - float2(0, px.y))) * 0.25;
                float edge = saturate(abs(around - depth) / max(depth, 0.1) * 30.0) * saturate(1.0 - depth / 120.0);

                half luma = dot(scene, half3(0.2126, 0.7152, 0.0722));
                half3 graded = lerp(luma.xxx, scene, 0.28) * half3(0.42, 0.48, 0.56);
                graded += edge * half3(0.16, 0.2, 0.26);

                // The wave: a bright shell at the wave's distance with a soft trail behind it.
                float shell = exp(-abs(depth - _InstinctWave) * 0.35);
                float trail = saturate(1.0 - (_InstinctWave - depth) / 25.0) * step(depth, _InstinctWave);
                graded += (shell * 0.55 + trail * 0.06) * _InstinctWaveFade * half3(0.55, 0.75, 1.0);

                return half4(lerp(scene, graded, _InstinctAmount), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Highlight"
            ZWrite Off ZTest Always Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 128
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings2 { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; float eyeDepth : TEXCOORD2; };

            Varyings2 Vert(Attributes input)
            {
                Varyings2 o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = GetWorldSpaceViewDir(positionWS);
                o.eyeDepth = -TransformWorldToView(positionWS).z;
                return o;
            }

            half4 Frag(Varyings2 input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                bool hidden = input.eyeDepth > sceneDepth + 0.08;

                half fresnel = pow(1.0 - saturate(abs(dot(normalize(input.normalWS), normalize(input.viewWS)))), 2.0);
                half scan = 0.88 + 0.12 * sin(input.positionCS.y * 1.4);

                half alpha = hidden ? (0.5 + 0.45 * fresnel) * scan : saturate(fresnel * 0.9 + 0.06);
                half3 color = _Color.rgb * (hidden ? 0.75 + 0.9 * fresnel : 0.6 + 1.2 * fresnel);
                return half4(color, alpha * _InstinctAmount * _Color.a);
            }
            ENDHLSL
        }
    }
}
