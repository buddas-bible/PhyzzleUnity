using NUnit.Framework;
using Phyzzle.UI;
using UnityEngine.InputSystem;

namespace Phyzzle.Tests
{
    public sealed class PlayerInputDeviceKindTests
    {
        private Keyboard keyboard;
        private Gamepad gamepad;

        [SetUp]
        public void SetUp()
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(gamepad);
        }

        [Test]
        public void Tracker_DefaultsToKeyboard()
        {
            PlayerInputDeviceTracker tracker = new();

            Assert.That(tracker.Current, Is.EqualTo(PlayerInputDeviceKind.Keyboard));
        }

        [Test]
        public void GamepadActivity_SwitchesToGamepad()
        {
            PlayerInputDeviceTracker tracker = new();

            bool changed = tracker.Notify(gamepad);

            Assert.That(changed, Is.True);
            Assert.That(tracker.Current, Is.EqualTo(PlayerInputDeviceKind.Gamepad));
        }

        [Test]
        public void KeyboardActivity_AfterGamepad_SwitchesBack()
        {
            PlayerInputDeviceTracker tracker = new();
            tracker.Notify(gamepad);

            bool changed = tracker.Notify(keyboard);

            Assert.That(changed, Is.True);
            Assert.That(tracker.Current, Is.EqualTo(PlayerInputDeviceKind.Keyboard));
        }

        [Test]
        public void SameDeviceKind_DoesNotReportChange()
        {
            PlayerInputDeviceTracker tracker = new();

            bool changed = tracker.Notify(keyboard);

            Assert.That(changed, Is.False);
        }
    }
}
