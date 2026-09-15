using System.Collections.Generic;
using System.Linq;

using Common.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Actions;
using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia;

public static partial class PlayerManager
{
    private static readonly Dictionary<int, Player> networkPlayers = new();

    public static void ApplyNetworkState(Snapshot state)
    {
        var roomMembers = state.Room?.Members.ToDictionary(p => p.Uid) ?? new();
        var world = state.World.Select(p => p.Uid).ToHashSet();
        foreach (var uid in Peers.Keys.Concat(PublicPeers.Keys).Where(uid => !world.Contains(uid)).ToArray())
        {
            RemovePeer(uid);
            networkPlayers.Remove(uid);
        }

        foreach (var player in state.World)
        {
            if (player.Uid == Local.Uid) continue;
            bool inRoom = roomMembers.TryGetValue(player.Uid, out var member);
            var data = member ?? player;
            networkPlayers.TryGetValue(player.Uid, out var old);
            networkPlayers[player.Uid] = data;
            bool joined = inRoom && !Peers.ContainsKey(player.Uid);
            bool left = !inRoom && Peers.ContainsKey(player.Uid);
            bool created = !TryGetVisiblePeer(player.Uid, out var peer);
            if (created)
                peer = new PeerPlayer(player.Uid, ResourceDataBase.FromNetwork(data.Resources));
            if (joined || left)
            {
                peer.DespawnCharacter();
                peer.ResetState();
                if (joined)
                {
                    peer.IncrementalDataBase = ResourceDataBase.FromNetwork(data.Resources);
                    peer.DataBase = ResourceDataBase.Expand(peer.IncrementalDataBase);
                }
            }
            Peers.TryRemove(player.Uid, out _);
            PublicPeers.TryRemove(player.Uid, out _);
            (inRoom ? Peers : PublicPeers)[player.Uid] = peer;
            if (left) DayDestinationManager.OnPeerLeft(player.Uid);
            peer.Id = player.Name;
            peer.Scene = player.Scene;
            peer.HasMotion = data.HasMotion;
            if (created || old?.Skin != player.Skin)
            {
                peer.Skin = PlayerProfile.ReadSkin(player.Skin);
                peer.UpdateCharacterSprite();
            }
            if (!peer.CanRender) peer.DespawnCharacter();
            else if (data.HasMotion && (created || old?.Motion != data.Motion || old.HasMotion != data.HasMotion || joined || left))
            {
                if (peer.unit == null) peer.SpawnForScene();
                else ApplyLatestMotion(peer);
            }
            if (peer.unit != null) FloatingTextHelper.SetPlayerLabel(peer.Uid, LiveModeManager.GetDisplayName(peer.Uid), peer.unit.transform);
            if (joined && GameSession.IsRoomHost)
                RoomReadyAction.Send(player.Uid);
        }
    }

    public static void ApplyLatestMotion(PeerPlayer peer)
    {
        if (!peer.CanRender || !networkPlayers.TryGetValue(peer.Uid, out var player) || !player.HasMotion) return;
        var motion = player.Motion;
        if (GameFlow.LocalScene == Scene.DayScene)
            peer.SyncFromPeer((MapLabel)motion.Map, motion.Sprinting, motion.Speed,
                new(motion.DirectionX, motion.DirectionY), new(motion.X, motion.Y));
        else peer.NightSyncFromPeer(motion.Speed, new(motion.DirectionX, motion.DirectionY), new(motion.X, motion.Y));
    }
}
