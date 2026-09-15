using MemoryPack;

using MetaMystia.UI;

namespace MetaMystia.Multiplayer.Actions;

/// <summary>任何玩家 → 所有玩家：通告普通备菜或幽幽子试炼本轮备菜就绪。</summary>
[MemoryPackable]
[AutoLog]
public partial class PrepReadyAction : Action
{
    public int PrepRound { get; set; }

    public override void OnReceivedDerived()
    {
        if (PrepRound > 0)
        {
            if (GameFlow.LocalScene == Common.UI.Scene.WorkScene)
                PrepSceneManager.ReceiveYuyukoPrepReady(SenderUid, PrepRound);
            return;
        }
        if (GameFlow.LocalScene != Common.UI.Scene.IzakayaPrepScene) return;
        PlayerManager.SetPeerPrepOver(SenderUid);
        PrepSceneManager.TryCompletePrep();
        InGameConsole.ShowPassive(TextId.ReadyForWork.Get(LiveModeManager.GetDisplayName(SenderUid)));
    }

    public static void Send() => new PrepReadyAction
    {
        PrepRound = PrepSceneManager.IsYuyukoChallenge ? PrepSceneManager.YuyukoPrepRound : 0
    }.Enqueue();
}
