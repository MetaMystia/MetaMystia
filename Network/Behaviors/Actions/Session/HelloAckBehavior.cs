using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class HelloAckBehavior
{
    /// <summary>服务端端点向指定客机确认控制面连接。</summary>
    public static void Send(int clientUid)
    {
        if (!MpManager.IsRoomHost) return;

        var players = new System.Collections.Generic.List<PlayerSummary>
        {
            PlayerManager.SummaryFromPeer(
                PlayerManager.Local,
                MpConstants.DirectRoomId,
                WireRoomRole.Host,
                MpManager.LocalScene.ToWire())
        };
        foreach (var peer in PlayerManager.Peers)
        {
            ushort roomId = peer.Uid == clientUid ? MpConstants.PublicRoomId : MpConstants.DirectRoomId;
            var role = peer.Uid == clientUid ? WireRoomRole.None : WireRoomRole.Client;
            players.Add(PlayerManager.SummaryFromPeer(peer, roomId, role, peer.Scene.ToWire()));
        }

        new HelloAckAction
        {
            AssignedUid = clientUid,
            Players = players.ToArray(),
            WireTargetUid = clientUid,
        }.Enqueue();
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<HelloAckAction>(Handle,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(HelloAckAction action)
    {
        PlayerManager.Local.Uid = action.AssignedUid;
        PlayerManager.LoadSummaries(action.Players);
        Plugin.Instance?.Log.LogMessage($"Assigned UID: {action.AssignedUid}");
        if (MpWire.Session.TransportKind == TransportKind.RelayClient)
        {
            MpWire.Session.EnterRelayPublic();
            MpWire.OnRelayPublicEntered();
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.MultiplayerPublicConnected.Get());
        }
    }
}
