using UnityEngine;

using Common.CharacterUtility;
using GameData.RunTime.Common;
using JetBrains.Annotations;

namespace MetaMystia;

/// <summary>
/// 本地玩家，管理与本地操控角色相关的状态
/// </summary>
[AutoLog]
public partial class LocalPlayer : NetPlayer
{
    public bool CharacterSpawnedAndInitialized => GetCharacterUnit() != null;

    public override CharacterControllerUnit GetCharacterUnit()
    {
        if (Common.SceneDirector.instance == null)
        {
            Log.LogWarning($"SceneDirector instance is null");
            return null;
        }
        if (Common.SceneDirector.Instance.characterCollection.TryGetValue("Self", out var characterUnit))
        {
            return characterUnit;
        }
        Log.LogWarning($"Cannot find character unit for 'Self'");
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
        Log.LogInfo($"LocalPlayer state reset");
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
        var clothes = RunTimeAlbum.GetPlayerClothes();
        Skin.SetSkin(-1, clothes.skinIndex.selectedType, clothes.skinIndex.index);
    }
}
