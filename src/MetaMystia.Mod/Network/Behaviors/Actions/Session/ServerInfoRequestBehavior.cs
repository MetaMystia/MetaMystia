namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ServerInfoRequestBehavior
{
    public static void Send() =>
        new ServerInfoRequestAction
        {
            ClientGameVersion = Plugin.GameVersion,
            ClientModVersion = Plugin.ModVersion,
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ServerInfoRequestAction>(Handle,
            receiveScope: NetReceiveScope.EndpointOnly);
    }

    private static void Handle(ServerInfoRequestAction action)
    {
        ServerInfoReplyBehavior.Send(action.SenderUid, ServerMode.Direct, PlayerManager.PlayerTable.Count + 1);
    }
}
