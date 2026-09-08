using NUnit.Framework;
using Phyzzle.Abilities;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using Phyzzle.UI;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>PlayerHudPresenterStateTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class PlayerHudPresenterStateTests
    {
        /// <summary>
        /// <c>AttachSelecting_MapsTargetState</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void AttachSelecting_MapsTargetState()
        {
            PlayerHudState state = PlayerHudPresenterState.Build(
                AttachAbilityController.AbilityState.Selecting,
                attachHasTarget: true,
                RewindAbilityController.AbilityState.Default,
                rewindHasTarget: false,
                rotateHeld: false,
                touchingAttachable: false,
                islandSize: 1);

            Assert.That(state.Mode, Is.EqualTo(PlayerHudMode.AttachSelecting));
            Assert.That(state.HasTarget, Is.True);
        }

        /// <summary>
        /// <c>AttachHolding_MapsHoldDetails</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void AttachHolding_MapsHoldDetails()
        {
            PlayerHudState state = PlayerHudPresenterState.Build(
                AttachAbilityController.AbilityState.Holding,
                attachHasTarget: false,
                RewindAbilityController.AbilityState.Default,
                rewindHasTarget: false,
                rotateHeld: true,
                touchingAttachable: true,
                islandSize: 3);

            Assert.That(state.Mode, Is.EqualTo(PlayerHudMode.AttachHolding));
            Assert.That(state.RotateMode, Is.True);
            Assert.That(state.TouchingAttachable, Is.True);
            Assert.That(state.IslandSize, Is.EqualTo(3));
        }

        /// <summary>
        /// <c>RewindSelecting_MapsRewindTarget</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void RewindSelecting_MapsRewindTarget()
        {
            PlayerHudState state = PlayerHudPresenterState.Build(
                AttachAbilityController.AbilityState.Default,
                attachHasTarget: false,
                RewindAbilityController.AbilityState.Selecting,
                rewindHasTarget: true,
                rotateHeld: false,
                touchingAttachable: false,
                islandSize: 1);

            Assert.That(state.Mode, Is.EqualTo(PlayerHudMode.RewindSelecting));
            Assert.That(state.HasTarget, Is.True);
        }

        /// <summary>
        /// <c>DefaultControllers_MapHiddenHud</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void DefaultControllers_MapHiddenHud()
        {
            PlayerHudState state = PlayerHudPresenterState.Build(
                AttachAbilityController.AbilityState.Default,
                attachHasTarget: false,
                RewindAbilityController.AbilityState.Default,
                rewindHasTarget: false,
                rotateHeld: false,
                touchingAttachable: false,
                islandSize: 1);

            Assert.That(state.Mode, Is.EqualTo(PlayerHudMode.Default));
        }

        /// <summary>
        /// <c>AbilitySelection_IsPassedToHudState</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void AbilitySelection_IsPassedToHudState()
        {
            PlayerHudState state = PlayerHudPresenterState.Build(
                AttachAbilityController.AbilityState.Default,
                attachHasTarget: false,
                RewindAbilityController.AbilityState.Default,
                rewindHasTarget: false,
                rotateHeld: false,
                touchingAttachable: false,
                islandSize: 1,
                selectedAbility: PlayerAbilityController.AbilityKind.Rewind,
                showAbilityNeighbors: true);

            Assert.That(state.SelectedAbility,
                Is.EqualTo(PlayerAbilityController.AbilityKind.Rewind));
            Assert.That(state.ShowAbilityNeighbors, Is.True);
        }
    }
}
