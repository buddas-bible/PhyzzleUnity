Shader "Hidden/Phyzzle/RewindMask"
{
    Properties
    {
        [HideInInspector] _RewindPreviewMaskWeight("Preview Mask Weight", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Back
        ZWrite Off
        ZTest LEqual
        Blend One One
        BlendOp Max

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _RewindPreviewMaskWeight;
        CBUFFER_END

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

        Varyings MaskVertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Player Preserve"
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment Fragment
            float4 Fragment(Varyings input) : SV_Target { return float4(1, 0, 0, 1); }
            ENDHLSL
        }

        Pass
        {
            Name "Eligible"
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment Fragment
            float4 Fragment(Varyings input) : SV_Target { return float4(0, 1, 0, 1); }
            ENDHLSL
        }

        Pass
        {
            Name "Active"
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment Fragment
            float4 Fragment(Varyings input) : SV_Target
            {
                return float4(0, 0, saturate(_RewindPreviewMaskWeight), 1);
            }
            ENDHLSL
        }
    }
}
