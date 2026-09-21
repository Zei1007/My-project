using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Runs the multi-phase boss fight on top of the shared ZombieController AI. Phases swap stats
    /// and attack sets at health thresholds; every attack telegraphs before it lands.
    /// </summary>
    [RequireComponent(typeof(ZombieController))]
    public class BossBrain : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] GameObject telegraphPrefab;
        [SerializeField] GameObject burstFxPrefab;
        [SerializeField] GameObject zombiePrefab;

        [Header("Feel")]
        [SerializeField] float phaseTransitionDuration = 1.2f;
        [SerializeField] float slamShake = 0.55f;

        ZombieController _zombie;
        BossDefinition _definition;
        StatModifier _phaseSpeedMod;
        StatModifier _phaseDamageMod;
        StatModifier _phaseResistMod;

        readonly List<ZombieController> _overlapBuffer = new List<ZombieController>();

        int _phaseIndex = -1;
        float _attackTimer;
        bool _busy;
        bool _active;
        float _waveScalar = 1f;

        public BossDefinition Definition { get { return _definition; } }
        public int PhaseIndex { get { return _phaseIndex; } }
        public int PhaseCount { get { return _definition != null ? _definition.phases.Count : 0; } }

        void Awake()
        {
            _zombie = GetComponent<ZombieController>();
        }

        /// <summary>Called by WaveManager right after ZombieController.Initialize.</summary>
        public void Initialize(BossDefinition definition, float waveScalar)
        {
            _definition = definition;
            _waveScalar = Mathf.Max(0.1f, waveScalar);
            _phaseIndex = -1;
            _busy = false;
            _active = false;

            _zombie.Health.Died -= OnBossDied;
            _zombie.Health.Died += OnBossDied;

            StopAllCoroutines();
            StartCoroutine(RunIntro());
        }

        void OnDisable()
        {
            if (_zombie != null && _zombie.Health != null) _zombie.Health.Died -= OnBossDied;
            StopAllCoroutines();
            _active = false;
        }

        IEnumerator RunIntro()
        {
            GameEvents.RaiseBossSpawned(_zombie, _definition);
            CameraShake.Shake(0.5f);

            // The boss holds still during its entrance so the player can read the threat.
            var frozen = new StatModifier(StatType.MoveSpeed, ModifierOp.Multiplicative, 0.0001f, this);
            _zombie.Stats.AddModifier(frozen);

            yield return new WaitForSeconds(Mathf.Max(0.1f, _definition.introDuration));

            _zombie.Stats.RemoveModifier(frozen);
            EnterPhase(0);
            _active = true;
        }

        void Update()
        {
            if (!_active || _definition == null || !_zombie.IsAlive) return;

            // Phase changes are driven by health, checked every frame rather than on damage events
            // so a single huge hit that skips a threshold still lands in the right phase.
            float fraction = _zombie.Health.Max > 0f ? _zombie.Health.Current / _zombie.Health.Max : 0f;
            int wanted = _definition.PhaseIndexFor(fraction);
            if (wanted > _phaseIndex && !_busy)
            {
                StartCoroutine(TransitionTo(wanted));
                return;
            }

            if (_busy) return;

            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f) return;

            var phase = CurrentPhase();
            if (phase == null || phase.attacks.Count == 0) return;

            _attackTimer = Mathf.Max(0.5f, phase.attackInterval);
            StartCoroutine(PerformAttack(PickAttack(phase)));
        }

        BossPhase CurrentPhase()
        {
            if (_definition == null || _definition.phases.Count == 0) return null;
            return _definition.phases[Mathf.Clamp(_phaseIndex, 0, _definition.phases.Count - 1)];
        }

        BossAttackEntry PickAttack(BossPhase phase)
        {
            float total = 0f;
            for (int i = 0; i < phase.attacks.Count; i++) total += Mathf.Max(0.01f, phase.attacks[i].weight);

            float roll = Random.value * total;
            for (int i = 0; i < phase.attacks.Count; i++)
            {
                roll -= Mathf.Max(0.01f, phase.attacks[i].weight);
                if (roll <= 0f) return phase.attacks[i];
            }
            return phase.attacks[phase.attacks.Count - 1];
        }

        IEnumerator TransitionTo(int index)
        {
            _busy = true;

            // Slam to a halt, shockwave, then come back harder.
            CameraShake.Shake(0.8f);
            SpawnBurst(transform.position, 3.5f, CurrentPhase() != null ? CurrentPhase().auraColor : Color.magenta);

            var root = new StatModifier(StatType.MoveSpeed, ModifierOp.Multiplicative, 0.0001f, "transition");
            _zombie.Stats.AddModifier(root);

            if (_zombie.Animator != null) _zombie.Animator.PlayAttack();

            yield return new WaitForSeconds(phaseTransitionDuration);

            _zombie.Stats.RemoveAllFromSource("transition");
            EnterPhase(index);

            // Every phase change calls in help, so the arena never goes quiet.
            SummonAdds(3);

            _busy = false;
        }

        void EnterPhase(int index)
        {
            _phaseIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _definition.phases.Count - 1));
            var phase = CurrentPhase();
            if (phase == null) return;

            // Replace the previous phase's stat shift rather than stacking it.
            _zombie.Stats.RemoveAllFromSource(this);

            _phaseSpeedMod = new StatModifier(StatType.MoveSpeed, ModifierOp.Multiplicative, phase.moveSpeedMultiplier, this);
            _phaseDamageMod = new StatModifier(StatType.Damage, ModifierOp.Multiplicative, phase.damageMultiplier, this);
            _phaseResistMod = new StatModifier(StatType.DamageTaken, ModifierOp.Multiplicative, phase.damageTakenMultiplier, this);

            _zombie.Stats.AddModifier(_phaseSpeedMod);
            _zombie.Stats.AddModifier(_phaseDamageMod);
            _zombie.Stats.AddModifier(_phaseResistMod);

            _attackTimer = Mathf.Max(0.6f, phase.attackInterval * 0.5f);

            GameEvents.RaiseBossPhaseChanged(_phaseIndex, _definition.phases.Count, phase.phaseName);
        }

        // --- attacks ------------------------------------------------------------

        IEnumerator PerformAttack(BossAttackEntry attack)
        {
            _busy = true;

            switch (attack.type)
            {
                case BossAttackType.GroundSlam: yield return GroundSlam(attack); break;
                case BossAttackType.RadialBurst: yield return RadialBurst(attack); break;
                case BossAttackType.SummonAdds: yield return SummonWave(attack); break;
                case BossAttackType.ChargeDash: yield return ChargeDash(attack); break;
                case BossAttackType.HazardField: yield return HazardField(attack); break;
            }

            _busy = false;
        }

        /// <summary>Telegraphed AoE where the player is standing - dodgeable by moving.</summary>
        IEnumerator GroundSlam(BossAttackEntry attack)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) yield break;

            Vector3 center = player.transform.position;
            ShowTelegraph(center, attack.radius, attack.windup);

            yield return new WaitForSeconds(attack.windup);

            SpawnBurst(center, attack.radius, new Color(1f, 0.55f, 0.2f, 0.8f));
            CameraShake.Shake(slamShake);
            if (_zombie.Animator != null) _zombie.Animator.PlayAttack();

            DamagePlayerInRadius(center, attack.radius, attack.damageMultiplier);
        }

        /// <summary>Ring of projectiles - punishes standing at mid range.</summary>
        IEnumerator RadialBurst(BossAttackEntry attack)
        {
            if (_definition.enemyProjectilePrefab == null) yield break;

            ShowTelegraph(transform.position, attack.radius * 0.6f, attack.windup);
            yield return new WaitForSeconds(attack.windup);

            if (!_zombie.IsAlive) yield break;

            int count = Mathf.Max(3, attack.count);
            float damage = _zombie.Stats.Get(StatType.Damage) * attack.damageMultiplier;
            float offset = Random.value * 360f;

            for (int i = 0; i < count; i++)
            {
                float angle = offset + 360f / count * i;
                var rotation = Quaternion.Euler(0f, 0f, angle);
                var projectile = PoolManager.Spawn<EnemyProjectile>(
                    _definition.enemyProjectilePrefab, transform.position, rotation);
                if (projectile != null)
                    projectile.Configure(damage, 5.5f * StageProjectileScalar(), 0.55f, new Color(0.85f, 0.4f, 1f));
            }

            CameraShake.Shake(0.3f);
            if (_zombie.Animator != null) _zombie.Animator.PlayAttack();
        }

        IEnumerator SummonWave(BossAttackEntry attack)
        {
            ShowTelegraph(transform.position, 2.2f, attack.windup);
            yield return new WaitForSeconds(attack.windup);

            SummonAdds(attack.count);
            if (_zombie.Animator != null) _zombie.Animator.PlayAttack();
        }

        /// <summary>Telegraphed line, then a fast dash along it.</summary>
        IEnumerator ChargeDash(BossAttackEntry attack)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) yield break;

            Vector3 origin = transform.position;
            Vector3 direction = (player.transform.position - origin).normalized;
            Vector3 destination = origin + direction * Mathf.Max(4f, attack.radius * 2.5f);

            if (telegraphPrefab != null)
            {
                var telegraph = PoolManager.Spawn<TelegraphFx>(telegraphPrefab, origin, Quaternion.identity);
                if (telegraph != null)
                    telegraph.PlayLine(origin, destination, 1.8f, attack.windup, new Color(1f, 0.3f, 0.3f));
            }

            yield return new WaitForSeconds(attack.windup);
            if (!_zombie.IsAlive) yield break;

            // Dash as a temporary speed multiplier so it flows through the same stat pipeline.
            var dash = new StatModifier(StatType.MoveSpeed, ModifierOp.Multiplicative, 5.5f, "dash");
            _zombie.Stats.AddModifier(dash);
            CameraShake.Shake(0.4f);

            float elapsed = 0f;
            while (elapsed < 0.55f && _zombie.IsAlive)
            {
                elapsed += Time.deltaTime;
                DamagePlayerInRadius(transform.position, 1.3f, attack.damageMultiplier * 0.5f, 0.35f);
                yield return null;
            }

            _zombie.Stats.RemoveAllFromSource("dash");
        }

        IEnumerator HazardField(BossAttackEntry attack)
        {
            if (_definition.hazardPrefab == null) yield break;

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            Vector3 focus = player != null ? player.transform.position : transform.position;

            int count = Mathf.Max(1, attack.count);
            var spots = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(1.5f, Mathf.Max(2f, attack.radius * 2f));
                spots[i] = focus + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
                ShowTelegraph(spots[i], attack.radius, attack.windup);
            }

            yield return new WaitForSeconds(attack.windup);

            float damage = _zombie.Stats.Get(StatType.Damage) * attack.damageMultiplier * 0.25f;
            for (int i = 0; i < count; i++)
            {
                var hazard = PoolManager.Spawn<HazardZone>(_definition.hazardPrefab, spots[i], Quaternion.identity);
                if (hazard != null)
                    hazard.Configure(damage, attack.radius, 6f, new Color(0.45f, 0.95f, 0.35f, 0.5f));
            }

            if (_zombie.Animator != null) _zombie.Animator.PlayAttack();
        }

        // --- helpers ------------------------------------------------------------

        void SummonAdds(int count)
        {
            if (_definition.addDefinition == null || zombiePrefab == null) return;

            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                Vector3 position = transform.position
                    + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(1.5f, 3.2f);

                var add = PoolManager.Spawn<ZombieController>(zombiePrefab, position, Quaternion.identity);
                if (add == null) continue;

                var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
                add.Initialize(_definition.addDefinition, _waveScalar, player != null ? player.transform : null);
            }
        }

        void ShowTelegraph(Vector3 center, float radius, float windup)
        {
            if (telegraphPrefab == null) return;
            var telegraph = PoolManager.Spawn<TelegraphFx>(telegraphPrefab, center, Quaternion.identity);
            if (telegraph != null)
                telegraph.PlayCircle(center, radius, windup, new Color(1f, 0.35f, 0.3f));
        }

        void SpawnBurst(Vector3 center, float radius, Color color)
        {
            if (burstFxPrefab == null) return;
            var fx = PoolManager.Spawn<SimpleFx>(burstFxPrefab, center, Quaternion.identity);
            if (fx != null) fx.PlayBurst(center, radius, color);
        }

        void DamagePlayerInRadius(Vector3 center, float radius, float damageMultiplier, float cooldownGuard = 0f)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null || player.Health.IsDead) return;

            if (Vector2.Distance(player.transform.position, center) > radius) return;

            // Continuous attacks (the dash) must not damage every single frame.
            if (cooldownGuard > 0f)
            {
                if (Time.time - _lastContinuousHit < cooldownGuard) return;
                _lastContinuousHit = Time.time;
            }

            player.Health.TakeDamage(_zombie.Stats.Get(StatType.Damage) * damageMultiplier, gameObject);
        }

        float _lastContinuousHit = -99f;

        static float StageProjectileScalar()
        {
            var waves = GameManager.Instance != null ? GameManager.Instance.Waves : null;
            return waves != null ? waves.EnemyProjectileSpeedScalar : 1f;
        }

        void OnBossDied(GameObject killer)
        {
            _active = false;
            StopAllCoroutines();
            CameraShake.Shake(1f);
            GameEvents.RaiseBossDefeated(_definition);
        }
    }
}
