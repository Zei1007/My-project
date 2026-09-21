using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieShooter
{
    /// <summary>
    /// Where movement and aim come from. Implementations are stateless adapters over a device, so
    /// PlayerController never branches on platform.
    /// </summary>
    public interface IInputSource
    {
        Vector2 ReadMove();

        /// <summary>
        /// Returns false when the player is not aiming manually, which is the normal case on mobile.
        /// The caller then falls back to auto-aim at the nearest target.
        /// </summary>
        bool TryReadAim(Vector3 selfWorldPosition, out Vector2 aim);
    }

    /// <summary>
    /// Picks whichever device the player is actually touching, every frame. Touch wins over gamepad
    /// wins over keyboard, so plugging in a controller mid-run just works.
    ///
    /// Mobile design note: there is deliberately no aim stick by default. Weapons already auto-target
    /// the nearest zombie, so a second thumb control would add occlusion and fatigue for no gain.
    /// Assign AimJoystick (or use a gamepad) if manual aim is wanted.
    /// </summary>
    public static class InputRouter
    {
        /// <summary>Set by the on-screen controls when they build.</summary>
        public static VirtualJoystick MoveJoystick;
        public static VirtualJoystick AimJoystick;

        /// <summary>
        /// Off: this is a mobile game, and the weapons auto-target - a pointer that turned the gun
        /// without changing where shots went made the gun lie about what it was shooting.
        /// </summary>
        public static bool DesktopMouseAim = false;

        const float StickDeadzone = 0.22f;

        static IInputSource _cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            MoveJoystick = null;
            AimJoystick = null;
            _cached = null;
            DesktopMouseAim = false;
        }

        public static IInputSource Resolve()
        {
            if (_cached == null) _cached = new CompositeInput();
            return _cached;
        }

        /// <summary>True when the build should show on-screen controls.</summary>
        public static bool WantsTouchControls
        {
            get
            {
#if UNITY_EDITOR
                // Let the Editor exercise the touch path via the Device Simulator.
                return Application.isMobilePlatform || Touchscreen.current != null;
#else
                return Application.isMobilePlatform || Touchscreen.current != null;
#endif
            }
        }

        class CompositeInput : IInputSource
        {
            public Vector2 ReadMove()
            {
                if (MoveJoystick != null && MoveJoystick.IsHeld)
                    return MoveJoystick.Value;

                var gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    Vector2 stick = gamepad.leftStick.ReadValue();
                    if (stick.sqrMagnitude > StickDeadzone * StickDeadzone) return stick;
                }

                Vector2 keys = Vector2.zero;
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) keys.y += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) keys.y -= 1f;
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) keys.x -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) keys.x += 1f;
                }
                return keys.sqrMagnitude > 1f ? keys.normalized : keys;
            }

            public bool TryReadAim(Vector3 selfWorldPosition, out Vector2 aim)
            {
                aim = Vector2.zero;

                if (AimJoystick != null && AimJoystick.IsHeld)
                {
                    aim = AimJoystick.Value;
                    return aim.sqrMagnitude > 0.0001f;
                }

                var gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    Vector2 stick = gamepad.rightStick.ReadValue();
                    if (stick.sqrMagnitude > StickDeadzone * StickDeadzone)
                    {
                        aim = stick.normalized;
                        return true;
                    }
                }

                // Mouse aim, desktop only. Suppressed while on-screen controls are up so a tap
                // does not yank the aim across the screen.
                if (!DesktopMouseAim || MoveJoystick != null) return false;

                var mouse = Mouse.current;
                var camera = Camera.main;
                if (mouse == null || camera == null) return false;

                Vector3 screen = mouse.position.ReadValue();
                screen.z = Mathf.Abs(camera.transform.position.z - selfWorldPosition.z);
                Vector3 world = camera.ScreenToWorldPoint(screen);

                Vector2 delta = (Vector2)(world - selfWorldPosition);
                if (delta.sqrMagnitude < 0.0004f) return false;

                aim = delta.normalized;
                return true;
            }
        }
    }
}
