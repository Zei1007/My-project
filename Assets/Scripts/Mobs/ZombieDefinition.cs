using UnityEngine;

namespace ZombieShooter
{
    public enum ZombieTier
    {
        Walker,
        Runner,
        Elite,
        MiniBoss,
        Boss,
    }

    /// <summary>The one special trick an Elite gets on top of the shared chase-and-melee AI.</summary>
    public enum EliteAbility
    {
        None,
        /// <summary>Winds up, then dashes at the player.</summary>
        Charge,
        /// <summary>Detonates on death, damaging the player if they are close.</summary>
        DeathExplosion,
        /// <summary>Periodic burst of movement speed.</summary>
        SpeedBurst,
    }

    /// <summary>
    /// A mob archetype. Tier multipliers and the wave scalar are applied on top of these base
    /// numbers at spawn time, so one Walker asset covers wave 1 and wave 30.
    /// </summary>
    [CreateAssetMenu(fileName = "MOB_New", menuName = "Zombie Shooter/Zombie Definition")]
    public class ZombieDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "zombie_id";
        public string displayName = "Walker";
        public ZombieTier tier = ZombieTier.Walker;

        [Header("Visuals")]
        [Tooltip("Body sprites for the animated rig. Drop real artwork into these slots.")]
        public CharacterPartSet parts = new CharacterPartSet();
        [Tooltip("Multiplied over the part sprites. Leave white - the parts already carry their colours.")]
        public Color tint = Color.white;
        public float scale = 1f;
        [Tooltip("Legacy single-sprite fallback, used only if parts are unset.")]
        public Sprite sprite;

        [Header("Base Stats")]
        public float health = 20f;
        public float moveSpeed = 1.8f;
        public float contactDamage = 8f;
        [Tooltip("Seconds between contact hits.")]
        public float attackInterval = 0.8f;
        public float xpValue = 1f;
        [Tooltip("Base score for a kill, before the stage multiplier. Feeds the leaderboard.")]
        public int scoreValue = 10;

        [Header("Tier Scaling")]
        [Tooltip("Multiplies health on top of the wave scalar.")]
        public float healthMultiplier = 1f;
        [Tooltip("Multiplies contact damage on top of the wave scalar.")]
        public float damageMultiplier = 1f;

        [Header("Elite / Boss")]
        public EliteAbility ability = EliteAbility.None;
        public float abilityCooldown = 5f;
        [Tooltip("Charge speed multiplier, explosion damage, or speed-burst multiplier.")]
        public float abilityMagnitude = 3f;
        [Tooltip("Explosion radius for DeathExplosion.")]
        public float abilityRadius = 2.5f;
        [Tooltip("Shows a dedicated boss healthbar and announcement.")]
        public bool showBossHealthBar;

        [Header("Ranged")]
        [Tooltip("Keeps its distance and shoots instead of closing to melee.")]
        public bool isRanged;
        [Tooltip("Distance it tries to hold from the player.")]
        public float preferredRange = 5f;
        public float shotInterval = 2.4f;
        [Tooltip("Projectile speed at wave 1. WaveManager scales it up stage by stage.")]
        public float projectileSpeed = 4f;
        [Tooltip("Projectile damage as a multiple of this mob's contact damage.")]
        public float projectileDamageMultiplier = 0.8f;
        public Color projectileTint = new Color(0.55f, 1f, 0.35f);
        public GameObject projectilePrefab;

        [Header("Drops")]
        [Tooltip("Chance (0-1) to drop a timed pickup buff on death.")]
        [Range(0f, 1f)] public float pickupDropChance = 0.02f;
        [Tooltip("Rewards beyond soul gems. Each line rolls independently on death.")]
        public System.Collections.Generic.List<LootEntry> loot = new System.Collections.Generic.List<LootEntry>();

        void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
            if (scale <= 0f) scale = 1f;
        }
    }
}
