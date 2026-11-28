using MemoryPack;

using GameData.Core.Collections;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 关闭上菜面板时确认上桌。操作者本地已由原版写入订单；主机逐槽裁定，接受的部分广播，拒绝的部分回复操作者退回托盘。
/// 订单上齐后主机发起评价。
/// </summary>
[MemoryPackable]
public partial class ConfirmServeMessage : MultiplayerMessage, IGuestEvent
{
    public int ActorUid { get; set; }
    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    public SellableFood Food { get; set; }
    public SellableFood Beverage { get; set; }

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        if (GameSession.IsRoomHost) Arbitrate();
        else if (ActorUid != PlayerManager.Local.Uid) GuestRegistry.Current.Post(this);
    }

    public bool Apply(GuestRecord guest, bool force)
    {
        if (OrderSeq > guest.OrderSeq) return force && guest.Track != GuestTrack.Scripted;
        if (OrderSeq < guest.OrderSeq || guest.State != GuestPhase.WaitingServe) return true;
        var order = guest.CurrentOrder;
        if (order == null) return true;
        Serve(guest, order, Food?.ToSellable(), Sellable.SellableType.Food);
        Serve(guest, order, Beverage?.ToSellable(), Sellable.SellableType.Beverage);
        if (order.IsFullfilled) ServeSlots.ClosePanel(guest.DeskCode, guest, returnLocal: true);
        return true;
    }

    private static void Serve(GuestRecord guest, GuestsManager.OrderBase order, Sellable item, Sellable.SellableType type)
    {
        if (item == null) return;
        if (type == Sellable.SellableType.Food)
        {
            order.ServedFoodInAir = null;
            order.ServFood = item;
        }
        else
        {
            order.ServedBeverageInAir = null;
            order.ServBeverage = item;
        }
        guest.SetSlot(type, null);
        if (ServeSlots.Same(guest.Claim(type), item)) guest.SetClaim(type, null);
        ServeSlots.UpdateDesk(guest.DeskCode, item, type);
        ServeSlots.UpdatePanel(guest.DeskCode, item, type, canCancel: false);
    }

    private void Arbitrate()
    {
        var guest = GuestRegistry.Current.Find(RuntimeId);
        var order = guest?.CurrentOrder;
        bool open = guest != null && order != null && guest.State == GuestPhase.WaitingServe && OrderSeq == guest.OrderSeq
            && VersusMatch.CanServe(SenderUid, guest.DeskCode);
        var food = Food?.ToSellable();
        var beverage = Beverage?.ToSellable();
        // 槽位须仍是确认者所见的物品，且尚未上桌或投掷中。
        bool acceptFood = food != null && open && order.ServFood == null && order.ServedFoodInAir == null
            && ServeSlots.Same(guest.WillServeFood, food);
        bool acceptBeverage = beverage != null && open && order.ServBeverage == null && order.ServedBeverageInAir == null
            && ServeSlots.Same(guest.WillServeBeverage, beverage);
        var rejectFood = food != null && !acceptFood ? food : null;
        var rejectBeverage = beverage != null && !acceptBeverage ? beverage : null;
        if (rejectFood != null || rejectBeverage != null)
        {
            Log.Warning($"Confirm #{RuntimeId}/{OrderSeq} from {SenderUid} rejected: food={rejectFood?.Text?.BriefName}, beverage={rejectBeverage?.Text?.BriefName}");
            ServeRejectMessage.Send(SenderUid, RuntimeId, OrderSeq, true, rejectFood, rejectBeverage);
        }
        if (!acceptFood && !acceptBeverage) return;
        if (acceptFood) Serve(guest, order, food, Sellable.SellableType.Food);
        if (acceptBeverage) Serve(guest, order, beverage, Sellable.SellableType.Beverage);
        VersusMatch.Claim(SenderUid, guest.DeskCode);
        new ConfirmServeMessage
        {
            RuntimeId = RuntimeId,
            OrderSeq = OrderSeq,
            Food = acceptFood ? Food : null,
            Beverage = acceptBeverage ? Beverage : null,
            ActorUid = SenderUid,
        }.Enqueue();
        if (!order.IsFullfilled) return;
        ServeSlots.ClosePanel(guest.DeskCode, guest, returnLocal: true);
        if (guest.Track == GuestTrack.Scripted) YuyukoGuestSync.EvaluateConfirmed();
        else GuestsManager.Instance.EvaluateOrder(guest.Controller, false, null);
    }

    /// <summary>
    /// 本端关闭面板确认上桌，原版已写入本地订单。确认的是本端放入的物品时转为待主机结果；
    /// 否则本端放入的物品仍在等主机拒绝，保留以便退回。
    /// </summary>
    public static void Local(GuestGroupController controller, Sellable food, Sellable beverage)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest == null || guest.State != GuestPhase.WaitingServe || (food == null && beverage == null)) return;
        if (food != null)
        {
            if (ServeSlots.Same(guest.ClaimFood, food))
            {
                if (GameSession.IsRoomClient) guest.ConfirmedFood = guest.ClaimFood;
                guest.ClaimFood = null;
            }
            guest.WillServeFood = null;
        }
        if (beverage != null)
        {
            if (ServeSlots.Same(guest.ClaimBeverage, beverage))
            {
                if (GameSession.IsRoomClient) guest.ConfirmedBeverage = guest.ClaimBeverage;
                guest.ClaimBeverage = null;
            }
            guest.WillServeBeverage = null;
        }
        // 房主本端上菜不经裁定，原版已写入订单，直接占桌。
        if (GameSession.IsRoomHost) VersusMatch.Claim(PlayerManager.Local.Uid, guest.DeskCode);
        new ConfirmServeMessage
        {
            RuntimeId = guest.RuntimeId,
            OrderSeq = guest.OrderSeq,
            Food = SellableFood.FromSellable(food),
            Beverage = SellableFood.FromSellable(beverage),
            ActorUid = PlayerManager.Local.Uid,
        }.Enqueue();
    }
}
