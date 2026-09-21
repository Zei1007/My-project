using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Shared zombie AI: chase the player, deal contact damage on a cooldown, die into XP. Tier
    /// differences come from the definition's numbers plus one optional elite ability, so every
    /// tier from Walker to Boss runs this same script. Visuals are delegated to CharacterRig /
    /// CharacterAnimator, which is why bosses can be dressed differently without touching the AI.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Health))]
    public class ZombieController : MonoBehaviour, IPoolable
    {
        [Header("Rig")]
        [SerializeField] CharacterRig rig;
        [SerializeField] CharacterAnimator animator;
        [Tooltip("Speed treated as a full-effort run, for the walk cycle.")]
        [SerializeField] float animationReferenceSpeed = 3.2f;

        [Header("Drops")]
        [SerializeField] GameObject xpOrbPrefab;
        [SerializeField] GameObject deathFxPrefab;

        [Header("Death")]
        [Tooltip("Time the death animation plays before the instance returns to the pool.")]
        [SerializeField] float despawnDelay = 0.55f;

        Rigidbody2D _rigidbody;
        Health _health;
        Collider2D _collider;
        StatSheet _stats;

        ZombieDefinition _definition;
        Transform _target;
        float _attackCooldown;
        float _abilityCooldown;
        float _chargeRemaining;
        float _despawnTimer = -1f;
        Vector2 _chargeDirection;
        Vector2 _knockbackVelocity;
        float _waveScalar = 1f;
        float _shotCooldown;
        float _strafeSign = 1f;
        bool _alive;

        public bool IsAlive { get { return _alive && _health != null && !_health.IsDead; } }
        public ZombieDefinition Definition { get { return _definition; } }
        public StatSheet Stats { get { return _stats; } }
        public Health Health { get { return _health; } }
        public CharacterRig Rig { get { return rig; } }
        public CharacterAnimator Animator { get { return animator; } }
        public float XPValue { get { return _definition != null ? _definition.xpValue : 1f; } }

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _collider = GetComponent<Collider2D>();
            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;

            if (rig == null) rig = GetComponentInChildren<CharacterRig>(true);
            if (animator == null) animator = GetComponentInChildren<CharacterAnimator>(true);
        }

        /// <summary>Called by the spawner right after the pool hands this instance over.</summary>
        public void Initialize(ZombieDefinition definition, float waveScalar, Transform target)
        {
            _definition = definition;
            _waveScalar = Mathf.Max(0.1f, waveScalar);
            _target = target;

            _stats = new StatSheet();
            _stats.SetBase(StatType.MaxHealth, definition.health * definition.healthMultiplier * _waveScalar);
            _stats.SetBase(StatType.MoveSpeed, definition.moveSpeed);
            _stats.SetBase(StatType.Damage, definition.contactDamage * definition.damageMultiplier * _waveScalar);
            _stats.SetBase(StatType.AttackInterval, definition.attackInterval);
            _stats.SetBase(StatType.DamageTaken, 1f);

            _health.Init(_stats);
            _health.Died -= OnDied;
            _health.Died += OnDied;

            _attackCooldown = 0f;
            _abilityCooldown = definition.abilityCooldown;
            _chargeRemaining = 0f;
            _despawnTimer = -1f;
            _knockbackVelocity = Vector2.zero;
            _shotCooldown = definition.shotInterval * Random.Range(0.6f, 1.2f);
            _strafeSign = Random.value < 0.5f ? -1f : 1f;
            _alive = true;

            if (_collider != null) _collider.enabled = true;

            if (rig != null)
            {
                rig.ApplyParts(definition.parts);
                rig.ApplyDepthShading();
                rig.SetWeaponSprite(null);
            }
            if (animator != null) animator.ResetAnimation();

            transform.localScale = Vector3.one * definition.scale;
            EnemyRegistry.Register(this);
        }

        public void OnSpawned()
        {
            _alive = true;
        }

        public void OnDespawned()
        {
            EnemyRegistry.Unregister(this);
            _alive = false;
            _despawnTimer = -1f;
            if (_health != null) _health.Died -= OnDied;
            if (_rigidbody != null) _rigidbody.linearVelocity = Vector2.zero;
        }

        void OnDisable()
        {
            EnemyRegistry.Unregister(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Dying: the rig plays its death animation, then the instance goes back to the pool.
            if (_despawnTimer >= 0f)
            {
                _despawnTimer -= dt;
                if (_despawnTimer <= 0f) PoolManager.Despawn(gameObject);
                return;
            }

            if (!IsAlive) return;

            _attackCooldown -= dt;
            TickAbility(dt);
            TickRanged(dt);
        }

        void FixedUpdate()
        {
            if (!IsAlive)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            if (_target == null)
            {
                var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
                if (player != null) _target = player.transform;
                if (_target == null) { _rigidbody.linearVelocity = Vector2.zero; return; }
            }

            Vector2 toTarget = (Vector2)_target.position - _rigidbody.position;
            Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;

            float speed = _stats.Get(StatType.MoveSpeed);
            Vector2 velocity;
            if (_chargeRemaining > 0f)
                velocity = _chargeDirection * (speed * _definition.abilityMagnitude);
            else if (_definition.isRanged)
                velocity = RangedMovement(direction, toTarget.magnitude, speed);
            else
                velocity = direction * speed;

            // Knockback decays rather than being applied as an impulse, so it reads consistently
            // regardless of the mob's mass.
            velocity += _knockbackVelocity;
            _knockbackVelocity = Vector2.Lerp(_knockbackVelocity, Vector2.zero, 10f * Time.fixedDeltaTime);

            _rigidbody.linearVelocity = velocity;

            if (animator != null)
            {
                animator.SetLocomotion(velocity.magnitude / Mathf.Max(0.01f, animationReferenceSpeed));
                animator.SetFacingFromVelocity(velocity.x);
            }
        }

        /// <summary>
        /// Ranged mobs hold a band around their preferred range: close in when too far, back off
        /// when crowded, and circle while in the sweet spot so they are not a static target.
        /// </summary>
        Vector2 RangedMovement(Vector2 direction, float distance, float speed)
        {
            float preferred = _definition.preferredRange;

            if (distance > preferred * 1.1f) return direction * speed;
            if (distance < preferred * 0.65f) return -direction * (speed * 0.8f);

            Vector2 strafe = new Vector2(-direction.y, direction.x) * _strafeSign;
            return strafe * (speed * 0.45f);
        }

        void TickRanged(float dt)
        {
            if (_definition == null || !_definition.isRanged || _definition.projectilePrefab == null) return;
            if (_target == null) return;

            _shotCooldown -= dt;
            if (_shotCooldown > 0f) return;

            Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
            if (toTarget.magnitude > _definition.preferredRange * 1.4f) return;   // too far to bother

            _shotCooldown = _definition.shotInterval;
            if (Random.value < 0.3f) _strafeSign = -_strafeSign;

            // Speed rises with the stage: slow and dodgeable early, fast late.
            var waves = GameManager.Instance != null ? GameManager.Instance.Waves : null;
            float speedScalar = waves != null ? waves.EnemyProjectileSpeedScalar : 1f;

            float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            var shot = PoolManager.Spawn<EnemyProjectile>(_definition.projectilePrefab,
                transform.position + (Vector3)(toTarget.normalized * 0.35f), Quaternion.Euler(0f, 0f, angle));
            if (shot != null)
            {
                shot.Configure(_stats.Get(StatType.Damage) * _definition.projectileDamageMultiplier,
                               _definition.projectileSpeed * speedScalar, 1f, _definition.projectileTint);
            }

            if (animator != null) animator.PlayAttack();
        }

        void TickAbility(float dt)
        {
            if (_definition == null || _definition.ability == EliteAbility.None) return;

            if (_chargeRemaining > 0f)
            {
                _chargeRemaining -= dt;
                return;
            }

            _abilityCooldown -= dt;
            if (_abilityCooldown > 0f) return;

            _abilityCooldown = _definition.abilityCooldown;

            switch (_definition.ability)
            {
                case EliteAbility.Charge:
                    if (_target != null)
                    {
                        _chargeDirection = ((Vector2)_target.position - _rigidbody.position).normalized;
                        _chargeRemaining = 0.45f;
                        if (animator != null) animator.PlayAttack();
                    }
                    break;

                case EliteAbility.SpeedBurst:
                    // Self-buff through the same stat pipeline the player uses.
                    var burst = new StatModifier(StatType.MoveSpeed, ModifierOp.Multiplicative,
                                                 _definition.abilityMagnitude, this);
                    _stats.AddModifier(burst);
                    StartCoroutine(RemoveAfter(burst, 1.2f));
                    break;
            }
        }

        System.Collections.IEnumerator RemoveAfter(StatModifier modifier, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_stats != null) _stats.RemoveModifier(modifier);
        }

        /// <summary>Single entry point for all incoming damage, from any weapon category.</summary>
        public void ApplyHit(float damage, Vector2 direction, float knockback, GameObject source)
        {
            if (!IsAlive) return;

            if (knockback > 0f)
                _knockbackVelocity += direction.normalized * knockback;

            if (animator != null) animator.PlayHit();

            float before = _health.Current;
            _health.TakeDamage(damage, source);
            float dealt = before - _health.Current;

            if (dealt > 0f)
                DamageNumbers.Show(transform.position + Vector3.up * 0.6f, dealt, false);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (!IsAlive || _attackCooldown > 0f) return;

            var player = collision.collider.GetComponentInParent<PlayerController>();
            if (player == null || player.Health.IsDead) return;

            player.Health.TakeDamage(_stats.Get(StatType.Damage), gameObject);
            _attackCooldown = _stats.Get(StatType.AttackInterval);

            if (animator != null) animator.PlayAttack();
        }

        void OnDied(GameObject killer)
        {
            if (!_alive) return;
            _alive = false;

            if (_definition != null && _definition.ability == EliteAbility.DeathExplosion)
                Explode();

            if (deathFxPrefab != null)
            {
                var fx = PoolManager.Spawn<SimpleFx>(deathFxPrefab, transform.position, Quaternion.identity);
                if (fx != null) fx.PlayBurst(transform.position, 0.6f * transform.localScale.x, Color.white);
            }

            DropXP();
            LootDropper.Drop(_definition, transform.position);

            GameEvents.RaiseEnemyKilled(this, transform.position);
            EnemyRegistry.Unregister(this);

            // Stop colliding and let the death animation play out before pooling.
            if (_collider != null) _collider.enabled = false;
            _rigidbody.linearVelocity = Vector2.zero;
            if (animator != null) animator.PlayDeath();

            _despawnTimer = despawnDelay;
        }

        void Explode()
        {
            float radius = _definition.abilityRadius;
            float damage = _stats.Get(StatType.Damage) * _definition.abilityMagnitude;

            if (deathFxPrefab != null)
            {
                var fx = PoolManager.Spawn<SimpleFx>(deathFxPrefab, transform.position, Quaternion.identity);
                if (fx != null) fx.PlayBurst(transform.position, radius, new Color(1f, 0.5f, 0.1f, 0.85f));
            }
            CameraShake.Shake(0.35f, 0.25f);

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return;

            if (Vector2.Distance(player.transform.position, transform.position) <= radius)
                player.Health.TakeDamage(damage, gameObject);
        }

        void DropXP()
        {
            if (xpOrbPrefab == null) return;
            var orb = PoolManager.Spawn<XPOrb>(xpOrbPrefab, transform.position, Quaternion.identity);
            if (orb != null) orb.Configure(XPValue);
        }
    }
}
