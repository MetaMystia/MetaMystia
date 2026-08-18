using MemoryPack;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>主机 → 全体玩家：确认备菜阶段全员就绪，并下发主机权威备菜表。</summary>
[MemoryPackable]
[AutoLog]
public partial class PrepAllReadyAction : Action
{
    public UpdatePrepAction.Table PrepTable { get; set; } = new();

    [RequireHostSender]
    [CheckScene(Common.UI.Scene.IzakayaPrepScene)]
    public override void OnReceivedDerived()
    {
        PrepSceneManager.ApplyHostTable(PrepTable);
        IzakayaConfigPannelPatch.PrepOver();
    }

    public static void Send()
    {
        if (!MpManager.IsRoomHost) return;
        new PrepAllReadyAction { PrepTable = PrepSceneManager.GetLocalPrepTableSnapshot() }.Enqueue();
    }
}
