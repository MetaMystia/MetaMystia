using MemoryPack;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>客机 → 主机：本地缺少该顾客或缺前置事件，请求快照。</summary>
[MemoryPackable]
public partial class GuestQueryMessage : MultiplayerMessage
{
    public int RuntimeId { get; set; }

    [HostOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var guest = GuestRegistry.Current.Find(RuntimeId);
        var controller = guest?.Controller;
        var snapshot = new GuestSnapshotMessage { RuntimeId = RuntimeId, Phase = GuestPhase.Left, WireTargetUid = SenderUid };
        if (controller != null && guest.Track == GuestTrack.Replicated && !guest.IsLeft && controller.HaveNotLeft())
        {
            snapshot.SpawnInfo = guest.SpawnInfo;
            snapshot.Phase = guest.State;
            snapshot.DeskCode = controller.DeskCode;
            snapshot.OrderSeq = guest.OrderSeq;
            if (guest.State == GuestPhase.WaitingServe && guest.CurrentOrder is { } order)
            {
                snapshot.Order = GenerateOrderMessage.From(guest, GuestsManager.OrderGenerationResult.Succeed, null, order);
                snapshot.Order.OrderSeq = guest.OrderSeq;
                snapshot.ServedFood = SellableFood.FromSellable(order.ServFood);
                snapshot.ServedBeverage = SellableFood.FromSellable(order.ServBeverage);
            }
        }
        snapshot.Send();
    }

    public static void Send(int runtimeId) => new GuestQueryMessage { RuntimeId = runtimeId }.Enqueue();
}
