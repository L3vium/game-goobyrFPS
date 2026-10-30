// Author : Baptiste Vu Liem
// Unlit HDR colour (vertex colour x _BaseColor) for glows, bulbs, tracers and particles.
// _SrcBlend/_DstBlend pick opaque, additive (One One) or alpha (SrcAlpha OneMinusSrcAlpha).
Shader "Goobyr/Glow"
{
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _SrcBlend ("Src", Float) = 1
        [HideInInspector] _DstBlend ("Dst", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        _SoftEdge ("Soft Edge", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _SoftEdge;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half fog : TEXCOORD2; };

            V Vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                half4 c = _BaseColor * i.color;
                if (_SoftEdge > 0)
                {
                    half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                    c.a *= lerp(1, saturate(abs(dot(normalize(i.normalWS), v)) * 1.6), _SoftEdge);
                }
                c.rgb = MixFog(c.rgb, i.fog * 0.5 + 0.5);
                return c;
            }
            ENDHLSL
        }
    }
}
