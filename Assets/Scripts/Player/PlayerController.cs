using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Twin-stick movement with a separate aim direction, so the player can strafe while firing.
    /// Input arrives through IInputSource rather than being read here, which is what lets the same
    /// controller serve keyboard, gamepad and an on-screen thumbstick without branching.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] PlayerDefinition definition;

        [Header("Rig")]
        [SerializeField] CharacterRig rig;
        [SerializeField] CharacterAnimator animator;
        [SerializeField] GameObject muzzleFlashPrefab;

        [Header("Aim")]
        [Tooltip("How long the gun holds the direction of the last shot before turning to the next target.")]
        [SerializeField] float shotHoldTime = 0.35f;

        [Header("Arena")]
        [Tooltip("Half-extents the player is kept inside. Zero disables clamping.")]
        [SerializeField] Vector2 arenaHalfExtents = new Vector2(24f, 14f);

        Rigidbody2D _rigidbody;
        Health _health;
        WeaponInventory _inventory;
        BuffController _buffs;
        PlayerExperience _experience;
        IInputSource _input;

        StatSheet _stats;
        Vector2 _moveInput;
        Vector2 _aimDirection = Vector2.right;
        Vector2 _lastShotDirection = Vector2.right;
        float _lastShotTime = -99f;

        public StatSheet Stats { get { return _stats; } }
        public Vector2 AimDirection { get { return _aimDirection; } }
        public Vector2 MoveInput { get { return _moveInput; } }
        public Health Health { get { return _health; } }
        public WeaponInventory Inventory { get { return _inventory; } }
        public BuffController Buffs { get { return _buffs; } }
        public PlayerExperience Experience { get { return _experience; } }
        public PlayerDefinition Definition { get { return definition; } }
        public CharacterRig Rig { get { return rig; } }

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _inventory = GetComponent<WeaponInventory>();
            _buffs = GetComponent<BuffController>();
            _experience = GetComponent<PlayerExperience>();

            if (rig == null) rig = GetComponentInChildren<CharacterRig>(true);
            if (animator == null) animator = GetComponentInChildren<CharacterAnimator>(true);

            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;

            _stats = new StatSheet();
            if (definition != null) definition.baseStats.ApplyTo(_stats);
        }

        void Start()
        {
            _input = InputRouter.Resolve();

            if (_buffs != null) _buffs.Initialize(_stats);
            if (_inventory != null)
            {
                _inventory.Initialize(_stats);
                _inventory.Fired += OnWeaponFired;
                _inventory.Changed += RefreshHeldWeapon;
            }
            if (_experience != null) _experience.Initialize(this);

            _health.Init(_stats);
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;

            RefreshHeldWeapon();
        }

        void OnDestroy()
        {
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
            }
            if (_inventory != null)
            {
                _inventory.Fired -= OnWeaponFired;
                _inventory.Changed -= RefreshHeldWeapon;
            }
        }

        void Update()
        {
            if (_health.IsDead) { _moveInput = Vector2.zero; return; }
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Running)
            {
                _moveInput = Vector2.zero;
                if (animator != null) animator.SetLocomotion(0f);
                return;
            }

            if (_input == null) _input = InputRouter.Resolve();

            _moveInput = Vector2.ClampMagnitude(_input.ReadMove(), 1f);
            ReadAim();
            UpdateRig();
        }

        void FixedUpdate()
        {
            if (_health.IsDead)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            float speed = _stats.Get(StatType.MoveSpeed);
            _rigidbody.linearVelocity = _moveInput * speed;

            if (arenaHalfExtents.x > 0f && arenaHalfExtents.y > 0f)
            {
                Vector2 p = _rigidbody.position;
                p.x = Mathf.Clamp(p.x, -arenaHalfExtents.x, arenaHalfExtents.x);
                p.y = Mathf.Clamp(p.y, -arenaHalfExtents.y, arenaHalfExtents.y);
                if (p != _rigidbody.position) _rigidbody.position = p;
            }
        }

        /// <summary>
        /// The gun points where it fires, not at a cursor - weapons auto-target, so anything else
        /// would show the gun aimed one way while bullets leave another. In priority order:
        /// the shot just fired, then the target the gun will shoot next, then an opt-in aim stick,
        /// then the walking direction.
        /// </summary>
        void ReadAim()
        {
            // 1. Just fired: hold exactly along the shot.
            if (Time.time - _lastShotTime < shotHoldTime)
            {
                _aimDirection = _lastShotDirection;
                return;
            }

            // 2. Pre-aim at what the gun will fire at next - the nearest zombie inside its reach.
            var target = EnemyRegistry.FindNearest(GunOrigin(), GunReach());
            if (target != null)
            {
                Vector2 toTarget = (Vector2)target.transform.position - GunOrigin();
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    _aimDirection = toTarget.normalized;
                    return;
                }
            }

            // 3. Nothing to shoot: an opt-in aim stick or gamepad right stick may look around.
            Vector2 manual;
            if (_input.TryReadAim(transform.position, out manual) && manual.sqrMagnitude > 0.0001f)
            {
                _aimDirection = manual.normalized;
                return;
            }

            // 4. Otherwise face the way we are walking.
            if (_moveInput.sqrMagnitude > 0.01f) _aimDirection = _moveInput.normalized;
        }

        Vector2 GunOrigin()
        {
            return _inventory != null && _inventory.Muzzle != null
                ? (Vector2)_inventory.Muzzle.position
                : (Vector2)transform.position;
        }

        /// <summary>Reach of the equipped gun - the same range its auto-target uses.</summary>
        float GunReach()
        {
            var gun = _inventory != null ? (_inventory.Ranged ?? _inventory.Melee) : null;
            return gun != null ? gun.Range(_stats) : 7f;
        }

        void UpdateRig()
        {
            if (animator == null) return;

            float maxSpeed = Mathf.Max(0.01f, _stats.Get(StatType.MoveSpeed));
            animator.SetLocomotion(_rigidbody.linearVelocity.magnitude / maxSpeed);

            // Face the way we are shooting, not the way we are walking - strafing should still
            // point the gun at the target.
            animator.SetFacingFromVelocity(_aimDirection.x);

            ApplyGunRotation();
        }

        void ApplyGunRotation()
        {
            if (rig == null || rig.weaponPivot == null) return;

            // The rig mirrors via negative X scale, so the aim angle has to be un-mirrored.
            float angle = Mathf.Atan2(_aimDirection.y, _aimDirection.x * rig.Facing) * Mathf.Rad2Deg;
            rig.weaponPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>Shows the equipped ranged weapon in hand (the melee weapon if there is no gun).</summary>
        public void RefreshHeldWeapon()
        {
            if (rig == null || _inventory == null) return;

            var held = _inventory.Ranged ?? _inventory.Melee;
            rig.SetWeaponSprite(held != null ? held.Definition.worldSprite : null);
        }

        void OnWeaponFired(WeaponInstance weapon, Vector2 direction)
        {
            if (animator != null) animator.PlayAttack();

            // The gun follows gun shots. The melee sweep fires on its own rhythm and must not
            // yank the gun off its target - unless there is no gun equipped at all.
            bool drivesGun = !weapon.Definition.IsMelee || _inventory.Ranged == null;
            if (drivesGun && direction.sqrMagnitude > 0.0001f)
            {
                _lastShotDirection = direction.normalized;
                _lastShotTime = Time.time;
                _aimDirection = _lastShotDirection;

                // Snap now, so the muzzle flash below appears at the barrel it came out of.
                if (animator != null) animator.SetFacingFromVelocity(_aimDirection.x);
                ApplyGunRotation();
            }

            if (weapon.Definition.IsMelee) return;   // no muzzle flash or recoil kick for a blade

            if (muzzleFlashPrefab != null && rig != null && rig.weaponPivot != null)
            {
                Vector3 origin = rig.weaponPivot.position + (Vector3)(direction.normalized * 0.45f);
                var fx = PoolManager.Spawn<SimpleFx>(muzzleFlashPrefab, origin, Quaternion.identity);
                if (fx != null) fx.PlayBurst(origin, 0.34f, weapon.Definition.tint);
            }

            // Heavier weapons kick the camera; a pistol should not.
            float kick = weapon.CurrentTier.knockback * 0.012f;
            if (kick > 0.01f) CameraShake.Shake(Mathf.Min(0.12f, kick));
        }

        void OnDamaged(float amount, GameObject source)
        {
            if (animator != null) animator.PlayHit();
            DamageNumbers.ShowPlayerDamage(transform.position + Vector3.up * 0.8f, amount);
            CameraShake.Shake(0.18f);
        }

        void OnDied(GameObject killer)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            if (animator != null) animator.PlayDeath();
            CameraShake.Shake(0.6f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            if (arenaHalfExtents.x > 0f && arenaHalfExtents.y > 0f)
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(arenaHalfExtents.x * 2f, arenaHalfExtents.y * 2f, 0f));
        }
    }
}
