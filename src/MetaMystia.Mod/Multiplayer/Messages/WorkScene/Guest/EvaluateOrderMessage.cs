using MemoryPack;

using Mystia.Scenes;

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

    protected override bool ClientOnlyReceive => true;
    protected override bool DiscardOnStory => true;
    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var seq = OrderSeq;
        var food = Food?.ToSellable();
        var bev = Beverage?.ToSellable();
        var result = (GuestEvaluation)(int)EvalResult;
        var fsm = GuestsMap.GetGuestFsm(rid);
        QueueForGuest(fsm, nameof(GuestFSM.DoEvaluateOrder),
            () => GuestFSM.DoEvaluateOrder(rid, seq, food, bev, result));
    }

    public static void Send(int runtimeId, int orderSeq, DishProxy? food, DishProxy? beverage, GuestEvaluation result) =>
        new EvaluateOrderMessage
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Food = SellableFood.FromProxy(food),
            Beverage = SellableFood.FromProxy(beverage),
            EvalResult = (GuestGroupController.EvaluationResult)(int)result
        }.Enqueue();
}
