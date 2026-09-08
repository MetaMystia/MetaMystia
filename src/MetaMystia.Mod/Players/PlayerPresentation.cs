using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Common.CharacterUtility;
using Common.UI;

using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia;

// 对照“应当出现的角色”与现有角色进行调整。销毁角色不删除玩家数据。
public static class PlayerPresentation
{
    private static readonly Dictionary<int, PeerPlayer> Players = new();
    private static readonly Dictionary<int, AppearanceSnapshot> Rendered = new();
    public static IReadOnlyDictionary<int, PeerPlayer> Avatars { get; } = new ReadOnlyDictionary<int, PeerPlayer>(Players);
    private static CharacterControllerUnit _localUnit;

    public static void Reconcile()
    {
        var session = MpWire.Session;
        foreach (int uid in Players.Keys.Where(uid => !session.Players.ContainsKey(uid) || uid == session.SelfUid).ToArray())
        {
            Players[uid].DespawnCharacter();
            Players.Remove(uid);
            Rendered.Remove(uid);
        }
        foreach (int uid in session.Players.Keys.Where(uid => uid != session.SelfUid))
            if (!Players.ContainsKey(uid)) Players.Add(uid, new PeerPlayer(uid));
        bool ready = PlayerManager.CharacterSpawnedAndInitialized && (MpManager.LocalScene == Scene.DayScene
            ? DayScene.SceneManager.Instance?.CurrentActiveMap?.height != null
            : MpManager.LocalScene == Scene.WorkScene && NightScene.MapManager.Instance?.height != null);
        foreach (var pair in Players)
        {
            var peer = pair.Value;
            var data = ModPlayerStore.Get(pair.Key);
            MotionSnapshot motion = null;
            if (ready && data != null)
            {
                if (MpManager.LocalScene == Scene.DayScene && data.Presence.Scene == Scene.DayScene && data.Presence.Map == LocalPlayer.CurrentMapLabel)
                    motion = data.Presence.Motion;
                else if (MpManager.LocalScene == Scene.WorkScene && data.Presence.Scene == Scene.WorkScene
                    && RoomGameplay.IsReady && RoomGameplay.Phase == GameplayPhase.Night
                    && session.Room?.Members.ContainsKey(pair.Key) == true) motion = data.NightMotion;
            }
            if (motion == null)
            {
                if (peer.unit != null) peer.DespawnCharacter();
                Rendered.Remove(pair.Key);
                continue;
            }
            if (peer.unit == null) { peer.Spawn(motion); Rendered.Remove(pair.Key); }
            peer.Apply(motion);
            if (data.Appearance != null && (!Rendered.TryGetValue(pair.Key, out var appearance) || appearance != data.Appearance))
            {
                peer.UpdateCharacterSprite();
                Rendered[pair.Key] = data.Appearance;
            }
            FloatingTextHelper.UpdatePlayerLabel(pair.Key, LiveModeManager.GetDisplayName(pair.Key));
        }
        if (!session.IsOnline)
        {
            FloatingTextHelper.ClearAllLabels();
            _localUnit = null;
        }
        else if (ready && PlayerManager.Local.unit != _localUnit)
        {
            _localUnit = PlayerManager.Local.unit;
            PlayerManager.Local.UpdateCharacterSprite();
            FloatingTextHelper.SetPlayerLabel(session.SelfUid, LiveModeManager.GetDisplayName(session.SelfUid), _localUnit.transform);
        }
    }

    public static void FixedUpdate()
    {
        if (!MpWire.Session.IsOnline || MpManager.InStory) return;
        foreach (var player in Players.Values) player.OnFixedUpdate();
    }
}
