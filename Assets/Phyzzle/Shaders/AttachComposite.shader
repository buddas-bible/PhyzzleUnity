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
                    _BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
                float3 mask = SampleMask(uv);
                float eligibleOutline = OutlineAt(uv, max(0.0, _AttachEligibleOutlinePixels), 0) * (1.0 - mask.r);
                float focusedOutline = OutlineAt(uv, max(0.0, _AttachFocusedOutlinePixels), 1) * (1.0 - mask.g);
                float heldOutline = OutlineAt(uv, max(0.0, _AttachHeldOutlinePixels), 2) * (1.0 - mask.b);

                float eligibleWeight = saturate(mask.r * 0.62 + eligibleOutline * 0.45);
                float focusedWeight = saturate(mask.g * 0.82 + focusedOutline * 0.72);
                float pulse = 1.0 + sin((uv.x + uv.y) * 36.0 + _Time.y *
                    _AttachHeldPulseSpeed * 6.2831853) * _AttachHeldPulseStrength;
                float heldWeight = saturate(mask.b * 0.82 + heldOutline * 0.72);
                float3 treated = lerp(source.rgb, _AttachEligibleColor.rgb, eligibleWeight);
                treated = lerp(treated, _AttachFocusedColor.rgb, focusedWeight);
                treated = lerp(treated, _AttachHeldColor.rgb * pulse, heldWeight);
                return float4(lerp(source.rgb, treated, saturate(_AttachVisualBlend)), source.a);
            }
            ENDHLSL
        }
    }
}
