using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using ZombieShooter;

/// <summary>Play-mode checks for holstered upgrades, live buff timers and gun-follows-shot aiming.</summary>
public static class AimTests
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    static T Load<T>(string path) where T : Object { return AssetDatabase.LoadAssetAtPath<T>(path); }
    static PlayerController Player { get { return GameManager.Instance.Player; } }

    static void Prepare()
    {
        var p = Player;
        var rb = p.GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;   // pinned, but the controller keeps running so aim updates
        p.transform.position = Vector3.zero;
        p.Stats.AddModifier(new StatModifier(StatType.DamageTaken, ModifierOp.Multiplicative, 0f, "test"));
        PoolManager.DespawnAll();
        Time.timeScale = 1f;
    }

    public static string Holstered()
    {
        Prepare();
        var inv = Player.Inventory;
        var pistol = Load<WeaponDefinition>("Assets/Data/Weapons/WPN_Pistol.asset");
        var smg = Load<WeaponDefinition>("Assets/Data/Weapons/WPN_SMG.asset");
        var sb = new StringBuilder();

        inv.Equip(pistol);
        inv.Equip(smg);   // pistol is now holstered
        int before = inv.StoredLevel(pistol);
        inv.Upgrade(pistol);
        sb.AppendLine("pistol holstered: LV" + before + " -> LV" + inv.StoredLevel(pistol) + " while SMG equipped (" + inv.Ranged.Definition.displayName + ")");

        inv.Equip(pistol);
        sb.AppendLine("re-equip pistol: equipped at LV" + inv.Ranged.Level);

        // How often a holstered tier card shows up (weapon cards forced on to measure it).
        inv.Equip(smg);
        var service = Object.FindAnyObjectByType<UpgradeService>();
        var so = new SerializedObject(service);
        float oldChance = so.FindProperty("weaponCardChance").floatValue;
        typeof(UpgradeService).GetField("weaponCardChance", Private).SetValue(service, 1f);
        int holsteredCards = 0;
        string example = "";
        for (int i = 0; i < 300; i++)
        {
            foreach (var o in service.Roll(Player))
            {
                if (o.Kind == UpgradeKind.WeaponLevel && o.Tag.StartsWith("HOLSTERED"))
                {
                    holsteredCards++;
                    if (example == "") example = o.Tag + " | " + o.Title + " | " + o.Body.Replace("\n", " / ");
                }
            }
        }
        typeof(UpgradeService).GetField("weaponCardChance", Private).SetValue(service, oldChance);
        sb.AppendLine("holstered tier cards in 300 rolls: " + holsteredCards + "  e.g. [" + example + "]");
        return sb.ToString();
    }

    public static async Task<string> BuffTimer()
    {
        Prepare();
        var frenzy = Load<BuffDefinition>("Assets/Data/Buffs/BUF_Frenzy.asset");
        Player.Buffs.Apply(frenzy);

        var hud = Object.FindAnyObjectByType<GameHUD>();
        var text = (UnityEngine.UI.Text)typeof(GameHUD).GetField("_buffText", Private).GetValue(hud);

        var sb = new StringBuilder();
        for (int i = 0; i < 4; i++)
        {
            string line = text.text.Split('\n').FirstOrDefault(l => l.StartsWith(frenzy.displayName)) ?? "(missing)";
            sb.AppendLine("t+" + (i * 1.2f).ToString("0.0") + "s: " + line);
            await Task.Delay(1200);
        }
        return sb.ToString();
    }

    public static async Task<string> GunDirection()
    {
        Prepare();
        var sb = new StringBuilder();
        Player.Inventory.Equip(Load<WeaponDefinition>("Assets/Data/Weapons/WPN_Pistol.asset"));
        var brute = Load<ZombieDefinition>("Assets/Data/Mobs/MOB_Brute.asset");
        var prefab = Load<GameObject>("Assets/Prefabs/Mobs/PRE_Zombie.prefab");

        // One target at a time in each quadrant; the gun should line up with the shot every time.
        foreach (var spot in new[] { new Vector3(3f, 3f, 0f), new Vector3(-4f, 1f, 0f), new Vector3(-2f, -3.5f, 0f), new Vector3(4f, -2f, 0f) })
        {
            PoolManager.DespawnAll();
            var z = PoolManager.Spawn<ZombieController>(prefab, spot, Quaternion.identity);
            z.Initialize(brute, 30f, null);
            z.Stats.AddModifier(new StatModifier(StatType.MoveSpeed, ModifierOp.Multiplicative, 0.0001f, "test"));

            await Task.Delay(1100);

            var rig = Player.Rig;
            // The weapon sprite sits along the pivot's local +X, so its world offset is the true
            // on-screen barrel direction, mirroring included.
            Vector2 barrel = ((Vector2)(rig.weaponRenderer.transform.position - rig.weaponPivot.position)).normalized;
            Vector2 toTarget = ((Vector2)(z.transform.position - rig.weaponPivot.position)).normalized;

            var bullet = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).FirstOrDefault(b => b.gameObject.activeInHierarchy);
            string bulletInfo = bullet != null
                ? Vector2.Angle(bullet.transform.right, barrel).ToString("0.0") + " deg"
                : "no bullet in flight";

            sb.AppendLine("target at " + (Vector2)spot + ": barrel vs target " + Vector2.Angle(barrel, toTarget).ToString("0.0")
                + " deg, barrel vs bullet " + bulletInfo + ", facing " + rig.Facing);
        }
        return sb.ToString();
    }
}
