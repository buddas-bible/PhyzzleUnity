using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Phyzzle.Player;
using Phyzzle.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>PlayerInputReaderPlayModeTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class PlayerInputReaderPlayModeTests
    {
        private readonly InputTestFixture inputFixture = new();
        private GameObject inputObject;
        private PlayerInputReader reader;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            inputFixture.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            inputObject = new GameObject("Input");
            reader = inputObject.AddComponent<PlayerInputReader>();
        }

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(inputObject);
            yield return null;
            inputFixture.TearDown();
        }

        /// <summary>
        /// <c>MouseDeltaAndArrowKey_ProduceUnclampedPointerLookAndIndependentDpadCommand</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void MouseDeltaAndArrowKey_ProduceUnclampedPointerLookAndIndependentDpadCommand()
        {
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(1800f, -900f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.E));
            InputSystem.Update();

            reader.Sample();

            Assert.That(reader.Look.x, Is.EqualTo(2f).Within(0.00001f));
            Assert.That(reader.Look.y, Is.EqualTo(-1f).Within(0.00001f));
            Assert.That(ReadLookIsPointerDelta(), Is.True);
            Assert.That(reader.Dpad.x, Is.EqualTo(0f));
            Assert.That(reader.Dpad.y, Is.EqualTo(1f));
            Assert.That(reader.DpadPressed.x, Is.EqualTo(0f));
            Assert.That(reader.DpadPressed.y, Is.EqualTo(1f));
            Assert.That(reader.RotateHeld, Is.True);
            Assert.That(reader.Move, Is.EqualTo(Vector2.zero));
            Assert.That(reader.LeftTrigger, Is.EqualTo(0f));
        }

        /// <summary>
        /// <c>GamepadRightStickAndDpad_RemainRateLookAndCommandInputs</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void GamepadRightStickAndDpad_RemainRateLookAndCommandInputs()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState
            {
                rightStick = new Vector2(0.5f, -0.25f),
                buttons = 1u << (int)GamepadButton.DpadLeft
            });
            InputSystem.Update();

            reader.Sample();

            Assert.That(reader.Look.x, Is.EqualTo(0.3761f).Within(0.001f));
            Assert.That(reader.Look.y, Is.EqualTo(-0.1880f).Within(0.001f));
            Assert.That(ReadLookIsPointerDelta(), Is.False);
            Assert.That(reader.Dpad.x, Is.EqualTo(-1f));
            Assert.That(reader.Dpad.y, Is.EqualTo(0f));
            Assert.That(reader.DpadPressed.x, Is.EqualTo(-1f));
            Assert.That(reader.CurrentInputDevice, Is.EqualTo(PlayerInputDeviceKind.Gamepad));
        }

        /// <summary>
        /// <c>Wasd_RemainsMovementWithoutKeyboardTriggerPath</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Wasd_RemainsMovementWithoutKeyboardTriggerPath()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            InputSystem.Update();

            reader.Sample();

            float diagonal = 1f / Mathf.Sqrt(2f);
            Assert.That(reader.Move.x, Is.EqualTo(diagonal).Within(0.00001f));
            Assert.That(reader.Move.y, Is.EqualTo(diagonal).Within(0.00001f));
            Assert.That(reader.Look, Is.EqualTo(Vector2.zero));
            Assert.That(reader.Dpad, Is.EqualTo(Vector2.zero));
            Assert.That(reader.LeftTrigger, Is.EqualTo(0f));
        }

        /// <summary>
        /// <c>MouseActivityAfterGamepad_SwitchesUiModeBackToKeyboardMouse</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void MouseActivityAfterGamepad_SwitchesUiModeBackToKeyboardMouse()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.right });
            InputSystem.Update();
            Assert.That(reader.CurrentInputDevice, Is.EqualTo(PlayerInputDeviceKind.Gamepad));

            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            InputSystem.Update();
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(1f, 0f));
            InputSystem.Update();
            reader.Sample();

            Assert.That(reader.CurrentInputDevice, Is.EqualTo(PlayerInputDeviceKind.Keyboard));
            Assert.That(ReadLookIsPointerDelta(), Is.True);
        }

        /// <summary>
        /// <c>ArrowDpadPressed_FiresOnceUntilReleasedAndPressedAgain</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void ArrowDpadPressed_FiresOnceUntilReleasedAndPressedAgain()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            InputSystem.Update();
            reader.Sample();
            Assert.That(reader.DpadPressed.x, Is.EqualTo(1f));

            reader.Sample();
            Assert.That(reader.DpadPressed.x, Is.EqualTo(0f));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            reader.Sample();
            Assert.That(reader.DpadPressed.x, Is.EqualTo(0f));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            InputSystem.Update();
            reader.Sample();
            Assert.That(reader.DpadPressed.x, Is.EqualTo(1f));
        }

        /// <summary>
        /// <c>ArrowAndDigitSelectionInputs_AreSeparated</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void ArrowAndDigitSelectionInputs_AreSeparated()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            InputSystem.Update();
            reader.Sample();

            Assert.That(reader.ConsumeNextAbility(), Is.True);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            reader.Sample();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
            InputSystem.Update();
            reader.Sample();

            Assert.That(reader.ConsumeNextAbility(), Is.False);
        }

        /// <summary>
        /// <c>ArrowDpadPressed_OppositeDirectionWithoutNeutralFiresNewCommand</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void ArrowDpadPressed_OppositeDirectionWithoutNeutralFiresNewCommand()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            InputSystem.Update();
            reader.Sample();

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow));
            InputSystem.Update();
            reader.Sample();

            Assert.That(reader.DpadPressed.x, Is.EqualTo(-1f));
            Assert.That(reader.DpadPressed.y, Is.EqualTo(0f));
        }

        /// <summary>
        /// <c>ArrowDpadPressed_DiagonalSignChangeFiresBothNewCommands</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void ArrowDpadPressed_DiagonalSignChangeFiresBothNewCommands()
        {
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.UpArrow, Key.RightArrow));
            InputSystem.Update();
            reader.Sample();

            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.DownArrow, Key.LeftArrow));
            InputSystem.Update();
            reader.Sample();

            Assert.That(reader.DpadPressed.x, Is.EqualTo(-1f));
            Assert.That(reader.DpadPressed.y, Is.EqualTo(-1f));
        }

        /// <summary>
        /// <c>Disable_DoesNotDisablePreEnabledReferencedAction</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Disable_DoesNotDisablePreEnabledReferencedAction()
        {
            GameObject sharedInputObject = new("Shared Input");
            sharedInputObject.SetActive(false);
            PlayerInputReader sharedReader = sharedInputObject.AddComponent<PlayerInputReader>();
            InputActionAsset sharedAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputAction sharedMove = sharedAsset.AddActionMap("Shared").AddAction(
                "Shared Move",
                InputActionType.Value,
                "<Gamepad>/leftStick");
            InputActionReference sharedReference = InputActionReference.Create(sharedMove);
            FieldInfo moveReference = typeof(PlayerInputReader).GetField(
                "moveReference",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(moveReference, Is.Not.Null);
            moveReference.SetValue(sharedReader, sharedReference);
            sharedMove.Enable();

            sharedInputObject.SetActive(true);
            sharedReader.enabled = false;

            Assert.That(sharedMove.enabled, Is.True);
            Object.DestroyImmediate(sharedInputObject);
            Object.DestroyImmediate(sharedReference);
            Object.DestroyImmediate(sharedAsset);
        }

        /// <summary>
        /// <c>Disable_AfterMouseInputClearsPointerSourceState</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Disable_AfterMouseInputClearsPointerSourceState()
        {
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(18f, 0f));
            InputSystem.Update();
            reader.Sample();
            Assert.That(ReadLookIsPointerDelta(), Is.True);

            reader.enabled = false;

            Assert.That(reader.Look, Is.EqualTo(Vector2.zero));
            Assert.That(ReadLookIsPointerDelta(), Is.False);
        }

        /// <summary>
        /// <c>PhyzzlePlayer_LargeMouseDeltaReachesNormalCameraUnclamped</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void PhyzzlePlayer_LargeMouseDeltaReachesNormalCameraUnclamped()
        {
            GameObject armObject = new("CameraArm");
            GameObject coreObject = new("CameraCore");
            GameObject modelObject = new("Model");
            armObject.transform.SetParent(inputObject.transform, false);
            coreObject.transform.SetParent(armObject.transform, false);
            modelObject.transform.SetParent(inputObject.transform, false);
            coreObject.transform.localPosition = new Vector3(0f, 0f, -4f);
            PlayerCameraSettings settings = ScriptableObject.CreateInstance<PlayerCameraSettings>();
            settings.sensitivity = 30f;
            settings.collisionMask = 0;
            settings.hideModelDistance = 0f;
            PlayerCameraRig rig = inputObject.AddComponent<PlayerCameraRig>();
            rig.Configure(armObject.transform, coreObject.transform, modelObject.transform, settings);
            PhyzzlePlayer player = inputObject.AddComponent<PhyzzlePlayer>();
            player.Configure(reader, inputObject.GetComponent<PlayerMotor>(), rig, null);
            player.enabled = false;
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(1800f, 0f));
            InputSystem.Update();
            reader.Sample();

            MethodInfo lateUpdate = typeof(PhyzzlePlayer).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdate, Is.Not.Null);
            lateUpdate.Invoke(player, null);

            Assert.That(Quaternion.Angle(armObject.transform.localRotation, Quaternion.identity),
                Is.EqualTo(60f).Within(0.01f));
            Object.DestroyImmediate(settings);
        }

        /// <summary>
        /// <c>PhyzzlePlayer_FocusAndDisableOwnGameplayCursorOnlyInPlayMode</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void PhyzzlePlayer_FocusAndDisableOwnGameplayCursorOnlyInPlayMode()
        {
            PhyzzlePlayer player = inputObject.AddComponent<PhyzzlePlayer>();
            MethodInfo focus = typeof(PhyzzlePlayer).GetMethod(
                "OnApplicationFocus",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo enable = typeof(PhyzzlePlayer).GetMethod(
                "OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(focus, Is.Not.Null, "PhyzzlePlayer must handle gameplay focus changes.");
            Assert.That(enable, Is.Not.Null, "PhyzzlePlayer must restore cursor state on re-enable.");

            focus.Invoke(player, new object[] { false });
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);

            focus.Invoke(player, new object[] { true });
            Assert.That(Cursor.visible, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(
                Application.isFocused ? CursorLockMode.Locked : CursorLockMode.None));

            player.enabled = false;
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);

            player.enabled = true;
            Assert.That(Cursor.lockState, Is.EqualTo(
                Application.isFocused ? CursorLockMode.Locked : CursorLockMode.None));
            Assert.That(Cursor.visible, Is.EqualTo(!Application.isFocused));
        }

        /// <summary>
        /// <c>ReadLookIsPointerDelta</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private bool ReadLookIsPointerDelta()
        {
            PropertyInfo property = typeof(PlayerInputReader).GetProperty("LookIsPointerDelta");
            Assert.That(property, Is.Not.Null, "PlayerInputReader must expose LookIsPointerDelta.");
            return (bool)property.GetValue(reader);
        }
    }
}
