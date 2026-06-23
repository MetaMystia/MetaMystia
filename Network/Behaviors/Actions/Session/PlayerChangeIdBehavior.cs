using System.Linq;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PlayerChangeIdBehavior
{
    public static void Send(string newId)
    {
        PlayerManager.Local.Id = newId;
        FloatingTextHelper.UpdatePlayerLabel(PlayerManager.Local.Uid, LiveModeManager.GetDisplayName(PlayerManager.Local.Uid));
        new PlayerChangeIdAction { NewPlayerId = newId }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PlayerChangeIdAction>(Handle);
    }

    private static void Handle(PlayerChangeIdAction action)
    {
        if (!IsValidPeerId(action.NewPlayerId))
        {
            Plugin.Instance?.Log.LogWarning($"Invalid PlayerChangeId from uid={action.SenderUid}, ignoring");
            return;
        }
        if (PlayerManager.PlayerTable.Values.Any(record =>
                record.Uid != action.SenderUid &&
                string.Equals(record.PeerId, action.NewPlayerId, System.StringComparison.OrdinalIgnoreCase)))
        {
            Plugin.Instance?.Log.LogWarning($"Duplicate PlayerChangeId '{action.NewPlayerId}' from uid={action.SenderUid}, ignoring");
            return;
        }

        if (!PlayerManager.TryGetVisiblePeer(action.SenderUid, out var peer))
            return;

        var oldId = peer.Id;
        peer.Id = action.NewPlayerId;
        if (PlayerManager.TryGetRecord(action.SenderUid, out var record))
            record.PeerId = action.NewPlayerId;
        var oldDisplay = LiveModeManager.IsActive ? LiveModeManager.FormatUid(action.SenderUid) : oldId;
        var newDisplay = LiveModeManager.IsActive ? LiveModeManager.FormatUid(action.SenderUid) : action.NewPlayerId;
        InGameConsole.ShowPassiveFromAnyThread(TextId.PeerPlayerIdChanged.Get(oldDisplay, newDisplay));
        FloatingTextHelper.UpdatePlayerLabel(action.SenderUid, LiveModeManager.GetDisplayName(action.SenderUid));
    }

    private static bool IsValidPeerId(string peerId)
    {
        if (string.IsNullOrWhiteSpace(peerId)) return false;
        foreach (char c in peerId)
            if (c == '<' || c == '>' || char.IsWhiteSpace(c) || char.IsControl(c))
                return false;
        return true;
    }
}
