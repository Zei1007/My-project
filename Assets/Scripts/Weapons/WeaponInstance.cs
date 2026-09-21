using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A weapon the player actually owns: its definition plus the current tier and cooldown.
    /// All effective numbers are (authored tier value) combined with the owner's StatSheet, so a
    /// buff and a weapon upgrade compose without either knowing about the other.
    /// </summary>
    public class WeaponInstance
    {
        static readonly List<ZombieController> _areaBuffer = new List<ZombieController>();
        static readonly RaycastHit2D[] _rayBuffer = new RaycastHit2D[32];

        public WeaponDefinition Definition { get; private set; }
        public int Level { get; private set; }

        float _cooldown;

        public bool IsMaxLevel { get { return Level >= Definition.MaxLevel; } }
        public WeaponLevel CurrentTier { get { return Definition.GetLevel(Level); } }

        public WeaponInstance(WeaponDefinition definition, int level = 1)
        {
            Definition = definition;
            Level = Mathf.Clamp(level, 1, definition.MaxLevel);
        }

        public bool TryUpgrade()
        {
            if (IsMaxLevel) return false;
            Level++;
            return true;
        }

        /// <summary>Used when an evolution replaces this weapon in place.</summary>
        public void ReplaceWith(WeaponDefinition definition, int level = 1)
        {
            Definition = definition;
            Level = Mathf.Clamp(level, 1, definition.MaxLevel);
            _cooldown = 0f;
        }

        // --- effective stats -------------------------------------------------

        public float Damage(StatSheet s) { return CurrentTier.damage * s.Get(StatType.Damage); }
        public float Range(StatSheet s) { return CurrentTier.range * s.Get(StatType.Range); }
        public float AreaSize(StatSheet s) { return CurrentTier.areaSize * s.Get(StatType.AreaSize); }
        public int ProjectileCount(StatSheet s) { return Mathf.Max(1, CurrentTier.projectileCount + s.GetInt(StatType.ProjectileCount)); }
        public int Pierce(StatSheet s) { return Mathf.Max(0, CurrentTier.pierce + s.GetInt(StatType.Pierce)); }
        public float CritChance(StatSheet s) { return Mathf.Clamp01(CurrentTier.critChance + s.Get(StatType.CritChance)); }

        public float Interval(StatSheet s)
        {
            float shotsPerSecond = Mathf.Max(0.01f, CurrentTier.fireRate * s.Get(StatType.FireRate));
            return 1f / shotsPerSecond;
        }

        // --- firing ----------------------------------------------------------

        /// <summary>Returns true on the frames a shot is actually fired.</summary>
        public bool Tick(float deltaTime, Transform origin, StatSheet stats, Vector2 aimDirection,
                         LayerMask enemyMask, out Vector2 firedDirection)
        {
            firedDirection = aimDirection;

            _cooldown -= deltaTime;
            if (_cooldown > 0f) return false;

            Vector2 position = origin.position;
            float range = Range(stats);
            Vector2 direction = aimDirection;

            if (Definition.autoTarget)
            {
                var target = EnemyRegistry.FindNearest(position, range);
                if (target == null) return false;   // nothing in range: hold fire, cooldown stays ready
                direction = ((Vector2)target.transform.position - position).normalized;
            }

            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

            Fire(position, direction, stats, enemyMask);
            _cooldown = Interval(stats);

            firedDirection = direction;
            return true;
        }

        void Fire(Vector2 origin, Vector2 direction, StatSheet stats, LayerMask enemyMask)
        {
            switch (Definition.category)
            {
                case WeaponCategory.Projectile: FireProjectiles(origin, direction, stats); break;
                case WeaponCategory.Hitscan: FireHitscan(origin, direction, stats, enemyMask); break;
                case WeaponCategory.Area: FireArea(origin, stats); break;
                default: FireProjectiles(origin, direction, stats); break;
            }
        }

        /// <summary>Evenly fans projectileCount shots across the tier's spread angle.</summary>
        void FireProjectiles(Vector2 origin, Vector2 direction, StatSheet stats)
        {
            if (Definition.projectilePrefab == null) return;

            var tier = CurrentTier;
            int count = ProjectileCount(stats);
            float spread = tier.spreadAngle;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float step = count > 1 ? spread / (count - 1) : 0f;
            float start = count > 1 ? baseAngle - spread * 0.5f : baseAngle;

            float damage = Damage(stats);
            float speed = tier.projectileSpeed * stats.Get(StatType.ProjectileSpeed);
            int pierce = Pierce(stats);
            float crit = CritChance(stats);
            float critMult = stats.Get(StatType.CritMultiplier);
            float scale = AreaSize(stats);

            for (int i = 0; i < count; i++)
            {
                float angle = start + step * i;
                var rotation = Quaternion.Euler(0f, 0f, angle);
                var projectile = PoolManager.Spawn<Projectile>(Definition.projectilePrefab, origin, rotation);
                if (projectile == null) continue;

                projectile.Configure(damage, speed, pierce, tier.knockback, crit, critMult,
                                     scale, Definition.tint, Range(stats),
                                     GameManager.Instance != null ? GameManager.Instance.gameObject : null);
            }
        }

        /// <summary>Instant raycast; pierce decides how many zombies along the line are hit.</summary>
        void FireHitscan(Vector2 origin, Vector2 direction, StatSheet stats, LayerMask enemyMask)
        {
            var tier = CurrentTier;
            int shots = ProjectileCount(stats);
            float spread = tier.spreadAngle;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float step = shots > 1 ? spread / (shots - 1) : 0f;
            float start = shots > 1 ? baseAngle - spread * 0.5f : baseAngle;

            float damage = Damage(stats);
            float range = Range(stats);
            int maxTargets = Pierce(stats) + 1;
            float crit = CritChance(stats);
            float critMult = stats.Get(StatType.CritMultiplier);

            for (int s = 0; s < shots; s++)
            {
                float angle = (start + step * s) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                int hits = Physics2D.RaycastNonAlloc(origin, dir, _rayBuffer, range, enemyMask);
                SortByDistance(hits);

                int applied = 0;
                Vector2 endPoint = origin + dir * range;

                for (int i = 0; i < hits && applied < maxTargets; i++)
                {
                    var zombie = _rayBuffer[i].collider != null
                        ? _rayBuffer[i].collider.GetComponentInParent<ZombieController>()
                        : null;
                    if (zombie == null || !zombie.IsAlive) continue;

                    float dealt = damage;
                    if (crit > 0f && Random.value < crit) dealt *= critMult;

                    zombie.ApplyHit(dealt, dir, tier.knockback, null);
                    if (applied == 0) endPoint = _rayBuffer[i].point;
                    applied++;
                }

                SpawnBeam(origin, endPoint);
            }
        }

        /// <summary>Melee swing / mine field: damages everything inside the radius at once.</summary>
        void FireArea(Vector2 origin, StatSheet stats)
        {
            float radius = AreaSize(stats);
            float damage = Damage(stats);
            float crit = CritChance(stats);
            float critMult = stats.Get(StatType.CritMultiplier);
            var tier = CurrentTier;

            EnemyRegistry.FindInRadius(origin, radius, _areaBuffer);
            for (int i = 0; i < _areaBuffer.Count; i++)
            {
                var zombie = _areaBuffer[i];
                float dealt = damage;
                if (crit > 0f && Random.value < crit) dealt *= critMult;

                Vector2 away = ((Vector2)zombie.transform.position - origin).normalized;
                zombie.ApplyHit(dealt, away, tier.knockback, null);
            }

            if (Definition.effectPrefab != null)
            {
                var fx = PoolManager.Spawn<SimpleFx>(Definition.effectPrefab, origin, Quaternion.identity);
                if (fx != null) fx.PlayBurst(origin, radius, Definition.tint);
            }
        }

        void SpawnBeam(Vector2 from, Vector2 to)
        {
            if (Definition.effectPrefab == null) return;
            var fx = PoolManager.Spawn<SimpleFx>(Definition.effectPrefab, from, Quaternion.identity);
            if (fx != null) fx.PlayBeam(from, to, 0.08f, Definition.tint);
        }

        /// <summary>RaycastNonAlloc does not guarantee ordering; pierce needs nearest-first.</summary>
        static void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                var current = _rayBuffer[i];
                int j = i - 1;
                while (j >= 0 && _rayBuffer[j].distance > current.distance)
                {
                    _rayBuffer[j + 1] = _rayBuffer[j];
                    j--;
                }
                _rayBuffer[j + 1] = current;
            }
        }
    }
}
