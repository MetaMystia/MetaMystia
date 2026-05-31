using MemoryPack;

using GameData.Core.Collections;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
public partial class EvaluateOrderAction : Action
{
    public override ActionType Type => ActionType.EvaluateOrderAction;

    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    public SellableFood Food { get; set; }
    public SellableFood Beverage { get; set; }
    public GuestGroupController.EvaluationResult EvalResult { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        if (MpManager.IsConnectedHost) return;

        var rid = RuntimeId;
        var seq = OrderSeq;
        var food = Food?.ToSellable();
        var bev = Beverage?.ToSellable();
        var result = EvalResult;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            fsm?.Enqueue(nameof(GuestFSM.DoEvaluateOrder),
                () => GuestFSM.DoEvaluateOrder(rid, seq, food, bev, result));
        });
    }

    public static void Send(int runtimeId, int orderSeq, Sellable food, Sellable beverage, GuestGroupController.EvaluationResult result)
    {
        var action = new EvaluateOrderAction
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Food = SellableFood.FromSellable(food),
            Beverage = SellableFood.FromSellable(beverage),
            EvalResult = result
        };
        action.SendToHostOrBroadcast();
    }
}
