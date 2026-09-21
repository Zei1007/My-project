using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Drives spawning. Authored waves run first; past the end of the table waves are generated
    /// from the same pool with a rising difficulty scalar, so a run never hits a wall of content.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] GameObject zombiePrefab;
        [SerializeField] int prewarmCount = 80;

        [Header("Authored Waves")]
        [SerializeField] List<WaveDefinition> waves = new List<WaveDefinition>();

        [Header("Procedural Continuation")]
        [Tooltip("Mobs drawn from once the authored waves run out.")]
        [SerializeField] List<ZombieDefinition> proceduralPool = new List<ZombieDefinition>();
        [SerializeField] int proceduralBaseCount = 14;
        [SerializeField] float proceduralCountGrowth = 1.6f;
        [SerializeField] float proceduralSpawnInterval = 0.3f;
        [SerializeField] float proceduralIntermission = 3f;

        [Header("Special Tiers")]
        [SerializeField] ZombieDefinition eliteDefinition;
        [SerializeField] int eliteStartWave = 3;
        [SerializeField] int eliteCountCap = 6;
        [SerializeField] ZombieDefinition miniBossDefinition;
        [Tooltip("A Mini-Boss joins the wave on every multiple of this. 0 disables.")]
        [SerializeField] int miniBossInterval = 5;

        [Header("Boss")]
        [SerializeField] GameObject bossPrefab;
        [SerializeField] BossDefinition bossDefinition;
        [Tooltip("A Boss takes over the wave on every multiple of this. 0 disables.")]
        [SerializeField] int bossInterval = 10;
        [Tooltip("How far from the player the Boss enters. Kept inside the view so the entrance is seen.")]
        [SerializeField] float bossSpawnDistance = 4.2f;

        [Header("Difficulty")]
        [Tooltip("Wave number on X, stat multiplier on Y. Kept as a curve so pacing stays tunable.")]
        [SerializeField] AnimationCurve difficultyCurve = new AnimationCurve(
            new Keyframe(1f, 1f), new Keyframe(10f, 2f), new Keyframe(20f, 3.4f));
        [Tooltip("Added per wave once past the curve's last key, so it never flattens out entirely.")]
        [SerializeField] float growthBeyondCurve = 0.18f;

        [Header("Enemy Projectiles")]
        [Tooltip("Wave number on X, projectile speed multiplier on Y. Slow and dodgeable early, fast late.")]
        [SerializeField] AnimationCurve enemyProjectileSpeedCurve = new AnimationCurve(
            new Keyframe(1f, 0.55f), new Keyframe(5f, 0.8f), new Keyframe(10f, 1.05f),
            new Keyframe(20f, 1.5f), new Keyframe(30f, 1.9f));
        [Tooltip("Ceiling on the multiplier - past this, shots stop being readable at all.")]
        [SerializeField] float maxEnemyProjectileSpeed = 2.2f;

        [Header("Spawning")]
        [Tooltip("Distance from the player that mobs appear at - just outside the view.")]
        [SerializeField] float spawnRingRadius = 13f;
        [SerializeField] Vector2 arenaHalfExtents = new Vector2(24f, 14f);
        [SerializeField] float startDelay = 2f;

        readonly List<ZombieController> _spawnedThisWave = new List<ZombieController>();

        int _currentWave;
        float _waveTimer;
        bool _running;

        public int CurrentWave { get { return _currentWave; } }
        public float WaveTimer { get { return _waveTimer; } }
        public int AliveCount { get { return EnemyRegistry.Count; } }

        /// <summary>Enemy projectile speed multiplier for a given stage.</summary>
        public float ProjectileSpeedScalarFor(int wave)
        {
            if (enemyProjectileSpeedCurve == null || enemyProjectileSpeedCurve.length == 0) return 1f;
            return Mathf.Clamp(enemyProjectileSpeedCurve.Evaluate(Mathf.Max(1, wave)), 0.2f, maxEnemyProjectileSpeed);
        }

        /// <summary>Multiplier for the current stage - read by ranged mobs and the boss.</summary>
        public float EnemyProjectileSpeedScalar { get { return ProjectileSpeedScalarFor(_currentWave); } }

        public float ScalarFor(int wave)
        {
            if (difficultyCurve == null || difficultyCurve.length == 0) return 1f;

            float lastKeyTime = difficultyCurve[difficultyCurve.length - 1].time;
            if (wave <= lastKeyTime) return Mathf.Max(0.1f, difficultyCurve.Evaluate(wave));

            float endValue = difficultyCurve.Evaluate(lastKeyTime);
            return Mathf.Max(0.1f, endValue + (wave - lastKeyTime) * growthBeyondCurve);
        }

        void Start()
        {
            if (zombiePrefab != null && prewarmCount > 0)
                PoolManager.Prewarm(zombiePrefab, prewarmCount);

            StartCoroutine(RunWaves());
        }

        void OnEnable()
        {
            GameEvents.PlayerDied += StopRunning;
        }

        void OnDisable()
        {
            GameEvents.PlayerDied -= StopRunning;
        }

        void StopRunning()
        {
            _running = false;
            StopAllCoroutines();
        }

        IEnumerator RunWaves()
        {
            _running = true;
            yield return new WaitForSeconds(startDelay);

            while (_running)
            {
                _currentWave++;
                WaveDefinition wave = WaveFor(_currentWave);

                GameEvents.RaiseWaveStarted(_currentWave);
                yield return StartCoroutine(RunWave(wave, _currentWave));
                GameEvents.RaiseWaveCompleted(_currentWave);

                float intermission = wave != null ? wave.intermission : proceduralIntermission;
                float remaining = intermission;
                while (remaining > 0f)
                {
                    remaining -= Time.deltaTime;
                    _waveTimer = remaining;
                    GameEvents.RaiseWaveTimerChanged(remaining);
                    yield return null;
                }
            }
        }

        IEnumerator RunWave(WaveDefinition wave, int waveNumber)
        {
            _spawnedThisWave.Clear();
            float scalar = ScalarFor(waveNumber);
            var target = GameManager.Instance != null && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.transform
                : null;

            // Build the spawn queue: authored entries, then the seeded special tiers.
            var queue = new List<ZombieDefinition>();
            if (wave != null)
            {
                for (int i = 0; i < wave.entries.Count; i++)
                {
                    var entry = wave.entries[i];
                    if (entry.zombie == null) continue;
                    for (int c = 0; c < entry.count; c++) queue.Add(entry.zombie);
                }
            }

            AddSpecialTiers(queue, waveNumber);
            Shuffle(queue);

            // A boss wave is the boss plus a thinner trickle of regular mobs - the fight should be
            // about the boss, not about being buried by the usual crowd.
            bool isBossWave = IsBossWave(waveNumber);
            if (isBossWave)
            {
                SpawnBoss(waveNumber, target);
                if (queue.Count > 4) queue.RemoveRange(4, queue.Count - 4);
            }

            float interval = wave != null ? wave.spawnInterval : proceduralSpawnInterval;
            float duration = wave != null ? wave.duration : 0f;
            bool clearToAdvance = wave == null || wave.clearToAdvance;

            float elapsed = 0f;
            float spawnTimer = 0f;
            int spawned = 0;

            while (_running)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                spawnTimer -= dt;

                if (spawned < queue.Count && spawnTimer <= 0f)
                {
                    Spawn(queue[spawned], scalar, target);
                    spawned++;
                    spawnTimer = interval;
                }

                _waveTimer = elapsed;
                GameEvents.RaiseWaveTimerChanged(elapsed);

                bool timeUp = duration > 0f && elapsed >= duration;
                bool cleared = spawned >= queue.Count && (!clearToAdvance || AliveCount == 0);

                if (timeUp || cleared) break;

                yield return null;
            }
        }

        public bool IsBossWave(int waveNumber)
        {
            return bossDefinition != null && bossPrefab != null
                && bossInterval > 0 && waveNumber % bossInterval == 0;
        }

        void SpawnBoss(int waveNumber, Transform target)
        {
            float scalar = ScalarFor(waveNumber);

            // Spawn opposite the player so the entrance is visible rather than on top of them.
            Vector3 center = GameManager.PlayerPosition;
            Vector3 position = center + new Vector3(0f, bossSpawnDistance, 0f);
            position.x = Mathf.Clamp(position.x, -arenaHalfExtents.x + 2f, arenaHalfExtents.x - 2f);
            position.y = Mathf.Clamp(position.y, -arenaHalfExtents.y + 2f, arenaHalfExtents.y - 2f);

            var boss = PoolManager.Spawn<ZombieController>(bossPrefab, position, Quaternion.identity);
            if (boss == null) return;

            boss.Initialize(bossDefinition, scalar, target);

            var brain = boss.GetComponent<BossBrain>();
            if (brain != null) brain.Initialize(bossDefinition, scalar);
        }

        /// <summary>Elites start a few waves in and scale up; Mini-Bosses land on a fixed interval.</summary>
        void AddSpecialTiers(List<ZombieDefinition> queue, int waveNumber)
        {
            if (eliteDefinition != null && waveNumber >= eliteStartWave)
            {
                int elites = Mathf.Clamp(1 + (waveNumber - eliteStartWave) / 2, 1, eliteCountCap);
                for (int i = 0; i < elites; i++) queue.Add(eliteDefinition);
            }

            if (miniBossDefinition != null && miniBossInterval > 0
                && waveNumber % miniBossInterval == 0 && !IsBossWave(waveNumber))
                queue.Add(miniBossDefinition);
        }

        /// <summary>Waves past the authored table: same pool, more of it, scaled harder.</summary>
        WaveDefinition WaveFor(int waveNumber)
        {
            if (waveNumber - 1 < waves.Count && waves[waveNumber - 1] != null)
                return waves[waveNumber - 1];

            if (proceduralPool.Count == 0) return null;

            var generated = ScriptableObject.CreateInstance<WaveDefinition>();
            generated.label = "Wave " + waveNumber;
            generated.spawnInterval = proceduralSpawnInterval;
            generated.intermission = proceduralIntermission;
            generated.clearToAdvance = true;

            int beyond = Mathf.Max(0, waveNumber - waves.Count);
            int total = Mathf.RoundToInt(proceduralBaseCount + proceduralCountGrowth * beyond);

            // Spread the count across the pool, weighted toward the front of the list.
            int remaining = total;
            for (int i = 0; i < proceduralPool.Count && remaining > 0; i++)
            {
                bool last = i == proceduralPool.Count - 1;
                int share = last ? remaining : Mathf.Max(1, Mathf.RoundToInt(total * (0.55f / (i + 1))));
                share = Mathf.Min(share, remaining);

                generated.entries.Add(new WaveDefinition.Entry { zombie = proceduralPool[i], count = share });
                remaining -= share;
            }

            return generated;
        }

        void Spawn(ZombieDefinition definition, float scalar, Transform target)
        {
            if (definition == null || zombiePrefab == null) return;

            Vector3 position = PickSpawnPosition();
            var zombie = PoolManager.Spawn<ZombieController>(zombiePrefab, position, Quaternion.identity);
            if (zombie == null) return;

            zombie.Initialize(definition, scalar, target);
            _spawnedThisWave.Add(zombie);
        }

        /// <summary>A point on a ring around the player, pulled back inside the arena bounds.</summary>
        Vector3 PickSpawnPosition()
        {
            Vector3 center = GameManager.PlayerPosition;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnRingRadius;

                if (Mathf.Abs(candidate.x) <= arenaHalfExtents.x && Mathf.Abs(candidate.y) <= arenaHalfExtents.y)
                    return candidate;
            }

            // Ring is mostly outside the arena (player in a corner): clamp and accept.
            float a = Random.value * Mathf.PI * 2f;
            Vector3 fallback = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * spawnRingRadius;
            fallback.x = Mathf.Clamp(fallback.x, -arenaHalfExtents.x, arenaHalfExtents.x);
            fallback.y = Mathf.Clamp(fallback.y, -arenaHalfExtents.y, arenaHalfExtents.y);
            return fallback;
        }

        static void Shuffle(List<ZombieDefinition> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(arenaHalfExtents.x * 2f, arenaHalfExtents.y * 2f, 0f));
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(Application.isPlaying ? GameManager.PlayerPosition : Vector3.zero, spawnRingRadius);
        }
    }
}
