using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// The player's starting numbers. Meta-progression, when it lands, applies its permanent
    /// modifiers on top of these rather than editing them.
    /// </summary>
    [CreateAssetMenu(fileName = "PLR_Default", menuName = "Zombie Shooter/Player Definition")]
    public class PlayerDefinition : ScriptableObject
    {
        [Header("Base Stats")]
        public StatBlock baseStats = new StatBlock();

        [Header("Leveling")]
        [Tooltip("Highest level the player can reach. XP earned beyond it converts to score.")]
        public int maxLevel = 30;
        [Tooltip("Score awarded per point of XP collected after the level cap.")]
        public float scorePerOverflowXP = 5f;
        [Tooltip("XP required to reach level 2.")]
        public float baseXPToLevel = 5f;
        [Tooltip("Multiplied into the requirement each level.")]
        public float xpCurveGrowth = 1.25f;
        [Tooltip("Flat XP added to the requirement each level.")]
        public float xpCurveFlat = 2f;

        public float XPRequiredFor(int level)
        {
            float requirement = baseXPToLevel * Mathf.Pow(xpCurveGrowth, Mathf.Max(0, level - 1));
            requirement += xpCurveFlat * Mathf.Max(0, level - 1);
            return Mathf.Max(1f, Mathf.Round(requirement));
        }

        /// <summary>Fills in sane defaults so a freshly created asset is already playable.</summary>
        [ContextMenu("Fill Default Stats")]
        public void FillDefaults()
        {
            baseStats.entries.Clear();
            Add(StatType.MaxHealth, 100f);
            Add(StatType.Armor, 0f);
            Add(StatType.MoveSpeed, 5.5f);
            Add(StatType.Damage, 1f);
            Add(StatType.FireRate, 1f);
            Add(StatType.ProjectileSpeed, 1f);
            Add(StatType.ProjectileCount, 0f);
            Add(StatType.Pierce, 0f);
            Add(StatType.AreaSize, 1f);
            Add(StatType.Range, 1f);
            Add(StatType.CritChance, 0.05f);
            Add(StatType.CritMultiplier, 2f);
            Add(StatType.DamageTaken, 1f);
            Add(StatType.PickupRadius, 1.8f);
            Add(StatType.XPGain, 1f);
        }

        void Add(StatType stat, float value)
        {
            baseStats.entries.Add(new StatBlock.Entry { stat = stat, value = value });
        }
    }
}
