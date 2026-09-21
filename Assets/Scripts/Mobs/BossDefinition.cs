using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    public enum BossAttackType
    {
        /// <summary>Telegraphed AoE circle centred on where the player was standing.</summary>
        GroundSlam,
        /// <summary>Ring of projectiles fired outward from the boss.</summary>
        RadialBurst,
        /// <summary>Calls in a handful of regular mobs.</summary>
        SummonAdds,
        /// <summary>Telegraphed line, then a fast dash along it.</summary>
        ChargeDash,
        /// <summary>Drops lingering damage pools around the arena.</summary>
        HazardField,
    }

    [Serializable]
    public class BossAttackEntry
    {
        public BossAttackType type = BossAttackType.GroundSlam;
        [Tooltip("Relative chance of this attack being chosen in its phase.")]
        [Min(0.01f)] public float weight = 1f;

        [Header("Shape")]
        public float radius = 3.5f;
        [Tooltip("Projectile count, add count, or hazard count depending on the attack.")]
        public int count = 12;
        [Tooltip("Seconds of telegraph before the attack lands.")]
        public float windup = 0.9f;
        [Tooltip("Damage multiplier against the boss's contact damage.")]
        public float damageMultiplier = 1.4f;
    }

    /// <summary>
    /// One stage of the fight. The boss enters a phase when its health fraction drops to
    /// healthThreshold, and each phase swaps in its own attack set and stat multipliers - that
    /// change in behaviour partway through is what separates a boss from a big Mini-Boss.
    /// </summary>
    [Serializable]
    public class BossPhase
    {
        public string phaseName = "Phase";
        [Tooltip("Entered when health fraction falls to or below this. First phase should be 1.")]
        [Range(0f, 1f)] public float healthThreshold = 1f;

        [Header("Stat Shift")]
        public float moveSpeedMultiplier = 1f;
        public float damageMultiplier = 1f;
        public float damageTakenMultiplier = 1f;

        [Header("Pacing")]
        [Tooltip("Seconds between attacks in this phase.")]
        public float attackInterval = 3.5f;

        [Header("Attacks")]
        public List<BossAttackEntry> attacks = new List<BossAttackEntry>();

        [Header("Look")]
        public Color auraColor = new Color(0.6f, 0.2f, 0.9f, 0.5f);
    }

    /// <summary>
    /// A Boss is a ZombieDefinition with phases bolted on, so it still spawns, takes damage, drops
    /// XP and runs the shared chase AI - BossBrain only adds the attack layer on top.
    /// </summary>
    [CreateAssetMenu(fileName = "BOSS_New", menuName = "Zombie Shooter/Boss Definition")]
    public class BossDefinition : ZombieDefinition
    {
        [Header("Boss")]
        public string title = "The Devourer";
        [TextArea(2, 3)] public string introLine = "Something enormous is coming.";
        [Tooltip("Seconds the intro banner holds before the fight starts.")]
        public float introDuration = 2.5f;

        [Header("Phases")]
        [Tooltip("Ordered from full health down. The first entry should have a threshold of 1.")]
        public List<BossPhase> phases = new List<BossPhase>();

        [Header("Adds")]
        public ZombieDefinition addDefinition;
        public GameObject enemyProjectilePrefab;
        public GameObject hazardPrefab;

        /// <summary>The deepest phase whose threshold the current health fraction has reached.</summary>
        public int PhaseIndexFor(float healthFraction)
        {
            int index = 0;
            for (int i = 0; i < phases.Count; i++)
            {
                if (healthFraction <= phases[i].healthThreshold) index = i;
            }
            return Mathf.Clamp(index, 0, Mathf.Max(0, phases.Count - 1));
        }
    }
}
