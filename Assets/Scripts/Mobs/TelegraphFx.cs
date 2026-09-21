using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// The warning a boss attack shows before it lands. Every boss attack telegraphs - an AoE the
    /// player cannot see coming is not difficulty, it is just damage.
    /// </summary>
    public class TelegraphFx : MonoBehaviour, IPoolable
    {
        [SerializeField] SpriteRenderer fillRenderer;
        [SerializeField] SpriteRenderer ringRenderer;
        [Tooltip("Seconds the impact flash lingers after the windup completes.")]
        [SerializeField] float flashDuration = 0.18f;

        float _windup;
        float _elapsed;
        Color _color;
        bool _flashing;
        bool _isLine;
        Vector3 _targetScale;

        public void OnSpawned()
        {
            _elapsed = 0f;
            _flashing = false;
        }

        public void OnDespawned() { }

        /// <summary>Circular telegraph: the fill sweeps outward to the full radius over windup.</summary>
        public void PlayCircle(Vector3 center, float radius, float windup, Color color)
        {
            transform.position = center;
            transform.rotation = Quaternion.identity;

            _windup = Mathf.Max(0.01f, windup);
            _color = color;
            _elapsed = 0f;
            _flashing = false;
            _isLine = false;

            // Sprites are authored at 128px / 64 PPU = 2 units across, so radius maps to scale 1:1.
            _targetScale = Vector3.one * radius;
            transform.localScale = _targetScale;

            if (ringRenderer != null)
            {
                ringRenderer.transform.localScale = Vector3.one;
                ringRenderer.color = new Color(color.r, color.g, color.b, 0.9f);
            }
            if (fillRenderer != null)
            {
                fillRenderer.transform.localScale = Vector3.zero;
                fillRenderer.color = new Color(color.r, color.g, color.b, 0.14f);
            }
        }

        /// <summary>Line telegraph for dashes: a stretched bar from the boss along its charge path.</summary>
        public void PlayLine(Vector3 from, Vector3 to, float width, float windup, Color color)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;

            transform.position = from + delta * 0.5f;
            transform.right = length > 0.001f ? delta.normalized : Vector3.right;
            transform.localScale = Vector3.one;

            _windup = Mathf.Max(0.01f, windup);
            _color = color;
            _elapsed = 0f;
            _flashing = false;
            _isLine = true;
            _targetScale = new Vector3(length * 0.5f, width * 0.5f, 1f);

            if (ringRenderer != null) ringRenderer.color = new Color(0f, 0f, 0f, 0f);
            if (fillRenderer != null)
            {
                fillRenderer.transform.localScale = new Vector3(0f, _targetScale.y, 1f);
                fillRenderer.color = new Color(color.r, color.g, color.b, 0.30f);
            }
        }

        void Update()
        {
            _elapsed += Time.deltaTime;

            if (!_flashing)
            {
                float t = Mathf.Clamp01(_elapsed / _windup);

                if (fillRenderer != null)
                {
                    // The fill racing to the rim is the countdown - readable at a glance.
                    fillRenderer.transform.localScale = _isLine
                        ? new Vector3(_targetScale.x * t, _targetScale.y, 1f)   // line grows lengthwise
                        : Vector3.one * t;                                      // circle grows radially

                    var c = fillRenderer.color;
                    c.a = Mathf.Lerp(0.10f, 0.26f, t);
                    fillRenderer.color = c;
                }

                // Pulse the ring faster as the strike approaches.
                if (ringRenderer != null)
                {
                    float pulse = 1f + Mathf.Sin(_elapsed * Mathf.Lerp(8f, 26f, t)) * 0.04f;
                    ringRenderer.transform.localScale = Vector3.one * pulse;
                }

                if (t >= 1f)
                {
                    _flashing = true;
                    _elapsed = 0f;
                    if (fillRenderer != null)
                        fillRenderer.color = new Color(1f, 1f, 1f, 0.55f);
                }
                return;
            }

            float ft = Mathf.Clamp01(_elapsed / flashDuration);
            if (fillRenderer != null)
            {
                var c = Color.Lerp(Color.white, _color, ft);
                c.a = 0.55f * (1f - ft);
                fillRenderer.color = c;
            }
            if (ringRenderer != null)
            {
                var c = ringRenderer.color;
                c.a = 0.9f * (1f - ft);
                ringRenderer.color = c;
            }

            if (ft >= 1f) PoolManager.Despawn(gameObject);
        }
    }
}
