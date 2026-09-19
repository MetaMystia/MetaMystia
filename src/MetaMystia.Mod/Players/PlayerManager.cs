using System.Collections.Concurrent;
using System.Linq;

using UnityEngine;

using MetaMystia.Multiplayer;
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
    /// 从游戏中获取实际皮肤数据，并应用到已就绪的本地角色
    /// </summary>
    public static void InitLocalSkin()
    {
        Local.InitSkin();
        Local.UpdateCharacterSprite();
    }

    /// <summary>
    /// 刷新 NightScene 中的角色立绘（通过重新触发 SetupPortrayalVisual 前缀钩子）
    /// </summary>
    public static void RefreshPortrait(bool skipSceneCheck = false)
    {
        if (!skipSceneCheck && GameFlow.LocalScene != Common.UI.Scene.WorkScene) return;
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
    /// 销毁指定对端玩家的角色和头顶标签。
    /// 在移除 peer 之前调用，避免留下"幽灵"角色。
    /// </summary>
    public static void HidePeer(int uid)
    {
        if (Peers.TryGetValue(uid, out var peer) || PublicPeers.TryGetValue(uid, out peer))
            peer.ReleaseCharacter();
    }

    /// <summary>
    /// 移除一个对端玩家（先销毁角色和标签）
    /// </summary>
    public static bool RemovePeer(int uid)
    {
        HidePeer(uid);
        if (Peers.TryRemove(uid, out var peer))
        {
            DayDestinationManager.OnPeerLeft(uid);
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
    /// 断开连接时销毁所有对端角色和标签，并清除玩家状态。
    /// </summary>
    public static void ClearPeers()
    {
        DayDestinationManager.ResetSession();
        foreach (var peer in Peers.Values)
            peer.ReleaseCharacter();
        foreach (var peer in PublicPeers.Values)
            peer.ReleaseCharacter();
        UI.FloatingTextHelper.ClearAllLabels();
        Peers.Clear();
        PublicPeers.Clear();
        Log.LogMessage($"All peers cleared");
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

}
