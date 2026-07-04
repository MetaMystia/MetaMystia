using System.Linq;
using MetaMystia.UI;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class HelloAckBehavior
{
    /// <summary>服务端端点向指定客机确认控制面连接。</summary>
    public static void Send(int clientUid)
    {
        if (!MpManager.IsRoomHost) return;

        new HelloAckAction
        {
            AssignedUid = clientUid,
            Players = PlayerManager.RoomPlayers.Select(player => player.ToLiteData()).ToArray(),
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
        PlayerManager.LoadLitePlayers(action.Players);
        Plugin.Instance?.Log.LogMessage($"Assigned UID: {action.AssignedUid}");
        if (MpWire.Session.TransportKind == TransportKind.RelayClient)
        {
            MpWire.Session.EnterRelayPublic();
            PlayerManager.Local.RoomId = MpConstants.PublicRoomId;
            PlayerManager.Local.Role = WireRoomRole.None;
            MpWire.OnRelayPublicEntered();
            InGameConsole.ShowPassiveFromAnyThread(
                TextId.MultiplayerPublicConnected.Get());
        }
    }
}
