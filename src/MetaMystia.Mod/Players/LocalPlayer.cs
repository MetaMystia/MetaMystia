using UnityEngine;

using Common;
using Common.CharacterUtility;
using GameData.Core.Collections.CharacterUtility;
using GameData.RunTime.Common;

namespace MetaMystia;

/// <summary>
/// 本地玩家，管理与本地操控角色相关的状态
/// </summary>
public class LocalPlayer : NetPlayer
{
    public bool CharacterSpawnedAndInitialized => GetCharacterUnit() != null;

    public override CharacterControllerUnit GetCharacterUnit()
    {
        if (Common.SceneDirector.instance == null)
        {
            Log.Warning($"SceneDirector instance is null");
            return null;
        }
        if (Common.SceneDirector.Instance.characterCollection.TryGetValue("Self", out var characterUnit))
        {
            return characterUnit;
        }
        Log.Warning($"Cannot find character unit for 'Self'");
        return null;
    }

    /// <summary>
    /// 本地玩家速度直接读取 unit.MoveSpeedMultiplier
    /// </summary>
    public override float Speed
    {
        get => unit?.MoveSpeedMultiplier ?? 1f;
        set { if (unit != null) unit.MoveSpeedMultiplier = value; }
    }

    public override void ResetState()
    {
        base.ResetState();
        Log.Info($"LocalPlayer state reset");
    }

    /// <summary>
    /// 当前地图，从 SceneDirector 读取。
    /// </summary>
    public static MapLabel CurrentMapLabel
    {
        get
        {
            var key = Common.SceneDirector.Instance?.currentActiveScene?.Key;
            MapLabelExtensions.TryFromMapKey(key, out var label);
            return label;
        }
    }

    /// <summary>
    /// 是否通过 /skin set 手动设置了自定义皮肤覆盖
    /// </summary>
    public bool IsCustomSkinOverride { get; set; } = false;

    /// <summary>
    /// 从游戏中获取实际皮肤数据（仅在未手动覆盖时执行）
    /// </summary>
    public void InitSkin()
    {
        if (IsCustomSkinOverride) return;
        var selection = RunTimeAlbum.PlayerSkinSet();
        // 原生 Nullable<int> 空返回值无法被 Interop 正确包装，此处沿用原生选择条件。
        bool special = LoadingSceneManager.loadType == LoadingSceneManager.DataLoadType.Night
            && !RunTimeAlbum.UseCurrentSkinAtNight;
        Skin.SetSkin(-1, special ? CharacterSkinSets.SelectedType.Explicit : selection.selectedType,
            special ? 0 : selection.index);
    }

    public void RefreshSkin(bool resetOverride = false)
    {
        if (resetOverride) IsCustomSkinOverride = false;
        InitSkin();
        UpdateCharacterSprite();
        PlayerProfile.SendProfile();
        PlayerManager.RefreshPortrait();
    }
}
