using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>Pooled bullet. Moves along its own forward axis and despawns on lifetime or pierce exhaustion.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour, IPoolable
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] float maxLifetime = 4f;

        readonly HashSet<ZombieController> _alreadyHit = new HashSet<ZombieController>();

        float _damage;
        float _speed;
        int _pierceRemaining;
        float _knockback;
        float _critChance;
        float _critMultiplier;
        float _life;
        float _lifetime;
        GameObject _owner;

        /// <summary>
        /// Range is enforced by lifetime: the bullet expires once it has travelled the weapon's
        /// reach, so a short-range shotgun visibly stops short instead of crossing the arena.
        /// </summary>
        public void Configure(float damage, float speed, int pierce, float knockback,
                              float critChance, float critMultiplier, float scale, Color tint,
                              float range, GameObject owner)
        {
            _lifetime = speed > 0.01f ? Mathf.Min(maxLifetime, range / speed) : maxLifetime;
            _damage = damage;
            _speed = speed;
            _pierceRemaining = pierce;
            _knockback = knockback;
            _critChance = critChance;
            _critMultiplier = critMultiplier;
            _owner = owner;
            _life = 0f;
            _alreadyHit.Clear();

            transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);
            if (sprite != null) sprite.color = tint;
        }

        public void OnSpawned()
        {
            _life = 0f;
            _alreadyHit.Clear();
        }

        public void OnDespawned()
        {
            _alreadyHit.Clear();
            _owner = null;
        }

        void Update()
        {
            _life += Time.deltaTime;
            if (_life >= (_lifetime > 0f ? _lifetime : maxLifetime))
            {
                PoolManager.Despawn(gameObject);
                return;
            }

            transform.position += transform.right * (_speed * Time.deltaTime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var zombie = other.GetComponentInParent<ZombieController>();
            if (zombie == null || !zombie.IsAlive) return;
            if (_alreadyHit.Contains(zombie)) return;

            _alreadyHit.Add(zombie);

            float damage = _damage;
            if (_critChance > 0f && Random.value < _critChance)
                damage *= _critMultiplier;

            zombie.ApplyHit(damage, transform.right, _knockback, _owner);

            if (_pierceRemaining <= 0)
            {
                PoolManager.Despawn(gameObject);
                return;
            }
            _pierceRemaining--;
        }
    }
}
