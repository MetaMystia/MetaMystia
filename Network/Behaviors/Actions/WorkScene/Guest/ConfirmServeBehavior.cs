using GameData.Core.Collections;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ConfirmServeBehavior
{
    public static void Send(int runtimeId, int orderSeq, Sellable food, Sellable beverage, int senderUid = -1) =>
        new ConfirmServeAction
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Food = SellableFood.FromSellable(food),
            Beverage = SellableFood.FromSellable(beverage),
            SenderUid = senderUid == -1 ? PlayerManager.Local.Uid : senderUid
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ConfirmServeAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true);
    }

    private static void Handle(ConfirmServeAction action)
    {
        if (action.SenderUid == PlayerManager.Local.Uid)
            return;

        var rid = action.RuntimeId;
        var seq = action.OrderSeq;
        var food = action.Food?.ToSellable();
        var bev = action.Beverage?.ToSellable();
        var senderUid = action.SenderUid;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoConfirmServe),
            () => GuestFSM.DoConfirmServe(rid, seq, food, bev, senderUid));
    }
}
