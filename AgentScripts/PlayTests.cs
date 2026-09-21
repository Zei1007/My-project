using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using ZombieShooter;

/// <summary>Play-mode verification for the weapon, loot, health and ranged-enemy changes.</summary>
public static class PlayTests
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    static T Load<T>(string path) where T : Object { return AssetDatabase.LoadAssetAtPath<T>(path); }

    static PlayerController Player { get { return GameManager.Instance.Player; } }

    /// <summary>Pins the player so stale unfocused input cannot move them, and clears the field.</summary>
    static void Prepare()
    {
        var p = Player;
        p.enabled = false;
        var rb = p.GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        p.transform.position = Vector3.zero;
        PoolManager.DespawnAll();
        Time.timeScale = 1f;
    }

    public static string Weapons()
    {
        Prepare();
        var inv = Player.Inventory;
        var pistol = Load<WeaponDefinition>("Assets/Data/Weapons/WPN_Pistol.asset");
        var smg = Load<WeaponDefinition>("Assets/Data/Weapons/WPN_SMG.asset");
        var machete = Load<WeaponDefinition>("Assets/Data/Weapons/WPN_Machete.asset");
        var sb = new StringBuilder();

        sb.AppendLine("start: " + Describe(inv));
        inv.Upgrade(pistol);
        inv.Upgrade(pistol);
        sb.AppendLine("pistol upgraded twice: " + Describe(inv));

        inv.Equip(smg);
        sb.AppendLine("equip SMG (swap): " + Describe(inv) + "  | pistol stored LV" + inv.StoredLevel(pistol));

        inv.Upgrade(smg);
        inv.Equip(machete);
        sb.AppendLine("upgrade SMG, equip Machete: " + Describe(inv));

        inv.Equip(pistol);
        sb.AppendLine("swap back to Pistol: " + Describe(inv) + "  | SMG stored LV" + inv.StoredLevel(smg));
        return sb.ToString();
    }

    static string Describe(WeaponInventory inv)
    {
        return string.Join(", ", inv.Weapons.Select(w => w.Definition.displayName + " LV" + w.Level
                   + (w.Definition.IsMelee ? " [melee]" : " [ranged]")))
               + "  (" + inv.Weapons.Count + " equipped)";
    }

    public static string Health()
    {
        Prepare();
        var p = Player;
        var hp = Load<BuffDefinition>("Assets/Data/Buffs/BUF_MaxHealth.asset");
        var sb = new StringBuilder();
        sb.AppendLine("before: " + p.Health.Current.ToString("0") + "/" + p.Health.Max.ToString("0"));
        p.Buffs.Apply(hp);
        sb.AppendLine("after +25 max at full HP: " + p.Health.Current.ToString("0") + "/" + p.Health.Max.ToString("0") + "  (expect 125/125)");
        p.Health.TakeDamage(40f);
        p.Buffs.Apply(hp);
        sb.AppendLine("took 40, then +25 max: " + p.Health.Current.ToString("0") + "/" + p.Health.Max.ToString("0") + "  (expect 110/150)");
        return sb.ToString();
    }

    public static string Rolls()
    {
        Prepare();
        var service = Object.FindAnyObjectByType<UpgradeService>();
        int rolls = 400, weaponCards = 0, rollsWithWeapon = 0, maxInRoll = 0, total = 0, blessingWeapon = 0;
        for (int i = 0; i < rolls; i++)
        {
            var options = service.Roll(Player);
            int w = options.Count(o => o.Kind != UpgradeKind.Buff);
            total += options.Count;
            weaponCards += w;
            if (w > 0) rollsWithWeapon++;
            maxInRoll = Mathf.Max(maxInRoll, w);

            blessingWeapon += service.Roll(Player, true).Count(o => o.Kind != UpgradeKind.Buff);
        }
        return "over " + rolls + " level-ups: weapon cards " + weaponCards + "/" + total
             + " (" + (100f * weaponCards / total).ToString("0") + "% of cards), rolls with a weapon "
             + (100f * rollsWithWeapon / rolls).ToString("0") + "%, most in one roll " + maxInRoll
             + "\nblessing rolls with a weapon card: " + blessingWeapon + " (expect 0)";
    }

    public static async Task<string> Projectiles()
    {
        Prepare();
        var sb = new StringBuilder();
        var zombiePrefab = Load<GameObject>("Assets/Prefabs/Mobs/PRE_Zombie.prefab");
        var brute = Load<ZombieDefinition>("Assets/Data/Mobs/MOB_Brute.asset");

        foreach (var weapon in new[] { "WPN_Shotgun", "WPN_Pistol", "WPN_Rifle" })
        {
            PoolManager.DespawnAll();
            var def = Load<WeaponDefinition>("Assets/Data/Weapons/" + weapon + ".asset");
            Player.Inventory.Equip(def);

            // A tanky, frozen target at 6 units: inside Medium and Long reach, outside Short.
            var z = PoolManager.Spawn<ZombieController>(zombiePrefab, new Vector3(6f, 0f, 0f), Quaternion.identity);
            z.Initialize(brute, 20f, null);
            z.Stats.AddModifier(new StatModifier(StatType.MoveSpeed, ModifierOp.Multiplicative, 0.0001f, "test"));

            await Task.Delay(900);

            var bullets = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                .Where(b => b.gameObject.activeInHierarchy).ToList();
            string sprite = "none";
            foreach (var b in bullets)
            {
                var sr = b.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null && sr.enabled) sprite = sr.sprite.name;
            }
            sb.AppendLine(def.displayName + " (" + def.RangeLabel + ", reach " + def.GetLevel(1).range.ToString("0.0")
                + ") vs target at 6.0: bullets in flight=" + bullets.Count + ", sprite=" + sprite
                + ", target hp " + z.Health.Current.ToString("0") + "/" + z.Health.Max.ToString("0"));
        }
        return sb.ToString();
    }

    public static async Task<string> Spitter()
    {
        Prepare();
        var sb = new StringBuilder();
        var waves = GameManager.Instance.Waves;
        sb.AppendLine("projectile speed x: w1=" + waves.ProjectileSpeedScalarFor(1).ToString("0.00")
            + " w5=" + waves.ProjectileSpeedScalarFor(5).ToString("0.00")
            + " w10=" + waves.ProjectileSpeedScalarFor(10).ToString("0.00")
            + " w20=" + waves.ProjectileSpeedScalarFor(20).ToString("0.00")
            + " w30=" + waves.ProjectileSpeedScalarFor(30).ToString("0.00"));

        Player.Stats.AddModifier(new StatModifier(StatType.DamageTaken, ModifierOp.Multiplicative, 0f, "test"));
        Player.Inventory.enabled = false;   // hold fire so the spitter lives long enough to shoot

        var def = Load<ZombieDefinition>("Assets/Data/Mobs/MOB_Spitter.asset");
        var z = PoolManager.Spawn<ZombieController>(Load<GameObject>("Assets/Prefabs/Mobs/PRE_Zombie.prefab"),
            new Vector3(9f, 0f, 0f), Quaternion.identity);
        z.Initialize(def, 1f, Player.transform);

        float minDist = 99f, maxDist = 0f, shotSpeed = 0f;
        int shotsSeen = 0;
        var speedField = typeof(EnemyProjectile).GetField("_speed", Private);
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(150);
            float d = Vector2.Distance(z.transform.position, Player.transform.position);
            if (i > 12) { minDist = Mathf.Min(minDist, d); maxDist = Mathf.Max(maxDist, d); }

            var shots = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)
                .Where(s => s.gameObject.activeInHierarchy).ToList();
            if (shots.Count > 0)
            {
                shotsSeen = Mathf.Max(shotsSeen, shots.Count);
                shotSpeed = (float)speedField.GetValue(shots[0]);
            }
        }
        Player.Inventory.enabled = true;
        sb.AppendLine("spitter held " + minDist.ToString("0.0") + "-" + maxDist.ToString("0.0") + " units away (preferred "
            + def.preferredRange + "), max shots in flight " + shotsSeen + ", shot speed " + shotSpeed.ToString("0.00")
            + " u/s at wave " + waves.CurrentWave);
        return sb.ToString();
    }

    public static async Task<string> Loot()
    {
        Prepare();
        var sb = new StringBuilder();
        var p = Player;
        var zombiePrefab = Load<GameObject>("Assets/Prefabs/Mobs/PRE_Zombie.prefab");

        var elite = Load<ZombieDefinition>("Assets/Data/Mobs/MOB_EliteCharger.asset");
        for (int i = 0; i < 10; i++)
        {
            var z = PoolManager.Spawn<ZombieController>(zombiePrefab, new Vector3(4f + i * 0.3f, 3f, 0f), Quaternion.identity);
            z.Initialize(elite, 1f, null);
            z.Health.Kill();
        }
        await Task.Delay(400);
        var loot = Object.FindObjectsByType<LootPickup>(FindObjectsSortMode.None).Where(l => l.gameObject.activeInHierarchy).ToList();
        sb.AppendLine("10 elite kills -> " + loot.Count + " drops: "
            + string.Join(", ", loot.GroupBy(l => l.Type).Select(g => g.Key + " x" + g.Count())));

        var dropper = Object.FindAnyObjectByType<LootDropper>();
        var apply = typeof(LootDropper).GetMethod("Apply", Private);

        p.Health.TakeDamage(60f);
        float before = p.Health.Current;
        apply.Invoke(dropper, new object[] { LootType.HealthPotion, p });
        sb.AppendLine("potion: " + before.ToString("0") + " -> " + p.Health.Current.ToString("0") + " / " + p.Health.Max.ToString("0"));

        apply.Invoke(dropper, new object[] { LootType.Shield, p });
        sb.AppendLine("shield: armor " + p.Health.Armor.ToString("0") + "/" + p.Health.MaxArmor.ToString("0"));
        return sb.ToString();
    }

    public static async Task<string> BossLoot()
    {
        Prepare();
        var boss = Load<BossDefinition>("Assets/Data/Mobs/BOSS_Devourer.asset");
        var z = PoolManager.Spawn<ZombieController>(Load<GameObject>("Assets/Prefabs/Mobs/PRE_Boss.prefab"),
            new Vector3(0f, 3f, 0f), Quaternion.identity);
        z.Initialize(boss, 1f, null);
        z.Health.Kill();
        await Task.Delay(400);
        var loot = Object.FindObjectsByType<LootPickup>(FindObjectsSortMode.None).Where(l => l.gameObject.activeInHierarchy).ToList();
        return "boss kill -> " + loot.Count + " drops: " + string.Join(", ", loot.GroupBy(l => l.Type).Select(g => g.Key + " x" + g.Count()));
    }

    public static string Blessing()
    {
        GameEvents.RaiseBonusChoice();
        var hud = Object.FindAnyObjectByType<GameHUD>();
        var opts = (System.Collections.Generic.List<UpgradeOption>)typeof(GameHUD).GetField("_currentOptions", Private).GetValue(hud);
        var title = (UnityEngine.UI.Text)typeof(GameHUD).GetField("_levelUpTitle", Private).GetValue(hud);
        return "state=" + GameManager.Instance.State + " title=" + title.text
             + " cards=" + string.Join(" | ", opts.Select(o => o.Kind + ":" + o.Title));
    }

    public static string ChooseFirst()
    {
        var hud = Object.FindAnyObjectByType<GameHUD>();
        typeof(GameHUD).GetMethod("Choose", Private).Invoke(hud, new object[] { 0 });
        return "state=" + GameManager.Instance.State;
    }
}
