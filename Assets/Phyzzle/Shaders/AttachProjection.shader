Shader "Phyzzle/AttachProjection"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZTest LEqual
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Attach Projection"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _AttachProjectionPlane;
            float3 _AttachProjectionDirection;
            float _AttachProjectionOpacity;
            float _AttachProjectionBias;
            float _AttachProjectionDepthTolerance;
            float _AttachVisualBlend;
            float4 _AttachHeldColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 normal = _AttachProjectionPlane.xyz;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                // 평면식 dot(N, P) + d = 0에 P' = P - D*t를 대입해
                // t = (dot(N, P) + d) / dot(N, D) 만큼 지정 방향으로 이동시키면 수신 평면 위의 점이 됨
                float projectionDistance = (dot(normal, world) + _AttachProjectionPlane.w) /
                    dot(normal, _AttachProjectionDirection);
                float3 projected = world - _AttachProjectionDirection * projectionDistance;
                // Z-fighting을 피하기 위해 수신 평면 Normal 방향으로 작은 bias를 더해 렌더
                output.positionCS = TransformWorldToHClip(projected + normal * _AttachProjectionBias);
                return output;
            }

            float ToEyeDepth(float rawDepth)
            {
                // Perspective와 Orthographic 카메라는 depth 변환식이 다르므로 각각 eye-space 거리로 통일
                return unity_OrthoParams.w == 0
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = ToEyeDepth(SampleSceneDepth(uv));
                float fragmentDepth = ToEyeDepth(input.positionCS.z);
                // 투영 조각과 실제 Scene 표면의 eye depth 차이로 '수신 표면에 붙어 있는 픽셀'만 판정
                float depthDifference = abs(sceneDepth - fragmentDepth);
                float tolerance = max(_AttachProjectionDepthTolerance, 0.0001);
                // Fade near receiver intersections; empty space still contributes no color.
                // tolerance의 절반부터 부드럽게 감쇠해 평면 오차나 depth 정밀도 때문에 경계가 끊기는 현상을 줄임
                float receiverFade = 1.0 - smoothstep(tolerance * 0.5, tolerance, depthDifference);
                // 수신 표면에서 너무 멀어진 투영 픽셀은 완전히 버려 빈 공간에 잔상이 남지 않게 함
                clip(receiverFade - 0.0001);
                return float4(_AttachHeldColor.rgb,
                    _AttachHeldColor.a * _AttachProjectionOpacity * saturate(_AttachVisualBlend) * receiverFade);
            }
            ENDHLSL
        }
    }
}
