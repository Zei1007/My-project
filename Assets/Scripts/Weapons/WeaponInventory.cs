using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// The player's weapons: exactly one ranged weapon and one melee weapon can be equipped at a time.
    ///
    /// Taking a different ranged weapon swaps it in, but every weapon's tier is remembered for the
    /// whole run - swap back to the Pistol later and it returns at the level you left it, not level 1.
    /// That keeps swapping a real choice instead of a punishment.
    /// </summary>
    public class WeaponInventory : MonoBehaviour
    {
        [SerializeField] WeaponDefinition startingWeapon;
        [SerializeField] LayerMask enemyMask;
        [SerializeField] Transform muzzle;

        // Every weapon picked up this run and the tier it has reached, equipped or not.
        readonly Dictionary<WeaponDefinition, int> _owned = new Dictionary<WeaponDefinition, int>();
        readonly List<WeaponInstance> _active = new List<WeaponInstance>(2);

        WeaponInstance _ranged;
        WeaponInstance _melee;
        PlayerController _player;
        StatSheet _stats;

        /// <summary>Raised on each shot so the rig, muzzle flash and camera can react.</summary>
        public event Action<WeaponInstance, Vector2> Fired;

        /// <summary>Raised when a weapon is equipped, swapped or upgraded.</summary>
        public event Action Changed;

        /// <summary>The equipped weapons only - at most one ranged and one melee.</summary>
        public IReadOnlyList<WeaponInstance> Weapons { get { return _active; } }
        public WeaponInstance Ranged { get { return _ranged; } }
        public WeaponInstance Melee { get { return _melee; } }
        public Transform Muzzle { get { return muzzle; } }

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            if (muzzle == null) muzzle = transform;
        }

        public void Initialize(StatSheet stats)
        {
            _stats = stats;
            _owned.Clear();
            _ranged = null;
            _melee = null;
            RebuildActive();

            if (startingWeapon != null) Equip(startingWeapon);
        }

        void Update()
        {
            if (_stats == null) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Running) return;

            Vector2 aim = _player != null ? _player.AimDirection : (Vector2)transform.right;
            float dt = Time.deltaTime;

            for (int i = 0; i < _active.Count; i++)
            {
                Vector2 firedDirection;
                if (_active[i].Tick(dt, muzzle, _stats, aim, enemyMask, out firedDirection) && Fired != null)
                    Fired(_active[i], firedDirection);
            }
        }

        // --- queries -----------------------------------------------------------

        public bool Owns(WeaponDefinition definition)
        {
            return definition != null && _owned.ContainsKey(definition);
        }

        /// <summary>The tier this weapon has reached this run, or 0 if never picked up.</summary>
        public int StoredLevel(WeaponDefinition definition)
        {
            int level;
            return definition != null && _owned.TryGetValue(definition, out level) ? level : 0;
        }

        /// <summary>Every weapon picked up this run, equipped or holstered.</summary>
        public IEnumerable<WeaponDefinition> OwnedWeapons { get { return _owned.Keys; } }

        public bool IsEquipped(WeaponDefinition definition)
        {
            return Find(definition) != null;
        }

        /// <summary>Equipped instance of this weapon, or null.</summary>
        public WeaponInstance Find(WeaponDefinition definition)
        {
            if (_ranged != null && _ranged.Definition == definition) return _ranged;
            if (_melee != null && _melee.Definition == definition) return _melee;
            return null;
        }

        /// <summary>What equipping this weapon would replace, if anything.</summary>
        public WeaponInstance SlotOccupant(WeaponDefinition definition)
        {
            if (definition == null) return null;
            return definition.IsMelee ? _melee : _ranged;
        }

        // Kept for older call sites: "has" now means "has it equipped".
        public bool Has(WeaponDefinition definition) { return IsEquipped(definition); }

        // --- changes -----------------------------------------------------------

        /// <summary>
        /// Puts this weapon in its slot, replacing whatever was there. A weapon picked up before
        /// comes back at its remembered tier.
        /// </summary>
        public WeaponInstance Equip(WeaponDefinition definition)
        {
            if (definition == null) return null;

            var equipped = Find(definition);
            if (equipped != null) return equipped;

            int level = StoredLevel(definition);
            if (level <= 0) level = 1;
            _owned[definition] = level;

            var instance = new WeaponInstance(definition, level);
            if (definition.IsMelee) _melee = instance;
            else _ranged = instance;

            RebuildActive();
            RaiseChanged();
            return instance;
        }

        // Kept for older call sites.
        public WeaponInstance Add(WeaponDefinition definition) { return Equip(definition); }

        /// <summary>Raises a weapon one tier. The stored tier moves even if the weapon is holstered.</summary>
        public bool Upgrade(WeaponDefinition definition)
        {
            if (!Owns(definition)) return false;

            int level = _owned[definition];
            if (level >= definition.MaxLevel) return false;
            _owned[definition] = level + 1;

            var instance = Find(definition);
            if (instance != null)
            {
                instance.TryUpgrade();
                TryEvolve(instance);
            }

            RaiseChanged();
            return true;
        }

        void RebuildActive()
        {
            _active.Clear();
            if (_ranged != null) _active.Add(_ranged);
            if (_melee != null) _active.Add(_melee);
        }

        void RaiseChanged()
        {
            if (Changed != null) Changed();
        }

        /// <summary>
        /// Vampire-Survivors-style combo: a maxed weapon plus the matching passive becomes its
        /// evolved form. Checked on upgrade and whenever a new buff lands.
        /// </summary>
        public bool TryEvolve(WeaponInstance instance)
        {
            if (instance == null || instance.Definition.evolvesInto == null) return false;
            if (!instance.IsMaxLevel) return false;

            var required = instance.Definition.requiredBuff;
            if (required != null)
            {
                var buffs = GetComponent<BuffController>();
                if (buffs == null || !buffs.Has(required)) return false;
            }

            var from = instance.Definition;
            instance.ReplaceWith(from.evolvesInto);
            _owned.Remove(from);
            _owned[instance.Definition] = instance.Level;

            RaiseChanged();
            return true;
        }

        public void CheckAllEvolutions()
        {
            for (int i = 0; i < _active.Count; i++)
                TryEvolve(_active[i]);
        }
    }
}
