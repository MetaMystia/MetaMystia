using MetaMystia.UI;

namespace MetaMystia;

public static class LiveModeManager
{
    public static LiveMode Mode => ConfigManager.LiveStreamingMode?.Value ?? LiveMode.Off;

    public static bool IsActive => Mode != LiveMode.Off;
    public static bool MaskMessages => Mode == LiveMode.Full;
    public static bool SuppressFloatingChatBubbles => IsActive;
    public static bool LockConsoleUi => Mode == LiveMode.Partial;
    public static bool ShowUntrustedZoneOutline => Mode == LiveMode.Partial;

    public static string FormatUid(int uid) => $"UID-{uid}";

    public static string GetDisplayName(int uid, string peerIdFallback = null)
    {
        if (Mode != LiveMode.Off)
            return FormatUid(uid);

        if (uid == PlayerManager.Local.Uid)
            return GetLocalDisplayName();
        if (PlayerManager.TryGetVisiblePeer(uid, out var peer))
            return peer.Id;
        if (!string.IsNullOrEmpty(peerIdFallback))
            return peerIdFallback;
        return $"uid={uid}";
    }

    public static string GetLocalDisplayName()
    {
        if (Mode == LiveMode.Off)
            return MpManager.PlayerId ?? "Player";
        return FormatUid(PlayerManager.Local.Uid);
    }

    public static string MaskMessage(string message)
    {
        if (!MaskMessages || string.IsNullOrEmpty(message))
            return message;
        return new string('*', message.Length);
    }

    public static void ApplyMode(LiveMode mode)
    {
        var previous = Mode;
        ConfigManager.LiveStreamingMode.Value = mode;
        InGameConsole.ClearLogs();

        if (mode != LiveMode.Off)
            RefreshAllLabels();
        else if (previous != LiveMode.Off)
            RefreshAllLabels();

        if (mode == LiveMode.Partial)
            InGameConsole.ShowPassive(TextId.LivePartialReminder.Get());
    }

    public static void RefreshAllLabels()
    {
        FloatingTextHelper.UpdatePlayerLabel(PlayerManager.Local.Uid, GetDisplayName(PlayerManager.Local.Uid));

        foreach (var kvp in PlayerManager.Peers)
            FloatingTextHelper.UpdatePlayerLabel(kvp.Key, GetDisplayName(kvp.Key));

        foreach (var kvp in PlayerManager.PublicPeers)
        {
            if (!PlayerManager.Peers.ContainsKey(kvp.Key))
                FloatingTextHelper.UpdatePlayerLabel(kvp.Key, GetDisplayName(kvp.Key));
        }
    }
}
