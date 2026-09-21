using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    public enum LootType
    {
        /// <summary>Restores a chunk of health.</summary>
        HealthPotion,
        /// <summary>A timed armor shield that soaks damage before health.</summary>
        Shield,
        /// <summary>An extra buff: opens a buffs-only choice screen.</summary>
        Blessing,
        /// <summary>Timed fire-rate and speed surge.</summary>
        Frenzy,
        /// <summary>Pulls every soul gem on the floor to the player.</summary>
        Magnet,
    }

    /// <summary>One line of a mob's loot table. Each line rolls independently.</summary>
    [Serializable]
    public class LootEntry
    {
        public LootType type = LootType.HealthPotion;
        [Range(0f, 1f)] public float chance = 0.25f;
        [Min(1)] public int count = 1;
    }

    /// <summary>
    /// A dropped reward. Behaves like a soul gem - pulled in once the player is close - but its
    /// effect is immediate and specific, which is what makes killing an elite feel different from
    /// killing twenty walkers.
    /// </summary>
    public class LootPickup : MonoBehaviour, IPoolable
    {
        [SerializeField] SpriteRenderer iconRenderer;
        [SerializeField] SpriteRenderer glowRenderer;
        [SerializeField] OrbVisual visual;
        [SerializeField] float collectDistance = 0.45f;
        [SerializeField] float homingSpeed = 10f;
        [SerializeField] float acceleration = 20f;
        [SerializeField] float lifetime = 60f;
        [Tooltip("Loot must be approached - it only starts homing inside this fraction of the pickup radius.")]
        [SerializeField] float attractRadiusScale = 0.8f;

        LootType _type;
        float _speed;
        float _age;
        bool _homing;
        Vector3 _scatterTarget;
        float _scatterTime;

        public LootType Type { get { return _type; } }

        public void Configure(LootType type, Sprite icon, Color glow, Vector3 scatterTarget)
        {
            _type = type;
            if (iconRenderer != null) iconRenderer.sprite = icon;
            if (glowRenderer != null) glowRenderer.color = glow;
            if (visual != null) visual.SetGlowColor(glow);
            _scatterTarget = scatterTarget;
            _scatterTime = 0.25f;
        }

        public void OnSpawned()
        {
            _speed = 0f;
            _age = 0f;
            _homing = false;
            if (visual != null) visual.SetAttracting(false);
        }

        public void OnDespawned() { }

        void Update()
        {
            float dt = Time.deltaTime;

            // A short hop outward so several drops fan out instead of stacking on one pixel.
            if (_scatterTime > 0f)
            {
                _scatterTime -= dt;
                transform.position = Vector3.Lerp(transform.position, _scatterTarget, 14f * dt);
                return;
            }

            _age += dt;
            if (_age >= lifetime)
            {
                PoolManager.Despawn(gameObject);
                return;
            }

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null || player.Health.IsDead) return;

            Vector2 toPlayer = (Vector2)(player.transform.position - transform.position);
            float distance = toPlayer.magnitude;

            if (!_homing)
            {
                float radius = player.Stats.Get(StatType.PickupRadius) * attractRadiusScale;
                if (distance > radius) return;
                _homing = true;
                if (visual != null) visual.SetAttracting(true);
            }

            if (distance <= collectDistance)
            {
                LootDropper.Collect(this, player);
                PoolManager.Despawn(gameObject);
                return;
            }

            _speed = Mathf.Min(homingSpeed, _speed + acceleration * dt);
            transform.position += (Vector3)(toPlayer.normalized * (_speed * dt));
        }
    }
}
