Shader "Hidden/Phyzzle/AttachComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Attach Composite"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_AttachMaskTexture);

            float _AttachVisualBlend;
            float4 _AttachEligibleColor;
            float4 _AttachFocusedColor;
            float4 _AttachHeldColor;
            float _AttachEligibleOutlinePixels;
            float _AttachFocusedOutlinePixels;
            float _AttachHeldOutlinePixels;
            float _AttachHeldPulseSpeed;
            float _AttachHeldPulseStrength;

            float3 SampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_AttachMaskTexture, sampler_PointClamp, uv).rgb;
            }

            float OutlineAt(float2 uv, float radiusPixels, int channel)
            {
                // 현재 픽셀에서 상하좌우 radius만큼 떨어진 마스크를 샘플링해 주변에 대상이 있는지 확인
                float2 offsetX = float2(_BlitTexture_TexelSize.x * radiusPixels, 0.0);
                float2 offsetY = float2(0.0, _BlitTexture_TexelSize.y * radiusPixels);
                float4 samples = float4(
                    SampleMask(uv + offsetX)[channel],
                    SampleMask(uv - offsetX)[channel],
                    SampleMask(uv + offsetY)[channel],
                    SampleMask(uv - offsetY)[channel]);
                // 네 방향 중 하나라도 마스크가 있으면 현재 픽셀을 외곽선 후보로 사용
                return max(max(samples.x, samples.y), max(samples.z, samples.w));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 source = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
                float3 mask = SampleMask(uv);
                // 주변에는 마스크가 있지만 현재 픽셀에는 없는 영역만 남겨 내부 채움과 겹치지 않는 외곽선을 생성
                float eligibleOutline = OutlineAt(uv, max(0.0, _AttachEligibleOutlinePixels), 0) * (1.0 - mask.r);
                float focusedOutline = OutlineAt(uv, max(0.0, _AttachFocusedOutlinePixels), 1) * (1.0 - mask.g);
                float heldOutline = OutlineAt(uv, max(0.0, _AttachHeldOutlinePixels), 2) * (1.0 - mask.b);

                // 내부 마스크와 외곽선의 가중치를 다르게 섞어 대상 내부는 약하게, 선택 상태의 테두리는 더 선명하게 표현
                float eligibleWeight = saturate(mask.r * 0.62 + eligibleOutline * 0.45);
                float focusedWeight = saturate(mask.g * 0.82 + focusedOutline * 0.72);
                // UV 대각선 위상 + 시간으로 Held 상태에 흐르는 주기적인 밝기 변화를 생성
                float pulse = 1.0 + sin((uv.x + uv.y) * 36.0 + _Time.y *
                    _AttachHeldPulseSpeed * 6.2831853) * _AttachHeldPulseStrength;
                float heldWeight = saturate(mask.b * 0.82 + heldOutline * 0.72);
                // Eligible -> Focused -> Held 순서로 덮어써 더 강한 상호작용 상태가 최종 색상 우선권을 가짐
                float3 treated = lerp(source.rgb, _AttachEligibleColor.rgb, eligibleWeight);
                treated = lerp(treated, _AttachFocusedColor.rgb, focusedWeight);
                treated = lerp(treated, _AttachHeldColor.rgb * pulse, heldWeight);
                return float4(lerp(source.rgb, treated, saturate(_AttachVisualBlend)), source.a);
            }
            ENDHLSL
        }
    }
}
