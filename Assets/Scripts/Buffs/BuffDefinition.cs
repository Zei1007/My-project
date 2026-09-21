using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    public enum BuffDuration
    {
        /// <summary>Permanent for the rest of the run - level-up picks and meta upgrades.</summary>
        Instant,
        /// <summary>Expires after a set number of seconds - dropped pickups, elite curses.</summary>
        Timed,
        /// <summary>Lasts until something revokes it by source - zone effects, auras.</summary>
        Conditional,
    }

    /// <summary>
    /// A bundle of StatModifiers with a lifetime. Buffs and debuffs are the same asset type - a
    /// debuff is just modifiers that make numbers worse, which is what lets the "curse for reward"
    /// choices reuse the whole pipeline.
    /// </summary>
    [CreateAssetMenu(fileName = "BUF_New", menuName = "Zombie Shooter/Buff Definition")]
    public class BuffDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "buff_id";
        public string displayName = "New Buff";
        [Tooltip("Up to 3 letters shown on the on-screen buff badge, e.g. DMG, ROF, HP.")]
        public string shortLabel = "";
        [TextArea(2, 4)] public string description = "";
        public Sprite icon;
        public Color tint = Color.white;
        [Tooltip("Marks this as a downside. Used by UI tinting and by curse-for-reward pairings.")]
        public bool isDebuff;

        [Header("Lifetime")]
        public BuffDuration durationType = BuffDuration.Instant;
        [Tooltip("Seconds, for Timed buffs only.")]
        public float duration = 10f;
        [Tooltip("Maximum level (times it can be taken). 0 uses the default cap of 5. Timed buffs refresh at the cap.")]
        public int maxStacks = 0;

        public const int DefaultMaxStacks = 5;

        /// <summary>The effective cap - every buff has one, so nothing scales forever.</summary>
        public int MaxLevel { get { return maxStacks > 0 ? maxStacks : DefaultMaxStacks; } }

        /// <summary>Permanent blessings occupy a buff slot; timed loot buffs and curses do not.</summary>
        public bool TakesSlot { get { return durationType == BuffDuration.Instant && !isDebuff; } }

        /// <summary>Badge text: the authored short label, or initials of the name.</summary>
        public string BadgeLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(shortLabel)) return shortLabel;
                var sb = new System.Text.StringBuilder();
                foreach (var word in displayName.Split(' '))
                    if (word.Length > 0 && sb.Length < 3) sb.Append(char.ToUpperInvariant(word[0]));
                return sb.ToString();
            }
        }

        [Header("Effect")]
        public List<StatModifier> modifiers = new List<StatModifier>();

        [Header("Curse for Reward")]
        [Tooltip("Optional. Applied alongside this buff - the downside half of a risk/reward pick.")]
        public BuffDefinition pairedCurse;

        public string AutoDescription()
        {
            if (!string.IsNullOrEmpty(description)) return description;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(modifiers[i].Describe());
            }
            return sb.ToString();
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
            if (duration < 0f) duration = 0f;
        }
    }
}
