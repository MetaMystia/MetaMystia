using System;

namespace MetaMystia.UI;

internal static class NetworkText
{
    // 协议及 IO 诊断保留在日志，玩家看到稳定的本地化说明。
    public static string Error(string reason, TextId fallback = TextId.MpRequestRejected) => (reason switch
    {
        "Protocol version mismatch" => TextId.MpProtocolMismatch,
        "Game or Mod version mismatch" => TextId.MpRoomVersionMismatch,
        "Server is full" => TextId.MpServerFull,
        "Room is full" => TextId.MpRoomFull,
        "Room admission is closed" => TextId.MpRoomClosed,
        "Room no longer exists" => TextId.MpRoomMissing,
        "Already in a room" => TextId.MpRoomAlreadyJoined,
        "Room binding changed" => TextId.MpRoomBindingChanged,
        "Host permission required" => TextId.MpRoomHostOnly,
        "Invalid room settings" => TextId.MpRoomSettingsInvalid,
        "Invalid name" or "Invalid identity" => TextId.MpPlayerIdInvalid,
        _ when reason.Contains("timed out", StringComparison.OrdinalIgnoreCase) => TextId.MpConnectionTimeout,
        _ => fallback
    }).Get();
}
