using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Common.CharacterUtility;
using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia;

/// <summary>
/// 统一管理本地玩家和所有远程对端玩家
/// </summary>
[AutoLog]
public static partial class PlayerManager
{
    /// <summary>
    /// 本地玩家实例
    /// </summary>
    public static LocalPlayer Local { get; } = new();

    /// <summary>
    /// 所有已连接的远程玩家（key = uid）
    /// </summary>
    public static ConcurrentDictionary<int, PeerPlayer> Peers { get; } = new();

    /// <summary>
    /// 同服务器公共同步域内的远程玩家（不参与房间玩法流程）。
    /// </summary>
    public static ConcurrentDictionary<int, PeerPlayer> PublicPeers { get; } = new();

    /// <summary>
    /// 单一远程玩家表。Peers / PublicPeers 仅是按当前本地房间投影出的兼容索引。
    /// </summary>
    public static ConcurrentDictionary<int, PlayerRecord> PlayerTable { get; } = new();

    /// <summary>
    /// 当前对端玩家（1v1 便捷访问，返回第一个 Peer）
    /// 多人场景下，调用方应遍历 Peers 集合
    /// </summary>
    public static PeerPlayer Peer => Peers.Values.FirstOrDefault();

    public static bool TryGetVisiblePeer(int uid, out PeerPlayer peer)
    {
        peer = null;
        if (!PlayerTable.TryGetValue(uid, out var record)) return false;
        peer = record.Player;
        return peer != null;
    }

    public static bool TryGetRoomPeer(int uid, out PeerPlayer peer) =>
        Peers.TryGetValue(uid, out peer);

    public static bool TryGetRecord(int uid, out PlayerRecord record) =>
        PlayerTable.TryGetValue(uid, out record);

    public static bool IsSameRoom(ushort roomId) =>
        MpManager.Session.IsInRoom && roomId == MpManager.Session.RoomId;

    /// <summary>
    /// 根据 UID 获取对端玩家显示名（直播模式下为 UID-{uid}）
    /// </summary>
    public static string GetPeerName(int uid) =>
        LiveModeManager.GetDisplayName(uid);

    #region Local 便捷属性

    public static MapLabel LocalMapLabel => LocalPlayer.CurrentMapLabel;
    public static bool LocalIsSprinting { get => Local.IsSprinting; set => Local.IsSprinting = value; }
    public static Vector2 LocalInputDirection { get => Local.InputDirection; set => Local.InputDirection = value; }
    public static bool CharacterSpawnedAndInitialized => Local.CharacterSpawnedAndInitialized;
    public static bool LocalIsDayOver { get => Local.IsDayOver; set => Local.IsDayOver = value; }
    public static bool LocalIsPrepOver { get => Local.IsPrepOver; set => Local.IsPrepOver = value; }
    public static Vector2 LocalPosition => Local.Position;

    #endregion

    #region Peer 聚合属性

    /// <summary>
    /// 所有对端是否都已完成 Day（聚合判断）
    /// </summary>
    public static bool AllPeersDayOver =>
        Peers.Count > 0 && Peers.Values.All(p => p.IsDayOver);

    /// <summary>
    /// 所有对端是否都已完成 Prep（聚合判断）
    /// </summary>
    public static bool AllPeersPrepOver =>
        Peers.Count > 0 && Peers.Values.All(p => p.IsPrepOver);

    /// <summary>
    /// 全员（本地 + 所有对端）是否都已完成 Day
    /// </summary>
    public static bool AllDayOver => LocalIsDayOver && AllPeersDayOver;

    /// <summary>
    /// 全员（本地 + 所有对端）是否都已完成 Prep
    /// </summary>
    public static bool AllPrepOver => LocalIsPrepOver && AllPeersPrepOver;

    /// <summary>
    /// 所有对端是否都已选择了与指定地图/等级一致的居酒屋
    /// </summary>
    public static bool AllPeersSelectedSameIzakaya(MapLabel mapLabel, int level) =>
        Peers.Count > 0 && Peers.Values.All(p =>
            p.IzakayaMapLabel.IsSelected() && p.IzakayaLevel != 0
            && p.IzakayaMapLabel == mapLabel && p.IzakayaLevel == level);

    /// <summary>
    /// 是否所有对端都已做出选择（不论是否与本地一致）
    /// </summary>
    public static bool AllPeersHaveSelected =>
        Peers.Count > 0 && Peers.Values.All(p =>
            p.IzakayaMapLabel.IsSelected() && p.IzakayaLevel != 0);

    #endregion

    #region Per-Peer 状态修改（通过 SenderUid 定位）

    public static void SetPeerDayOver(int uid)
    {
        if (Peers.TryGetValue(uid, out var peer))
            peer.IsDayOver = true;
        else
            Log.LogWarning($"SetPeerDayOver: peer uid={uid} not found");
    }

    public static void SetPeerPrepOver(int uid)
    {
        if (Peers.TryGetValue(uid, out var peer))
            peer.IsPrepOver = true;
        else
            Log.LogWarning($"SetPeerPrepOver: peer uid={uid} not found");
    }

    public static void SetPeerIzakayaSelection(int uid, MapLabel mapLabel, int level)
    {
        if (Peers.TryGetValue(uid, out var peer))
        {
            peer.IzakayaMapLabel = mapLabel;
            peer.IzakayaLevel = level;
        }
        else
            Log.LogWarning($"SetPeerIzakayaSelection: peer uid={uid} not found");
    }

    /// <summary>
    /// 获取选择不一致的首个 Peer 的选择描述（用于通知），无不一致则返回 null
    /// </summary>
    public static string GetFirstMismatchSelection(MapLabel mapLabel, int level)
    {
        foreach (var peer in Peers.Values)
        {
            if (!peer.IzakayaMapLabel.IsSelected() || peer.IzakayaLevel == 0)
                return $"{LiveModeManager.GetDisplayName(peer.Uid)}: {TextId.PeerIzakayaNotSelected.Get()}";
            if (peer.IzakayaMapLabel != mapLabel || peer.IzakayaLevel != level)
                return $"{LiveModeManager.GetDisplayName(peer.Uid)}: {peer.IzakayaMapLabel.FormatIzakayaSelection(peer.IzakayaLevel)}";
        }
        return null;
    }

    #endregion

    #region 资源可用性聚合判断（所有玩家都拥有该资源才视为可用）

    public static bool FoodAvailable(int id) =>
        Local.DataBase.FoodAvailable(id) && Peers.Values.All(p => p.DataBase.FoodAvailable(id));

    public static bool RecipeAvailable(int id) =>
        Local.DataBase.RecipeAvailable(id) && Peers.Values.All(p => p.DataBase.RecipeAvailable(id));

    public static bool BeverageAvailable(int id) =>
        Local.DataBase.BeverageAvailable(id) && Peers.Values.All(p => p.DataBase.BeverageAvailable(id));

    public static bool IngredientAvailable(int id) =>
        Local.DataBase.IngredientAvailable(id) && Peers.Values.All(p => p.DataBase.IngredientAvailable(id));

    public static bool CookerAvailable(int id) =>
        Local.DataBase.CookerAvailable(id) && Peers.Values.All(p => p.DataBase.CookerAvailable(id));

    public static bool ItemAvailable(int id) =>
        Local.DataBase.ItemAvailable(id) && Peers.Values.All(p => p.DataBase.ItemAvailable(id));

    public static bool IzakayaAvailable(int id) =>
        Local.DataBase.IzakayaAvailable(id) && Peers.Values.All(p => p.DataBase.IzakayaAvailable(id));

    public static bool NormalGuestAvailable(int id) =>
        Local.DataBase.NormalGuestAvailable(id) && Peers.Values.All(p => p.DataBase.NormalGuestAvailable(id));

    public static bool SpecialGuestAvailable(int id) =>
        Local.DataBase.SpecialGuestAvailable(id) && Peers.Values.All(p => p.DataBase.SpecialGuestAvailable(id));

    #endregion

    #region 生命周期

    public static void StartCoroutines()
    {
        PluginManager.Instance.StartManagedCoroutine(MoveSyncLoop());
    }

    private static IEnumerator MoveSyncLoop()
    {
        var wait = new WaitForSeconds(2f);
        while (true)
        {
            MoveSyncBehavior.Send();
            yield return wait;
        }
    }

    /// <summary>
    /// 重置所有玩家的同步状态（IsDayOver、IsPrepOver、IzakayaSelection 等）。
    /// 在 Prep 结束 / Work 开始 / 联机初始化时调用，避免后进场景的玩家覆盖先进场景玩家已提交的状态。
    /// </summary>
    public static void ResetState()
    {
        Local.ResetState();
        foreach (var peer in Peers.Values)
            peer.ResetState();
        Log.LogInfo($"PlayerManager state reset (peers: {Peers.Count})");
    }
    
    public static void SpawnPeersForCurrentScene(IEnumerable<PeerPlayer> peers = null)
    {
        if (MpManager.LocalScene is not Common.UI.Scene.DayScene and not Common.UI.Scene.WorkScene)
            return;

        var collection = Common.SceneDirector.Instance?.characterCollection;
        if (collection == null || !collection.TryGetValue("Self", out var localUnit) || localUnit == null)
            return;

        foreach (var peer in (peers ?? GetSpawnCandidates()).Where(p => p != null && IsSpawnCandidate(p.Uid)))
        {
            peer.ResetMotion();
            peer.SpawnAtFarPosition();
            peer.PostSpawnSetupForCurrentScene();
        }

        UI.FloatingTextHelper.SetPlayerLabel(
            Local.Uid, LiveModeManager.GetDisplayName(Local.Uid), localUnit.transform);
    }

    private static IEnumerable<PeerPlayer> GetSpawnCandidates()
    {
        return MpManager.LocalScene switch
        {
            Common.UI.Scene.DayScene => PlayerTable.Values
                .Where(record => record.Scene == WireScene.DayScene)
                .Select(record => record.Player)
                .Where(peer => peer != null),
            Common.UI.Scene.WorkScene => Peers.Values,
            _ => []
        };
    }

    private static bool IsSpawnCandidate(int uid)
    {
        return MpManager.LocalScene switch
        {
            Common.UI.Scene.DayScene => PlayerTable.ContainsKey(uid),
            Common.UI.Scene.WorkScene => Peers.ContainsKey(uid),
            _ => false
        };
    }

    public static void TryEnsureDayScenePeer(int uid)
    {
        if (uid == Local.Uid || MpManager.LocalScene != Common.UI.Scene.DayScene)
            return;
        if (!TryGetRecord(uid, out var record) || record.Scene != WireScene.DayScene)
            return;
        if (!TryGetVisiblePeer(uid, out var peer))
            return;
        SpawnPeersForCurrentScene(new[] { peer });
    }

    /// <summary>
    /// 非 DayScene/WorkScene：销毁所有对端角色（保留 PlayerTable 记录）。
    /// </summary>
    public static void DespawnAllPeers()
    {
        foreach (var record in PlayerTable.Values)
            record.Player?.DespawnCharacter();
        UI.FloatingTextHelper.ClearAllLabels();
        Log.LogInfo("PlayerManager all peer characters despawned");
    }

    /// <summary>
    /// 检查指定 PeerId 是否已有在线连接
    /// </summary>
    public static bool IsPeerIdOnline(string peerId)
    {
        foreach (var kvp in PlayerTable)
        {
            if (string.Equals(kvp.Value.PeerId, peerId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 握手成功后，根据对端 UID 创建并注册 PeerPlayer
    /// </summary>
    public static PeerPlayer AddPeer(PlayerInfoData info)
    {
        return UpsertRoomMember(new RoomMember
        {
            Uid = info.Uid,
            PeerId = info.PeerId,
            Role = info.Uid == MpManager.Session.HostUid ? WireRoomRole.Host : WireRoomRole.Client,
            Scene = MpManager.LocalScene.ToWire(),
            Skin = info.Skin,
            Resources = info.IncrementalDataBase,
        }, MpManager.Session.RoomId);
    }

    public static PeerPlayer AddPublicPeer(PlayerInfoData info)
    {
        return UpsertPresence(new PlayerPresenceAction
        {
            Uid = info.Uid,
            PeerId = info.PeerId,
            RoomId = MpConstants.PublicRoomId,
            Scene = MpManager.LocalScene.ToWire(),
            Skin = info.Skin,
        });
    }

    public static void LoadSummaries(PlayerSummary[] summaries)
    {
        ClearPeers();
        if (summaries == null) return;
        foreach (var summary in summaries)
            UpsertSummary(summary);

        SpawnPeersForCurrentScene();
    }

    public static PeerPlayer UpsertSummary(PlayerSummary summary)
    {
        if (summary == null || summary.Uid == Local.Uid) return null;
        var record = GetOrCreateRecord(summary.Uid);
        record.PeerId = summary.PeerId ?? "";
        record.RoomId = summary.RoomId;
        record.Scene = summary.Scene;
        record.Skin = summary.Skin ?? new PlayerSkinData();
        record.Role = summary.Role;
        record.Player = CreateOrUpdatePeer(record, null);
        RefreshIndexesFor(record);
        return record.Player;
    }

    public static PeerPlayer UpsertPresence(PlayerPresenceAction presence)
    {
        if (presence == null || presence.Uid == Local.Uid) return null;
        var record = GetOrCreateRecord(presence.Uid);
        bool wasInRoom = Peers.ContainsKey(presence.Uid);
        bool roomChanged = record.RoomId != presence.RoomId;
        record.PeerId = presence.PeerId ?? "";
        record.RoomId = presence.RoomId;
        record.Scene = presence.Scene;
        record.Skin = presence.Skin ?? new PlayerSkinData();
        if (roomChanged)
            record.Resources = null;
        if (presence.RoomId == MpConstants.PublicRoomId)
            record.Role = WireRoomRole.None;
        record.Player = CreateOrUpdatePeer(record, null);
        RefreshIndexesFor(record);
        if (wasInRoom && !IsSameRoom(record.RoomId))
            HidePeer(presence.Uid);
        return record.Player;
    }

    public static PeerPlayer UpsertRoomMember(RoomMember member, ushort roomId)
    {
        if (member == null || member.Uid == Local.Uid) return null;
        var record = GetOrCreateRecord(member.Uid);
        record.PeerId = member.PeerId ?? "";
        record.RoomId = roomId;
        record.Scene = member.Scene;
        record.Skin = member.Skin ?? new PlayerSkinData();
        record.Resources = member.Resources;
        record.Role = member.Role;
        record.Player = CreateOrUpdatePeer(record, member.Resources);
        RefreshIndexesFor(record);
        Log.LogMessage($"Upserted room peer '{record.PeerId}' (uid={record.Uid}, room={MpSession.FormatRoomId(record.RoomId)}, characterId='{record.Player.CharacterId}')");
        return record.Player;
    }

    public static PlayerSummary LocalSummary(WireRoomRole role)
    {
        return new PlayerSummary
        {
            Uid = Local.Uid,
            PeerId = Local.Id,
            RoomId = MpManager.Session.RoomId,
            Scene = MpManager.LocalScene.ToWire(),
            Skin = Local.Skin,
            Role = role,
        };
    }

    public static PlayerSummary SummaryFromPeer(NetPlayer player, ushort roomId, WireRoomRole role, WireScene scene)
    {
        return new PlayerSummary
        {
            Uid = player.Uid,
            PeerId = player.Id,
            RoomId = roomId,
            Scene = scene,
            Skin = player.Skin,
            Role = role,
        };
    }

    public static RoomMember RoomMemberFromPeer(NetPlayer player, WireRoomRole role, WireScene scene)
    {
        return new RoomMember
        {
            Uid = player.Uid,
            PeerId = player.Id,
            Role = role,
            Scene = scene,
            Skin = player.Skin,
            Resources = player.IncrementalDataBase,
        };
    }

    public static RoomMember LocalRoomMember(WireRoomRole role)
    {
        return new RoomMember
        {
            Uid = Local.Uid,
            PeerId = Local.Id,
            Role = role,
            Scene = MpManager.LocalScene.ToWire(),
            Skin = Local.Skin,
            Resources = Local.IncrementalDataBase,
        };
    }

    private static PlayerRecord GetOrCreateRecord(int uid) =>
        PlayerTable.GetOrAdd(uid, static key => new PlayerRecord { Uid = key });

    private static PeerPlayer CreateOrUpdatePeer(PlayerRecord record, ResourceDataBaseData resources)
    {
        var peer = record.Player;
        if (peer == null)
        {
            peer = new PeerPlayer(record.Uid, resources) { Id = record.PeerId };
            peer.ResetState();
            peer.ResetMotion();
        }
        else
        {
            peer.Id = record.PeerId;
            if (resources != null)
            {
                peer.IncrementalDataBase = resources;
                peer.DataBase = ResourceDataBaseData.Expand(resources);
            }
        }
        if (record.Skin != null) peer.Skin = record.Skin;
        return peer;
    }

    private static void RefreshIndexesFor(PlayerRecord record)
    {
        if (record.Player == null) return;

        PublicPeers.TryRemove(record.Uid, out _);
        Peers.TryRemove(record.Uid, out _);

        if (IsSameRoom(record.RoomId) && record.HasResources)
            Peers[record.Uid] = record.Player;
        else
            PublicPeers[record.Uid] = record.Player;
    }

    /// <summary>
    /// 从游戏中获取实际皮肤数据，并在角色就绪后应用皮肤
    /// </summary>
    public static void InitLocalSkin()
    {
        Local.InitSkin();
        // 场景切换后角色会被重建，需要在 unit 就绪后重新应用皮肤
        SgrYuki.CommandScheduler.Enqueue(
            executeWhen: () => Local.unit != null,
            execute: () => Local.UpdateCharacterSprite(),
            timeoutSeconds: 30
        );
    }

    /// <summary>
    /// 刷新 NightScene 中的角色立绘（通过重新触发 SetupPortrayalVisual 前缀钩子）
    /// </summary>
    public static void RefreshPortrait(bool skipSceneCheck = false)
    {
        if (!skipSceneCheck && MpManager.LocalScene != Common.UI.Scene.WorkScene) return;
        var uiManager = NightScene.UI.UIManager.Instance;
        if (uiManager != null)
        {
            var actual = GameData.RunTime.Common.RunTimeAlbum.UseCurrentSkinAtNight;
            GameData.RunTime.Common.RunTimeAlbum.UseCurrentSkinAtNight = true;
            uiManager.InitializePlayerPortrayal();
            GameData.RunTime.Common.RunTimeAlbum.UseCurrentSkinAtNight = actual;
        }
    }

    /// <summary>
    /// 销毁指定对端玩家的角色、取消 pending spawn，并移除头顶标签。
    /// 在移除 peer 之前调用，避免留下"幽灵"角色。
    /// </summary>
    public static void HidePeer(int uid)
    {
        if (PlayerTable.TryGetValue(uid, out var record))
        {
            record.Player?.DespawnCharacter();
        }
        else if (Peers.TryGetValue(uid, out var peer) || PublicPeers.TryGetValue(uid, out peer))
            peer.DespawnCharacter();
        UI.FloatingTextHelper.RemovePlayerLabel(uid);
    }

    /// <summary>
    /// 移除一个对端玩家（先销毁角色和标签）
    /// </summary>
    public static bool RemovePeer(int uid)
    {
        HidePeer(uid);
        Peers.TryRemove(uid, out var roomPeer);
        PublicPeers.TryRemove(uid, out var publicPeer);
        if (PlayerTable.TryRemove(uid, out var record))
        {
            Log.LogMessage($"Removed peer '{record.PeerId}' (uid={uid})");
            return true;
        }
        return roomPeer != null || publicPeer != null;
    }

    /// <summary>
    /// 清除所有对端玩家（先销毁所有角色并取消 pending spawn，断开连接时调用）
    /// </summary>
    public static void ClearPeers()
    {
        foreach (var peer in Peers.Values)
            peer.DespawnCharacter();
        foreach (var peer in PublicPeers.Values)
            peer.DespawnCharacter();
        UI.FloatingTextHelper.ClearAllLabels();
        PlayerTable.Clear();
        Peers.Clear();
        PublicPeers.Clear();
        Log.LogMessage($"All peers cleared");
    }

    /// <summary>
    /// 清除房间内对端玩家（销毁角色、取消 pending spawn，并移除标签）
    /// </summary>
    public static void ClearRoomPeers()
    {
        foreach (var kvp in Peers)
        {
            kvp.Value.DespawnCharacter();
            UI.FloatingTextHelper.RemovePlayerLabel(kvp.Key);
            if (PlayerTable.TryGetValue(kvp.Key, out var record))
                record.Resources = null;
        }
        Peers.Clear();
        RebuildIndexes();
        Log.LogMessage("Room peers cleared");
    }

    public static void SyncRoomPeersBeforeAssign(ushort roomId, IEnumerable<int> memberUids)
    {
        var incoming = memberUids ?? [];
        var stalePeers = Peers.Keys.Except(incoming).ToList();

        foreach (var uid in stalePeers)
        {
            Peers[uid].DespawnCharacter();
            UI.FloatingTextHelper.RemovePlayerLabel(uid);
            if (PlayerTable.TryGetValue(uid, out var record) && record.RoomId == roomId)
                record.Resources = null;
        }
        RebuildIndexes();
        Log.LogMessage($"Room peer roster synced (room={MpSession.FormatRoomId(roomId)}, members={Peers.Count})");
    }

    public static void RebuildIndexes()
    {
        Peers.Clear();
        PublicPeers.Clear();
        foreach (var record in PlayerTable.Values)
            RefreshIndexesFor(record);
    }

    #endregion

    #region FixedUpdate

    /// <summary>
    /// 在 FixedUpdate 中为所有 Peer 执行位置修正。
    /// 房间内 Peers 与公域 PublicPeers 都参与；同一 uid 不会同时存在于两个集合。
    /// </summary>
    public static void OnFixedUpdate()
    {
        foreach (var peer in Peers.Values)
            peer.OnFixedUpdate();
        foreach (var peer in PublicPeers.Values)
            peer.OnFixedUpdate();
    }

    #endregion

    #region Peer 静态便捷方法（1v1 兼容，委托给 Peer）

    [OnMainThread]
    public static void EnablePeerCollision(CharacterControllerUnit unit, bool enable = true)
    {
        unit?.UpdateColliderStatus(enable);
        if (unit?.rb2d != null)
            unit.rb2d.isKinematic = !enable;
        Log.Info($"set collision for {unit?.name} to {enable}");
    }

    [OnMainThread]
    public static void EnablePeerCollision(bool enable = true) =>
        EnablePeerCollision(Peer?.GetCharacterUnit(), enable);

    public static bool IsMetaMystiaPlayer(string label) =>
        PlayerTable.Values.Any(record => record.Player?.CharacterId == label);

    #endregion
}
