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
                float projectionDistance = (dot(normal, world) + _AttachProjectionPlane.w) /
                    dot(normal, _AttachProjectionDirection);
                float3 projected = world - _AttachProjectionDirection * projectionDistance;
                output.positionCS = TransformWorldToHClip(projected + normal * _AttachProjectionBias);
                return output;
            }

            float ToEyeDepth(float rawDepth)
            {
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
                clip(_AttachProjectionDepthTolerance - abs(sceneDepth - fragmentDepth));
                return float4(_AttachHeldColor.rgb,
                    _AttachHeldColor.a * _AttachProjectionOpacity * saturate(_AttachVisualBlend));
            }
            ENDHLSL
        }
    }
}
