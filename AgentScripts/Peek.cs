using System.Linq;
using System.Reflection;
using UnityEngine;
using ZombieShooter;

public static class Peek
{
    public static string Choices()
    {
        var hud = Object.FindAnyObjectByType<GameHUD>();
        var f = BindingFlags.NonPublic | BindingFlags.Instance;
        var opts = (System.Collections.Generic.List<UpgradeOption>)typeof(GameHUD).GetField("_currentOptions", f).GetValue(hud);
        var title = (UnityEngine.UI.Text)typeof(GameHUD).GetField("_levelUpTitle", f).GetValue(hud);
        var queue = (System.Collections.Generic.Queue<bool>)typeof(GameHUD).GetField("_pendingChoices", f).GetValue(hud);
        return "state=" + GameManager.Instance.State + " title=" + title.text + " queue=[" + string.Join(",", queue) + "] cards="
             + string.Join(" | ", opts.Select(o => o.Kind + ":" + o.Title + " (" + o.Tag + ")"));
    }
}
