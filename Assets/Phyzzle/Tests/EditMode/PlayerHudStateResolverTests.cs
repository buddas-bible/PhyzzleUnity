using NUnit.Framework;
using Phyzzle.UI;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>PlayerHudStateResolverTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class PlayerHudStateResolverTests
    {
        /// <summary>
        /// <c>Default_HidesAbilityHud</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Default_HidesAbilityHud()
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(PlayerHudMode.Default));

            Assert.That(visual.AnyVisible, Is.False);
        }

        /// <summary>
        /// <c>SelectingWithoutTarget_ShowsDefaultCrosshairAndSearchPrompt</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(PlayerHudMode.AttachSelecting)]
        [TestCase(PlayerHudMode.RewindSelecting)]
        public void SelectingWithoutTarget_ShowsDefaultCrosshairAndSearchPrompt(PlayerHudMode mode)
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(mode, hasTarget: false));

            Assert.That(visual.DefaultCrosshair, Is.True);
            Assert.That(visual.TargetCrosshair, Is.False);
            Assert.That(visual.AttachDefault, Is.True);
            Assert.That(visual.Catch, Is.False);
        }

        /// <summary>
        /// <c>SelectingTarget_ShowsTargetCrosshairAndCatchPrompt</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(PlayerHudMode.AttachSelecting)]
        [TestCase(PlayerHudMode.RewindSelecting)]
        public void SelectingTarget_ShowsTargetCrosshairAndCatchPrompt(PlayerHudMode mode)
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(mode, hasTarget: true));

            Assert.That(visual.DefaultCrosshair, Is.True);
            Assert.That(visual.TargetCrosshair, Is.True);
            Assert.That(visual.AttachDefault, Is.True);
            Assert.That(visual.Catch, Is.True);
        }

        /// <summary>
        /// <c>HoldingSingleObject_ShowsSingleDetachPrompt</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void HoldingSingleObject_ShowsSingleDetachPrompt()
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(PlayerHudMode.AttachHolding, islandSize: 1));

            Assert.That(visual.AttachHoldSingle, Is.True);
            Assert.That(visual.AttachHoldIsland, Is.False);
            Assert.That(visual.RotationArrow, Is.False);
        }

        /// <summary>
        /// <c>HoldingIsland_ShowsIslandDetachPrompt</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void HoldingIsland_ShowsIslandDetachPrompt()
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(PlayerHudMode.AttachHolding, islandSize: 2));

            Assert.That(visual.AttachHoldSingle, Is.False);
            Assert.That(visual.AttachHoldIsland, Is.True);
        }

        /// <summary>
        /// <c>HoldingRotate_ShowsSizeSpecificRotationPrompt</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(1, true, false)]
        [TestCase(2, false, true)]
        public void HoldingRotate_ShowsSizeSpecificRotationPrompt(
            int islandSize,
            bool expectedSingle,
            bool expectedIsland)
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(
                    PlayerHudMode.AttachHolding,
                    rotateMode: true,
                    islandSize: islandSize));

            Assert.That(visual.RotationSingle, Is.EqualTo(expectedSingle));
            Assert.That(visual.RotationIsland, Is.EqualTo(expectedIsland));
            Assert.That(visual.RotationArrow, Is.True);
            Assert.That(visual.AttachHoldSingle, Is.False);
            Assert.That(visual.AttachHoldIsland, Is.False);
        }

        /// <summary>
        /// <c>HoldingWhileTouching_ShowsAttachPrompt</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void HoldingWhileTouching_ShowsAttachPrompt()
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(PlayerHudMode.AttachHolding, touchingAttachable: true));

            Assert.That(visual.Stick, Is.True);
        }
    }
}
