using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using UnityEngine;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>AttachVisualSettingsTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class AttachVisualSettingsTests
    {
        /// <summary>
        /// <c>VisualLayerBits_AreDistinctAndDoNotOverlapRewind</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void VisualLayerBits_AreDistinctAndDoNotOverlapRewind()
        {
            Assert.That(AttachVisualLayers.Owned,
                Is.EqualTo(AttachVisualLayers.Eligible |
                           AttachVisualLayers.Focused |
                           AttachVisualLayers.Held));
            Assert.That(AttachVisualLayers.Owned & RewindVisualLayers.Owned, Is.Zero);
        }

        /// <summary>
        /// <c>VisualDefaults_MatchApprovedDesign</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void VisualDefaults_MatchApprovedDesign()
        {
            AttachSettings settings = ScriptableObject.CreateInstance<AttachSettings>();
            Assert.That(settings.visualEnterDuration, Is.EqualTo(0.14f));
            Assert.That(settings.visualExitDuration, Is.EqualTo(0.16f));
            Assert.That(settings.projectionMaxDistance, Is.EqualTo(12f));
            Assert.That(settings.projectionDepthTolerance, Is.EqualTo(0.06f));
            Object.DestroyImmediate(settings);
        }
    }
}
