using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;
using NightScene.EventUtility;
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
            rows.Add((s == null ? "empty" : s.Type + " id=" + s.Id));
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
            return "order desk=" + code + " ServFood=" + (o.ServFood == null ? "null" : o.ServFood.Id.ToString()) + " ServBev=" + (o.ServBeverage == null ? "null" : o.ServBeverage.Id.ToString()) + " HasEvaluated=" + g.HasEvaluated;
        }
        return "reimu not seated";
    }

    public static object Execute()
    {
        var rows = new List<string>();
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
        rows.Add("countdown=" + EventManager.Instance.TotalCountDown);
        rows.Add("panel=" + panel.GetInstanceID());
        var oc = panel.OpenContext;
        rows.Add("throwMode=" + oc.Value.isThrowDeliverMode + " manual=" + oc.Value.isGuestManualControlled);
        rows.Add("BEFORE " + TrayRows() + " | " + OrderRow());
        panel.Send(food);
        rows.Add("AFTER_FOOD " + TrayRows() + " | " + OrderRow());
        panel.Send(bev);
        rows.Add("AFTER_BEV " + TrayRows() + " | " + OrderRow());
        panel.CloseExternPanel();
        rows.Add("AFTER_CLOSE " + TrayRows() + " | " + OrderRow());
        return string.Join("\n", rows);
    }
}
