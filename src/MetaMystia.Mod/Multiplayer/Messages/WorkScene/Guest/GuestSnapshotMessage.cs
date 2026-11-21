using MemoryPack;

using MetaMystia.Multiplayer.Guests;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 主机 → 请求者：顾客快照。客机丢弃该顾客的待应用事件，按快照阶段直接生成、入队或入座，并补上当前订单与已上菜。
/// 主机已没有该顾客时阶段为 Left，客机强制清理。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class GuestSnapshotMessage : MultiplayerMessage
{
    public int RuntimeId { get; set; }
    public GuestSpawnInfo SpawnInfo { get; set; }
    public GuestPhase Phase { get; set; }
    public int DeskCode { get; set; }
    public int OrderSeq { get; set; }
    public GenerateOrderMessage Order { get; set; }
    public SellableFood ServedFood { get; set; }
    public SellableFood ServedBeverage { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var registry = GuestRegistry.Current;
        var guest = registry.Find(RuntimeId);
        if (SpawnInfo == null)
        {
            if (guest != null) new GuestLeaveMessage { RuntimeId = RuntimeId, Reason = LeaveReason.Force }.Apply(guest, true);
            return;
        }
        guest ??= registry.Add(RuntimeId, GuestTrack.Replicated, SpawnInfo);
        if (guest.IsLeft) return;
        guest.Inbox.Clear();
        guest.Waited = -1;
        if (guest.Controller == null) GuestReplay.Spawn(guest);
        if (Phase == GuestPhase.Queued)
        {
            if (guest.State == GuestPhase.Constructed) GuestReplay.Queue(guest, false);
            return;
        }
        if (Phase == GuestPhase.Constructed) return;
        if (guest.State is GuestPhase.Constructed or GuestPhase.Queued)
            new GuestSeatMessage { RuntimeId = RuntimeId, DeskCode = DeskCode, FromQueue = true }.Apply(guest, true);
        if (Phase != GuestPhase.Seating) GuestReplay.Arrive(guest);
        if (Order != null)
        {
            Order.Apply(guest, true);
            new ConfirmServeMessage { RuntimeId = RuntimeId, OrderSeq = OrderSeq, Food = ServedFood, Beverage = ServedBeverage }.Apply(guest, true);
        }
        else if (OrderSeq > guest.OrderSeq)
        {
            guest.OrderSeq = OrderSeq;
            guest.State = Phase == GuestPhase.Leaving ? GuestPhase.Leaving : GuestPhase.Eaten;
        }
    }

    public void Send() => Enqueue();
}
