using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;
using NightScene.GuestManagementUtility;
using NightScene.UI.GuestManagementUtility;
using UnityEngine;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<WorkSceneServePannel>();
        if (panel == null || !panel.isActiveAndEnabled) return "No serve panel open";
        var tray = IzakayaTray.Instance;
        Sellable food = null;
        Sellable bev = null;
        for (int i = 0; i < tray.TrayMaxNum; i++)
        {
            var s = tray.Tray[i];
            if (s == null) continue;
            if (s.Type == Sellable.SellableType.Food && food == null) food = s;
            if (s.Type == Sellable.SellableType.Beverage && bev == null) bev = s;
        }
        if (food == null || bev == null) return "Need food and beverage on tray; food=" + (food == null ? "no" : "yes") + " bev=" + (bev == null ? "no" : "yes");
        panel.Send(food);
        panel.Send(bev);
        panel.CloseExternPanel();
        return "Submitted food id=" + food.Id + " and bev id=" + bev.Id + " then closed serve panel";
    }
}
