using MemoryPack;

using MetaMystia.UI;

namespace MetaMystia.Network;

/// <summary>任何玩家 → 所有玩家：通告本人白天阶段就绪（DayScene）。</summary>
[MemoryPackable]
[AutoLog]
[RoomRelay]
public partial class DayReadyAction : Action
{
    [CheckScene(Common.UI.Scene.DayScene)]
    public override void OnReceivedDerived()
    {
        PlayerManager.SetPeerDayOver(SenderUid);
        MpManager.DayOver();
        InGameConsole.ShowPassive(TextId.ReadyForWork.Get(LiveModeManager.GetDisplayName(SenderUid)));
    }

    public static void Send() => new DayReadyAction().Enqueue();
}
