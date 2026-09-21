using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Smoothed top-down follow camera, clamped so the view never leaves the arena. Leads slightly
    /// toward the aim direction so the player sees more of what they are shooting at.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float smoothTime = 0.12f;
        [SerializeField] float aimLead = 1.4f;
        [SerializeField] Vector2 arenaHalfExtents = new Vector2(24f, 14f);

        Camera _camera;
        PlayerController _player;
        CameraShake _shake;
        Vector3 _velocity;
        Vector3 _basePosition;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _shake = GetComponent<CameraShake>();
        }

        void Start()
        {
            if (target == null)
            {
                _player = FindAnyObjectByType<PlayerController>();
                if (_player != null) target = _player.transform;
            }
            else
            {
                _player = target.GetComponent<PlayerController>();
            }

            if (target != null)
            {
                _basePosition = Desired(target.position, Vector2.zero);
                transform.position = _basePosition;
            }
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector2 aim = _player != null ? _player.AimDirection : Vector2.zero;
            Vector3 desired = Desired(target.position, aim);

            // Smooth toward the target, then add shake on top - shake must not be smoothed away.
            Vector3 smoothed = Vector3.SmoothDamp(_basePosition, desired, ref _velocity, smoothTime);
            _basePosition = smoothed;

            if (_shake != null)
            {
                transform.position = smoothed + _shake.Offset;
                transform.rotation = Quaternion.Euler(0f, 0f, _shake.Roll);
            }
            else
            {
                transform.position = smoothed;
            }
        }

        Vector3 Desired(Vector3 targetPosition, Vector2 aim)
        {
            Vector3 desired = targetPosition + (Vector3)(aim * aimLead);
            desired.z = transform.position.z;

            if (_camera != null && _camera.orthographic)
            {
                // Keep the arena edges from sliding into view.
                float halfHeight = _camera.orthographicSize;
                float halfWidth = halfHeight * _camera.aspect;

                float limitX = Mathf.Max(0f, arenaHalfExtents.x - halfWidth);
                float limitY = Mathf.Max(0f, arenaHalfExtents.y - halfHeight);

                desired.x = Mathf.Clamp(desired.x, -limitX, limitX);
                desired.y = Mathf.Clamp(desired.y, -limitY, limitY);
            }

            return desired;
        }
    }
}
