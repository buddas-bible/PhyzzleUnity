using NUnit.Framework;
using Phyzzle.UI;
using UnityEngine;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>PlayerHudSafeAreaTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class PlayerHudSafeAreaTests
    {
        /// <summary>
        /// <c>CalculateAnchors_FullScreen_ReturnsFullRect</c> 테스트 시나리오를 검증한다.
        /// </summary>
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

        /// <summary>
        /// <c>CalculateAnchors_SideInsets_NormalizesAgainstScreen</c> 테스트 시나리오를 검증한다.
        /// </summary>
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

        /// <summary>
        /// <c>CalculateAnchors_InvalidScreen_ReturnsFullRect</c> 테스트 시나리오를 검증한다.
        /// </summary>
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
