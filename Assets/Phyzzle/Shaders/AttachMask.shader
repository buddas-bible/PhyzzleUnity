Shader "Hidden/Phyzzle/AttachMask"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Back
        ZWrite Off
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Eligible"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target { return float4(1, 0, 0, 1); }
            ENDHLSL
        }

        Pass
        {
            Name "Focused"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target { return float4(0, 1, 0, 1); }
            ENDHLSL
        }

        Pass
        {
            Name "Held"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target { return float4(0, 0, 1, 1); }
            ENDHLSL
        }
    }
}
