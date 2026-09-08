using UnityEngine.Rendering.Universal;

namespace Phyzzle.Tests
{
    /// <summary>
    /// 테스트에서 사용하는 <c>SentinelRenderFeature</c> 보조 타입이다.
    /// </summary>
    internal sealed class SentinelRenderFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// <c>Create</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        public override void Create()
        {
        }

        /// <summary>
        /// <c>AddRenderPasses</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
        }
    }
}
