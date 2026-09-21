using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Pooled one-shot visual: a sprite that fades out and returns itself. Doubles as the hitscan
    /// tracer (stretched between two points) and the area-weapon burst (a scaled circle).
    /// </summary>
    public class SimpleFx : MonoBehaviour, IPoolable
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] float lifetime = 0.12f;
        [SerializeField] bool scaleUp;

        float _elapsed;
        Color _color = Color.white;
        Vector3 _endScale;

        void Reset()
        {
            sprite = GetComponentInChildren<SpriteRenderer>();
        }

        public void OnSpawned()
        {
            _elapsed = 0f;
        }

        public void OnDespawned() { }

        /// <summary>Stretches the sprite from one point to another - used for hitscan tracers.</summary>
        public void PlayBeam(Vector3 from, Vector3 to, float width, Color color)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;

            transform.position = from + delta * 0.5f;
            transform.right = length > 0.0001f ? delta.normalized : Vector3.right;
            _endScale = new Vector3(Mathf.Max(0.01f, length), Mathf.Max(0.01f, width), 1f);
            transform.localScale = _endScale;

            _color = color;
            scaleUp = false;
            Apply(0f);
        }

        /// <summary>Expanding circle - used for area/melee weapons and elite death explosions.</summary>
        public void PlayBurst(Vector3 center, float radius, Color color)
        {
            transform.position = center;
            transform.right = Vector3.right;
            _endScale = Vector3.one * Mathf.Max(0.05f, radius * 2f);
            transform.localScale = _endScale * 0.4f;

            _color = color;
            scaleUp = true;
            Apply(0f);
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            float t = lifetime <= 0f ? 1f : Mathf.Clamp01(_elapsed / lifetime);
            Apply(t);

            if (t >= 1f) PoolManager.Despawn(gameObject);
        }

        void Apply(float t)
        {
            if (sprite == null) return;

            var c = _color;
            c.a = _color.a * (1f - t);
            sprite.color = c;

            if (scaleUp)
                transform.localScale = Vector3.Lerp(_endScale * 0.4f, _endScale, t);
        }
    }
}
