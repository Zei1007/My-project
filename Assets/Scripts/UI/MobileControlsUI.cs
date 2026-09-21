using UnityEngine;
using UnityEngine.UI;

namespace ZombieShooter
{
    /// <summary>
    /// On-screen controls for touch devices.
    ///
    /// There is one thumbstick: movement. Aiming is automatic, because the weapons already target
    /// the nearest zombie - adding an aim stick would cost a second thumb, cover a quarter of a
    /// phone screen, and change nothing about what the player can hit. Manual aim stays available
    /// as an opt-in for players who want it.
    /// </summary>
    public class MobileControlsUI : MonoBehaviour
    {
        [Header("Testing")]
        [Tooltip("Build the on-screen controls even on desktop, to check the touch layout in the Editor.")]
        [SerializeField] bool forceTouchControls;

        [Header("Layout")]
        [Tooltip("Fraction of screen width the movement stick may be placed in.")]
        [SerializeField] float moveZoneWidth = 0.55f;

        [Header("Manual Aim (opt-in)")]
        [Tooltip("Adds a second stick on the right for manual aim. Off by default.")]
        [SerializeField] bool enableAimStick;

        [Header("Look")]
        [SerializeField] Color ringColor = new Color(1f, 1f, 1f, 0.16f);
        [SerializeField] Color knobColor = new Color(1f, 1f, 1f, 0.38f);

        VirtualJoystick _moveStick;
        VirtualJoystick _aimStick;
        RectTransform _pauseButton;

        public VirtualJoystick MoveStick { get { return _moveStick; } }

        public void Build(Transform canvas, Sprite ringSprite, Sprite knobSprite, bool forceTouch = false)
        {
            forceTouchControls |= forceTouch;

            if (!forceTouchControls && !InputRouter.WantsTouchControls)
            {
                // Desktop: no on-screen controls, and the mouse keeps aiming.
                return;
            }

            _moveStick = VirtualJoystick.Create(canvas, "MoveStick",
                new Vector2(0f, 0f), new Vector2(moveZoneWidth, 0.82f),
                ringColor, knobColor, ringSprite, knobSprite);
            InputRouter.MoveJoystick = _moveStick;

            if (enableAimStick)
            {
                _aimStick = VirtualJoystick.Create(canvas, "AimStick",
                    new Vector2(1f - moveZoneWidth, 0f), new Vector2(1f, 0.82f),
                    ringColor, knobColor, ringSprite, knobSprite);
                InputRouter.AimJoystick = _aimStick;
            }

            BuildPauseButton(canvas);
        }

        void BuildPauseButton(Transform canvas)
        {
            var button = UIFactory.CardButton(canvas, new Color(0.14f, 0.16f, 0.20f, 0.7f));
            _pauseButton = button.GetComponent<RectTransform>();
            _pauseButton.anchorMin = new Vector2(1f, 1f);
            _pauseButton.anchorMax = new Vector2(1f, 1f);
            _pauseButton.pivot = new Vector2(1f, 1f);
            _pauseButton.anchoredPosition = new Vector2(-30f, -74f);
            _pauseButton.sizeDelta = new Vector2(88f, 88f);

            var label = UIFactory.Label(_pauseButton, "Label", "II", 34, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(label.rectTransform);

            button.onClick.AddListener(delegate
            {
                var manager = GameManager.Instance;
                if (manager != null) manager.TogglePause();
            });
        }

        void OnDestroy()
        {
            if (InputRouter.MoveJoystick == _moveStick) InputRouter.MoveJoystick = null;
            if (InputRouter.AimJoystick == _aimStick) InputRouter.AimJoystick = null;
        }
    }
}
