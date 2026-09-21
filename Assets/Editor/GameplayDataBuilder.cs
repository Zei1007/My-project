using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZombieShooter.EditorTools
{
    /// <summary>
    /// Authors the gameplay numbers that live in data assets: weapon range classes and tiers, the
    /// Rifle, the ranged Spitter, the shield buff, loot tables and the wave mix. Kept as code so the
    /// tuning is reviewable in a diff and reproducible, rather than scattered across inspectors.
    /// </summary>
    public static class GameplayDataBuilder
    {
        const string WeaponDir = "Assets/Data/Weapons";
        const string MobDir = "Assets/Data/Mobs";
        const string BuffDir = "Assets/Data/Buffs";
        const string WaveDir = "Assets/Data/Waves";

        [MenuItem("Tools/Zombie Shooter/Apply Gameplay Data")]
        public static void Apply()
        {
            BuildWeapons();
            BuildShieldBuff();
            BuildSpitter();
            ApplyLootTables();
            ApplyWaves();

            AssetDatabase.SaveAssets();
            Debug.Log("[GameplayDataBuilder] gameplay data applied.");
        }

        // --- weapons -----------------------------------------------------------

        static void BuildWeapons()
        {
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/PRE_Projectile.prefab");
            var pellet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/PRE_Pellet.prefab");

            // Pistol - medium range, steady and accurate.
            var pistol = Weapon("WPN_Pistol", "pistol", "Pistol", WeaponCategory.Projectile, WeaponRangeClass.Medium,
                new Color(1f, 0.93f, 0.62f), "Steady, accurate shots.", bullet, "WPN_Pistol");
            Tiers(pistol, WeaponRangeClass.Medium, 16f, new[]
            {
                T("",                            10f, 2.4f, 1, 0, 0f,  0.6f),
                T("+50% damage",                 15f, 2.4f, 1, 0, 0f,  0.6f),
                T("Faster fire rate",            15f, 3.2f, 1, 0, 0f,  0.6f),
                T("Shots pierce 1 extra zombie", 19f, 3.2f, 1, 1, 0f,  0.8f),
                T("Double shot",                 24f, 3.6f, 2, 1, 8f,  0.8f),
            });

            // SMG - medium range, fast and light. Converted from hitscan so its rounds are visible.
            var smg = Weapon("WPN_SMG", "smg", "SMG", WeaponCategory.Projectile, WeaponRangeClass.Medium,
                new Color(0.62f, 0.86f, 1f), "A rattling spray. Low damage, very high rate of fire.", bullet, "WPN_SMG");
            Tiers(smg, WeaponRangeClass.Medium, 19f, new[]
            {
                T("",                     5f,  7f, 1, 0, 8f,  0.1f),
                T("+40% fire rate",       5f, 10f, 1, 0, 9f,  0.1f),
                T("+60% damage",          8f, 10f, 1, 0, 9f,  0.1f),
                T("Rounds pierce 1",      8f, 12f, 1, 1, 10f, 0.1f),
                T("Twin barrels",        10f, 12f, 2, 1, 12f, 0.1f),
            });

            // Shotgun - short range, devastating up close.
            var shotgun = Weapon("WPN_Shotgun", "shotgun", "Shotgun", WeaponCategory.Projectile, WeaponRangeClass.Short,
                new Color(1f, 0.64f, 0.38f), "A cone of pellets. Brutal at point blank, useless at range.", pellet, "WPN_Shotgun");
            Tiers(shotgun, WeaponRangeClass.Short, 13f, new[]
            {
                T("",                          7f, 1.1f,  5, 0, 38f, 1.5f),
                T("+2 pellets",                7f, 1.1f,  7, 0, 40f, 1.5f),
                T("+45% damage",              10f, 1.2f,  7, 0, 40f, 1.8f),
                T("Pellets pierce",           10f, 1.3f,  7, 1, 42f, 2.0f),
                T("+3 pellets, tight choke",  13f, 1.4f, 10, 1, 34f, 2.2f),
            });

            // Rifle - long range, slow and piercing. The answer to Spitters.
            var rifle = Weapon("WPN_Rifle", "rifle", "Rifle", WeaponCategory.Projectile, WeaponRangeClass.Long,
                new Color(0.80f, 0.92f, 1f), "Slow, heavy rounds that punch through a line of zombies.", bullet, "WPN_Rifle");
            Tiers(rifle, WeaponRangeClass.Long, 24f, new[]
            {
                T("",                        28f, 0.9f, 1, 2, 0f, 1.2f),
                T("+40% damage",             39f, 0.9f, 1, 2, 0f, 1.2f),
                T("Faster bolt cycling",     39f, 1.2f, 1, 3, 0f, 1.4f),
                T("Pierces 4 zombies",       48f, 1.2f, 1, 4, 0f, 1.6f),
                T("Twin shot",               58f, 1.4f, 2, 4, 5f, 1.8f),
            });

            // Machete - the melee slot. Area sweep around the player.
            var machete = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponDir + "/WPN_Machete.asset");
            if (machete != null)
            {
                machete.rangeClass = WeaponRangeClass.Short;
                EditorUtility.SetDirty(machete);
            }

            foreach (var w in new[] { pistol, smg, shotgun, rifle }) EditorUtility.SetDirty(w);
        }

        struct TierSpec
        {
            public string text;
            public float damage, fireRate, spread, knockback;
            public int count, pierce;
        }

        static TierSpec T(string text, float damage, float fireRate, int count, int pierce, float spread, float knockback)
        {
            return new TierSpec { text = text, damage = damage, fireRate = fireRate, count = count,
                                  pierce = pierce, spread = spread, knockback = knockback };
        }

        static WeaponDefinition Weapon(string asset, string id, string display, WeaponCategory category,
                                       WeaponRangeClass range, Color tint, string description,
                                       GameObject projectile, string sprite)
        {
            string path = WeaponDir + "/" + asset + ".asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (w == null)
            {
                w = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(w, path);
            }

            w.id = id;
            w.displayName = display;
            w.category = category;
            w.rangeClass = range;
            w.tint = tint;
            w.description = description;
            w.autoTarget = true;
            w.projectilePrefab = projectile;
            w.worldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PixelArtGenerator.WeaponDir + "/" + sprite + ".png");
            return w;
        }

        /// <summary>Tier reach grows 6% per level from the range-class base - noticeable, never a class jump.</summary>
        static void Tiers(WeaponDefinition w, WeaponRangeClass range, float speed, TierSpec[] specs)
        {
            float baseRange = WeaponDefinition.BaseRange(range);
            w.levels = new List<WeaponLevel>();
            for (int i = 0; i < specs.Length; i++)
            {
                var s = specs[i];
                w.levels.Add(new WeaponLevel
                {
                    upgradeText = s.text,
                    damage = s.damage,
                    fireRate = s.fireRate,
                    projectileCount = s.count,
                    pierce = s.pierce,
                    spreadAngle = s.spread,
                    range = baseRange * (1f + 0.06f * i),
                    projectileSpeed = speed,
                    areaSize = 1f,
                    knockback = s.knockback,
                });
            }
        }

        // --- buffs -------------------------------------------------------------

        /// <summary>The shield loot: a timed armor layer. Not in the level-up pool.</summary>
        static void BuildShieldBuff()
        {
            string path = BuffDir + "/BUF_Shield.asset";
            var b = AssetDatabase.LoadAssetAtPath<BuffDefinition>(path);
            if (b == null)
            {
                b = ScriptableObject.CreateInstance<BuffDefinition>();
                AssetDatabase.CreateAsset(b, path);
            }

            b.id = "loot_shield";
            b.displayName = "Arcane Shield";
            b.description = "+40 armor for 20 seconds.";
            b.durationType = BuffDuration.Timed;
            b.duration = 20f;
            b.maxStacks = 0;
            b.tint = new Color(0.4f, 0.7f, 1f);
            b.modifiers = new List<StatModifier> { new StatModifier(StatType.Armor, ModifierOp.Additive, 40f) };
            EditorUtility.SetDirty(b);
        }

        // --- mobs --------------------------------------------------------------

        /// <summary>The ranged mob: holds its distance and lobs acid. Its shots speed up each stage.</summary>
        static void BuildSpitter()
        {
            string path = MobDir + "/MOB_Spitter.asset";
            var m = AssetDatabase.LoadAssetAtPath<ZombieDefinition>(path);
            if (m == null)
            {
                m = ScriptableObject.CreateInstance<ZombieDefinition>();
                AssetDatabase.CreateAsset(m, path);
            }

            m.id = "spitter";
            m.displayName = "Spitter";
            m.tier = ZombieTier.Runner;
            m.parts = CharacterRigBuilder.LoadParts("spitter");
            m.tint = Color.white;
            m.scale = 0.95f;

            m.health = 16f;
            m.moveSpeed = 1.6f;
            m.contactDamage = 5f;
            m.attackInterval = 1f;
            m.xpValue = 2f;

            m.isRanged = true;
            m.preferredRange = 5.5f;
            m.shotInterval = 2.6f;
            m.projectileSpeed = 4.2f;              // x0.55 at wave 1 -> 2.3 u/s, x1.9 at wave 30 -> 8 u/s
            m.projectileDamageMultiplier = 1.4f;
            m.projectileTint = new Color(0.62f, 1f, 0.32f);
            m.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mobs/PRE_EnemyProjectile.prefab");

            EditorUtility.SetDirty(m);
        }

        static void ApplyLootTables()
        {
            // Regular mobs: a rare potion, so healing exists outside elite kills.
            Loot("MOB_Walker", L(LootType.HealthPotion, 0.012f));
            Loot("MOB_Runner", L(LootType.HealthPotion, 0.012f));
            Loot("MOB_Spitter", L(LootType.HealthPotion, 0.02f));
            Loot("MOB_Brute", L(LootType.HealthPotion, 0.04f), L(LootType.Shield, 0.02f));

            // Elites: usually something, occasionally a blessing.
            var elite = new[]
            {
                L(LootType.HealthPotion, 0.45f), L(LootType.Shield, 0.30f), L(LootType.Frenzy, 0.25f),
                L(LootType.Magnet, 0.20f), L(LootType.Blessing, 0.12f),
            };
            Loot("MOB_EliteCharger", elite);
            Loot("MOB_EliteBomber", elite);

            // Mini-boss: a guaranteed potion and magnet, good odds on the rest.
            Loot("MOB_MiniBoss",
                L(LootType.HealthPotion, 1f), L(LootType.Magnet, 1f), L(LootType.Shield, 0.7f),
                L(LootType.Blessing, 0.6f), L(LootType.Frenzy, 0.5f));

            // Boss: a full haul, every time.
            Loot("BOSS_Devourer",
                L(LootType.HealthPotion, 1f, 2), L(LootType.Shield, 1f), L(LootType.Blessing, 1f),
                L(LootType.Magnet, 1f), L(LootType.Frenzy, 1f));
        }

        static LootEntry L(LootType type, float chance, int count = 1)
        {
            return new LootEntry { type = type, chance = chance, count = count };
        }

        static void Loot(string asset, params LootEntry[] entries)
        {
            var m = AssetDatabase.LoadAssetAtPath<ZombieDefinition>(MobDir + "/" + asset + ".asset");
            if (m == null) { Debug.LogWarning("[GameplayDataBuilder] missing mob " + asset); return; }
            m.loot = new List<LootEntry>(entries);
            EditorUtility.SetDirty(m);
        }

        // --- waves -------------------------------------------------------------

        /// <summary>Spitters join from wave 2 and grow as a share of each wave.</summary>
        static void ApplyWaves()
        {
            var spitter = AssetDatabase.LoadAssetAtPath<ZombieDefinition>(MobDir + "/MOB_Spitter.asset");
            if (spitter == null) return;

            var plan = new Dictionary<string, int> { { "WAV_02", 2 }, { "WAV_03", 3 }, { "WAV_04", 5 }, { "WAV_05", 6 } };
            foreach (var kv in plan)
            {
                var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(WaveDir + "/" + kv.Key + ".asset");
                if (wave == null) continue;

                wave.entries.RemoveAll(e => e.zombie == spitter);
                wave.entries.Add(new WaveDefinition.Entry { zombie = spitter, count = kv.Value, startDelay = 3f });
                EditorUtility.SetDirty(wave);
            }
        }
    }
}
