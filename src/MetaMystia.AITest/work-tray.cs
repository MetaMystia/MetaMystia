using System.Collections.Generic;

using GameData.RunTime.NightSceneUtility;
using NightScene.EventUtility;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        var tray = IzakayaTray.Instance;
        rows.Add("countdown=" + EventManager.Instance.TotalCountDown + " trayMax=" + tray.TrayMaxNum + " trayEmpty=" + tray.IsTrayEmpty + " trayFull=" + tray.IsTrayFull);
        for (int i = 0; i < tray.TrayMaxNum; i++)
        {
            var s = tray.Tray[i];
            rows.Add("tray" + i + "=" + (s == null ? "empty" : s.Type + " id=" + s.Id + " " + s.Text.Name));
        }
        return string.Join("\n", rows);
    }
}
