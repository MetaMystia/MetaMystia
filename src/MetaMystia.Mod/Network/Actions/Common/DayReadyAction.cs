using MemoryPack;

namespace MetaMystia.Network;

/// <summary>旧白天就绪消息，仅保留协议编号；改用 DayDestinationIntentAction。</summary>
[MemoryPackable]
[AutoLog]
[RoomRelay]
public partial class DayReadyAction : Action
{
    [CheckScene(Common.UI.Scene.DayScene)]
    public override void OnReceivedDerived()
    {
        // 保留协议编号；白天入口已改为带轮次和目的地的协商。
    }

    public static void Send() => new DayReadyAction().Enqueue();
}
