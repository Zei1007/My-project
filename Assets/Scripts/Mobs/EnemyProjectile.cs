using UnityEngine;

namespace ZombieShooter
{
    /// <summary>Pooled boss projectile. Travels in a straight line and damages the player on contact.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnemyProjectile : MonoBehaviour, IPoolable
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] float maxLifetime = 6f;
        [SerializeField] float spinSpeed = 180f;

        float _damage;
        float _speed;
        float _life;
        bool _spent;

        public void Configure(float damage, float speed, float scale, Color tint)
        {
            _damage = damage;
            _speed = speed;
            _life = 0f;
            _spent = false;

            transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);
            if (sprite != null) sprite.color = tint;
        }

        public void OnSpawned()
        {
            _life = 0f;
            _spent = false;
        }

        public void OnDespawned() { }

        void Update()
        {
            _life += Time.deltaTime;
            if (_life >= maxLifetime)
            {
                PoolManager.Despawn(gameObject);
                return;
            }

            transform.position += transform.right * (_speed * Time.deltaTime);
            if (sprite != null)
                sprite.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_spent) return;

            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.Health.IsDead) return;

            _spent = true;
            player.Health.TakeDamage(_damage, gameObject);
            PoolManager.Despawn(gameObject);
        }
    }
}
