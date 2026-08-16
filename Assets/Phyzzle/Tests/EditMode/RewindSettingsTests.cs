using System.IO;
using NUnit.Framework;
using Phyzzle.Abilities.Rewind;
using UnityEditor;
using UnityEngine;

namespace Phyzzle.Tests
{
    public sealed class RewindSettingsTests
    {
        [Test]
        public void Defaults_UseTwentySecondsAndCurrentFixedTickCapacity()
        {
            RewindSettings settings = ScriptableObject.CreateInstance<RewindSettings>();
            try
            {
                Assert.That(settings.historyDuration, Is.EqualTo(20f));
                Assert.That(settings.playbackRate, Is.EqualTo(1f));
                Assert.That(settings.GetHistoryCapacity(0.02f), Is.EqualTo(1001));

                float activeFixedDeltaTime = Time.fixedDeltaTime;
                int activeCapacity = settings.GetHistoryCapacity(activeFixedDeltaTime);
                float representedWindow = (activeCapacity - 1) * activeFixedDeltaTime;
                float firstEvictionTime = activeCapacity * activeFixedDeltaTime;

                Assert.That(activeCapacity, Is.EqualTo(1001));
                Assert.That(Mathf.Abs(representedWindow - settings.historyDuration),
                    Is.LessThanOrEqualTo(activeFixedDeltaTime * 0.5f));
                Assert.That(firstEvictionTime,
                    Is.GreaterThanOrEqualTo(settings.historyDuration));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void SelectionVisualDefaults_MatchApprovedPcPresentation()
        {
            RewindSettings settings = ScriptableObject.CreateInstance<RewindSettings>();
            try
            {
                Assert.That(settings.selectionVisualEnterDuration, Is.EqualTo(0.14f));
                Assert.That(settings.selectionVisualExitDuration, Is.EqualTo(0.16f));
                Assert.That(settings.selectionWorldSaturation, Is.EqualTo(0.12f));
                Assert.That(settings.eligibleVisualColor,
                    Is.EqualTo((Color)new Color32(0xD7, 0xA5, 0x2D, 0xFF)));
                Assert.That(settings.activeVisualColor,
                    Is.EqualTo((Color)new Color32(0xFF, 0xD4, 0x5C, 0xFF)));
                Assert.That(settings.eligibleOutlinePixels, Is.EqualTo(1f));
                Assert.That(settings.activeOutlinePixels, Is.EqualTo(2.5f));
                Assert.That(settings.previewPathWidth, Is.EqualTo(0.06f));
                Assert.That(settings.previewPathMaxPoints, Is.EqualTo(256));
                Assert.That(settings.previewGhostCount, Is.EqualTo(8));
                Assert.That(settings.previewGhostAlpha, Is.EqualTo(0.28f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ProjectAsset_SerializesDesignerVisualTuning()
        {
            const string path = "Assets/Phyzzle/Settings/RewindSettings.asset";
            Assert.That(AssetDatabase.LoadAssetAtPath<RewindSettings>(path), Is.Not.Null);
            string yaml = File.ReadAllText(Path.GetFullPath(path));
            StringAssert.Contains("selectionVisualEnterDuration: 0.14", yaml);
            StringAssert.Contains("selectionVisualExitDuration: 0.16", yaml);
            StringAssert.Contains("previewPathMaxPoints: 256", yaml);
            StringAssert.Contains("previewGhostCount: 8", yaml);
            StringAssert.Contains("previewGhostAlpha: 0.28", yaml);
        }
    }
}
