using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    public enum UpgradeKind
    {
        NewWeapon,
        WeaponLevel,
        Buff,
    }

    /// <summary>One card on the level-up screen.</summary>
    public class UpgradeOption
    {
        public UpgradeKind Kind;
        public WeaponDefinition Weapon;
        public BuffDefinition Buff;
        public string Title;
        public string Body;
        public string Tag;
        public Color Tint = Color.white;

        public void Apply(PlayerController player)
        {
            if (player == null) return;

            switch (Kind)
            {
                case UpgradeKind.NewWeapon:
                    player.Inventory.Equip(Weapon);
                    break;
                case UpgradeKind.WeaponLevel:
                    player.Inventory.Upgrade(Weapon);
                    break;
                case UpgradeKind.Buff:
                    player.Buffs.Apply(Buff);
                    break;
            }
        }
    }

    /// <summary>
    /// Builds the level-up choices. Weapon tiers, weapon swaps and buffs compete for the same
    /// three slots, but weapons are deliberately scarce: they are weighted low and capped per roll,
    /// so most level-ups are about buffs and a new gun stays an event.
    /// </summary>
    public class UpgradeService : MonoBehaviour
    {
        [SerializeField] List<WeaponDefinition> weaponPool = new List<WeaponDefinition>();
        [SerializeField] List<BuffDefinition> buffPool = new List<BuffDefinition>();
        [SerializeField] int choicesPerLevel = 3;

        [Header("Weapon Scarcity")]
        [Tooltip("Relative chance of a weapon card vs a buff card. Below 1 makes weapons rarer than buffs.")]
        [SerializeField] float weaponWeight = 0.35f;
        [SerializeField] float buffWeight = 1f;
        [Tooltip("Most weapon cards (new, swap or tier) allowed in a single roll.")]
        [SerializeField] int maxWeaponCardsPerRoll = 1;
        [Tooltip("Chance a roll is allowed to contain a weapon card at all.")]
        [Range(0f, 1f)] [SerializeField] float weaponCardChance = 0.55f;

        readonly List<UpgradeOption> _candidates = new List<UpgradeOption>();

        public int ChoicesPerLevel { get { return choicesPerLevel; } }

        /// <summary>Rolls a set of cards. A blessing (elite/boss loot) offers buffs only.</summary>
        public List<UpgradeOption> Roll(PlayerController player, bool buffsOnly = false)
        {
            var result = new List<UpgradeOption>();
            if (player == null) return result;

            BuildCandidates(player, buffsOnly);
            if (_candidates.Count == 0) return result;

            var pool = new List<UpgradeOption>(_candidates);

            // Some rolls get no weapon card at all - on top of the low weight.
            int weaponCap = buffsOnly || Random.value > weaponCardChance ? 0 : maxWeaponCardsPerRoll;
            bool anyBuffs = pool.Exists(o => o.Kind == UpgradeKind.Buff);
            if (!anyBuffs) weaponCap = choicesPerLevel;   // never leave the player with nothing

            int weaponCards = 0;
            int take = Mathf.Min(choicesPerLevel, pool.Count);

            for (int i = 0; i < take && pool.Count > 0; i++)
            {
                if (weaponCards >= weaponCap)
                    pool.RemoveAll(o => o.Kind != UpgradeKind.Buff);
                if (pool.Count == 0) break;

                int index = PickWeightedIndex(pool);
                var pick = pool[index];
                pool.RemoveAt(index);

                if (pick.Kind != UpgradeKind.Buff) weaponCards++;
                result.Add(pick);
            }
            return result;
        }

        void BuildCandidates(PlayerController player, bool buffsOnly)
        {
            _candidates.Clear();

            var inventory = player.Inventory;
            var buffs = player.Buffs;

            if (!buffsOnly)
            {
                // Tiers of every owned weapon - holstered ones included, so investing in a weapon
                // you swapped away from still pays off when you swap back.
                foreach (var owned in inventory.OwnedWeapons)
                {
                    int level = inventory.StoredLevel(owned);
                    if (level >= owned.MaxLevel) continue;

                    bool holstered = !inventory.IsEquipped(owned);
                    var nextTier = owned.GetLevel(level + 1);
                    string body = (string.IsNullOrEmpty(nextTier.upgradeText) ? "Improves this weapon." : nextTier.upgradeText)
                                  + "\n" + owned.RangeLabel;
                    if (holstered) body += "\nHolstered - ready when you equip it";

                    _candidates.Add(new UpgradeOption
                    {
                        Kind = UpgradeKind.WeaponLevel,
                        Weapon = owned,
                        Title = owned.displayName,
                        Tag = (holstered ? "HOLSTERED  " : "") + "LV " + (level + 1) + "/" + owned.MaxLevel,
                        Body = body,
                        Tint = owned.tint,
                    });
                }

                // Weapons that are not equipped - new ones, or ones to swap back to.
                for (int i = 0; i < weaponPool.Count; i++)
                {
                    var definition = weaponPool[i];
                    if (definition == null || inventory.IsEquipped(definition)) continue;

                    int stored = inventory.StoredLevel(definition);
                    var occupant = inventory.SlotOccupant(definition);

                    string body = definition.description + "\n" + definition.RangeLabel;
                    if (occupant != null) body += "\nReplaces " + occupant.Definition.displayName;

                    _candidates.Add(new UpgradeOption
                    {
                        Kind = UpgradeKind.NewWeapon,
                        Weapon = definition,
                        Title = definition.displayName,
                        Tag = stored > 0 ? "EQUIP  LV " + stored + "/" + definition.MaxLevel : "NEW",
                        Body = body,
                        Tint = definition.tint,
                    });
                }
            }

            // Buffs that can still gain a level. With every slot full, only buffs already held
            // are offered - that is what makes the slot cap a real build decision.
            for (int i = 0; i < buffPool.Count; i++)
            {
                var definition = buffPool[i];
                if (definition == null || !buffs.CanLevelUp(definition)) continue;

                int stacks = buffs.StacksOf(definition);
                string level = "LV " + (stacks + 1) + "/" + definition.MaxLevel;
                _candidates.Add(new UpgradeOption
                {
                    Kind = UpgradeKind.Buff,
                    Buff = definition,
                    Title = definition.displayName,
                    Tag = definition.isDebuff ? "CURSE  " + level : level,
                    Body = definition.AutoDescription()
                           + (stacks == 0 && definition.TakesSlot ? "\nUses a buff slot" : ""),
                    Tint = definition.tint,
                });
            }
        }

        int PickWeightedIndex(List<UpgradeOption> pool)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++) total += WeightOf(pool[i]);

            float roll = Random.value * total;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= WeightOf(pool[i]);
                if (roll <= 0f) return i;
            }
            return pool.Count - 1;
        }

        float WeightOf(UpgradeOption option)
        {
            return option.Kind == UpgradeKind.Buff ? Mathf.Max(0.01f, buffWeight)
                                                   : Mathf.Max(0.01f, weaponWeight);
        }
    }
}
