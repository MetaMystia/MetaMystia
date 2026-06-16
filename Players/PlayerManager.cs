using System.Collections.Concurrent;
using System.Linq;
using UnityEngine;

using Common.CharacterUtility;
using MetaMystia.Protocol.Data;

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
    /// 当前对端玩家（1v1 便捷访问，返回第一个 Peer）
    /// 多人场景下，调用方应遍历 Peers 集合
    /// </summary>
    public static PeerPlayer Peer => Peers.Values.FirstOrDefault();

    public static bool TryGetVisiblePeer(int uid, out PeerPlayer peer) =>
        Peers.TryGetValue(uid, out peer) || PublicPeers.TryGetValue(uid, out peer);

    /// <summary>
    /// 根据 UID 获取对端玩家显示名（直播模式下为 UID-{uid}）
    /// </summary>
    public static string GetPeerName(int uid) =>
        LiveModeManager.GetDisplayName(uid);

    #region Local 便捷属性

    public static string LocalMapLabel => LocalPlayer.MapLabel;
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
    public static bool AllPeersSelectedSameIzakaya(string mapLabel, int level) =>
        Peers.Count > 0 && Peers.Values.All(p =>
            !string.IsNullOrEmpty(p.IzakayaMapLabel) && p.IzakayaLevel != 0
            && p.IzakayaMapLabel == mapLabel && p.IzakayaLevel == level);

    /// <summary>
    /// 是否所有对端都已做出选择（不论是否与本地一致）
    /// </summary>
    public static bool AllPeersHaveSelected =>
        Peers.Count > 0 && Peers.Values.All(p =>
            !string.IsNullOrEmpty(p.IzakayaMapLabel) && p.IzakayaLevel != 0);

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

    public static void SetPeerIzakayaSelection(int uid, string mapLabel, int level)
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
    public static string GetFirstMismatchSelection(string mapLabel, int level)
    {
        foreach (var peer in Peers.Values)
        {
            if (string.IsNullOrEmpty(peer.IzakayaMapLabel) || peer.IzakayaLevel == 0)
                return $"{LiveModeManager.GetDisplayName(peer.Uid)}: 未选择";
            if (peer.IzakayaMapLabel != mapLabel || peer.IzakayaLevel != level)
                return $"{LiveModeManager.GetDisplayName(peer.Uid)}: {Utils.GetMapLabelNameCN(peer.IzakayaMapLabel)} {Utils.GetMapLevelNameCN(peer.IzakayaLevel)}";
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

    /// <summary>
    /// 为所有 Peer 生成角色（SpawnForScene）并重置运动插值状态。
    /// 在 DayScene / WorkScene 开始时调用。
    /// 同时为本地玩家创建头顶标签。
    /// </summary>
    public static void SpawnPeers()
    {
        foreach (var peer in Peers.Values)
        {
            peer.ResetMotion();
            peer.SpawnForScene();
        }
        foreach (var peer in PublicPeers.Values)
        {
            peer.ResetMotion();
            peer.SpawnForScene();
        }
        // 为本地玩家也添加头顶标签（等 Local unit 初始化后）
        SgrYuki.CommandScheduler.Enqueue(
            executeWhen: () => Local.unit != null,
            execute: () => UI.FloatingTextHelper.SetPlayerLabel(
                Local.Uid, LiveModeManager.GetDisplayName(Local.Uid), Local.unit.transform),
            timeoutSeconds: 30
        );
        Log.LogInfo($"PlayerManager peers spawned (peers: {Peers.Count})");
    }

    /// <summary>
    /// 检查指定 PeerId 是否已有在线连接
    /// </summary>
    public static bool IsPeerIdOnline(string peerId)
    {
        foreach (var kvp in Peers)
        {
            if (string.Equals(kvp.Value.Id, peerId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        foreach (var kvp in PublicPeers)
        {
            if (string.Equals(kvp.Value.Id, peerId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 握手成功后，根据对端 UID 创建并注册 PeerPlayer
    /// </summary>
    public static PeerPlayer AddPeer(PlayerInfoData info)
    {
        var uid = info.Uid;
        var peerId = info.PeerId;
        var skin = info.Skin;

        PublicPeers.TryRemove(uid, out _);

        if (Peers.TryGetValue(uid, out var existing))
        {
            Log.LogWarning($"Peer with uid={uid} already exists (id='{existing.Id}'), replacing");
        }
        var peer = new PeerPlayer(uid, ResourceDataBase.FromDatabaseData(info.IncrementalDataBase)) { Id = peerId };
        if (skin != null) peer.Skin = skin;
        peer.ResetState();
        peer.IsDayOver = info.IsDayOver;
        peer.IsPrepOver = info.IsPrepOver;
        peer.ResetMotion();
        Peers[uid] = peer;
        Log.LogMessage($"Added peer '{peerId}' (uid={uid}, characterId='{peer.CharacterId}')");
        return peer;
    }

    public static PeerPlayer AddPublicPeer(PlayerInfoData info)
    {
        var uid = info.Uid;
        var peerId = info.PeerId;
        if (Peers.TryGetValue(uid, out var publicPeer))
        {
            Log.LogInfo($"Public peer '{peerId}' (uid={uid}) is already in room peers, skipping public registration");
            return publicPeer;
        }

        var peer = new PeerPlayer(uid, ResourceDataBase.FromDatabaseData(info.IncrementalDataBase)) { Id = peerId };
        if (info.Skin != null) peer.Skin = info.Skin;
        peer.ResetState();
        peer.ResetMotion();
        PublicPeers[uid] = peer;
        Log.LogMessage($"Added public peer '{peerId}' (uid={uid}, characterId='{peer.CharacterId}')");
        return peer;
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
        if (Peers.TryGetValue(uid, out var peer) || PublicPeers.TryGetValue(uid, out peer))
            peer.DespawnCharacter();
        UI.FloatingTextHelper.RemovePlayerLabel(uid);
    }

    /// <summary>
    /// 移除一个对端玩家（先销毁角色和标签）
    /// </summary>
    public static bool RemovePeer(int uid)
    {
        HidePeer(uid);
        if (Peers.TryRemove(uid, out var peer))
        {
            Log.LogMessage($"Removed peer '{peer.Id}' (uid={uid})");
            return true;
        }
        if (PublicPeers.TryRemove(uid, out peer))
        {
            Log.LogMessage($"Removed public peer '{peer.Id}' (uid={uid})");
            return true;
        }
        return false;
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
        }
        Peers.Clear();
        Log.LogMessage("Room peers cleared");
    }

    #endregion

    #region FixedUpdate

    /// <summary>
    /// 在 FixedUpdate 中为所有 Peer 执行位置修正
    /// </summary>
    public static void OnFixedUpdate()
    {
        foreach (var peer in Peers.Values)
        {
            peer.OnFixedUpdate();
        }
        foreach (var peer in PublicPeers.Values)
        {
            peer.OnFixedUpdate();
        }
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

    public static bool IsPeerCharacter(string label) =>
        Peers.Values.Any(p => p.CharacterId == label) ||
        PublicPeers.Values.Any(p => p.CharacterId == label);

    #endregion
}
