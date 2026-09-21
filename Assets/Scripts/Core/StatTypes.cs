using System;
using System.Collections.Generic;

namespace ZombieShooter
{
    /// <summary>Every tunable number that buffs, weapon levels and mob tiers can touch.</summary>
    public enum StatType
    {
        MaxHealth,
        Armor,
        MoveSpeed,
        Damage,
        FireRate,
        ProjectileSpeed,
        ProjectileCount,
        Pierce,
        AreaSize,
        Range,
        CritChance,
        CritMultiplier,
        DamageTaken,
        PickupRadius,
        XPGain,
        AttackInterval,
    }

    /// <summary>
    /// The four operations from the design doc. Additive/Subtractive are FLAT adjustments to the
    /// base layer; Multiplicative/Divisive form the percentage layer. A "-10% move speed" debuff is
    /// therefore authored as Multiplicative 0.9 (or Divisive 1.111), not Subtractive 0.1 - keeping
    /// the two layers separate is what makes the stacking order deterministic.
    /// </summary>
    public enum ModifierOp
    {
        Additive,
        Subtractive,
        Multiplicative,
        Divisive,
    }

    /// <summary>One authored stat change. Runtime copies carry a Source so they can be revoked as a group.</summary>
    [Serializable]
    public class StatModifier
    {
        public StatType stat;
        public ModifierOp op = ModifierOp.Additive;
        public float value;

        [NonSerialized] public object Source;

        public StatModifier() { }

        public StatModifier(StatType stat, ModifierOp op, float value, object source = null)
        {
            this.stat = stat;
            this.op = op;
            this.value = value;
            Source = source;
        }

        public StatModifier Clone(object source)
        {
            return new StatModifier(stat, op, value, source);
        }

        public string Describe()
        {
            string name = Nice(stat);
            switch (op)
            {
                case ModifierOp.Additive: return "+" + value.ToString("0.##") + " " + name;
                case ModifierOp.Subtractive: return "-" + value.ToString("0.##") + " " + name;
                case ModifierOp.Multiplicative: return "x" + value.ToString("0.##") + " " + name;
                default: return "/" + value.ToString("0.##") + " " + name;
            }
        }

        public static string Nice(StatType t)
        {
            switch (t)
            {
                case StatType.MaxHealth: return "Max Health";
                case StatType.MoveSpeed: return "Move Speed";
                case StatType.FireRate: return "Fire Rate";
                case StatType.ProjectileSpeed: return "Projectile Speed";
                case StatType.ProjectileCount: return "Projectiles";
                case StatType.AreaSize: return "Area Size";
                case StatType.CritChance: return "Crit Chance";
                case StatType.CritMultiplier: return "Crit Damage";
                case StatType.DamageTaken: return "Damage Taken";
                case StatType.PickupRadius: return "Pickup Radius";
                case StatType.XPGain: return "XP Gain";
                case StatType.AttackInterval: return "Attack Interval";
                default: return t.ToString();
            }
        }
    }

    /// <summary>A serializable set of base stat values, authored on ScriptableObjects.</summary>
    [Serializable]
    public class StatBlock
    {
        [Serializable]
        public struct Entry
        {
            public StatType stat;
            public float value;
        }

        public List<Entry> entries = new List<Entry>();

        public void ApplyTo(StatSheet sheet)
        {
            for (int i = 0; i < entries.Count; i++)
                sheet.SetBase(entries[i].stat, entries[i].value);
        }
    }
}
