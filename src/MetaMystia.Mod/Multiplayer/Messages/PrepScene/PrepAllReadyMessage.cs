using MemoryPack;

using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机 → 全体玩家：确认备菜阶段全员就绪，并下发主机权威备菜表。</summary>
[MemoryPackable]
[AutoLog]
public partial class PrepAllReadyMessage : MultiplayerMessage
{
    public UpdatePrepMessage.Table PrepTable { get; set; } = new();
    public int PrepRound { get; set; }

    [RequireHostSender]
    public override void OnReceivedDerived()
    {
        if (PrepRound > 0)
        {
            if (GameFlow.LocalScene != Common.UI.Scene.WorkScene
                || !PrepSceneManager.CanFinishYuyukoPrep(PrepRound)) return;
        }
        else if (GameFlow.LocalScene != Common.UI.Scene.IzakayaPrepScene) return;
        PrepSceneManager.ApplyHostTable(PrepTable);
        IzakayaConfigPannelPatch.PrepOver();
    }

    public static void Send()
    {
        if (!GameSession.IsRoomHost) return;
        new PrepAllReadyMessage
        {
            PrepTable = PrepSceneManager.GetLocalPrepTableSnapshot(),
            PrepRound = PrepSceneManager.IsYuyukoChallenge ? PrepSceneManager.YuyukoPrepRound : 0
        }.Enqueue();
    }
}
