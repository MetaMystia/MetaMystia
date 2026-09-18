using System.Linq;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Actions;
using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia;

public static partial class PlayerManager
{
    public static void ApplyNetworkState(Snapshot state, bool roomChanged)
    {
        var members = state.Room?.Members.ToDictionary(p => p.Uid) ?? new();
        var world = state.World.Select(p => p.Uid).ToHashSet();
        foreach (var uid in Peers.Keys.Concat(PublicPeers.Keys).Where(uid => !world.Contains(uid)).ToArray())
            RemovePeer(uid);

        foreach (var player in state.World)
        {
            if (player.Uid == Local.Uid) continue;
            bool inRoom = members.TryGetValue(player.Uid, out var member);
            var data = member ?? player;
            bool wasInRoom = Peers.ContainsKey(player.Uid);
            if (!TryGetVisiblePeer(player.Uid, out var peer))
                peer = new PeerPlayer(player.Uid, data.Resources ?? new());

            bool joined = inRoom && (!wasInRoom || roomChanged || peer.NetworkState?.Membership != data.Membership);
            if (joined || (wasInRoom && (!inRoom || roomChanged))) peer.ResetState();
            if (joined)
            {
                peer.IncrementalDataBase = data.Resources ?? new();
                peer.DataBase = peer.IncrementalDataBase.Expand();
            }
            Peers.TryRemove(player.Uid, out _);
            PublicPeers.TryRemove(player.Uid, out _);
            (inRoom ? Peers : PublicPeers)[player.Uid] = peer;
            if (wasInRoom && !inRoom) DayDestinationManager.OnPeerLeft(player.Uid);
            peer.ApplyState(data);
            if (joined && GameSession.IsRoomHost) RoomInitialStateAction.Send(player.Uid);
        }
        RefreshLocalLabel();
    }

    public static void RefreshCharacters()
    {
        foreach (var peer in Peers.Values) peer.RefreshCharacter();
        foreach (var peer in PublicPeers.Values) peer.RefreshCharacter();
        RefreshLocalLabel();
    }

    private static void RefreshLocalLabel()
    {
        if (GameSession.IsOnline && GameFlow.CharactersReady && Local.unit != null)
            FloatingTextHelper.SetPlayerLabel(Local.Uid, LiveModeManager.GetDisplayName(Local.Uid), Local.unit.transform);
    }

    public static void OnSceneUnloading()
    {
        foreach (var peer in Peers.Values) peer.ReleaseCharacter(sceneUnloading: true);
        foreach (var peer in PublicPeers.Values) peer.ReleaseCharacter(sceneUnloading: true);
        FloatingTextHelper.ForgetPlayerLabels();
    }
}
