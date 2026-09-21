using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Rolls loot tables and applies loot effects. One instance lives on GameSystems and holds the
    /// shared art and the buffs some effects grant.
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        static LootDropper _instance;

        [SerializeField] GameObject lootPrefab;

        [Header("Icons (indexed by LootType)")]
        [SerializeField] Sprite[] icons = new Sprite[5];

        [Header("Effects")]
        [Tooltip("Fraction of max health a potion restores.")]
        [Range(0f, 1f)] [SerializeField] float potionHealFraction = 0.35f;
        [SerializeField] BuffDefinition shieldBuff;
        [SerializeField] BuffDefinition frenzyBuff;

        void Awake() { _instance = this; }

        void OnDestroy() { if (_instance == this) _instance = null; }

        public static Color GlowFor(LootType type)
        {
            switch (type)
            {
                case LootType.HealthPotion: return new Color(1f, 0.30f, 0.35f, 0.55f);
                case LootType.Shield: return new Color(0.40f, 0.70f, 1f, 0.55f);
                case LootType.Blessing: return new Color(0.80f, 0.45f, 1f, 0.60f);
                case LootType.Frenzy: return new Color(1f, 0.80f, 0.25f, 0.55f);
                default: return new Color(0.50f, 1f, 0.60f, 0.50f);
            }
        }

        public static string LabelFor(LootType type)
        {
            switch (type)
            {
                case LootType.HealthPotion: return "HEALTH POTION";
                case LootType.Shield: return "SHIELD";
                case LootType.Blessing: return "BLESSING";
                case LootType.Frenzy: return "FRENZY";
                default: return "SOUL MAGNET";
            }
        }

        /// <summary>Rolls a mob's loot table at its death position.</summary>
        public static void Drop(ZombieDefinition definition, Vector3 position)
        {
            if (_instance == null || definition == null || definition.loot == null) return;

            var drops = new List<LootType>();
            for (int i = 0; i < definition.loot.Count; i++)
            {
                var entry = definition.loot[i];
                if (entry == null) continue;
                for (int c = 0; c < entry.count; c++)
                    if (UnityEngine.Random.value < entry.chance) drops.Add(entry.type);
            }

            for (int i = 0; i < drops.Count; i++)
            {
                // Fan the drops around the corpse.
                float angle = (i / (float)Mathf.Max(1, drops.Count)) * Mathf.PI * 2f + UnityEngine.Random.value;
                float radius = drops.Count > 1 ? 0.8f : 0.2f;
                var target = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                _instance.Spawn(drops[i], position, target);
            }
        }

        void Spawn(LootType type, Vector3 position, Vector3 target)
        {
            if (lootPrefab == null) return;
            var pickup = PoolManager.Spawn<LootPickup>(lootPrefab, position, Quaternion.identity);
            if (pickup == null) return;

            int index = (int)type;
            var icon = icons != null && index < icons.Length ? icons[index] : null;
            pickup.Configure(type, icon, GlowFor(type), target);
        }

        public static void Collect(LootPickup pickup, PlayerController player)
        {
            if (_instance == null || pickup == null || player == null) return;
            _instance.Apply(pickup.Type, player);
            GameEvents.RaiseLootCollected(LabelFor(pickup.Type), GlowFor(pickup.Type));
        }

        void Apply(LootType type, PlayerController player)
        {
            switch (type)
            {
                case LootType.HealthPotion:
                    player.Health.Heal(player.Health.Max * potionHealFraction);
                    break;

                case LootType.Shield:
                    // The buff raises max armor; Health turns that increase into current armor.
                    if (shieldBuff != null) player.Buffs.Apply(shieldBuff);
                    player.Health.RefillArmor();
                    break;

                case LootType.Blessing:
                    GameEvents.RaiseBonusChoice();
                    break;

                case LootType.Frenzy:
                    if (frenzyBuff != null) player.Buffs.Apply(frenzyBuff);
                    break;

                case LootType.Magnet:
                    XPOrb.AttractAll();
                    break;
            }
        }
    }
}
