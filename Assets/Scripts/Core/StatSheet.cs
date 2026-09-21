using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Layered stat container shared by the player and every mob. Resolution order is fixed:
    ///   (base + additive - subtractive) * (product of multiplicative / product of divisive)
    /// so the order modifiers arrived in never changes the result.
    /// </summary>
    public class StatSheet
    {
        static readonly StatType[] AllStats = (StatType[])Enum.GetValues(typeof(StatType));

        readonly Dictionary<StatType, float> _base = new Dictionary<StatType, float>();
        readonly Dictionary<StatType, float> _cache = new Dictionary<StatType, float>();
        readonly List<StatModifier> _modifiers = new List<StatModifier>();

        bool _dirty = true;

        public event Action Changed;

        public IReadOnlyList<StatModifier> Modifiers { get { return _modifiers; } }

        public StatSheet()
        {
            for (int i = 0; i < AllStats.Length; i++)
                _base[AllStats[i]] = DefaultBase(AllStats[i]);
        }

        /// <summary>Neutral values: multiplier-style stats start at 1, additive-style at 0.</summary>
        public static float DefaultBase(StatType s)
        {
            switch (s)
            {
                case StatType.Damage:
                case StatType.FireRate:
                case StatType.ProjectileSpeed:
                case StatType.AreaSize:
                case StatType.Range:
                case StatType.DamageTaken:
                case StatType.XPGain:
                case StatType.AttackInterval:
                    return 1f;
                case StatType.CritMultiplier:
                    return 2f;
                default:
                    return 0f;
            }
        }

        public void SetBase(StatType stat, float value)
        {
            _base[stat] = value;
            MarkDirty();
        }

        public float GetBase(StatType stat)
        {
            float v;
            return _base.TryGetValue(stat, out v) ? v : DefaultBase(stat);
        }

        public float Get(StatType stat)
        {
            if (_dirty) Recalculate();
            float v;
            return _cache.TryGetValue(stat, out v) ? v : GetBase(stat);
        }

        public int GetInt(StatType stat)
        {
            return Mathf.RoundToInt(Get(stat));
        }

        public void AddModifier(StatModifier modifier)
        {
            if (modifier == null) return;
            _modifiers.Add(modifier);
            MarkDirty();
        }

        public void AddModifiers(IEnumerable<StatModifier> modifiers, object source)
        {
            if (modifiers == null) return;
            foreach (var m in modifiers)
            {
                if (m == null) continue;
                _modifiers.Add(m.Clone(source));
            }
            MarkDirty();
        }

        public void RemoveModifier(StatModifier modifier)
        {
            if (_modifiers.Remove(modifier)) MarkDirty();
        }

        /// <summary>Revokes everything a single buff / weapon / aura contributed.</summary>
        public void RemoveAllFromSource(object source)
        {
            if (source == null) return;
            int removed = _modifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
            if (removed > 0) MarkDirty();
        }

        public void ClearModifiers()
        {
            if (_modifiers.Count == 0) return;
            _modifiers.Clear();
            MarkDirty();
        }

        void MarkDirty()
        {
            _dirty = true;
            if (Changed != null) Changed();
        }

        void Recalculate()
        {
            _dirty = false;
            for (int s = 0; s < AllStats.Length; s++)
            {
                StatType stat = AllStats[s];
                float flat = 0f;
                float multiplier = 1f;

                for (int i = 0; i < _modifiers.Count; i++)
                {
                    var m = _modifiers[i];
                    if (m.stat != stat) continue;

                    switch (m.op)
                    {
                        case ModifierOp.Additive: flat += m.value; break;
                        case ModifierOp.Subtractive: flat -= m.value; break;
                        case ModifierOp.Multiplicative: multiplier *= m.value; break;
                        case ModifierOp.Divisive:
                            if (Mathf.Abs(m.value) > 0.0001f) multiplier /= m.value;
                            break;
                    }
                }

                _cache[stat] = ClampStat(stat, (GetBase(stat) + flat) * multiplier);
            }
        }

        static float ClampStat(StatType stat, float value)
        {
            switch (stat)
            {
                case StatType.CritChance:
                    return Mathf.Clamp01(value);
                case StatType.MaxHealth:
                    return Mathf.Max(1f, value);
                case StatType.FireRate:
                case StatType.AttackInterval:
                case StatType.Damage:
                case StatType.MoveSpeed:
                case StatType.AreaSize:
                case StatType.Range:
                    return Mathf.Max(0.01f, value);
                case StatType.DamageTaken:
                case StatType.ProjectileCount:
                case StatType.Pierce:
                case StatType.Armor:
                    return Mathf.Max(0f, value);
                default:
                    return value;
            }
        }
    }
}
