using NUnit.Framework;
using Phyzzle.UI;

namespace Phyzzle.Tests
{
    public sealed class PlayerHudStateResolverTests
    {
        [Test]
        public void Default_HidesAbilityHud()
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(PlayerHudMode.Default));

            Assert.That(visual.AnyVisible, Is.False);
        }

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

        [Test]
        public void HoldingSingleObject_ShowsSingleDetachPrompt()
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(PlayerHudMode.AttachHolding, islandSize: 1));

            Assert.That(visual.AttachHoldSingle, Is.True);
            Assert.That(visual.AttachHoldIsland, Is.False);
            Assert.That(visual.RotationArrow, Is.False);
        }

        [Test]
        public void HoldingIsland_ShowsIslandDetachPrompt()
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(PlayerHudMode.AttachHolding, islandSize: 2));

            Assert.That(visual.AttachHoldSingle, Is.False);
            Assert.That(visual.AttachHoldIsland, Is.True);
        }

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

        [Test]
        public void HoldingWhileTouching_ShowsAttachPrompt()
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(
                new PlayerHudState(PlayerHudMode.AttachHolding, touchingAttachable: true));

            Assert.That(visual.Stick, Is.True);
        }
    }
}
