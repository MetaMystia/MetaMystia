using System.Collections.Generic;
using System.Linq;

using Common.UI;

using MetaMystia.Network;
using MetaMystia.Network.Core;

namespace MetaMystia;

// 不保存 Unity 对象，也不从资源、场景或角色的存在推断成员身份。
public static class ModPlayerStore
{
    public sealed record PlayerData
    {
        public AppearanceSnapshot Appearance { get; init; }
        public PresenceSnapshot Presence { get; init; } = new(Scene.EmptyScene, 0, MapLabel.Unknown, null);
        public MotionSnapshot NightMotion { get; init; }
        public ResourceDataBase Resources { get; init; }
        internal long MembershipId { get; init; }
    }

    private static readonly Dictionary<int, PlayerData> Players = new();
    private static RoomBinding _binding;
    public static PlayerData Get(int uid) => Players.GetValueOrDefault(uid);

    public static void ReconcileSession()
    {
        var session = MpWire.Session;
        foreach (int uid in Players.Keys.Where(uid => !session.Players.ContainsKey(uid)).ToArray()) Players.Remove(uid);
        foreach (int uid in session.Players.Keys) Players.TryAdd(uid, new());
        foreach (var pair in Players.ToArray())
        {
            long membership = session.Room?.Members.GetValueOrDefault(pair.Key)?.MembershipId ?? 0;
            if (_binding != session.Binding || membership != pair.Value.MembershipId)
            {
                Players[pair.Key] = pair.Value with { Resources = null, NightMotion = null, MembershipId = membership };
            }
        }
        _binding = session.Binding;
    }

    public static void ApplyAppearance(int uid, AppearanceSnapshot snapshot)
    {
        if (snapshot != null && Players.TryGetValue(uid, out var player)) Players[uid] = player with { Appearance = snapshot };
    }

    public static void ApplyScene(int uid, Scene scene, long epoch)
    {
        if (!Players.TryGetValue(uid, out var player) || epoch < player.Presence.SceneEpoch) return;
        if (epoch == player.Presence.SceneEpoch && scene == player.Presence.Scene) return;
        Players[uid] = player with { Presence = new(scene, epoch, MapLabel.Unknown, null) };
    }

    public static void ApplyDayMotion(int uid, long epoch, MapLabel map, MotionSnapshot motion)
    {
        if (!Players.TryGetValue(uid, out var player) || epoch < player.Presence.SceneEpoch || !ValidMotion(motion)) return;
        Players[uid] = player with { Presence = new(Scene.DayScene, epoch, map, motion) };
    }

    public static void ApplyNightMotion(int uid, MotionSnapshot motion)
    {
        if (Players.TryGetValue(uid, out var player) && ValidMotion(motion)) Players[uid] = player with { NightMotion = motion };
    }

    public static void ClearNightMotion()
    {
        foreach (var pair in Players.ToArray()) Players[pair.Key] = pair.Value with { NightMotion = null };
    }

    public static void ApplyResources(int uid, ResourceManifest manifest)
    {
        if (!Players.TryGetValue(uid, out var player) || player.MembershipId == 0 || !ResourceDataBase.ValidManifest(manifest)) return;
        Players[uid] = player with { Resources = ResourceDataBase.FromManifest(manifest) };
    }

    public static bool AllResourcesReceived => MpWire.Session.Room != null && MpWire.Session.Room.Members.Keys
        .All(uid => uid == MpWire.Session.SelfUid ? PlayerManager.Local.DataBase.IsLoaded : Get(uid)?.Resources != null);

    private static bool ValidMotion(MotionSnapshot motion) => motion != null && float.IsFinite(motion.X) && float.IsFinite(motion.Y)
        && float.IsFinite(motion.Vx) && float.IsFinite(motion.Vy) && float.IsFinite(motion.Speed) && motion.Speed is >= 0 and <= 100;
}
