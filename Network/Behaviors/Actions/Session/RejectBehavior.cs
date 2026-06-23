using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class RejectBehavior
{
    /// <summary>
    /// 主机向指定客机发送拒绝消息，然后断开连接。
    /// </summary>
    public static void SendAndDisconnect(int uid, RejectReason reason, params string[] args)
    {
        // 先断开再发 Reject 会导致 DirectTcp 找不到 targetUid；Reject 应在断开前入队，
        // 且 DirectTcp 对找不到的 targetUid 不得广播（否则会误伤所有在线客机）。
        SendOnly(uid, reason, args);
        MpWire.DisconnectClient(uid, notify: false);
    }

    public static void SendOnly(int uid, RejectReason reason, params string[] args) =>
        new RejectAction { Reason = reason, Args = args, WireTargetUid = uid }.Enqueue();

    public static void BroadcastServerClosing() =>
        new RejectAction { Reason = RejectReason.ServerClosed }.Enqueue();

    private static TextId ToTextId(RejectReason reason) => reason switch
    {
        RejectReason.ModVersionMismatch => TextId.ModVersionMismatch,
        RejectReason.GameVersionMismatch => TextId.GameVersionMismatch,
        RejectReason.GameResourcesNotLoaded => TextId.GameResourcesNotLoaded,
        RejectReason.PrepWorkReconnectBlocked => TextId.PrepWorkReconnectBlocked,
        RejectReason.RoomFull => TextId.RoomFull,
        RejectReason.RoomIdExhausted => TextId.RoomIdExhausted,
        RejectReason.InvalidPlayerId => TextId.MpPlayerIdInvalid,
        RejectReason.DuplicatePeerId => TextId.DuplicatePeerId,
        RejectReason.UnsupportedServerMode => TextId.UnsupportedServerMode,
        RejectReason.RoomRequestUnsupported => TextId.RoomRequestUnsupported,
        RejectReason.RoomNotFound => TextId.RoomNotFound,
        RejectReason.KickedFromRoom => TextId.KickedFromRoom,
        RejectReason.KickedFromServer => TextId.KickedFromServer,
        RejectReason.ServerClosed => TextId.ServerClosed,
        _ => TextId.MpDisconnected,
    };

    internal static string FormatReason(RejectReason reason, string[] args) =>
        ToTextId(reason).Get(args);

    public static void ShowAndDisconnect(RejectReason reason, params string[] args)
    {
        var text = FormatReason(reason, args);
        Plugin.Instance?.Log.LogWarning($"Connection rejected: {text}");
        InGameConsole.ShowPassiveFromAnyThread(text);
        MpWire.DisconnectPeer();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<RejectAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(RejectAction action)
    {
        ShowAndDisconnect(action.Reason, action.Args);
    }
}
