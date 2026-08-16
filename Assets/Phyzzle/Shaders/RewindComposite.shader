Shader "Hidden/Phyzzle/RewindComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Rewind Composite"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_RewindMaskTexture);

            float _RewindSelectionBlend;
            float _RewindWorldSaturation;
            float4 _RewindEligibleColor;
            float4 _RewindActiveColor;
            float _RewindEligibleOutlinePixels;
            float _RewindActiveOutlinePixels;

            float3 SampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_RewindMaskTexture, sampler_PointClamp, uv).rgb;
            }

            float OutlineAt(float2 uv, float radiusPixels, int channel)
            {
                float2 offsetX = float2(_BlitTexture_TexelSize.x * radiusPixels, 0.0);
                float2 offsetY = float2(0.0, _BlitTexture_TexelSize.y * radiusPixels);
                float4 samples = float4(
                    SampleMask(uv + offsetX)[channel],
                    SampleMask(uv - offsetX)[channel],
                    SampleMask(uv + offsetY)[channel],
                    SampleMask(uv - offsetY)[channel]);
                return max(max(samples.x, samples.y), max(samples.z, samples.w));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 source = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv,
                    _BlitMipLevel);
                float3 mask = SampleMask(uv);
                float eligibleOutline = OutlineAt(
                    uv,
                    max(0.0, _RewindEligibleOutlinePixels),
                    1) * (1.0 - mask.g);
                float activeOutline = OutlineAt(
                    uv,
                    max(0.0, _RewindActiveOutlinePixels),
                    2) * (1.0 - mask.b);

                float luminance = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
                float3 world = lerp(
                    luminance.xxx,
                    source.rgb,
                    saturate(_RewindWorldSaturation));
                float eligibleWeight = saturate(mask.g * 0.62 + eligibleOutline * 0.45);
                float activeWeight = saturate(mask.b * 0.82 + activeOutline * 0.72);
                float3 treated = lerp(world, _RewindEligibleColor.rgb, eligibleWeight);
                treated = lerp(treated, _RewindActiveColor.rgb, activeWeight);
                treated = lerp(treated, source.rgb, saturate(mask.r));
                float3 result = lerp(source.rgb, treated, saturate(_RewindSelectionBlend));
                return float4(result, source.a);
            }
            ENDHLSL
        }
    }
}
