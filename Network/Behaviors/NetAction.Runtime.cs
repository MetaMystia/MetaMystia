using System;
using System.Linq;
using BepInEx.Logging;

namespace MetaMystia.Network;

internal static class NetActionRuntime
{
    internal static void OnReceivedByHandler(
        this NetAction action,
        Common.UI.Scene? targetScene,
        bool discardOnStory,
        bool requireHostSender,
        NetReceiveScope receiveScope,
        System.Action handle)
    {
        action.LogActionReceived();
        if (targetScene != null && MpManager.LocalScene != targetScene.Value)
        {
            LogInfo($"{MpManager.RoleTag} Received in invalid scene: {action.ActionName()}: {action.ToLogString()}");
            return;
        }
        if (discardOnStory && MpManager.InStory)
        {
            LogInfo($"{MpManager.RoleTag} Discarded (in story): {action.ActionName()}");
            return;
        }
        if (requireHostSender && action.SenderUid != MpManager.Session.HostUid)
        {
            LogWarning($"{MpManager.RoleTag} {action.ActionName()} from non-host uid={action.SenderUid}, ignoring");
            return;
        }
        if (receiveScope == NetReceiveScope.ClientOnly && MpManager.Session.IsRoomHost)
            return;
        if (receiveScope == NetReceiveScope.HostOnly && !MpManager.Session.IsRoomHost)
        {
            LogWarning($"{MpManager.RoleTag} {action.ActionName()} received by non-host, ignoring");
            return;
        }

        handle();
    }

    internal static void Enqueue(this NetAction action, bool lowPriority = false)
    {
        if (!MpWire.CanSend) return;
        if (action.ShouldDiscardOnStory())
        {
            LogInfo($"{MpManager.RoleTag} Will not send (in story): {action.ActionName()}");
            return;
        }
        action.LogActionSend();
        MpWire.EnqueueSend(action, lowPriority);
    }

    private static bool ShouldDiscardOnStory(this NetAction action)
    {
        if (!MpManager.InStory) return false;
        return ModNetActionBehaviors.ShouldDiscardOnStory(action);
    }

    private static string ActionName(this NetAction action) => action.GetType().Name;

    private static string ToLogString(this NetAction action)
    {
        if (action is HelloAckAction helloAck)
        {
            var players = helloAck.Players ?? [];
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                helloAck.AssignedUid,
                PlayersCount = players.Length,
                PlayerIds = players.Take(3).Select(peer => peer.PeerId).ToArray(),
                PlayersTruncated = players.Length > 3
            });
        }

        if (action is RoomAssignAction roomAssign)
        {
            var members = roomAssign.Members ?? [];
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                RoomId = MpSession.FormatRoomId(roomAssign.RoomId),
                MembersCount = members.Length,
                MemberIds = members.Take(3).Select(peer => peer.PeerId).ToArray(),
                MembersTruncated = members.Length > 3,
                HostUid = members.FirstOrDefault(peer => peer.Role == WireRoomRole.Host)?.Uid
            });
        }

        return System.Text.Json.JsonSerializer.Serialize((object)action,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                IncludeFields = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
    }

    private static void LogActionReceived(this NetAction action)
    {
        var logStr = $"{MpManager.RoleTag} Received {action.ActionName()}{(ReceiveLogOnlyAction(action) ? "" : $": {action.ToLogString()}")}";
        LogAction(ReceiveLogLevel(action), logStr);
    }

    private static void LogActionSend(this NetAction action)
    {
        var logStr = $"{MpManager.RoleTag} Send {action.ActionName()}{(SendLogOnlyAction(action) ? "" : $": {action.ToLogString()}")}";
        LogAction(SendLogLevel(action), logStr);
    }

    private static LogLevel ReceiveLogLevel(NetAction action) => action switch
    {
        PingAction or PongAction or DayMoveSyncAction or NightMoveSyncAction => LogLevel.Debug,
        HelloAction or HelloAckAction or RoomAssignAction or MessageAction or PlayerPresenceAction or PeerLeaveAction or BuffAction => LogLevel.Message,
        RejectAction => LogLevel.Warning,
        _ => LogLevel.Info,
    };

    private static LogLevel SendLogLevel(NetAction action) => action switch
    {
        PingAction or PongAction or DayMoveSyncAction or NightMoveSyncAction => LogLevel.Debug,
        HelloAction or MessageAction or BuffAction => LogLevel.Message,
        _ => LogLevel.Info,
    };

    private static bool ReceiveLogOnlyAction(NetAction action) =>
        action is UpdatePrepAction or ExtractFoodAction or StoreFoodAction or StoreSellableAction;

    private static bool SendLogOnlyAction(NetAction action) =>
        action is UpdatePrepAction or ExtractFoodAction or StoreFoodAction or StoreSellableAction;

    private static void LogAction(LogLevel logLevel, string logStr)
    {
        switch (logLevel)
        {
            case LogLevel.Debug:
                LogDebug(logStr);
                break;
            case LogLevel.Warning:
                LogWarning(logStr);
                break;
            case LogLevel.Error:
                LogError(logStr);
                break;
            case LogLevel.Fatal:
                LogFatal(logStr);
                break;
            case LogLevel.Message:
                LogMessage(logStr);
                break;
            default:
                LogInfo(logStr);
                break;
        }
    }

    private static void LogDebug(string message) => Plugin.Instance?.Log.LogDebug(message);
    private static void LogInfo(string message) => Plugin.Instance?.Log.LogInfo(message);
    private static void LogMessage(string message) => Plugin.Instance?.Log.LogMessage(message);
    private static void LogWarning(string message) => Plugin.Instance?.Log.LogWarning(message);
    private static void LogError(string message) => Plugin.Instance?.Log.LogError(message);
    private static void LogFatal(string message) => Plugin.Instance?.Log.LogFatal(message);
}
