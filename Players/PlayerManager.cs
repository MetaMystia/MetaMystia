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

    #region 玩家表

    /// <summary>
    /// 全程玩家全表
    /// </summary>
    public static ConcurrentDictionary<int, PeerPlayer> PlayerTable { get; } = new();

    /// <summary>
    /// 同房间玩家投影视图，与 PublicPeers 按互斥
    /// </summary>
    public static IEnumerable<PeerPlayer> Peers => PlayerTable.Values.Where(InRoomScope);

    /// <summary>
    /// 公共同步域内玩家投影视图，与 Peers 按互斥
    /// </summary>
    public static IEnumerable<PeerPlayer> PublicPeers => PlayerTable.Values.Where(p => !InRoomScope(p));

    /// <summary>
    /// 当前对端玩家（1v1 便捷访问，返回第一个 Peer）
    /// 多人场景下，调用方应遍历 Peers 集合
    /// </summary>
    public static PeerPlayer Peer => Peers.FirstOrDefault();

    /// <summary>
    /// 根据 UID 获取对端玩家显示名（直播模式下为 UID-{uid}）
    /// </summary>
    public static string GetPeerName(int uid) =>
        LiveModeManager.GetDisplayName(uid);

    #endregion

    #region 房间相关

    private static bool InRoomScope(PeerPlayer peer) =>
        IsSameRoom(peer.RoomId) && peer.HasResources;

    public static bool TryGetRoomPeer(int uid, out PeerPlayer peer)
    {
        peer = PlayerTable.TryGetValue(uid, out var p) && InRoomScope(p) ? p : null;
        return peer != null;
    }

    /// <summary>指定 uid 当前是否为同房对端（替代旧 Peers.ContainsKey）。</summary>
    public static bool IsRoomPeer(int uid) =>
        PlayerTable.TryGetValue(uid, out var peer) && InRoomScope(peer);

    public static bool IsSameRoom(ushort roomId) =>
        MpManager.Session.IsInRoom && roomId == MpManager.Session.RoomId;

    /// <summary>同房对端展示顺序：房主优先，其余按 uid 升序。</summary>
    public static IEnumerable<PeerPlayer> RoomPeersOrdered =>
        Peers.OrderByDescending(p => p.Uid == MpManager.Session.HostUid).ThenBy(p => p.Uid);

    /// <summary>公域对端展示顺序：按 uid 升序。</summary>
    public static IEnumerable<PeerPlayer> PublicPeersOrdered =>
        PublicPeers.OrderBy(p => p.Uid);

    #endregion

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
        Peers.Any() && Peers.All(p => p.IsDayOver);

    /// <summary>
    /// 所有对端是否都已完成 Prep（聚合判断）
    /// </summary>
    public static bool AllPeersPrepOver =>
        Peers.Any() && Peers.All(p => p.IsPrepOver);

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
        Peers.Any() && Peers.All(p =>
            p.IzakayaMapLabel.IsSelected() && p.IzakayaLevel != 0
            && p.IzakayaMapLabel == mapLabel && p.IzakayaLevel == level);

    /// <summary>
    /// 是否所有对端都已做出选择（不论是否与本地一致）
    /// </summary>
    public static bool AllPeersHaveSelected =>
        Peers.Any() && Peers.All(p =>
            p.IzakayaMapLabel.IsSelected() && p.IzakayaLevel != 0);

    #endregion

    #region Per-Peer 状态修改（通过 SenderUid 定位）

    public static void SetPeerDayOver(int uid)
    {
        if (TryGetRoomPeer(uid, out var peer))
            peer.IsDayOver = true;
        else
            Log.LogWarning($"SetPeerDayOver: peer uid={uid} not found");
    }

    public static void SetPeerPrepOver(int uid)
    {
        if (TryGetRoomPeer(uid, out var peer))
            peer.IsPrepOver = true;
        else
            Log.LogWarning($"SetPeerPrepOver: peer uid={uid} not found");
    }

    public static void SetPeerIzakayaSelection(int uid, MapLabel mapLabel, int level)
    {
        if (TryGetRoomPeer(uid, out var peer))
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
        foreach (var peer in Peers)
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
        Local.DataBase.FoodAvailable(id) && Peers.All(p => p.DataBase.FoodAvailable(id));

    public static bool RecipeAvailable(int id) =>
        Local.DataBase.RecipeAvailable(id) && Peers.All(p => p.DataBase.RecipeAvailable(id));

    public static bool BeverageAvailable(int id) =>
        Local.DataBase.BeverageAvailable(id) && Peers.All(p => p.DataBase.BeverageAvailable(id));

    public static bool IngredientAvailable(int id) =>
        Local.DataBase.IngredientAvailable(id) && Peers.All(p => p.DataBase.IngredientAvailable(id));

    public static bool CookerAvailable(int id) =>
        Local.DataBase.CookerAvailable(id) && Peers.All(p => p.DataBase.CookerAvailable(id));

    public static bool ItemAvailable(int id) =>
        Local.DataBase.ItemAvailable(id) && Peers.All(p => p.DataBase.ItemAvailable(id));

    public static bool IzakayaAvailable(int id) =>
        Local.DataBase.IzakayaAvailable(id) && Peers.All(p => p.DataBase.IzakayaAvailable(id));

    public static bool NormalGuestAvailable(int id) =>
        Local.DataBase.NormalGuestAvailable(id) && Peers.All(p => p.DataBase.NormalGuestAvailable(id));

    public static bool SpecialGuestAvailable(int id) =>
        Local.DataBase.SpecialGuestAvailable(id) && Peers.All(p => p.DataBase.SpecialGuestAvailable(id));

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
        foreach (var peer in Peers)
            peer.ResetState();
        Log.LogInfo($"PlayerManager state reset (peers: {Peers.Count()})");
    }

    public static void SpawnPeersForCurrentScene(IEnumerable<PeerPlayer> peers = null)
    {
        if (MpManager.LocalScene is not Common.UI.Scene.DayScene and not Common.UI.Scene.WorkScene)
            return;

        var collection = Common.SceneDirector.Instance?.characterCollection;
        if (collection == null || !collection.TryGetValue("Self", out var localUnit) || localUnit == null)
            return;

        foreach (var peer in (peers ?? GetSpawnCandidates()).Where(p => p != null && IsSpawnCandidate(p)))
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
            Common.UI.Scene.DayScene => PlayerTable.Values.Where(p => p.Scene == Common.UI.Scene.DayScene),
            Common.UI.Scene.WorkScene => Peers,
            _ => []
        };
    }

    private static bool IsSpawnCandidate(PeerPlayer peer)
    {
        return MpManager.LocalScene switch
        {
            Common.UI.Scene.DayScene => true,
            Common.UI.Scene.WorkScene => InRoomScope(peer),
            _ => false
        };
    }

    public static void TryEnsureDayScenePeer(int uid)
    {
        if (uid == Local.Uid || MpManager.LocalScene != Common.UI.Scene.DayScene)
            return;
        if (!PlayerTable.TryGetValue(uid, out var peer) || peer.Scene != Common.UI.Scene.DayScene)
            return;
        if (peer.GetCharacterUnit() != null)
            return;

        SpawnPeersForCurrentScene(new[] { peer });
    }

    /// <summary>
    /// 非 DayScene/WorkScene：销毁所有对端角色（保留 PlayerTable 记录）。
    /// </summary>
    public static void DespawnAllPeers()
    {
        foreach (var peer in PlayerTable.Values)
            peer.DespawnCharacter();
        UI.FloatingTextHelper.ClearAllLabels();
        Log.LogInfo("PlayerManager all peer characters despawned");
    }

    /// <summary>
    /// 检查指定 PeerId 是否已有在线连接
    /// </summary>
    public static bool IsPeerIdOnline(string peerId) =>
        PlayerTable.Values.Any(p => string.Equals(p.Id, peerId, System.StringComparison.OrdinalIgnoreCase));

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
        var peer = GetOrCreatePeer(summary.Uid);
        peer.Id = summary.PeerId ?? "";
        peer.RoomId = summary.RoomId;
        peer.Scene = summary.Scene.ToGame();
        peer.Skin = summary.Skin ?? new PlayerSkinData();
        peer.Role = summary.Role;
        return peer;
    }

    public static PeerPlayer UpsertPresence(PlayerPresenceAction presence)
    {
        if (presence == null || presence.Uid == Local.Uid) return null;
        var peer = GetOrCreatePeer(presence.Uid);
        bool wasInRoom = InRoomScope(peer);
        bool roomChanged = peer.RoomId != presence.RoomId;
        peer.Id = presence.PeerId ?? "";
        peer.RoomId = presence.RoomId;
        peer.Scene = presence.Scene.ToGame();
        peer.Skin = presence.Skin ?? new PlayerSkinData();
        if (roomChanged)
            peer.ApplyResources(null);
        if (presence.RoomId == MpConstants.PublicRoomId)
            peer.Role = WireRoomRole.None;
        if (wasInRoom && !IsSameRoom(peer.RoomId))
            HidePeer(presence.Uid);
        return peer;
    }

    public static PeerPlayer UpsertRoomMember(RoomMember member, ushort roomId)
    {
        if (member == null || member.Uid == Local.Uid) return null;
        var peer = GetOrCreatePeer(member.Uid);
        peer.Id = member.PeerId ?? "";
        peer.RoomId = roomId;
        peer.Scene = member.Scene.ToGame();
        peer.Skin = member.Skin ?? new PlayerSkinData();
        peer.Role = member.Role;
        peer.ApplyResources(member.Resources);
        Log.LogMessage($"Upserted room peer '{peer.Id}' (uid={peer.Uid}, room={MpSession.FormatRoomId(peer.RoomId)}, characterId='{peer.CharacterId}')");
        return peer;
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

    private static PeerPlayer GetOrCreatePeer(int uid) =>
        PlayerTable.GetOrAdd(uid, static key =>
        {
            var peer = new PeerPlayer(key);
            peer.ResetState();
            peer.ResetMotion();
            return peer;
        });

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
        if (PlayerTable.TryGetValue(uid, out var peer))
            peer.DespawnCharacter();
        UI.FloatingTextHelper.RemovePlayerLabel(uid);
    }

    /// <summary>
    /// 移除一个对端玩家（先销毁角色和标签）
    /// </summary>
    public static bool RemovePeer(int uid)
    {
        HidePeer(uid);
        if (PlayerTable.TryRemove(uid, out var peer))
        {
            Log.LogMessage($"Removed peer '{peer.Id}' (uid={uid})");
            return true;
        }
        return false;
    }

    /// <summary>
    /// 清除所有对端玩家（先销毁所有角色并取消 pending spawn，断开连接时调用）
    /// </summary>
    public static void ClearPeers()
    {
        foreach (var peer in PlayerTable.Values)
            peer.DespawnCharacter();
        UI.FloatingTextHelper.ClearAllLabels();
        PlayerTable.Clear();
        Log.LogMessage($"All peers cleared");
    }

    /// <summary>
    /// 清除房间内对端玩家：销毁角色、移除标签，并降级资源表（退到公域投影）。
    /// </summary>
    public static void ClearRoomPeers()
    {
        foreach (var peer in Peers.ToList())
        {
            peer.DespawnCharacter();
            UI.FloatingTextHelper.RemovePlayerLabel(peer.Uid);
            peer.ApplyResources(null);
        }
        Log.LogMessage("Room peers cleared");
    }

    /// <summary>
    /// RoomAssign 落地前，把不在新名单中的旧同房 peer 清掉角色并降级资源表。
    /// </summary>
    public static void SyncRoomPeersBeforeAssign(ushort roomId, IEnumerable<int> memberUids)
    {
        var incoming = memberUids?.ToHashSet() ?? [];
        foreach (var peer in Peers.Where(p => !incoming.Contains(p.Uid)).ToList())
        {
            peer.DespawnCharacter();
            UI.FloatingTextHelper.RemovePlayerLabel(peer.Uid);
            if (peer.RoomId == roomId)
                peer.ApplyResources(null);
        }
        Log.LogMessage($"Room peer roster synced (room={MpSession.FormatRoomId(roomId)}, members={Peers.Count()})");
    }

    #endregion

    #region FixedUpdate

    /// <summary>
    /// 在 FixedUpdate 中为所有 Peer 执行位置修正（房间内与公域对端都参与）。
    /// </summary>
    public static void OnFixedUpdate()
    {
        foreach (var peer in PlayerTable.Values)
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
        PlayerTable.Values.Any(peer => peer.CharacterId == label);

    #endregion
}
