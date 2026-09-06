Shader "Hidden/Phyzzle/Tests/AttachMaskProbe"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D(_AttachMaskTexture);
            float4 Frag(Varyings input) : SV_Target
            {
                return float4(SAMPLE_TEXTURE2D(_AttachMaskTexture, sampler_PointClamp, input.texcoord).rgb, 1);
            }
            ENDHLSL
        }
    }
}
