using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Trauma-based camera shake. Exposes an offset rather than writing to the transform, so it
    /// composes with CameraFollow instead of fighting it for the same position every frame.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        static CameraShake _instance;

        [SerializeField] float decayPerSecond = 2.2f;
        [SerializeField] float maxOffset = 0.9f;
        [SerializeField] float maxRoll = 3.5f;
        [SerializeField] float frequency = 26f;

        float _trauma;
        float _seed;

        public Vector3 Offset { get; private set; }
        public float Roll { get; private set; }

        void Awake()
        {
            _instance = this;
            _seed = Random.value * 100f;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Adds trauma. Magnitude is 0-1; it accumulates and decays, so bursts stack.</summary>
        public static void Shake(float magnitude, float _ = 0f)
        {
            if (_instance == null) return;
            _instance._trauma = Mathf.Clamp01(_instance._trauma + magnitude);
        }

        void LateUpdate()
        {
            if (_trauma <= 0f)
            {
                Offset = Vector3.zero;
                Roll = 0f;
                return;
            }

            // Squaring trauma makes small hits subtle and big ones violent.
            float shake = _trauma * _trauma;
            float t = Time.unscaledTime * frequency;

            Offset = new Vector3(
                (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * maxOffset * shake,
                (Mathf.PerlinNoise(_seed + 13f, t) * 2f - 1f) * maxOffset * shake,
                0f);

            Roll = (Mathf.PerlinNoise(_seed + 27f, t) * 2f - 1f) * maxRoll * shake;

            _trauma = Mathf.Max(0f, _trauma - decayPerSecond * Time.unscaledDeltaTime);
        }
    }
}
