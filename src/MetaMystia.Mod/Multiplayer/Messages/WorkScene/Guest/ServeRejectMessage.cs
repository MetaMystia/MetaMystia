using MemoryPack;

using GameData.Core.Collections;

using MetaMystia.Multiplayer.Guests;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机 → 操作者：放入或确认被拒绝，所列物品退回操作者托盘。</summary>
[MemoryPackable]
public partial class ServeRejectMessage : MultiplayerMessage
{
    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    /// <summary>被拒的是确认上桌；否则是放入。</summary>
    public bool Confirm { get; set; }
    public SellableFood Food { get; set; }
    public SellableFood Beverage { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var guest = GuestRegistry.Current.Find(RuntimeId);
        if (guest == null) return;
        var current = OrderSeq == guest.OrderSeq;
        if (Food != null) ServeSlots.OnRejected(guest, Sellable.SellableType.Food, Food.ToSellable(), Confirm, current);
        if (Beverage != null) ServeSlots.OnRejected(guest, Sellable.SellableType.Beverage, Beverage.ToSellable(), Confirm, current);
    }

    public static void Send(int uid, int runtimeId, int orderSeq, bool confirm, Sellable food, Sellable beverage) =>
        new ServeRejectMessage
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Confirm = confirm,
            Food = SellableFood.FromSellable(food),
            Beverage = SellableFood.FromSellable(beverage),
            WireTargetUid = uid,
        }.Enqueue();
}
