namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ServerInfoReplyBehavior
{
    public static void Send(int targetUid, ServerMode serverMode, int onlineCount) =>
        new ServerInfoReplyAction
        {
            GameVersion = Plugin.GameVersion,
            ModVersion = Plugin.ModVersion,
            ServerMode = serverMode,
            MaxPlayers = ConfigManager.MaxPlayers.Value,
            OnlineCount = onlineCount,
            WireTargetUid = targetUid,
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ServerInfoReplyAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(ServerInfoReplyAction action)
    {
        if (action.ModVersion != Plugin.ModVersion)
        {
            RejectBehavior.ShowAndDisconnect(RejectReason.ModVersionMismatch);
            return;
        }

        if (action.GameVersion != Plugin.GameVersion)
        {
            RejectBehavior.ShowAndDisconnect(RejectReason.GameVersionMismatch);
            return;
        }

        switch (action.ServerMode)
        {
            case ServerMode.Direct:
                HelloBehavior.Send();
                break;
            case ServerMode.Relay:
                MpWire.Session.BeginConnecting(TransportKind.RelayClient);
                PlayerManager.Local.RoomId = MpConstants.PublicRoomId;
                PlayerManager.Local.Role = WireRoomRole.None;
                HelloBehavior.Send();
                break;
            default:
                RejectBehavior.ShowAndDisconnect(RejectReason.UnsupportedServerMode);
                break;
        }
    }
}
