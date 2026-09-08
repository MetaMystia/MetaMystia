using MemoryPack;

using MetaMystia.UI;

namespace MetaMystia.Network;

/// <summary>任何玩家 → 所有玩家：通告本人备菜阶段就绪（IzakayaPrepScene）。</summary>
[MemoryPackable]
[AutoLog]
public partial class PrepReadyAction : Action
{
    [CheckScene(Common.UI.Scene.IzakayaPrepScene)]
    public override void OnReceivedDerived()
    {
        RoomGameplay.ReceiveReady(SenderUid, GameplayPhase.Prep);
    }

    public static void Send() => new PrepReadyAction().Enqueue();
}
