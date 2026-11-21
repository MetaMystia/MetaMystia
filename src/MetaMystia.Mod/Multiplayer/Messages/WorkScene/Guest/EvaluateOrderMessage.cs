using MemoryPack;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;

using static NightScene.GuestManagementUtility.GuestGroupController;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 主机：评价最终结果（彩蛋与改判回调之后，评价台词显示时）。
/// 客机写入菜酒后重放原版评价，并在计算出口与评价后续处理入口两处换成主机结果。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class EvaluateOrderMessage : MultiplayerMessage, IGuestEvent
{
    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    public SellableFood Food { get; set; }
    public SellableFood Beverage { get; set; }
    public EvaluationResult EvalResult { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => GuestRegistry.Current.Post(this);

    public bool Apply(GuestRecord guest, bool force)
    {
        if (OrderSeq < guest.OrderSeq || guest.State is GuestPhase.Evaluating or GuestPhase.Eaten or GuestPhase.Left) return true;
        if (OrderSeq > guest.OrderSeq || guest.State != GuestPhase.WaitingServe) return force;
        var controller = guest.Controller;
        var order = guest.CurrentOrder;
        if (order == null) return true;
        var food = Food?.ToSellable();
        var beverage = Beverage?.ToSellable();
        ServeSlots.SettleOnEvaluate(guest, food, beverage);
        ServeSlots.ClosePanel(guest.DeskCode, guest, returnLocal: false);
        guest.WillServeFood = null;
        guest.WillServeBeverage = null;
        order.ServedFoodInAir = null;
        order.ServedBeverageInAir = null;
        order.ServFood = food;
        order.ServBeverage = beverage;
        ServeSlots.UpdateDesk(guest.DeskCode, food, GameData.Core.Collections.Sellable.SellableType.Food);
        ServeSlots.UpdateDesk(guest.DeskCode, beverage, GameData.Core.Collections.Sellable.SellableType.Beverage);
        guest.FinalEval = EvalResult;
        guest.State = GuestPhase.Evaluating;
        GuestsManager.Instance.EvaluateOrder(controller, false);
        return true;
    }

    /// <summary>评价计算已开始（TryOverrideEvaluateByBuff 之后），最终结果在评价台词显示时发出。</summary>
    public static void MarkPending(GuestGroupController controller)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest?.Track == GuestTrack.Replicated && controller.HasEvaluated) guest.EvalPending = true;
    }

    /// <summary>评价后续处理入口：客机换成主机结果并消耗；两端记为已吃完，续单或离场由主机决定。</summary>
    public static void BeforePostEvaluation(GuestGroupController controller, ref EvaluationResult result)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest == null || guest.Track != GuestTrack.Replicated) return;
        if (guest.FinalEval != EvaluationResult.Null)
        {
            result = guest.FinalEval;
            guest.FinalEval = EvaluationResult.Null;
        }
        if (guest.State == GuestPhase.Evaluating) guest.State = GuestPhase.Eaten;
    }

    public static void Detect(GuestGroupController controller, EvaluationResult result)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest is not { EvalPending: true }) return;
        guest.EvalPending = false;
        var order = controller.PeekOrders();
        guest.WillServeFood = null;
        guest.WillServeBeverage = null;
        ServeSlots.SettleOnEvaluate(guest, order.ServFood, order.ServBeverage);
        if (guest.State != GuestPhase.WaitingServe) Log.Warning($"Guest {guest} evaluated from unexpected phase");
        guest.State = GuestPhase.Evaluating;
        new EvaluateOrderMessage
        {
            RuntimeId = guest.RuntimeId,
            OrderSeq = guest.OrderSeq,
            Food = SellableFood.FromSellable(order.ServFood),
            Beverage = SellableFood.FromSellable(order.ServBeverage),
            EvalResult = result,
        }.Enqueue();
    }
}
