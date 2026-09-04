using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using UnityEngine;

namespace Phyzzle.Tests
{
    public sealed class AttachVisualSettingsTests
    {
        [Test]
        public void VisualLayerBits_AreDistinctAndDoNotOverlapRewind()
        {
            Assert.That(AttachVisualLayers.Owned,
                Is.EqualTo(AttachVisualLayers.Eligible |
                           AttachVisualLayers.Focused |
                           AttachVisualLayers.Held));
            Assert.That(AttachVisualLayers.Owned & RewindVisualLayers.Owned, Is.Zero);
        }

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
