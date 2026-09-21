using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieShooter;
using ZombieShooter.EditorTools;

/// <summary>Agent entry points, run via `unity command run_script`. Kept outside Assets/ so edits trigger no reimport.</summary>
public static class Build
{
    /// <summary>Regenerates art, rebuilds prefabs + gameplay data, repacks atlases, and reports.</summary>
    public static string Content()
    {
        PixelArtGenerator.GenerateAll();
        GameContentBuilder.RebuildAll();
        SpriteAtlasBuilder.BuildAll();

        var sb = new StringBuilder();
        foreach (var n in new[] { "SPR_Bullet", "SPR_Pellet", "SPR_EnemyShot", "LOOT_Potion", "LOOT_Shield", "LOOT_Blessing", "LOOT_Frenzy", "LOOT_Magnet" })
            sb.Append(n).Append('=').Append(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/FX/" + n + ".png") != null).Append(' ');
        sb.AppendLine();

        var proj = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/PRE_Projectile.prefab");
        var sr = proj.GetComponent<SpriteRenderer>();
        sb.AppendLine("PRE_Projectile sprite = " + (sr.sprite != null ? sr.sprite.name : "NULL"));

        foreach (var w in new[] { "WPN_Pistol", "WPN_SMG", "WPN_Shotgun", "WPN_Rifle", "WPN_Machete" })
        {
            var d = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/" + w + ".asset");
            sb.AppendLine(w + ": " + d.category + " | " + d.RangeLabel + " | range=" + d.GetLevel(1).range.ToString("0.0")
                + " | proj=" + (d.projectilePrefab != null ? d.projectilePrefab.name : "-") + " | sprite=" + (d.worldSprite != null));
        }

        var sp = AssetDatabase.LoadAssetAtPath<ZombieDefinition>("Assets/Data/Mobs/MOB_Spitter.asset");
        sb.AppendLine("Spitter ranged=" + sp.isRanged + " proj=" + (sp.projectilePrefab != null) + " head=" + (sp.parts.head != null));
        var boss = AssetDatabase.LoadAssetAtPath<ZombieDefinition>("Assets/Data/Mobs/BOSS_Devourer.asset");
        sb.AppendLine("Boss loot lines=" + boss.loot.Count);
        return sb.ToString();
    }

    /// <summary>Wires the Arena scene: loot dropper, weapon/buff pools, spitters, fresh player.</summary>
    public static string Scene()
    {
        var sb = new StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var systems = GameObject.Find("GameSystems");

        // Loot dropper.
        var dropper = systems.GetComponent<LootDropper>();
        if (dropper == null) dropper = systems.AddComponent<LootDropper>();
        var so = new SerializedObject(dropper);
        so.FindProperty("lootPrefab").objectReferenceValue = Load<GameObject>("Assets/Prefabs/Pickups/PRE_Loot.prefab");
        var icons = so.FindProperty("icons");
        string[] iconNames = { "LOOT_Potion", "LOOT_Shield", "LOOT_Blessing", "LOOT_Frenzy", "LOOT_Magnet" };
        icons.arraySize = iconNames.Length;
        for (int i = 0; i < iconNames.Length; i++)
            icons.GetArrayElementAtIndex(i).objectReferenceValue = Load<Sprite>("Assets/Art/FX/" + iconNames[i] + ".png");
        so.FindProperty("shieldBuff").objectReferenceValue = Load<BuffDefinition>("Assets/Data/Buffs/BUF_Shield.asset");
        so.FindProperty("frenzyBuff").objectReferenceValue = Load<BuffDefinition>("Assets/Data/Buffs/BUF_Frenzy.asset");
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("LootDropper wired");

        // Upgrade pool: every weapon (the equipped ones are skipped at roll time), scarcer weapon cards.
        var upgrades = systems.GetComponent<UpgradeService>();
        so = new SerializedObject(upgrades);
        SetList(so.FindProperty("weaponPool"), new Object[]
        {
            Load<WeaponDefinition>("Assets/Data/Weapons/WPN_Pistol.asset"),
            Load<WeaponDefinition>("Assets/Data/Weapons/WPN_SMG.asset"),
            Load<WeaponDefinition>("Assets/Data/Weapons/WPN_Shotgun.asset"),
            Load<WeaponDefinition>("Assets/Data/Weapons/WPN_Rifle.asset"),
            Load<WeaponDefinition>("Assets/Data/Weapons/WPN_Machete.asset"),
        });
        so.FindProperty("weaponWeight").floatValue = 0.35f;
        so.FindProperty("maxWeaponCardsPerRoll").intValue = 1;
        so.FindProperty("weaponCardChance").floatValue = 0.55f;
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("UpgradeService: 5 weapons, weight 0.35, max 1 weapon card, 55% roll chance");

        // Spitters in the procedural continuation.
        var waves = systems.GetComponent<WaveManager>();
        so = new SerializedObject(waves);
        SetList(so.FindProperty("proceduralPool"), new Object[]
        {
            Load<ZombieDefinition>("Assets/Data/Mobs/MOB_Walker.asset"),
            Load<ZombieDefinition>("Assets/Data/Mobs/MOB_Runner.asset"),
            Load<ZombieDefinition>("Assets/Data/Mobs/MOB_Spitter.asset"),
            Load<ZombieDefinition>("Assets/Data/Mobs/MOB_Brute.asset"),
        });
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("WaveManager: spitter in procedural pool");

        // Fresh player instance - the inventory component changed shape.
        var old = Object.FindAnyObjectByType<PlayerController>();
        Vector2 arena = new Vector2(16f, 10f);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Prefabs/Player/PRE_Player.prefab"));
        player.name = "Player";
        player.transform.position = Vector3.zero;
        var controller = player.GetComponent<PlayerController>();
        so = new SerializedObject(controller);
        so.FindProperty("arenaHalfExtents").vector2Value = arena;
        so.ApplyModifiedPropertiesWithoutUndo();

        so = new SerializedObject(systems.GetComponent<GameManager>());
        so.FindProperty("player").objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("Player re-instantiated");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        sb.AppendLine("scene saved");
        return sb.ToString();
    }

    static T Load<T>(string path) where T : Object { return AssetDatabase.LoadAssetAtPath<T>(path); }

    static void SetList(SerializedProperty list, Object[] items)
    {
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
