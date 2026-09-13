using MemoryPack;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>主机 → 全体玩家：确认备菜阶段全员就绪，并下发主机权威备菜表。</summary>
[MemoryPackable]
[AutoLog]
public partial class PrepAllReadyAction : Action
{
    public UpdatePrepAction.Table PrepTable { get; set; } = new();
    public int PrepRound { get; set; }

    [RequireHostSender]
    public override void OnReceivedDerived()
    {
        if (PrepRound > 0)
        {
            if (MpManager.LocalScene != Common.UI.Scene.WorkScene
                || !PrepSceneManager.CanFinishYuyukoPrep(PrepRound)) return;
        }
        else if (MpManager.LocalScene != Common.UI.Scene.IzakayaPrepScene) return;
        PrepSceneManager.ApplyHostTable(PrepTable);
        IzakayaConfigPannelPatch.PrepOver();
    }

    public static void Send()
    {
        if (!MpManager.IsRoomHost) return;
        new PrepAllReadyAction
        {
            PrepTable = PrepSceneManager.GetLocalPrepTableSnapshot(),
            PrepRound = PrepSceneManager.IsYuyukoChallenge ? PrepSceneManager.YuyukoPrepRound : 0
        }.Enqueue();
    }
}
