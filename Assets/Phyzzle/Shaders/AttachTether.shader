Shader "Phyzzle/AttachTether"
{
    Properties
    {
        [HDR] _TetherColor ("Energy Color", Color) = (0.263, 0.941, 0.541, 1)
        _TetherLength ("Length", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "RenderType" = "Transparent" }
        Cull Off
        ZTest LEqual
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "Attach Holding Tether"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TetherColor;
                float _TetherLength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // UV.y의 중앙을 0, 양 끝을 1로 바꿔 테더 폭 방향의 거리값으로 사용
                float across = abs(input.uv.y * 2.0 - 1.0);
                float halo = pow(saturate(1.0 - across), 2.0);
                float core = 1.0 - smoothstep(0.06, 0.30, across);
                // Constant world-space flow speed makes longer tethers feel connected to the hand.
                // UV.x에 실제 길이를 곱해 테더 길이가 달라도 World 거리당 파형 간격과 흐르는 속도가 일정하게 보이도록 함
                float flow = pow(saturate(sin(input.uv.x * _TetherLength * 9.0 - _Time.y * 10.0)), 4.0);
                // 넓은 halo와 좁은 core를 합치고 flow가 지나갈 때 밝기/alpha를 동시에 높여 에너지 흐름을 표현
                float alpha = saturate((halo * 0.45 + core * 0.6) * (0.7 + flow * 0.6)) * _TetherColor.a;
                float3 color = _TetherColor.rgb * (1.0 + flow * 0.35) + core * float3(0.08, 0.12, 0.10);
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
