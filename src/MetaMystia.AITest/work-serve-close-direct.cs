using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;
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
        if (food == null || bev == null) return "Need food and beverage; food=" + (food == null ? "no" : "yes") + " bev=" + (bev == null ? "no" : "yes");
        panel.Send(food);
        panel.Send(bev);
        panel.OnPanelClose();
        return "Sent food id=" + food.Id + " bev id=" + bev.Id + " then called OnPanelClose directly";
    }
}
