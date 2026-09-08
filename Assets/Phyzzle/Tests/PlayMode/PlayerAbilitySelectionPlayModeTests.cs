using System.Collections;
using NUnit.Framework;
using Phyzzle.Abilities;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>PlayerAbilitySelectionPlayModeTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class PlayerAbilitySelectionPlayModeTests
    {
        private readonly InputTestFixture inputFixture = new();
        private GameObject playerObject;
        private GameObject targetObject;
        private GameObject serviceObject;
        private PlayerInputReader input;
        private PlayerAbilityController abilities;
        private AttachAbilityController attach;
        private AttachSettings attachSettings;
        private Keyboard keyboard;
        private Gamepad gamepad;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            inputFixture.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();

            playerObject = new GameObject("Ability Selection Player");
            GameObject modelObject = new("Model");
            GameObject armObject = new("CameraArm");
            GameObject coreObject = new("CameraCore");
            modelObject.transform.SetParent(playerObject.transform, false);
            armObject.transform.SetParent(playerObject.transform, false);
            coreObject.transform.SetParent(armObject.transform, false);
            armObject.transform.localPosition = new Vector3(0f, 2f, 0f);
            coreObject.transform.localPosition = new Vector3(0f, 0f, -4f);

            input = playerObject.AddComponent<PlayerInputReader>();
            attachSettings = ScriptableObject.CreateInstance<AttachSettings>();
            serviceObject = new GameObject("AttachmentService");
            AttachmentService service = serviceObject.AddComponent<AttachmentService>();
            service.Configure(attachSettings);

            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "Ability Selection Target";
            targetObject.transform.position = new Vector3(0f, 2f, 5f);
            Rigidbody targetBody = targetObject.AddComponent<Rigidbody>();
            targetBody.useGravity = false;
            AttachableObject attachable = targetObject.AddComponent<AttachableObject>();
            attachable.Configure(service);

            AttachTargeting targeting = playerObject.AddComponent<AttachTargeting>();
            targeting.Configure(armObject.transform, coreObject.transform, attachSettings);
            AttachHoldController hold = playerObject.AddComponent<AttachHoldController>();
            hold.Configure(modelObject.transform, null, service, attachSettings);
            attach = playerObject.AddComponent<AttachAbilityController>();
            attach.Configure(null, null, targeting, hold, attachSettings);
            RewindAbilityController rewind = playerObject.AddComponent<RewindAbilityController>();
            abilities = playerObject.AddComponent<PlayerAbilityController>();
            abilities.Configure(null, attach, rewind);
            Physics.SyncTransforms();
        }

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerObject);
            Object.Destroy(targetObject);
            Object.Destroy(serviceObject);
            Object.Destroy(attachSettings);
            yield return null;
            inputFixture.TearDown();
        }

        /// <summary>
        /// <c>GamepadDpadRight_CyclesRewindBackToAttach</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void GamepadDpadRight_CyclesRewindBackToAttach()
        {
            PressGamepad(GamepadButton.DpadRight);
            Assert.That(abilities.SelectedAbility,
                Is.EqualTo(PlayerAbilityController.AbilityKind.Rewind));
            ReleaseGamepad();

            PressGamepad(GamepadButton.DpadRight);

            Assert.That(abilities.SelectedAbility,
                Is.EqualTo(PlayerAbilityController.AbilityKind.Attach));
        }

        /// <summary>
        /// <c>KeyboardLeftArrow_CyclesAttachBackToRewind</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void KeyboardLeftArrow_CyclesAttachBackToRewind()
        {
            PressKeyboard(Key.LeftArrow);

            Assert.That(abilities.SelectedAbility,
                Is.EqualTo(PlayerAbilityController.AbilityKind.Rewind));
        }

        /// <summary>
        /// <c>KeyboardDigits_DirectlySelectAttachAndRewind</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void KeyboardDigits_DirectlySelectAttachAndRewind()
        {
            PressKeyboard(Key.Digit2);
            Assert.That(abilities.SelectedAbility,
                Is.EqualTo(PlayerAbilityController.AbilityKind.Rewind));
            ReleaseKeyboard();

            PressKeyboard(Key.Digit1);

            Assert.That(abilities.SelectedAbility,
                Is.EqualTo(PlayerAbilityController.AbilityKind.Attach));
        }

        /// <summary>
        /// <c>AttachHolding_DpadCommandDoesNotChangeSelectedAbility</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void AttachHolding_DpadCommandDoesNotChangeSelectedAbility()
        {
            attach.EnterSelecting();
            Assert.That(attach.TryBeginHolding(), Is.True);
            Assert.That(abilities.SelectedAbility,
                Is.EqualTo(PlayerAbilityController.AbilityKind.Attach));

            InputSystem.QueueStateEvent(gamepad, new GamepadState
            {
                buttons = 1u << (int)GamepadButton.DpadRight
            });
            InputSystem.Update();
            input.Sample();
            Assert.That(input.DpadPressed.x, Is.EqualTo(1f));

            abilities.TickUpdate(input);

            Assert.That(abilities.SelectedAbility,
                Is.EqualTo(PlayerAbilityController.AbilityKind.Attach));
            Assert.That(attach.State, Is.EqualTo(AttachAbilityController.AbilityState.Holding));
        }

        /// <summary>
        /// <c>PressGamepad</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void PressGamepad(GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState
            {
                buttons = 1u << (int)button
            });
            InputSystem.Update();
            input.Sample();
            abilities.TickUpdate(input);
        }

        /// <summary>
        /// <c>ReleaseGamepad</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void ReleaseGamepad()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            InputSystem.Update();
            input.Sample();
            abilities.TickUpdate(input);
        }

        /// <summary>
        /// <c>PressKeyboard</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void PressKeyboard(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            input.Sample();
            abilities.TickUpdate(input);
        }

        /// <summary>
        /// <c>ReleaseKeyboard</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void ReleaseKeyboard()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            input.Sample();
            abilities.TickUpdate(input);
        }
    }
}
