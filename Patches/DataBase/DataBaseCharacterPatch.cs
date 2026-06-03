using HarmonyLib;
using UnityEngine.UI;

using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(GameData.Core.Collections.CharacterUtility.DataBaseCharacter))]
[AutoLog]
public partial class DataBaseCharacterPatch
{
    [HarmonyPatch(nameof(DataBaseCharacter.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        Log.LogInfo("DataBaseCharacter.Initialize Postfix called.");
        ResourceExManager.OnDataBaseCharacterInitialized();
        PlayerManager.Local.DataBase.LoadResourceIds();
    }

    [HarmonyPatch(nameof(DataBaseCharacter.GetNPCLabel))]
    [HarmonyPrefix]
    public static bool GetNPCLabel_Prefix(ref string __result, SchedulerNode.Character identity)
    {
        // Log.LogWarning($"GetNPCLabel_Prefix called for identity: {identity} result: {__result}");

        var config = ResourceExManager.GetCharacterConfig(identity.characterId, identity.characterIdentity.ToString());
        if (config != null)
        {
            __result = config.label;
            return SkipOriginal;
        }

        return RunOriginal;
    }

    // /skin 立绘覆盖 > ResourceEX/Clothes 立绘覆盖 > 游戏原逻辑
    [HarmonyPatch(nameof(DataBaseCharacter.SetupPortrayalVisual))]
    [HarmonyPrefix]
    public static bool SetupPortrayalVisual_Prefix(ref Image imageComponent)
    {
        // /skin 立绘覆盖
        if (PlayerManager.Local?.IsCustomSkinOverride == true)
        {
            var sprite = PlayerManager.Local.Skin.ResolvePortraitSprite();
            if (sprite != null)
            {
                imageComponent.overrideSprite = null;
                imageComponent.sprite = sprite;
                return SkipOriginal;
            }
            Log.Warning("Custom skin override active but portrait sprite is null, falling through to game logic.");
        }

        // ResourceEx 服装立绘覆盖
        var currentSkin = GameData.RunTime.Common.RunTimeAlbum.CurrentPlayerSkin;
        if (ResourceExManager.IsResourceExCloth(currentSkin))
        {
            if (ResourceExManager.TryGetClothPortrait(currentSkin, out var sprite))
            {
                imageComponent.overrideSprite = sprite;
                Log.Info($"Applied ResourceEx cloth portrait for skin ID {currentSkin}");
            }
            else
            {
                Log.Info($"ResourceEx cloth ID {currentSkin} has no portrait configured.");
            }
        }

        return RunOriginal;
    }

    /// <summary>
    /// 拦截游戏内部的符卡拥有检查，让神绮等已注册自定义符卡的角色通过检查。
    /// 游戏原生的 CheckCharacterHasSpell 不读取我们写入的 CharacterHasSpell 字典，
    /// 所以需要通过 Prefix 强制返回 true。
    /// </summary>
    [HarmonyPatch(nameof(DataBaseCharacter.CheckCharacterHasSpell))]
    [HarmonyPrefix]
    public static bool CheckCharacterHasSpell_Prefix(ref bool __result, int specialGuestId)
    {
        if (ResourceExManager.IsShinkiSpellRegistered()
            && (ResourceExManager.IsShinkiCharacterId(specialGuestId) || ResourceExManager.IsShinkiResourceExId(specialGuestId)))
        {
            __result = true;
            return SkipOriginal;
        }
        return RunOriginal;
    }
}
