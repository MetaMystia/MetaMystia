using MemoryPack;

namespace MetaMystia.Network;

/// <summary>旧白天确认消息，仅保留协议编号；改用 DayDestinationConfirmAction。</summary>
[MemoryPackable]
[AutoLog]
public partial class DayAllReadyAction : Action
{
    [RequireHostSender]
    [CheckScene(Common.UI.Scene.DayScene)]
    public override void OnReceivedDerived()
    {
        // 保留协议编号，旧确认不能绕过目的地裁定。
    }

    public static void Send()
    {
        if (!MpManager.IsRoomHost) return;
        new DayAllReadyAction().Enqueue();
    }
}
