using GameData.Core.Collections;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ServeSellableBehavior
{
    public static void Send(
        int runtimeId,
        int orderSeq,
        Sellable requested,
        Sellable basedOn,
        Sellable.SellableType sellableType,
        int senderUid = -1) =>
        new ServeSellableAction
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Requested = SellableFood.FromSellable(requested),
            BasedOn = SellableFood.FromSellable(basedOn),
            SellableType = sellableType.ToWire(),
            SenderUid = senderUid == -1 ? PlayerManager.Local.Uid : senderUid
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ServeSellableAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true);
    }

    private static void Handle(ServeSellableAction action)
    {
        if (action.SenderUid == PlayerManager.Local.Uid)
            return;

        var rid = action.RuntimeId;
        var seq = action.OrderSeq;
        var sellableType = action.SellableType.ToGameSellableType();
        var requested = action.Requested?.ToSellable();
        var basedOn = action.BasedOn?.ToSellable();
        var senderUid = action.SenderUid;
        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        fsm.Enqueue(nameof(GuestFSM.DoServe),
            () => GuestFSM.DoServe(rid, seq, requested, basedOn, sellableType, senderUid));
    }
}
