using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Phyzzle.Abilities;
using Phyzzle.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>PlayerHudViewPlayModeTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class PlayerHudViewPlayModeTests
    {
        private GameObject host;
        private PlayerHudView view;
        private GameObject defaultCrosshair;
        private GameObject targetCrosshair;
        private GameObject rotationArrow;
        private PlayerHudPromptSet gamepad;
        private PlayerHudPromptSet keyboard;
        private PlayerHudAbilitySelector abilitySelector;
        private PlayerHudAbilitySlot previousAbility;
        private PlayerHudAbilitySlot currentAbility;
        private PlayerHudAbilitySlot nextAbility;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            host = new GameObject("HUD Test Host");
            view = host.AddComponent<PlayerHudView>();
            defaultCrosshair = Child("Default Crosshair");
            targetCrosshair = Child("Target Crosshair");
            rotationArrow = Child("Rotation Arrow");
            gamepad = CreatePromptSet("Gamepad");
            keyboard = CreatePromptSet("Keyboard");
            abilitySelector = CreateAbilitySelector();
            view.Configure(
                defaultCrosshair,
                targetCrosshair,
                rotationArrow,
                gamepad,
                keyboard,
                abilitySelector);
        }

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        /// <summary>
        /// <c>SelectingTarget_ActivatesCrosshairsAndGamepadPrompts</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator SelectingTarget_ActivatesCrosshairsAndGamepadPrompts()
        {
            view.Render(
                new PlayerHudState(PlayerHudMode.AttachSelecting, hasTarget: true),
                PlayerInputDeviceKind.Gamepad);
            yield return null;

            Assert.That(defaultCrosshair.activeSelf, Is.True);
            Assert.That(targetCrosshair.activeSelf, Is.True);
            Assert.That(gamepad.Root.activeSelf, Is.True);
            Assert.That(gamepad.AttachDefault.activeSelf, Is.True);
            Assert.That(gamepad.Catch.activeSelf, Is.True);
            Assert.That(keyboard.Root.activeSelf, Is.False);
        }

        /// <summary>
        /// <c>KeyboardSwitch_ChangesPromptRootWithoutChangingCrosshair</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator KeyboardSwitch_ChangesPromptRootWithoutChangingCrosshair()
        {
            PlayerHudState state = new(PlayerHudMode.RewindSelecting, hasTarget: false);
            view.Render(state, PlayerInputDeviceKind.Gamepad);
            view.Render(state, PlayerInputDeviceKind.Keyboard);
            yield return null;

            Assert.That(defaultCrosshair.activeSelf, Is.True);
            Assert.That(gamepad.Root.activeSelf, Is.False);
            Assert.That(keyboard.Root.activeSelf, Is.True);
            Assert.That(keyboard.AttachDefault.activeSelf, Is.True);
        }

        /// <summary>
        /// <c>HoldingIslandRotation_ActivatesRotationPromptAndArrow</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator HoldingIslandRotation_ActivatesRotationPromptAndArrow()
        {
            view.Render(
                new PlayerHudState(
                    PlayerHudMode.AttachHolding,
                    rotateMode: true,
                    islandSize: 2),
                PlayerInputDeviceKind.Gamepad);
            yield return null;

            Assert.That(rotationArrow.activeSelf, Is.True);
            Assert.That(gamepad.RotationIsland.activeSelf, Is.True);
            Assert.That(gamepad.RotationSingle.activeSelf, Is.False);
            Assert.That(gamepad.AttachHoldIsland.activeSelf, Is.False);
        }

        /// <summary>
        /// <c>Default_HidesContextVisualsAndKeepsSelectedAbility</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator Default_HidesContextVisualsAndKeepsSelectedAbility()
        {
            view.Render(
                new PlayerHudState(PlayerHudMode.AttachSelecting, hasTarget: true),
                PlayerInputDeviceKind.Gamepad);
            view.Render(
                new PlayerHudState(
                    PlayerHudMode.Default,
                    selectedAbility: PlayerAbilityController.AbilityKind.Rewind),
                PlayerInputDeviceKind.Gamepad);
            yield return null;

            Assert.That(defaultCrosshair.activeSelf, Is.False);
            Assert.That(targetCrosshair.activeSelf, Is.False);
            Assert.That(rotationArrow.activeSelf, Is.False);
            Assert.That(gamepad.Root.activeSelf, Is.False);
            Assert.That(keyboard.Root.activeSelf, Is.False);
            Assert.That(abilitySelector.Root.activeSelf, Is.True);
            Assert.That(currentAbility.Rewind.activeSelf, Is.True);
            Assert.That(currentAbility.Attach.activeSelf, Is.False);
            Assert.That(previousAbility.Root.activeSelf, Is.False);
            Assert.That(nextAbility.Root.activeSelf, Is.False);
        }

        /// <summary>
        /// <c>ChangedAbility_ShowsTheOtherAbilityOnBothDimSideSlots</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator ChangedAbility_ShowsTheOtherAbilityOnBothDimSideSlots()
        {
            view.Render(
                new PlayerHudState(
                    PlayerHudMode.Default,
                    selectedAbility: PlayerAbilityController.AbilityKind.Attach,
                    showAbilityNeighbors: true),
                PlayerInputDeviceKind.Gamepad);
            yield return null;

            Assert.That(currentAbility.Attach.activeSelf, Is.True);
            Assert.That(currentAbility.Rewind.activeSelf, Is.False);
            Assert.That(previousAbility.Rewind.activeSelf, Is.True);
            Assert.That(nextAbility.Rewind.activeSelf, Is.True);
            Assert.That(previousAbility.Attach.activeSelf, Is.False);
            Assert.That(nextAbility.Attach.activeSelf, Is.False);
            Assert.That(previousAbility.Root.GetComponent<CanvasGroup>().alpha, Is.LessThan(1f));
            Assert.That(nextAbility.Root.GetComponent<CanvasGroup>().alpha, Is.LessThan(1f));
        }

        /// <summary>
        /// <c>Presenter_SelectionChangeUsesUnscaledTimeoutThenHidesNeighbors</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator Presenter_SelectionChangeUsesUnscaledTimeoutThenHidesNeighbors()
        {
            GameObject player = new("Ability Player");
            PlayerAbilityController abilities = player.AddComponent<PlayerAbilityController>();
            PlayerHudPresenter presenter = host.AddComponent<PlayerHudPresenter>();
            presenter.Configure(null, abilities, null, null, null, null, view);
            FieldInfo duration = typeof(PlayerHudPresenter).GetField(
                "abilitySelectionDuration",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(duration, Is.Not.Null);
            duration.SetValue(presenter, 0.02f);
            yield return null;

            abilities.Select(PlayerAbilityController.AbilityKind.Rewind);
            yield return null;
            Assert.That(previousAbility.Root.activeSelf, Is.True);
            Assert.That(nextAbility.Root.activeSelf, Is.True);

            float previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.04f);
            yield return null;
            Time.timeScale = previousTimeScale;

            Assert.That(currentAbility.Rewind.activeSelf, Is.True);
            Assert.That(previousAbility.Root.activeSelf, Is.False);
            Assert.That(nextAbility.Root.activeSelf, Is.False);
            Object.DestroyImmediate(player);
        }

        /// <summary>
        /// <c>Presenter_ReenableInitializesCurrentAbilityWithoutFalseCarousel</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator Presenter_ReenableInitializesCurrentAbilityWithoutFalseCarousel()
        {
            GameObject player = new("Reenabled Ability Player");
            PlayerAbilityController abilities = player.AddComponent<PlayerAbilityController>();
            PlayerHudPresenter presenter = host.AddComponent<PlayerHudPresenter>();
            presenter.Configure(null, abilities, null, null, null, null, view);
            yield return null;

            presenter.enabled = false;
            abilities.Select(PlayerAbilityController.AbilityKind.Rewind);
            presenter.enabled = true;
            yield return null;

            Assert.That(currentAbility.Rewind.activeSelf, Is.True);
            Assert.That(previousAbility.Root.activeSelf, Is.False);
            Assert.That(nextAbility.Root.activeSelf, Is.False);
            Object.DestroyImmediate(player);
        }

        /// <summary>
        /// <c>CreatePromptSet</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private PlayerHudPromptSet CreatePromptSet(string name)
        {
            return new PlayerHudPromptSet(
                Child(name),
                Child(name + " Attach Default"),
                Child(name + " Catch"),
                Child(name + " Hold Single"),
                Child(name + " Hold Island"),
                Child(name + " Rotation Single"),
                Child(name + " Rotation Island"),
                Child(name + " Stick"));
        }

        /// <summary>
        /// <c>CreateAbilitySelector</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private PlayerHudAbilitySelector CreateAbilitySelector()
        {
            GameObject root = Child("Ability Selector");
            previousAbility = CreateAbilitySlot(root.transform, "Previous", 0.3f);
            currentAbility = CreateAbilitySlot(root.transform, "Current", 1f);
            nextAbility = CreateAbilitySlot(root.transform, "Next", 0.3f);
            return new PlayerHudAbilitySelector(
                root,
                previousAbility,
                currentAbility,
                nextAbility);
        }

        /// <summary>
        /// <c>CreateAbilitySlot</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private PlayerHudAbilitySlot CreateAbilitySlot(
            Transform parent,
            string name,
            float alpha)
        {
            GameObject root = new(name);
            root.transform.SetParent(parent);
            root.AddComponent<CanvasGroup>().alpha = alpha;
            GameObject attach = new("Attach");
            attach.transform.SetParent(root.transform);
            GameObject rewind = new("Rewind");
            rewind.transform.SetParent(root.transform);
            return new PlayerHudAbilitySlot(root, attach, rewind);
        }

        /// <summary>
        /// <c>Child</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private GameObject Child(string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(host.transform);
            return child;
        }
    }
}
