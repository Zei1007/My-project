using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    public enum WeaponCategory
    {
        Hitscan,
        Projectile,
        Area,
        Support,
    }

    /// <summary>How far a weapon reaches. Authored as a class so the level-up card can say it plainly.</summary>
    public enum WeaponRangeClass
    {
        Short,
        Medium,
        Long,
    }

    /// <summary>One upgrade tier. Level 1 is the weapon as acquired; each later entry replaces it wholesale.</summary>
    [Serializable]
    public class WeaponLevel
    {
        [Tooltip("Shown on the level-up card when this tier is offered.")]
        [TextArea(1, 3)] public string upgradeText = "";

        public float damage = 10f;
        [Tooltip("Shots per second, before the owner's FireRate stat.")]
        public float fireRate = 2f;
        public int projectileCount = 1;
        public int pierce = 0;
        [Tooltip("Total arc in degrees across all projectiles.")]
        public float spreadAngle = 0f;
        public float range = 9f;
        public float projectileSpeed = 14f;
        [Tooltip("Radius for Area weapons; visual scale for projectiles.")]
        public float areaSize = 1f;
        [Range(0f, 1f)] public float critChance = 0f;
        public float knockback = 0f;
    }

    /// <summary>
    /// Data-driven weapon. New weapons and tiers are authored as assets - no new code - which is
    /// what keeps the upgrade pool cheap to extend.
    /// </summary>
    [CreateAssetMenu(fileName = "WPN_New", menuName = "Zombie Shooter/Weapon Definition")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "weapon_id";
        public string displayName = "New Weapon";
        [TextArea(2, 4)] public string description = "";
        public Sprite icon;
        [Tooltip("Shown in the player's hands while this weapon is held.")]
        public Sprite worldSprite;
        public Color tint = Color.white;

        [Header("Behaviour")]
        public WeaponCategory category = WeaponCategory.Projectile;
        [Tooltip("Short / Medium / Long. Sets the base reach; tiers can extend it slightly.")]
        public WeaponRangeClass rangeClass = WeaponRangeClass.Medium;
        [Tooltip("Auto-fire at the nearest target. Off means the weapon fires along the player's aim only.")]
        public bool autoTarget = true;
        [Tooltip("Required for Projectile weapons. Must carry a Projectile component.")]
        public GameObject projectilePrefab;
        [Tooltip("Optional visual for Hitscan and Area weapons.")]
        public GameObject effectPrefab;

        [Header("Tiers")]
        public List<WeaponLevel> levels = new List<WeaponLevel>();

        [Header("Evolution")]
        [Tooltip("Optional. At max level, holding requiredBuff swaps this weapon for the evolved one.")]
        public WeaponDefinition evolvesInto;
        public BuffDefinition requiredBuff;

        public int MaxLevel { get { return Mathf.Max(1, levels.Count); } }

        /// <summary>Melee weapons take the second slot; everything else competes for the ranged slot.</summary>
        public bool IsMelee { get { return category == WeaponCategory.Area; } }

        /// <summary>Base reach in world units for each range class. The view is 15 x 8.4 units.</summary>
        public static float BaseRange(WeaponRangeClass rangeClass)
        {
            switch (rangeClass)
            {
                case WeaponRangeClass.Short: return 4.5f;
                case WeaponRangeClass.Long: return 10.5f;
                default: return 7f;
            }
        }

        public string RangeLabel
        {
            get { return IsMelee ? "Melee" : rangeClass + " range"; }
        }

        public WeaponLevel GetLevel(int level)
        {
            if (levels == null || levels.Count == 0) return new WeaponLevel();
            int index = Mathf.Clamp(level - 1, 0, levels.Count - 1);
            return levels[index];
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
            if (levels.Count == 0) levels.Add(new WeaponLevel());
        }
    }
}
