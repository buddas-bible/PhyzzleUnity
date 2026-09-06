Shader "Phyzzle/AttachContactPreview"
{
    Properties
    {
        [HDR] _ContactColor ("Glue Color", Color) = (0.263, 0.941, 0.541, 1)
        _PreviewTime ("Animation Time", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+20" "RenderType" = "Transparent" }
        Cull Back
        ZTest Always
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Attach Contact Preview"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ContactColor;
                float _PreviewTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float ripple = sin(dot(input.positionOS.xyz, float3(8, 11, 6)) + _PreviewTime * 3.0)
                    * sin(dot(input.positionOS.xyz, float3(-5, 7, 9)) - _PreviewTime * 2.0);
                float3 position = input.positionOS.xyz + input.normalOS * ripple * 0.018;
                output.positionWS = TransformObjectToWorld(position);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
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
                float sceneDepth = ToEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS)));
                float fragmentDepth = ToEyeDepth(input.positionCS.z);
                // A contact at the center of two faces is buried inside both meshes. Keep only a faint cue there.
                float visibility = lerp(1.0, 0.12, smoothstep(0.005, 0.025, fragmentDepth - sceneDepth));
                float facing = saturate(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
                float rim = pow(1.0 - facing, 2.0);
                float flow = sin(dot(input.positionWS, float3(7, 12, 5)) - _PreviewTime * 4.0) * 0.5 + 0.5;
                float3 color = _ContactColor.rgb * (0.8 + rim * 0.35 + flow * 0.08);
                float alpha = _ContactColor.a * (0.62 + rim * 0.3) * visibility;
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
