using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Common.CharacterUtility;

using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia;

// 兼容游戏 Hook 的只读组合入口；不登记、删除或修改网络成员。
public static class PlayerManager
{
    public static LocalPlayer Local { get; } = new();
    public static IReadOnlyDictionary<int, PeerPlayer> PublicPeers => PlayerPresentation.Avatars;
    public static IReadOnlyDictionary<int, PeerPlayer> Peers => PublicPeers
        .Where(pair => MpWire.Session.Room?.Members.ContainsKey(pair.Key) == true).ToDictionary(pair => pair.Key, pair => pair.Value);
    public static PeerPlayer Peer => Peers.Values.FirstOrDefault();
    public static bool TryGetVisiblePeer(int uid, out PeerPlayer peer) => PublicPeers.TryGetValue(uid, out peer);
    public static string GetPeerName(int uid) => LiveModeManager.GetDisplayName(uid);
    public static MapLabel LocalMapLabel => LocalPlayer.CurrentMapLabel;
    public static bool LocalIsSprinting { get => Local.Sprinting; set => Local.Sprinting = value; }
    public static Vector2 LocalInputDirection { get => Local.Direction; set => Local.Direction = value; }
    public static bool CharacterSpawnedAndInitialized => Local.CharacterSpawnedAndInitialized;
    public static bool LocalIsDayOver => RoomGameplay.LocalDayReady;
    public static bool LocalIsPrepOver => RoomGameplay.LocalPrepReady;
    public static Vector2 LocalPosition => Local.Position;
    private static IEnumerable<int> OtherMembers => MpWire.Session.Room?.Members.Keys.Where(uid => uid != Local.Uid) ?? Enumerable.Empty<int>();
    public static bool AllPeersDayOver => OtherMembers.All(uid => RoomGameplay.IsPlayerReady(uid, GameplayPhase.Day));
    public static bool AllPeersPrepOver => OtherMembers.All(uid => RoomGameplay.IsPlayerReady(uid, GameplayPhase.Prep));
    public static bool AllDayOver => LocalIsDayOver && AllPeersDayOver;
    public static bool AllPrepOver => LocalIsPrepOver && AllPeersPrepOver;
    public static bool AllPeersSelectedSameIzakaya(MapLabel map, int level) =>
        OtherMembers.All(uid => RoomGameplay.Selection(uid) is { } selection && selection.Map == map && selection.Level == level);
    public static bool AllPeersHaveSelected => OtherMembers.All(uid => RoomGameplay.Selection(uid) != null);

    public static string GetFirstMismatchSelection(MapLabel map, int level)
    {
        foreach (int uid in OtherMembers)
        {
            var selected = RoomGameplay.Selection(uid);
            if (selected == null) return $"{GetPeerName(uid)}: {TextId.PeerIzakayaNotSelected.Get()}";
            if (selected.Map != map || selected.Level != level) return $"{GetPeerName(uid)}: {selected.Map.FormatIzakayaSelection(selected.Level)}";
        }
        return null;
    }

    private static bool Available(int id, Func<ResourceDataBase, int, bool> contains) =>
        contains(Local.DataBase, id) && (id < 6000 || OtherMembers.All(uid => ModPlayerStore.Get(uid)?.Resources is { } resources && contains(resources, id)));
    public static bool FoodAvailable(int id) => Available(id, (db, value) => db.FoodAvailable(value));
    public static bool RecipeAvailable(int id) => Available(id, (db, value) => db.RecipeAvailable(value));
    public static bool BeverageAvailable(int id) => Available(id, (db, value) => db.BeverageAvailable(value));
    public static bool IngredientAvailable(int id) => Available(id, (db, value) => db.IngredientAvailable(value));
    public static bool CookerAvailable(int id) => Available(id, (db, value) => db.CookerAvailable(value));
    public static bool ItemAvailable(int id) => Available(id, (db, value) => db.ItemAvailable(value));
    public static bool IzakayaAvailable(int id) => Available(id, (db, value) => db.IzakayaAvailable(value));
    public static bool NormalGuestAvailable(int id) => Available(id, (db, value) => db.NormalGuestAvailable(value));
    public static bool SpecialGuestAvailable(int id) => Available(id, (db, value) => db.SpecialGuestAvailable(value));
    public static void SpawnPeers() => PlayerPresentation.Reconcile();
    public static void OnFixedUpdate() => PlayerPresentation.FixedUpdate();
    public static void InitLocalSkin()
    {
        Local.InitSkin();
        Local.UpdateCharacterSprite();
    }
    public static void RefreshPortrait(bool skipSceneCheck = false)
    {
        if (!skipSceneCheck && MpManager.LocalScene != Common.UI.Scene.WorkScene) return;
        var ui = NightScene.UI.UIManager.Instance;
        if (ui == null) return;
        bool actual = GameData.RunTime.Common.RunTimeAlbum.UseCurrentSkinAtNight;
        GameData.RunTime.Common.RunTimeAlbum.UseCurrentSkinAtNight = true;
        ui.InitializePlayerPortrayal();
        GameData.RunTime.Common.RunTimeAlbum.UseCurrentSkinAtNight = actual;
    }
    public static void EnablePeerCollision(CharacterControllerUnit unit, bool enable = true)
    {
        unit?.UpdateColliderStatus(enable);
        if (unit?.rb2d != null) unit.rb2d.isKinematic = !enable;
    }
    public static void EnablePeerCollision(bool enable = true) => EnablePeerCollision(Peer?.unit, enable);
    public static bool IsPeerCharacter(string label) => PublicPeers.Values.Any(peer => peer.CharacterId == label);
}
