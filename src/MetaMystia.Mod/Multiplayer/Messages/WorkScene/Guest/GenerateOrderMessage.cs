using System.Linq;

using Il2CppSystem.Linq;
using MemoryPack;
using UnityEngine;

using GameData.Core.Collections.CharacterUtility;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;
using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 主机：点单结果（GenerateOrderSession 内部订单生成之后），带订单序号、点单人和点单后的资金与剩余点单数。
/// 客机在本地到座（首单）或吃完（续单）后重放原版点单入口，内部订单生成由 Hook 回填主机结果。
/// </summary>
[MemoryPackable]
public partial class GenerateOrderMessage : MultiplayerMessage, IGuestEvent
{
    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    public GuestsManager.OrderGenerationResult Result { get; set; }
    public GuestsManager.OrderGenerationResult? OverrideResult { get; set; }
    public GuestsManager.OrderBase.OrderType OrderType { get; set; }
    public int RequestFood { get; set; }
    public int RequestBev { get; set; }
    public int DeskCode { get; set; }
    public bool NotShowInUI { get; set; }
    public bool FreeOrder { get; set; }
    public int Fund { get; set; }
    public int RemainOrderCount { get; set; }
    public int OrderingGuest { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => GuestRegistry.Current.Post(this);

    /// <summary>首单等本地到座、续单等本地吃完；宽限期满后直接补完再点单。</summary>
    public bool Apply(GuestRecord guest, bool force)
    {
        if (OrderSeq <= guest.OrderSeq || guest.IsLeft) return true;
        var controller = guest.Controller;
        if (controller == null || guest.State is GuestPhase.Constructed or GuestPhase.Queued)
        {
            if (!force) return false;
            GuestRegistry.Current.Query(RuntimeId);
            return true;
        }
        if (guest.State is GuestPhase.Seating or GuestPhase.Evaluating or GuestPhase.WaitingServe && !force) return false;
        if (guest.State == GuestPhase.Seating) GuestReplay.Arrive(guest);
        var manager = GuestsManager.Instance;
        guest.FinalEval = GuestGroupController.EvaluationResult.Null;
        guest.PendingOrder = new GeneratedOrderInfo
        {
            OrderGenerationResult = Result,
            OverrideResult = OverrideResult,
            OrderData = BuildOrder(guest),
            Fund = Fund,
            RemainOrderCount = RemainOrderCount,
        };
        if (guest.State == GuestPhase.Seated) manager.FirstOrder(controller);
        else
        {
            manager.Register(manager.CanPlayerRepellGuest, controller);
            manager.GenerateOrderSession(controller, true);
        }
        guest.PendingOrder = null;
        guest.OrderSeq = OrderSeq;
        guest.WillServeFood = null;
        guest.WillServeBeverage = null;
        guest.State = (OverrideResult ?? Result) == GuestsManager.OrderGenerationResult.Succeed
            ? GuestPhase.WaitingServe : GuestPhase.Leaving;
        return true;
    }

    private GuestsManager.OrderBase BuildOrder(GuestRecord guest)
    {
        if (OrderType != GuestsManager.OrderBase.OrderType.Normal)
            return new GuestsManager.SpecialOrder(DataBaseCharacter.RefSGuest(guest.SpawnInfo.Ids[0]),
                RequestFood, RequestBev, DeskCode, NotShowInUI, FreeOrder);
        var controller = guest.Controller;
        var ordering = controller.GetAllGuests().ToArray().First();
        if (controller.TryCast<NormalGuestsController>() is { } normal
            && OrderingGuest >= 0 && OrderingGuest < normal.NormalGuestsGroups.Length)
        {
            ordering = normal.NormalGuestsGroups[OrderingGuest];
            controller.OrderingGuest = ordering;
        }
        return new GuestsManager.NormalOrder(ordering, RequestFood, RequestBev, DeskCode, NotShowInUI, FreeOrder);
    }

    public static void Detect(GuestsManager.OrderGenerationResult result, GuestGroupController controller, GuestsManager.OrderBase orderData)
    {
        var guest = GuestRegistry.Of(controller);
        if (guest == null || guest.Track != GuestTrack.Replicated) return;
        if (guest.State is not (GuestPhase.Seated or GuestPhase.Eaten))
            Log.Warning($"Guest {guest} ordered from unexpected phase");
        // 稀客的资金不足判定在内部订单生成之后，这里用无副作用的同等计算预测。
        GuestsManager.OrderGenerationResult? overrideResult = controller.ControllType == GuestsManager.GuestType.Special
            ? CheckRemainingFund(result, controller) : null;
        guest.OrderSeq++;
        guest.WillServeFood = null;
        guest.WillServeBeverage = null;
        guest.State = (overrideResult ?? result) == GuestsManager.OrderGenerationResult.Succeed
            ? GuestPhase.WaitingServe : GuestPhase.Leaving;
        From(guest, result, overrideResult, orderData).Enqueue();
    }

    /// <summary>按主机当前订单构造点单事件，供点单广播与快照使用。</summary>
    internal static GenerateOrderMessage From(GuestRecord guest, GuestsManager.OrderGenerationResult result,
        GuestsManager.OrderGenerationResult? overrideResult, GuestsManager.OrderBase orderData)
    {
        var controller = guest.Controller;
        var ordering = 0;
        if (controller.TryCast<NormalGuestsController>() is { } normal)
        {
            var groups = normal.NormalGuestsGroups;
            for (int i = 0; i < groups.Length; i++)
                if (groups[i].Pointer == controller.OrderingGuest?.Pointer) ordering = i;
        }
        return new GenerateOrderMessage
        {
            RuntimeId = guest.RuntimeId,
            OrderSeq = guest.OrderSeq,
            Result = result,
            OverrideResult = overrideResult,
            OrderType = orderData?.Type ?? GuestsManager.OrderBase.OrderType.Normal,
            RequestFood = orderData?.foodRequest ?? 0,
            RequestBev = orderData?.beverageRequest ?? 0,
            DeskCode = orderData?.DeskCode ?? -1,
            NotShowInUI = orderData?.NotShowInUI ?? false,
            FreeOrder = orderData?.FreeOrder ?? false,
            Fund = controller.GetFund,
            RemainOrderCount = controller.remainOrderCount,
            OrderingGuest = ordering,
        };
    }

    /// <summary>GenerateOrderSession 中稀客 CheckRemainingFund 的无副作用版本。</summary>
    private static GuestsManager.OrderGenerationResult CheckRemainingFund(GuestsManager.OrderGenerationResult oldResult, GuestGroupController toGenerate)
    {
        var filtered = toGenerate.AllOrders.ToArray().Where(x => !x.FreeOrder).ToArray();
        int spent = filtered.Length > 0 ? filtered.Select(x => x.Price).Aggregate((a, b) => a + b) : 0;
        int totalFund = toGenerate.MaxFundCarry + toGenerate.ExtraFundByBuff;
        if (spent <= totalFund) return oldResult;
        float enduranceMultiplier = 1f;
        if (toGenerate.Mood > 50)
            enduranceMultiplier = 1f + Mathf.Log(51f / (101 - Mathf.Min(toGenerate.Mood, 100)), 25f);
        return spent > totalFund * (toGenerate.EnduranceLimit * enduranceMultiplier)
            ? GuestsManager.OrderGenerationResult.ExceedEndurance
            : GuestsManager.OrderGenerationResult.NoMoney;
    }
}
