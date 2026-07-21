using MemoryPack;

using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 所有玩家：通告玩家 ID 变更
/// </summary>
[MemoryPackable]
[PublicRelay]
[AutoLog]
public partial class PlayerChangeIdAction : Action
{

    public string NewPlayerId { get; private set; }

    public override void OnReceivedDerived()
    {
        if (PlayerManager.TryGetVisiblePeer(SenderUid, out var peer))
        {
            var oldId = peer.Id;
            peer.Id = NewPlayerId;
            var oldDisplay = LiveModeManager.IsActive ? LiveModeManager.FormatUid(SenderUid) : oldId;
            var newDisplay = LiveModeManager.IsActive ? LiveModeManager.FormatUid(SenderUid) : NewPlayerId;
            InGameConsole.ShowPassiveFromAnyThread(TextId.PeerPlayerIdChanged.Get(oldDisplay, newDisplay));
            FloatingTextHelper.UpdatePlayerLabel(SenderUid, LiveModeManager.GetDisplayName(SenderUid));
        }
    }

    public static void Send(string newId)
    {
        // 更新本地玩家自己的头顶标签
        PlayerManager.Local.Id = newId;
        FloatingTextHelper.UpdatePlayerLabel(PlayerManager.Local.Uid, LiveModeManager.GetDisplayName(PlayerManager.Local.Uid));
        new PlayerChangeIdAction { NewPlayerId = newId }.Enqueue();
    }
}
