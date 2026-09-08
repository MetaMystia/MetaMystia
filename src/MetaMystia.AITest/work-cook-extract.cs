using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;
using Il2CppInterop.Runtime;
using NightScene.CookingUtility;
using UnityEngine;

public static class Payload
{
    public static object Execute()
    {
        CookController cook = null;
        foreach (var c in Object.FindObjectsOfType<CookController>(true))
            if (c.GridPosition == new Vector3Int(1, 3, 0)) { cook = c; break; }
        if (cook == null || cook.Phase != CookController.CookPhase.Finished)
            return "Cooker not finished; phase=" + (cook == null ? "none" : cook.Phase.ToString());
        var tray = IzakayaTray.Instance;
        if (tray.IsTrayFull) return "Tray full; cannot extract";
        var result = cook.Result;
        System.Action<Sellable> receive = value => tray.Receive(value);
        cook.Extract(DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Sellable>>(receive));
        cook.Cooker.OnPlayerFinishExtract(cook);
        return "Extracted food id=" + result.Id + " " + result.Text.Name + " trayEmpty=" + tray.IsTrayEmpty + " trayFull=" + tray.IsTrayFull;
    }
}
