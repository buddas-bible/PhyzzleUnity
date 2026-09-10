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
                // 상하좌우 이웃 마스크의 최대값을 사용해 별도 Blur 없이 화면 공간 외곽선을 만듦
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
                // 주변 마스크만 있고 현재 픽셀에는 값이 없는 부분을 남겨 대상 바깥쪽 외곽선을 계산
                float eligibleOutline = OutlineAt(
                    uv,
                    max(0.0, _RewindEligibleOutlinePixels),
                    1) * (1.0 - mask.g);
                float activeOutline = OutlineAt(
                    uv,
                    max(0.0, _RewindActiveOutlinePixels),
                    2) * (1.0 - mask.b);

                // Rec.709 luminance 가중치로 원본 색을 명도로 변환한 뒤 saturation 값만큼 원색을 되돌림
                float luminance = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
                float3 world = lerp(
                    luminance.xxx,
                    source.rgb,
                    saturate(_RewindWorldSaturation));
                float eligibleWeight = saturate(mask.g * 0.62 + eligibleOutline * 0.45);
                float activeWeight = saturate(mask.b * 0.82 + activeOutline * 0.72);
                float3 treated = lerp(world, _RewindEligibleColor.rgb, eligibleWeight);
                treated = lerp(treated, _RewindActiveColor.rgb, activeWeight);
                // R 채널은 플레이어 Preserve 마스크. 되감기 월드 효과가 플레이어 본체에는 적용되지 않도록 원본 색으로 복원
                treated = lerp(treated, source.rgb, saturate(mask.r));
                float3 result = lerp(source.rgb, treated, saturate(_RewindSelectionBlend));
                return float4(result, source.a);
            }
            ENDHLSL
        }
    }
}
