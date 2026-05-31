using GameData.Core.Collections;
using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 上菜/撤回 Action。设计参考 docs/GuestFSM-Model.md §2.8。
/// 不再 <c>[HostRelay]</c>：主机收到客机请求后做冲突仲裁，决定是否接受并广播。
/// 拒绝时不广播——根本原因（host 当前权威值）必然来自先前某次 host 状态变更，
/// 那次变更的广播已在主机→sender 的 TCP 流中（队列或在途），sender 早晚会处理并自然回滚。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class ServeSellableAction : Action
{
    public override ActionType Type => ActionType.ServeSellableAction;
    public int RuntimeId { get; set; }
    public int OrderSeq { get; set; }
    public SellableFood Requested { get; set; }
    public SellableFood BasedOn { get; set; }
    public Sellable.SellableType SellableType { get; set; }


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
        var sellableType = SellableType;

        var requested = Requested?.ToSellable();
        var basedOn = BasedOn?.ToSellable();
        var senderUid = SenderUid;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoServe),
                () => GuestFSM.DoServe(rid, seq, requested, basedOn, sellableType, senderUid));
        });
    }

    public static void Send(int runtimeId, int orderSeq, Sellable requested, Sellable basedOn, Sellable.SellableType sellableType, int senderUid = -1)
    {
        var action = new ServeSellableAction()
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Requested = SellableFood.FromSellable(requested),
            BasedOn = SellableFood.FromSellable(basedOn),
            SellableType = sellableType,
            SenderUid = senderUid == -1 ? PlayerManager.Local.Uid : senderUid
        };
        action.SendToHostOrBroadcast();
    }
}
