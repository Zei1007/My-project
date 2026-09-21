using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Flat list of every live zombie. Auto-targeting queries this instead of Physics2D overlap
    /// checks - with a few hundred mobs on screen a linear scan of transforms is far cheaper than
    /// repeated broadphase queries every time a weapon looks for a target.
    /// </summary>
    public static class EnemyRegistry
    {
        static readonly List<ZombieController> _all = new List<ZombieController>();

        public static IReadOnlyList<ZombieController> All { get { return _all; } }
        public static int Count { get { return _all.Count; } }

        public static void Register(ZombieController z)
        {
            if (z != null && !_all.Contains(z)) _all.Add(z);
        }

        public static void Unregister(ZombieController z)
        {
            _all.Remove(z);
        }

        public static void Clear()
        {
            _all.Clear();
        }

        /// <summary>Clears leftovers before any scene object wakes, for domain-reload-free play.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Clear();
        }

        /// <summary>Nearest live zombie within maxRange, or null. maxRange &lt;= 0 means unlimited.</summary>
        public static ZombieController FindNearest(Vector2 origin, float maxRange)
        {
            ZombieController best = null;
            float bestSqr = maxRange > 0f ? maxRange * maxRange : float.MaxValue;

            for (int i = 0; i < _all.Count; i++)
            {
                var z = _all[i];
                if (z == null || !z.IsAlive) continue;

                float sqr = ((Vector2)z.transform.position - origin).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = z;
                }
            }
            return best;
        }

        /// <summary>Fills results with every live zombie inside radius. Reuses the caller's list.</summary>
        public static void FindInRadius(Vector2 origin, float radius, List<ZombieController> results)
        {
            results.Clear();
            float sqrRadius = radius * radius;
            for (int i = 0; i < _all.Count; i++)
            {
                var z = _all[i];
                if (z == null || !z.IsAlive) continue;
                if (((Vector2)z.transform.position - origin).sqrMagnitude <= sqrRadius)
                    results.Add(z);
            }
        }
    }
}
