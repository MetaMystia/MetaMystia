using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;
using NightScene.GuestManagementUtility;
using NightScene.UI.GuestManagementUtility;
using UnityEngine;

public static class Payload
{
    static string TrayRows()
    {
        var t = IzakayaTray.Instance;
        var rows = new List<string>();
        for (int i = 0; i < t.TrayMaxNum; i++)
        {
            var s = t.Tray[i];
            rows.Add(s == null ? "empty" : s.Type + ":" + s.Id);
        }
        return "tray[" + string.Join(",", rows) + "]";
    }

    static string OrderRow()
    {
        foreach (int code in new[] { 0, 1, 2, 3, 4 })
        {
            var g = GuestsManager.Instance.GetInDeskGuest(code);
            if (g == null || !(g is SpecialGuestsController sp) || sp.SpecialGuest.Id != 7) continue;
            var o = g.PeekOrders();
            return "desk=" + code + " SF=" + (o.ServFood == null ? "n" : o.ServFood.Id.ToString()) + " SB=" + (o.ServBeverage == null ? "n" : o.ServBeverage.Id.ToString()) + " ev=" + g.HasEvaluated;
        }
        return "reimu not seated";
    }

    public static object Execute()
    {
        var rows = new List<string>();
        WorkSceneServePannel target = null;
        foreach (var p in Object.FindObjectsOfType<WorkSceneServePannel>(true))
            if (p.isActiveAndEnabled) { target = p; rows.Add("panel id=" + p.GetInstanceID() + " active"); }
        if (target == null) return "No active serve panel; " + (rows.Count == 0 ? "" : string.Join("|", rows));
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
        rows.Add("BEFORE " + TrayRows() + " | " + OrderRow());
        target.Send(food);
        rows.Add("AFTER_FOOD " + TrayRows() + " | " + OrderRow());
        target.Send(bev);
        rows.Add("AFTER_BEV " + TrayRows() + " | " + OrderRow());
        target.CloseExternPanel();
        rows.Add("AFTER_CLOSE " + TrayRows() + " | " + OrderRow());
        return string.Join("\n", rows);
    }
}
