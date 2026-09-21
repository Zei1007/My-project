using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Applies and expires buffs on a StatSheet. Lives on the player, but nothing here is
    /// player-specific - elites and bosses use the same component for their own self-buffs and for
    /// the debuffs they inflict.
    /// </summary>
    public class BuffController : MonoBehaviour
    {
        /// <summary>One live application. The instance is the modifier Source, so revoking is exact.</summary>
        public class ActiveBuff
        {
            public BuffDefinition Definition;
            public float Remaining;
            public int Stacks = 1;
            public bool Expires;
        }

        [Tooltip("How many different permanent buffs can be held. Timed loot buffs and curses do not use a slot.")]
        [SerializeField] int maxSlots = 6;

        readonly List<ActiveBuff> _active = new List<ActiveBuff>();

        StatSheet _stats;

        /// <summary>One entry per buff; repeated picks raise its Stacks (its level).</summary>
        public IReadOnlyList<ActiveBuff> Active { get { return _active; } }

        public int MaxSlots { get { return maxSlots; } }

        public int SlotsUsed
        {
            get
            {
                int used = 0;
                for (int i = 0; i < _active.Count; i++)
                    if (_active[i].Definition.TakesSlot) used++;
                return used;
            }
        }

        public bool HasFreeSlot { get { return SlotsUsed < maxSlots; } }

        public void Initialize(StatSheet stats)
        {
            _stats = stats;
            for (int i = 0; i < _active.Count; i++)
                _stats.RemoveAllFromSource(_active[i]);
            _active.Clear();
        }

        void Update()
        {
            if (_stats == null || _active.Count == 0) return;

            float dt = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var buff = _active[i];
                if (!buff.Expires) continue;

                buff.Remaining -= dt;
                if (buff.Remaining > 0f) continue;

                _stats.RemoveAllFromSource(buff);
                _active.RemoveAt(i);
                GameEvents.RaiseBuffExpired(buff.Definition);
            }
        }

        public bool Has(BuffDefinition definition)
        {
            return Find(definition) != null;
        }

        public ActiveBuff Find(BuffDefinition definition)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Definition == definition) return _active[i];
            return null;
        }

        public int StacksOf(BuffDefinition definition)
        {
            var b = Find(definition);
            return b == null ? 0 : b.Stacks;
        }

        /// <summary>
        /// True when applying would do something: a new buff needs a free slot (if it takes one),
        /// an owned buff needs to be under its level cap. A timed buff at its cap can still be
        /// applied - that refreshes its timer, which is what re-picking a Frenzy should do.
        /// </summary>
        public bool CanApply(BuffDefinition definition)
        {
            if (definition == null) return false;

            var existing = Find(definition);
            if (existing != null)
                return existing.Stacks < definition.MaxLevel || existing.Expires;

            return !definition.TakesSlot || HasFreeSlot;
        }

        /// <summary>Whether a level-up card should offer this buff: it must actually gain a level.</summary>
        public bool CanLevelUp(BuffDefinition definition)
        {
            if (definition == null || definition.durationType == BuffDuration.Timed) return false;

            var existing = Find(definition);
            if (existing != null) return existing.Stacks < definition.MaxLevel;

            return !definition.TakesSlot || HasFreeSlot;
        }

        public void Apply(BuffDefinition definition)
        {
            if (definition == null || _stats == null) return;
            if (!CanApply(definition)) return;

            var existing = Find(definition);

            if (existing != null)
            {
                // One entry per buff: another pick raises its level (up to the cap) and adds
                // one more copy of its modifiers under the same source, so expiry revokes all.
                if (existing.Stacks < definition.MaxLevel)
                {
                    existing.Stacks++;
                    _stats.AddModifiers(definition.modifiers, existing);
                }

                // Timed buffs refresh either way.
                if (existing.Expires) existing.Remaining = definition.duration;
            }
            else
            {
                var buff = new ActiveBuff
                {
                    Definition = definition,
                    Remaining = definition.duration,
                    Expires = definition.durationType == BuffDuration.Timed,
                    Stacks = 1,
                };

                _active.Add(buff);
                _stats.AddModifiers(definition.modifiers, buff);
            }

            GameEvents.RaiseBuffApplied(definition);

            // Taking a passive can complete a weapon evolution combo.
            var inventory = GetComponent<WeaponInventory>();
            if (inventory != null) inventory.CheckAllEvolutions();

            if (definition.pairedCurse != null) Apply(definition.pairedCurse);
        }

        /// <summary>Revokes a Conditional buff - leaving the zone that granted it, say.</summary>
        public void Remove(BuffDefinition definition)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Definition != definition) continue;
                _stats.RemoveAllFromSource(_active[i]);
                GameEvents.RaiseBuffExpired(definition);
                _active.RemoveAt(i);
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _active.Count; i++)
                _stats.RemoveAllFromSource(_active[i]);
            _active.Clear();
        }
    }
}
