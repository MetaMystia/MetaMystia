using GameData.Core.Collections;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class EvaluateOrderBehavior
{
    public static void Send(
        int runtimeId,
        int orderSeq,
        Sellable food,
        Sellable beverage,
        GuestGroupController.EvaluationResult result) =>
        new EvaluateOrderAction
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Food = SellableFood.FromSellable(food),
            Beverage = SellableFood.FromSellable(beverage),
            EvalResult = result.ToWire()
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<EvaluateOrderAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(EvaluateOrderAction action)
    {
        var rid = action.RuntimeId;
        var seq = action.OrderSeq;
        var food = action.Food?.ToSellable();
        var bev = action.Beverage?.ToSellable();
        var result = action.EvalResult.ToGameEvaluationResult();
        var fsm = GuestsMap.GetGuestFsm(rid);
        fsm?.Enqueue(nameof(GuestFSM.DoEvaluateOrder),
            () => GuestFSM.DoEvaluateOrder(rid, seq, food, bev, result));
    }
}
