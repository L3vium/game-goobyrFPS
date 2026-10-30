// Author : Baptiste Vu Liem
// Transparent toon for water, glass and gas: tinted, fresnel-brightened, gently waving.
Shader "Goobyr/ToonTransparent"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.3, 0.7, 0.85, 0.6)
        _FresnelColor ("Fresnel", Color) = (1, 1, 1, 1)
        _Wave ("Wave", Range(0, 0.5)) = 0.05
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1
        _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _FresnelColor, _EmissionColor;
                half _Wave, _WaveSpeed;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half fog : TEXCOORD2; };

            V Vert(A i)
            {
                V o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                ws.y += sin(ws.x * 0.35 + _Time.y * _WaveSpeed) * cos(ws.z * 0.3 + _Time.y * _WaveSpeed * 0.8) * _Wave;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half f = pow(1 - saturate(abs(dot(n, v))), 3);
                Light l = GetMainLight();
                half3 col = _BaseColor.rgb * (0.6 + 0.4 * saturate(dot(n, l.direction))) * l.color + _FresnelColor.rgb * f * 0.5 + _EmissionColor.rgb;
                col = MixFog(col, i.fog);
                return half4(col, saturate(_BaseColor.a + f * 0.3));
            }
            ENDHLSL
        }
    }
}
