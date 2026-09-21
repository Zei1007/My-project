using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A lingering damage pool. Ticks damage while the player stands in it and applies an optional
    /// Conditional debuff for as long as they stay - the arena-denial half of the boss fight, which
    /// is what stops the player parking in one spot.
    /// </summary>
    public class HazardZone : MonoBehaviour, IPoolable
    {
        [SerializeField] SpriteRenderer fillRenderer;
        [SerializeField] float tickInterval = 0.5f;
        [Tooltip("Applied while the player is inside, revoked on exit.")]
        [SerializeField] BuffDefinition insideDebuff;

        float _damagePerTick;
        float _radius;
        float _duration;
        float _elapsed;
        float _tickTimer;
        bool _playerInside;

        public void Configure(float damagePerTick, float radius, float duration, Color color)
        {
            _damagePerTick = damagePerTick;
            _radius = Mathf.Max(0.2f, radius);
            _duration = Mathf.Max(0.5f, duration);
            _elapsed = 0f;
            _tickTimer = 0f;
            _playerInside = false;

            transform.localScale = Vector3.one * _radius;
            if (fillRenderer != null) fillRenderer.color = color;
        }

        public void OnSpawned()
        {
            _elapsed = 0f;
            _tickTimer = 0f;
            _playerInside = false;
        }

        public void OnDespawned()
        {
            ClearDebuff();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _elapsed += dt;

            float remaining = _duration - _elapsed;
            if (remaining <= 0f)
            {
                ClearDebuff();
                PoolManager.Despawn(gameObject);
                return;
            }

            // Fade out over the last second so its expiry is visible.
            if (fillRenderer != null)
            {
                var c = fillRenderer.color;
                c.a = Mathf.Lerp(0f, 0.55f, Mathf.Min(1f, remaining));
                fillRenderer.color = c;
            }

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null || player.Health.IsDead)
            {
                ClearDebuff();
                return;
            }

            bool inside = Vector2.Distance(player.transform.position, transform.position) <= _radius;

            if (inside && !_playerInside && insideDebuff != null)
                player.Buffs.Apply(insideDebuff);
            else if (!inside && _playerInside)
                ClearDebuff();

            _playerInside = inside;
            if (!inside) return;

            _tickTimer -= dt;
            if (_tickTimer > 0f) return;

            _tickTimer = tickInterval;
            player.Health.TakeDamage(_damagePerTick, gameObject);
        }

        void ClearDebuff()
        {
            if (!_playerInside || insideDebuff == null) return;
            _playerInside = false;

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player != null) player.Buffs.Remove(insideDebuff);
        }
    }
}
