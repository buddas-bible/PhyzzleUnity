using NUnit.Framework;
using Phyzzle.UI;
using UnityEngine.InputSystem;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>PlayerInputDeviceKindTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class PlayerInputDeviceKindTests
    {
        private Keyboard keyboard;
        private Gamepad gamepad;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
        }

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(gamepad);
        }

        /// <summary>
        /// <c>Tracker_DefaultsToKeyboard</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Tracker_DefaultsToKeyboard()
        {
            PlayerInputDeviceTracker tracker = new();

            Assert.That(tracker.Current, Is.EqualTo(PlayerInputDeviceKind.Keyboard));
        }

        /// <summary>
        /// <c>GamepadActivity_SwitchesToGamepad</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void GamepadActivity_SwitchesToGamepad()
        {
            PlayerInputDeviceTracker tracker = new();

            bool changed = tracker.Notify(gamepad);

            Assert.That(changed, Is.True);
            Assert.That(tracker.Current, Is.EqualTo(PlayerInputDeviceKind.Gamepad));
        }

        /// <summary>
        /// <c>KeyboardActivity_AfterGamepad_SwitchesBack</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void KeyboardActivity_AfterGamepad_SwitchesBack()
        {
            PlayerInputDeviceTracker tracker = new();
            tracker.Notify(gamepad);

            bool changed = tracker.Notify(keyboard);

            Assert.That(changed, Is.True);
            Assert.That(tracker.Current, Is.EqualTo(PlayerInputDeviceKind.Keyboard));
        }

        /// <summary>
        /// <c>SameDeviceKind_DoesNotReportChange</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void SameDeviceKind_DoesNotReportChange()
        {
            PlayerInputDeviceTracker tracker = new();

            bool changed = tracker.Notify(keyboard);

            Assert.That(changed, Is.False);
        }
    }
}
