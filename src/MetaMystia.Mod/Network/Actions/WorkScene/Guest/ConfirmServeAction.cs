using MemoryPack;

using GameData.Core.Collections;

namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
public partial class ConfirmServeAction : Action
{

    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    public SellableFood Food { get; set; }
    public SellableFood Beverage { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        if (SenderUid == PlayerManager.Local.Uid)
        {
            // 本地玩家发出的请求返回的回声，直接忽略
            return;
        }

        var rid = RuntimeId;
        var seq = OrderSeq;
        var food = Food?.ToSellable();
        var bev = Beverage?.ToSellable();
        var senderUid = SenderUid;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoConfirmServe),
            () => GuestFSM.DoConfirmServe(rid, seq, food, bev, senderUid));
    }

    public static void Send(int runtimeId, int orderSeq, Sellable food, Sellable beverage, int senderUid = -1) =>
        new ConfirmServeAction
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Food = SellableFood.FromSellable(food),
            Beverage = SellableFood.FromSellable(beverage),
            SenderUid = senderUid == -1 ? PlayerManager.Local.Uid : senderUid
        }.Enqueue();
}
