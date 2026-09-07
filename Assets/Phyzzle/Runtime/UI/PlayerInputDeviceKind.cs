using UnityEngine.InputSystem;

namespace Phyzzle.UI
{
    public enum PlayerInputDeviceKind
    {
        Keyboard,
        Gamepad
    }

    /// <summary>
    /// 마지막으로 실제 입력을 수행한 장치를 키보드 또는 게임패드 종류로 추적한다.
    /// </summary>
    public sealed class PlayerInputDeviceTracker
    {
        public PlayerInputDeviceKind Current { get; private set; } = PlayerInputDeviceKind.Keyboard;

        /// <summary>
        /// 입력 장치를 반영하고 현재 장치 종류가 변경되었는지 반환한다.
        /// </summary>
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

