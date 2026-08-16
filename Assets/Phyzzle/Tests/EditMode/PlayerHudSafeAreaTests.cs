using NUnit.Framework;
using Phyzzle.UI;
using UnityEngine;

namespace Phyzzle.Tests
{
    public sealed class PlayerHudSafeAreaTests
    {
        [Test]
        public void CalculateAnchors_FullScreen_ReturnsFullRect()
        {
            PlayerHudSafeArea.CalculateAnchors(
                new Rect(0f, 0f, 1920f, 1080f),
                new Vector2(1920f, 1080f),
                out Vector2 min,
                out Vector2 max);

            Assert.That(min, Is.EqualTo(Vector2.zero));
            Assert.That(max, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void CalculateAnchors_SideInsets_NormalizesAgainstScreen()
        {
            PlayerHudSafeArea.CalculateAnchors(
                new Rect(100f, 0f, 1800f, 1000f),
                new Vector2(2000f, 1000f),
                out Vector2 min,
                out Vector2 max);

            Assert.That(min, Is.EqualTo(new Vector2(0.05f, 0f)));
            Assert.That(max, Is.EqualTo(new Vector2(0.95f, 1f)));
        }

        [Test]
        public void CalculateAnchors_InvalidScreen_ReturnsFullRect()
        {
            PlayerHudSafeArea.CalculateAnchors(
                new Rect(20f, 20f, 100f, 100f),
                Vector2.zero,
                out Vector2 min,
                out Vector2 max);

            Assert.That(min, Is.EqualTo(Vector2.zero));
            Assert.That(max, Is.EqualTo(Vector2.one));
        }
    }
}
