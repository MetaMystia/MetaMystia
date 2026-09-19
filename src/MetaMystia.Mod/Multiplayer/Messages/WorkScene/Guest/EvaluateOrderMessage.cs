using MemoryPack;

using GameData.Core.Collections;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class EvaluateOrderMessage : MultiplayerMessage
{

    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    public SellableFood Food { get; set; }
    public SellableFood Beverage { get; set; }
    public GuestGroupController.EvaluationResult EvalResult { get; set; }

    [ClientOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var seq = OrderSeq;
        var food = Food?.ToSellable();
        var bev = Beverage?.ToSellable();
        var result = EvalResult;
        var fsm = GuestsMap.GetGuestFsm(rid);
        QueueForGuest(fsm, nameof(GuestFSM.DoEvaluateOrder),
            () => GuestFSM.DoEvaluateOrder(rid, seq, food, bev, result));
    }

    public static void Send(int runtimeId, int orderSeq, Sellable food, Sellable beverage, GuestGroupController.EvaluationResult result) =>
        new EvaluateOrderMessage
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Food = SellableFood.FromSellable(food),
            Beverage = SellableFood.FromSellable(beverage),
            EvalResult = result
        }.Enqueue();
}
