using MemoryPack;

using GameData.Core.Collections;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 上菜面板放入或取消。操作者本地立即生效并上报；主机立即裁定，不排队：接受则广播，拒绝则只回复操作者。
/// 冲突按槽位当前内容与请求所依据的内容比较。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class ServeSellableMessage : MultiplayerMessage, IGuestEvent
{
    public int ActorUid { get; set; }
    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    public SellableFood Requested { get; set; }
    public SellableFood BasedOn { get; set; }
    public Sellable.SellableType SellableType { get; set; }

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
        var requested = Requested?.ToSellable();
        ServeSlots.OnRemoteChange(guest, SellableType, BasedOn?.ToSellable());
        guest.SetSlot(SellableType, requested);
        ServeSlots.UpdatePanel(guest.DeskCode, requested, SellableType, canCancel: true);
        ServeSlots.UpdateDesk(guest.DeskCode, requested, SellableType);
        return true;
    }

    private void Arbitrate()
    {
        var guest = GuestRegistry.Current.Find(RuntimeId);
        var requested = Requested?.ToSellable();
        var basedOn = BasedOn?.ToSellable();
        var order = guest?.CurrentOrder;
        var food = SellableType == Sellable.SellableType.Food;
        var slot = guest?.Slot(SellableType);
        // 已确认上桌、正在投掷，或请求所依据的内容与当前槽位不同（被他人抢先），均拒绝。
        bool accepted = guest != null && order != null && guest.State == GuestPhase.WaitingServe && OrderSeq == guest.OrderSeq
            && (food ? order.ServFood ?? order.ServedFoodInAir : order.ServBeverage ?? order.ServedBeverageInAir) == null
            && (slot == null || ServeSlots.Same(slot, requested) || ServeSlots.Same(slot, basedOn));
        if (!accepted)
        {
            Log.Warning($"Serve #{RuntimeId}/{OrderSeq} {requested?.Text?.BriefName ?? "cancel"} from {SenderUid} rejected, slot has {slot?.Text?.BriefName}");
            if (requested != null)
                ServeRejectMessage.Send(SenderUid, RuntimeId, OrderSeq, false, food ? requested : null, food ? null : requested);
            return;
        }
        ServeSlots.OnRemoteChange(guest, SellableType, basedOn);
        guest.SetSlot(SellableType, requested);
        ServeSlots.UpdatePanel(guest.DeskCode, requested, SellableType, canCancel: true);
        ServeSlots.UpdateDesk(guest.DeskCode, requested, SellableType);
        new ServeSellableMessage
        {
            RuntimeId = RuntimeId,
            OrderSeq = OrderSeq,
            Requested = Requested,
            BasedOn = BasedOn,
            SellableType = SellableType,
            ActorUid = SenderUid,
        }.Enqueue();
    }

    /// <summary>本端在面板放入（sellable 非空）或取消，物品已由原版从托盘取出或放回。</summary>
    public static void Local(GuestGroupController controller, Sellable sellable, Sellable.SellableType type)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest == null || guest.State != GuestPhase.WaitingServe) return;
        var basedOn = guest.Slot(type);
        guest.SetSlot(type, sellable);
        guest.SetClaim(type, sellable);
        new ServeSellableMessage
        {
            RuntimeId = guest.RuntimeId,
            OrderSeq = guest.OrderSeq,
            Requested = SellableFood.FromSellable(sellable),
            BasedOn = SellableFood.FromSellable(basedOn),
            SellableType = type,
            ActorUid = PlayerManager.Local.Uid,
        }.Enqueue();
    }
}
