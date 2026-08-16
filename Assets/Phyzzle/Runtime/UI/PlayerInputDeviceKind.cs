using UnityEngine.InputSystem;

namespace Phyzzle.UI
{
    public enum PlayerInputDeviceKind
    {
        Keyboard,
        Gamepad
    }

    public sealed class PlayerInputDeviceTracker
    {
        public PlayerInputDeviceKind Current { get; private set; } = PlayerInputDeviceKind.Keyboard;

        public bool Notify(InputDevice device)
        {
            PlayerInputDeviceKind next = device is Gamepad
                ? PlayerInputDeviceKind.Gamepad
                : PlayerInputDeviceKind.Keyboard;
            if (next == Current)
            {
                return false;
            }

            Current = next;
            return true;
        }
    }
}

