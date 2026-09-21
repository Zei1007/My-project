using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieShooter.EditorTools
{
    /// <summary>
    /// Rebuilds every prefab and data asset that depends on the generated art. Kept as a tool so a
    /// change to proportions, palettes or the rig can be rolled out to the whole cast in one click
    /// instead of being hand-patched across a dozen prefabs.
    /// </summary>
    public static class GameContentBuilder
    {
        const string PrefabPlayer = "Assets/Prefabs/Player/PRE_Player.prefab";
        const string PrefabZombie = "Assets/Prefabs/Mobs/PRE_Zombie.prefab";
        const string PrefabBoss = "Assets/Prefabs/Mobs/PRE_Boss.prefab";
        const string PrefabTelegraph = "Assets/Prefabs/Mobs/PRE_Telegraph.prefab";
        const string PrefabEnemyShot = "Assets/Prefabs/Mobs/PRE_EnemyProjectile.prefab";
        const string PrefabHazard = "Assets/Prefabs/Mobs/PRE_Hazard.prefab";
        const string PrefabMuzzle = "Assets/Prefabs/Weapons/PRE_FxMuzzle.prefab";

        const int LayerPlayer = 8;
        const int LayerEnemy = 9;
        const int LayerEnemyShot = 12;

        [MenuItem("Tools/Zombie Shooter/Rebuild All Content")]
        public static void RebuildAll()
        {
            BuildFxPrefabs();
            BuildCharacterPrefabs();
            AssignDefinitionArt();
            BuildBossDefinition();
            GameplayDataBuilder.Apply();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GameContentBuilder] rebuild complete.");
        }

        static Sprite Fx(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(PixelArtGenerator.FxDir + "/" + name + ".png");
        }

        static void EnsureDirectories()
        {
            Directory.CreateDirectory("Assets/Prefabs/Player");
            Directory.CreateDirectory("Assets/Prefabs/Mobs");
            Directory.CreateDirectory("Assets/Prefabs/Weapons");
            Directory.CreateDirectory("Assets/Prefabs/Pickups");
        }

        // --- FX --------------------------------------------------------------

        public static void BuildFxPrefabs()
        {
            EnsureDirectories();

            // Muzzle flash.
            {
                var go = new GameObject("PRE_FxMuzzle");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Fx("SPR_Muzzle");
                sr.sortingOrder = SortingBands.Fx;

                var fx = go.AddComponent<SimpleFx>();
                Set(fx, "sprite", sr);
                Set(fx, "lifetime", 0.07f);
                Save(go, PrefabMuzzle);
            }

            // Telegraph: a fill disc that grows inside a static rim.
            {
                var go = new GameObject("PRE_Telegraph");

                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(go.transform, false);
                var fill = fillGo.AddComponent<SpriteRenderer>();
                fill.sprite = Fx("SPR_Disc128");
                fill.sortingOrder = SortingBands.GroundDecal;      // painted on the floor

                var ringGo = new GameObject("Ring");
                ringGo.transform.SetParent(go.transform, false);
                var ring = ringGo.AddComponent<SpriteRenderer>();
                ring.sprite = Fx("SPR_Ring");
                ring.sortingOrder = SortingBands.GroundDecal + 1;

                var telegraph = go.AddComponent<TelegraphFx>();
                Set(telegraph, "fillRenderer", fill);
                Set(telegraph, "ringRenderer", ring);
                Save(go, PrefabTelegraph);
            }

            // Boss projectile.
            {
                var go = new GameObject("PRE_EnemyProjectile");
                go.layer = LayerEnemyShot;

                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(go.transform, false);
                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = Fx("SPR_EnemyShot");
                sr.sortingOrder = SortingBands.Projectile;
                spriteGo.transform.localScale = Vector3.one * 1.25f;

                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;

                var col = go.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.28f;

                var projectile = go.AddComponent<EnemyProjectile>();
                Set(projectile, "sprite", sr);
                Save(go, PrefabEnemyShot);
            }

            // Player bullets. The old prefab referenced a sprite from the retired placeholder
            // folder, which is why shots were invisible - rebuilt here against the pixel art.
            BuildBullet("PRE_Projectile", "SPR_Bullet", 0.22f);
            BuildBullet("PRE_Pellet", "SPR_Pellet", 0.14f);

            // Death/impact burst and hitscan beam, same story.
            {
                var go = new GameObject("PRE_FxBurst");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Fx("SPR_Glow");
                sr.sortingOrder = SortingBands.Fx;
                var fx = go.AddComponent<SimpleFx>();
                Set(fx, "sprite", sr);
                Set(fx, "lifetime", 0.22f);
                Save(go, "Assets/Prefabs/Weapons/PRE_FxBurst.prefab");
            }
            {
                var go = new GameObject("PRE_FxBeam");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Fx("SPR_Square");
                sr.sortingOrder = SortingBands.Fx;
                var fx = go.AddComponent<SimpleFx>();
                Set(fx, "sprite", sr);
                Set(fx, "lifetime", 0.07f);
                Save(go, "Assets/Prefabs/Weapons/PRE_FxBeam.prefab");
            }

            // Loot: icon over a coloured halo, bobbing like the soul gems.
            {
                var go = new GameObject("PRE_Loot");
                go.layer = 11;   // Pickup

                var glowGo = new GameObject("Glow");
                glowGo.transform.SetParent(go.transform, false);
                var glow = glowGo.AddComponent<SpriteRenderer>();
                glow.sprite = Fx("SPR_Glow");
                glow.sortingOrder = SortingBands.Pickup - 1;

                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(go.transform, false);
                var icon = iconGo.AddComponent<SpriteRenderer>();
                icon.sprite = Fx("LOOT_Potion");
                icon.sortingOrder = SortingBands.Pickup;

                var visual = go.AddComponent<OrbVisual>();
                Set(visual, "gem", iconGo.transform);
                Set(visual, "gemRenderer", icon);
                Set(visual, "glowRenderer", glow);
                Set(visual, "glowScale", 1.9f);

                var loot = go.AddComponent<LootPickup>();
                Set(loot, "iconRenderer", icon);
                Set(loot, "glowRenderer", glow);
                Set(loot, "visual", visual);

                Save(go, "Assets/Prefabs/Pickups/PRE_Loot.prefab");
            }

            // XP soul gem: a bobbing crystal over a breathing halo.
            {
                var go = new GameObject("PRE_XPOrb");
                go.layer = 11;   // Pickup

                var glowGo = new GameObject("Glow");
                glowGo.transform.SetParent(go.transform, false);
                var glow = glowGo.AddComponent<SpriteRenderer>();
                glow.sprite = Fx("SPR_Glow");
                glow.color = new Color(0.35f, 0.85f, 1f, 0.45f);
                glow.sortingOrder = SortingBands.Pickup - 1;

                var gemGo = new GameObject("Gem");
                gemGo.transform.SetParent(go.transform, false);
                var gem = gemGo.AddComponent<SpriteRenderer>();
                gem.sprite = Fx("SPR_XPGem");
                gem.sortingOrder = SortingBands.Pickup;

                var visual = go.AddComponent<OrbVisual>();
                Set(visual, "gem", gemGo.transform);
                Set(visual, "gemRenderer", gem);
                Set(visual, "glowRenderer", glow);

                var orb = go.AddComponent<XPOrb>();
                Set(orb, "sprite", gem);
                Set(orb, "visual", visual);

                Save(go, "Assets/Prefabs/Pickups/PRE_XPOrb.prefab");
            }

            // Hazard pool.
            {
                var go = new GameObject("PRE_Hazard");

                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(go.transform, false);
                var fill = fillGo.AddComponent<SpriteRenderer>();
                fill.sprite = Fx("SPR_Disc128");
                fill.sortingOrder = SortingBands.GroundDecal - 1;

                var hazard = go.AddComponent<HazardZone>();
                Set(hazard, "fillRenderer", fill);
                Set(hazard, "tickInterval", 0.5f);
                Save(go, PrefabHazard);
            }
        }

        /// <summary>A pooled player bullet. White sprite, so each weapon's tint colours it.</summary>
        static void BuildBullet(string name, string spriteName, float colliderRadius)
        {
            var go = new GameObject(name);
            go.layer = 10;   // Projectile

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Fx(spriteName);
            sr.sortingOrder = SortingBands.Projectile;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = colliderRadius;

            var projectile = go.AddComponent<Projectile>();
            Set(projectile, "sprite", sr);
            Set(projectile, "maxLifetime", 3f);

            Save(go, "Assets/Prefabs/Weapons/" + name + ".prefab");
        }

        // --- characters ------------------------------------------------------

        public static void BuildCharacterPrefabs()
        {
            EnsureDirectories();

            var orb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pickups/PRE_XPOrb.prefab");
            var burst = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/PRE_FxBurst.prefab");
            var muzzle = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabMuzzle);

            // --- Player ---
            {
                var go = new GameObject("Player");
                go.layer = LayerPlayer;
                go.tag = "Player";

                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                var col = go.AddComponent<CircleCollider2D>();
                col.radius = 0.32f;
                col.offset = new Vector2(0f, -0.1f);

                var health = go.AddComponent<Health>();
                Set(health, "isPlayer", true);

                go.AddComponent<BuffController>();
                go.AddComponent<PlayerExperience>();

                var inventory = go.AddComponent<WeaponInventory>();
                SetMask(inventory, "enemyMask", 1 << LayerEnemy);

                var rig = CharacterRigBuilder.Build(go, CharacterRigBuilder.LoadParts("player"), 1f, true);
                Set(inventory, "muzzle", rig.weaponPivot);

                var controller = go.AddComponent<PlayerController>();
                Set(controller, "rig", rig);
                Set(controller, "animator", rig.GetComponent<CharacterAnimator>());
                Set(controller, "muzzleFlashPrefab", muzzle);
                Set(controller, "arenaHalfExtents", new Vector2(24f, 14f));
                Set(controller, "definition", AssetDatabase.LoadAssetAtPath<PlayerDefinition>("Assets/Data/Player/PLR_Default.asset"));

                var startWeapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/WPN_Pistol.asset");
                Set(inventory, "startingWeapon", startWeapon);

                Save(go, PrefabPlayer);
            }

            // --- Zombie ---
            {
                var go = new GameObject("Zombie");
                go.layer = LayerEnemy;
                go.tag = "Zombie";

                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                var col = go.AddComponent<CircleCollider2D>();
                col.radius = 0.32f;
                col.offset = new Vector2(0f, -0.1f);

                var health = go.AddComponent<Health>();
                Set(health, "isPlayer", false);

                var rig = CharacterRigBuilder.Build(go, CharacterRigBuilder.LoadParts("walker"), 1f, false);

                var controller = go.AddComponent<ZombieController>();
                Set(controller, "rig", rig);
                Set(controller, "animator", rig.GetComponent<CharacterAnimator>());
                Set(controller, "xpOrbPrefab", orb);
                Set(controller, "deathFxPrefab", burst);

                Save(go, PrefabZombie);
            }

            // --- Boss ---
            {
                var go = new GameObject("Boss");
                go.layer = LayerEnemy;
                go.tag = "Zombie";

                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.mass = 20f;      // adds should not shove the boss around

                var col = go.AddComponent<CircleCollider2D>();
                col.radius = 0.34f;
                col.offset = new Vector2(0f, -0.1f);

                var health = go.AddComponent<Health>();
                Set(health, "isPlayer", false);

                var rig = CharacterRigBuilder.Build(go, CharacterRigBuilder.LoadParts("boss"), 1f, false);

                var controller = go.AddComponent<ZombieController>();
                Set(controller, "rig", rig);
                Set(controller, "animator", rig.GetComponent<CharacterAnimator>());
                Set(controller, "xpOrbPrefab", orb);
                Set(controller, "deathFxPrefab", burst);
                Set(controller, "despawnDelay", 1.2f);

                var brain = go.AddComponent<BossBrain>();
                Set(brain, "telegraphPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabTelegraph));
                Set(brain, "burstFxPrefab", burst);
                Set(brain, "zombiePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabZombie));

                Save(go, PrefabBoss);
            }
        }

        // --- data ------------------------------------------------------------

        /// <summary>Points every definition at its generated part set and weapon sprite.</summary>
        public static void AssignDefinitionArt()
        {
            var map = new Dictionary<string, string>
            {
                { "Assets/Data/Mobs/MOB_Walker.asset", "walker" },
                { "Assets/Data/Mobs/MOB_Runner.asset", "runner" },
                { "Assets/Data/Mobs/MOB_Brute.asset", "brute" },
                { "Assets/Data/Mobs/MOB_EliteCharger.asset", "elite_charger" },
                { "Assets/Data/Mobs/MOB_EliteBomber.asset", "bloater" },
                { "Assets/Data/Mobs/MOB_MiniBoss.asset", "miniboss" },
            };

            foreach (var kv in map)
            {
                var def = AssetDatabase.LoadAssetAtPath<ZombieDefinition>(kv.Key);
                if (def == null) { Debug.LogWarning("missing " + kv.Key); continue; }

                def.parts = CharacterRigBuilder.LoadParts(kv.Value);
                def.tint = Color.white;      // the parts carry their own colours now
                def.sprite = null;
                EditorUtility.SetDirty(def);
            }

            var weapons = new Dictionary<string, string>
            {
                { "Assets/Data/Weapons/WPN_Pistol.asset", "WPN_Pistol" },
                { "Assets/Data/Weapons/WPN_SMG.asset", "WPN_SMG" },
                { "Assets/Data/Weapons/WPN_Shotgun.asset", "WPN_Shotgun" },
                { "Assets/Data/Weapons/WPN_Machete.asset", "WPN_Machete" },
            };

            foreach (var kv in weapons)
            {
                var def = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(kv.Key);
                if (def == null) { Debug.LogWarning("missing " + kv.Key); continue; }

                def.worldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    PixelArtGenerator.WeaponDir + "/" + kv.Value + ".png");
                EditorUtility.SetDirty(def);
            }
        }

        /// <summary>Creates (or refreshes) the three-phase boss.</summary>
        public static void BuildBossDefinition()
        {
            const string path = "Assets/Data/Mobs/BOSS_Devourer.asset";

            var boss = AssetDatabase.LoadAssetAtPath<BossDefinition>(path);
            if (boss == null)
            {
                boss = ScriptableObject.CreateInstance<BossDefinition>();
                AssetDatabase.CreateAsset(boss, path);
            }

            boss.id = "boss_devourer";
            boss.displayName = "The Devourer";
            boss.title = "THE DEVOURER";
            boss.introLine = "The horde parts. Something worse walks through.";
            boss.introDuration = 2.5f;
            boss.tier = ZombieTier.Boss;
            boss.parts = CharacterRigBuilder.LoadParts("boss");
            boss.tint = Color.white;
            boss.scale = 2.6f;

            boss.health = 160f;
            boss.healthMultiplier = 14f;
            boss.moveSpeed = 1.35f;
            boss.contactDamage = 20f;
            boss.damageMultiplier = 1.5f;
            boss.attackInterval = 1.1f;
            boss.xpValue = 120f;
            boss.ability = EliteAbility.None;      // phases drive its behaviour instead
            boss.showBossHealthBar = true;
            boss.pickupDropChance = 1f;

            boss.addDefinition = AssetDatabase.LoadAssetAtPath<ZombieDefinition>("Assets/Data/Mobs/MOB_Runner.asset");
            boss.enemyProjectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEnemyShot);
            boss.hazardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabHazard);

            boss.phases = new List<BossPhase>
            {
                // Phase 1: slow, readable. Teaches the slam and the summon.
                new BossPhase
                {
                    phaseName = "PHASE I  -  THE APPROACH",
                    healthThreshold = 1f,
                    moveSpeedMultiplier = 1f,
                    damageMultiplier = 1f,
                    damageTakenMultiplier = 1f,
                    attackInterval = 4.0f,
                    auraColor = new Color(0.55f, 0.25f, 0.85f, 0.5f),
                    attacks = new List<BossAttackEntry>
                    {
                        new BossAttackEntry { type = BossAttackType.GroundSlam, weight = 2f, radius = 3.4f, windup = 1.0f, damageMultiplier = 1.4f },
                        new BossAttackEntry { type = BossAttackType.SummonAdds, weight = 1f, count = 4, windup = 0.8f },
                    },
                },

                // Phase 2: adds ranged pressure and a dash, and starts moving properly.
                new BossPhase
                {
                    phaseName = "PHASE II  -  THE HUNGER",
                    healthThreshold = 0.66f,
                    moveSpeedMultiplier = 1.25f,
                    damageMultiplier = 1.15f,
                    damageTakenMultiplier = 1f,
                    attackInterval = 3.0f,
                    auraColor = new Color(0.85f, 0.35f, 0.5f, 0.55f),
                    attacks = new List<BossAttackEntry>
                    {
                        new BossAttackEntry { type = BossAttackType.GroundSlam, weight = 1.5f, radius = 3.8f, windup = 0.85f, damageMultiplier = 1.4f },
                        new BossAttackEntry { type = BossAttackType.RadialBurst, weight = 1.5f, count = 14, windup = 0.7f, radius = 3f, damageMultiplier = 0.8f },
                        new BossAttackEntry { type = BossAttackType.ChargeDash, weight = 1f, radius = 4f, windup = 0.75f, damageMultiplier = 1.6f },
                        new BossAttackEntry { type = BossAttackType.SummonAdds, weight = 0.8f, count = 5, windup = 0.7f },
                    },
                },

                // Phase 3: enrage. Faster, hits harder, and denies ground with hazards.
                new BossPhase
                {
                    phaseName = "PHASE III  -  ENRAGED",
                    healthThreshold = 0.33f,
                    moveSpeedMultiplier = 1.55f,
                    damageMultiplier = 1.35f,
                    damageTakenMultiplier = 1.1f,   // slightly squishier, to keep the finish moving
                    attackInterval = 2.1f,
                    auraColor = new Color(1f, 0.35f, 0.2f, 0.6f),
                    attacks = new List<BossAttackEntry>
                    {
                        new BossAttackEntry { type = BossAttackType.RadialBurst, weight = 2f, count = 22, windup = 0.55f, radius = 3f, damageMultiplier = 0.85f },
                        new BossAttackEntry { type = BossAttackType.GroundSlam, weight = 1.5f, radius = 4.2f, windup = 0.7f, damageMultiplier = 1.5f },
                        new BossAttackEntry { type = BossAttackType.HazardField, weight = 1.5f, count = 4, radius = 2.2f, windup = 0.8f, damageMultiplier = 1.2f },
                        new BossAttackEntry { type = BossAttackType.ChargeDash, weight = 1.2f, radius = 5f, windup = 0.6f, damageMultiplier = 1.7f },
                    },
                },
            };

            EditorUtility.SetDirty(boss);
        }

        // --- helpers ---------------------------------------------------------

        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning("[GameContentBuilder] no field '" + field + "' on " + target.GetType().Name);
                return;
            }

            if (value is Object) prop.objectReferenceValue = (Object)value;
            else if (value is bool) prop.boolValue = (bool)value;
            else if (value is int) prop.intValue = (int)value;
            else if (value is float) prop.floatValue = (float)value;
            else if (value is Color) prop.colorValue = (Color)value;
            else if (value is Vector2) prop.vector2Value = (Vector2)value;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetMask(Object target, string field, int mask)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop != null)
            {
                prop.intValue = mask;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void Save(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
