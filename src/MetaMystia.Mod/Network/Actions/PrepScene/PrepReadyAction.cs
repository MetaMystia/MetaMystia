using MemoryPack;

using MetaMystia.UI;

namespace MetaMystia.Network;

/// <summary>任何玩家 → 所有玩家：通告本人备菜阶段就绪（IzakayaPrepScene）。</summary>
[MemoryPackable]
[AutoLog]
[RoomRelay]
public partial class PrepReadyAction : Action
{
    [CheckScene(Common.UI.Scene.IzakayaPrepScene)]
    public override void OnReceivedDerived()
    {
        PlayerManager.SetPeerPrepOver(SenderUid);
        MpManager.PrepOver();
        InGameConsole.ShowPassive(TextId.ReadyForWork.Get(LiveModeManager.GetDisplayName(SenderUid)));
    }

    public static void Send() => new PrepReadyAction().Enqueue();
}
