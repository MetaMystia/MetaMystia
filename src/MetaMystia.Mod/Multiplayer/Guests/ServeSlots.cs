using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;
using NightScene.Tiles;

using MetaMystia.Multiplayer.Messages;
using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Guests;

/// <summary>
/// 上菜槽位的显示与本端物品归属。本端从托盘放入的物品记为 Claim，确认上桌后转为 Confirmed；
/// 顾客离开时退回 Claim，主机拒绝时退回对应物品，其他情况视为已交给顾客或转给他人。
/// </summary>
public static class ServeSlots
{
    public static bool Same(Sellable a, Sellable b) =>
        a != null && b != null && SellableFood.ContentEquals(SellableFood.FromSellable(a), SellableFood.FromSellable(b));

    public static void UpdateDesk(int deskCode, Sellable sellable, Sellable.SellableType type)
    {
        if (!TileManager.Instance.GuestTables.ContainsKey(deskCode)) return;
        var displayer = TileManager.Instance.GuestTables[deskCode].tableDisplayer;
        if (displayer == null) return;
        if (type == Sellable.SellableType.Food) displayer.SetFoodVisual(sellable?.Text?.Visual);
        else displayer.SetBeverageVisual(sellable?.Text?.Visual);
    }

    /// <summary>更新已打开面板上该桌的待上菜显示。canCancel 为 false 表示已确认上桌。</summary>
    public static void UpdatePanel(int deskCode, Sellable sellable, Sellable.SellableType type, bool canCancel)
    {
        var panel = WorkSceneServePannelPatch.instanceRef;
        if (panel == null || WorkSceneServePannelPatch.PanelDeskCode != deskCode) return;
        var food = type == Sellable.SellableType.Food;
        if (food) panel.willServeFood = canCancel ? sellable : null;
        else panel.willServeBeverage = canCancel ? sellable : null;
        var button = food ? panel.servFood : panel.servBev;
        var outline = food ? panel.servFoodOutline : panel.servBevOutline;
        if (sellable == null) panel.ResetServedVisualOnUI(button, outline);
        else panel.SetServedVisualOnUI(button, outline, sellable, canCancel);
    }

    /// <summary>
    /// 关闭该桌的上菜面板，不触发本地上菜。returnLocal 时退回本端放入的物品：联机为未确认的 Claim，单机为面板中的物品。
    /// </summary>
    public static void ClosePanel(int deskCode, GuestRecord guest, bool returnLocal)
    {
        if (returnLocal && guest != null) ReturnClaims(guest);
        var panel = WorkSceneServePannelPatch.instanceRef;
        if (deskCode < 0 || panel == null || WorkSceneServePannelPatch.PanelDeskCode != deskCode) return;
        if (returnLocal && guest == null && !GameSession.HasRoomPeers)
        {
            if (panel.willServeFood != null) IzakayaTray.Instance.Receive(panel.willServeFood);
            if (panel.willServeBeverage != null) IzakayaTray.Instance.Receive(panel.willServeBeverage);
        }
        panel.willServeFood = null;
        panel.willServeBeverage = null;
        Bypass.Grant(panel);
        panel.CloseExternPanel();
    }

    /// <summary>退回本端未确认的物品。</summary>
    public static void ReturnClaims(GuestRecord guest)
    {
        if (guest.ClaimFood != null) IzakayaTray.Instance.Receive(guest.ClaimFood);
        if (guest.ClaimBeverage != null) IzakayaTray.Instance.Receive(guest.ClaimBeverage);
        guest.ClaimFood = null;
        guest.ClaimBeverage = null;
    }

    /// <summary>
    /// 评价时：已上桌的本端物品视为交出，其余放入的退回。已确认但未上桌的仍在等主机拒绝，保留以便退回。
    /// </summary>
    public static void SettleOnEvaluate(GuestRecord guest, Sellable food, Sellable beverage)
    {
        if (Same(guest.ClaimFood, food)) guest.ClaimFood = null;
        if (Same(guest.ClaimBeverage, beverage)) guest.ClaimBeverage = null;
        ReturnClaims(guest);
        if (Same(guest.ConfirmedFood, food)) guest.ConfirmedFood = null;
        if (Same(guest.ConfirmedBeverage, beverage)) guest.ConfirmedBeverage = null;
    }

    /// <summary>他人的上菜变更以本端物品为基准（替换或取消），本端物品已转到对方托盘。</summary>
    public static void OnRemoteChange(GuestRecord guest, Sellable.SellableType type, Sellable basedOn)
    {
        if (Same(guest.Claim(type), basedOn)) guest.SetClaim(type, null);
    }

    /// <summary>
    /// 主机拒绝本端的放入或确认：仍记在本端名下的物品退回托盘，避免重复退回。
    /// 被拒的确认属于当前订单时，撤下原版已写入本地订单（含投掷中）的该物品。
    /// </summary>
    public static void OnRejected(GuestRecord guest, Sellable.SellableType type, Sellable item, bool confirm, bool currentOrder)
    {
        var food = type == Sellable.SellableType.Food;
        Sellable owned = null;
        if (Same(guest.Claim(type), item))
        {
            owned = guest.Claim(type);
            guest.SetClaim(type, null);
        }
        else if (Same(food ? guest.ConfirmedFood : guest.ConfirmedBeverage, item))
        {
            owned = food ? guest.ConfirmedFood : guest.ConfirmedBeverage;
            if (food) guest.ConfirmedFood = null;
            else guest.ConfirmedBeverage = null;
        }
        var order = guest.CurrentOrder;
        if (currentOrder && !confirm && Same(guest.Slot(type), item))
        {
            guest.SetSlot(type, null);
            UpdatePanel(guest.DeskCode, null, type, true);
            UpdateDesk(guest.DeskCode, order == null ? null : food ? order.ServFood : order.ServBeverage, type);
        }
        if (currentOrder && confirm && order != null)
        {
            if (food && (Same(order.ServFood, item) || Same(order.ServedFoodInAir, item)))
            {
                order.ServFood = null;
                order.ServedFoodInAir = null;
                UpdateDesk(guest.DeskCode, guest.Slot(type), type);
            }
            else if (!food && (Same(order.ServBeverage, item) || Same(order.ServedBeverageInAir, item)))
            {
                order.ServBeverage = null;
                order.ServedBeverageInAir = null;
                UpdateDesk(guest.DeskCode, guest.Slot(type), type);
            }
        }
        if (owned != null) IzakayaTray.Instance.Receive(owned);
    }
}
