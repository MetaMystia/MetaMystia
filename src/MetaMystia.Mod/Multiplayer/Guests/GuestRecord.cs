using System.Collections.Generic;

using GameData.Core.Collections;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Messages;
using MetaMystia.Patch;

using static NightScene.GuestManagementUtility.GuestGroupController;

namespace MetaMystia.Multiplayer.Guests;

public enum GuestMode { Vanilla, Scripted, Host, Client }

public enum GuestTrack { Replicated, Scripted }

public enum GuestPhase
{
    Constructed,    // 已生成，尚未入座或排队
    Queued,
    Seating,        // 已分配桌位，正在走向座位
    Seated,         // 已到座，等待首单
    WaitingServe,
    Evaluating,     // 已评价，正在吃
    Eaten,          // 吃完，等待续单或离场
    Leaving,        // 主机点单失败，等待离场结果
    Left,
    Manual,         // 脚本顾客空闲，由剧情驱动
}

public enum LeaveReason : byte { Desk, PatientDepleted, Repel, Unseated, Force }

public enum RepelBranch : byte { Normal, Counted, Kisume }

/// <summary>客机待应用的主机事件。返回 true 表示已应用或已过期；force 为宽限期满后的追赶调用。</summary>
public interface IGuestEvent
{
    int RuntimeId { get; }
    bool Apply(GuestRecord guest, bool force);
}

/// <summary>一位联机顾客的同步状态。原版对象的状态以原版为准，这里只记录同步所需的部分。</summary>
public sealed class GuestRecord
{
    public int RuntimeId { get; init; }
    public GuestTrack Track { get; init; }
    /// <summary>客机按主机编号建立的记录。</summary>
    public bool Replica { get; init; }
    public GuestSpawnInfo SpawnInfo { get; init; }
    public GuestGroupController Controller { get; internal set; }
    public GuestPhase State { get; set; }

    /// <summary>主机每次点单递增，随点单、上菜和评价消息下发。脚本顾客由剧情序号赋值。</summary>
    public int OrderSeq { get; set; }
    public Sellable WillServeFood { get; set; }
    public Sellable WillServeBeverage { get; set; }

    /// <summary>主机：离桌入口之前记录的原因。</summary>
    public LeaveReason LeaveReason { get; set; }
    /// <summary>主机：评价已算出、最终结果尚未广播。</summary>
    public bool EvalPending { get; set; }
    /// <summary>客机：重放评价时使用的主机最终结果，评价后续处理开始时消耗。</summary>
    public EvaluationResult FinalEval { get; set; } = EvaluationResult.Null;
    /// <summary>客机：重放点单时暂存主机订单，供内部订单生成 Hook 回填。</summary>
    public GeneratedOrderInfo? PendingOrder { get; set; }
    /// <summary>客机：本人赶客的分支，离桌时上报。</summary>
    public RepelBranch? LocalRepel { get; set; }
    /// <summary>客机：重放入座的到座处理已执行。</summary>
    public bool Arrived { get; set; }

    /// <summary>本端从托盘放入、尚未确认的物品。</summary>
    public Sellable ClaimFood { get; set; }
    public Sellable ClaimBeverage { get; set; }
    /// <summary>本端已确认上桌、等待主机结果的物品；主机拒绝时退回。</summary>
    public Sellable ConfirmedFood { get; set; }
    public Sellable ConfirmedBeverage { get; set; }

    internal readonly Queue<IGuestEvent> Inbox = new();
    internal float Waited = -1;

    internal int SentMood = -1, SentFund = -1, SentRemain = -1, SentPatient = -1, SentMaxPatient = -1;

    public bool IsLeft => State == GuestPhase.Left;
    public bool IsSeated => State is GuestPhase.Seating or GuestPhase.Seated or GuestPhase.WaitingServe
        or GuestPhase.Evaluating or GuestPhase.Eaten or GuestPhase.Leaving or GuestPhase.Manual;
    public int DeskCode => Controller?.DeskCode ?? -1;
    public GuestsManager.OrderBase CurrentOrder => Controller?.AllOrdersCount > 0 ? Controller.PeekOrders() : null;

    public Sellable Slot(Sellable.SellableType type) =>
        type == Sellable.SellableType.Food ? WillServeFood : WillServeBeverage;

    public void SetSlot(Sellable.SellableType type, Sellable sellable)
    {
        if (type == Sellable.SellableType.Food) WillServeFood = sellable;
        else WillServeBeverage = sellable;
    }

    public Sellable Claim(Sellable.SellableType type) =>
        type == Sellable.SellableType.Food ? ClaimFood : ClaimBeverage;

    public void SetClaim(Sellable.SellableType type, Sellable sellable)
    {
        if (type == Sellable.SellableType.Food) ClaimFood = sellable;
        else ClaimBeverage = sellable;
    }

    public override string ToString() => $"#{RuntimeId}/{State}";
}
